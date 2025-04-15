// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using CommunityToolkit.WinUI.Lottie.CompMetadata;
using CommunityToolkit.WinUI.Lottie.GenericData;
using CommunityToolkit.WinUI.Lottie.UIData.CodeGen.Tables;
using CommunityToolkit.WinUI.Lottie.UIData.Tools;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.MetaData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;
using Expr = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using Mgce = CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using Sn = System.Numerics;
using Wg = CommunityToolkit.WinUI.Lottie.WinCompData.Wg;
using Wmd = CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;
using Wui = CommunityToolkit.WinUI.Lottie.WinCompData.Wui;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIDataCodeGen
    public
#endif
    abstract class InstantiatorGeneratorBase : IAnimatedVisualSourceInfo
    {
        // The name of the field holding the singleton reusable ExpressionAnimation.
        const string SingletonExpressionAnimationName = "_reusableExpressionAnimation";

        // The name of the field holding the theme properties.
        const string ThemePropertiesFieldName = "_themeProperties";

        // The name of the constant holding the duration of the animation in ticks.
        const string DurationTicksFieldName = "c_durationTicks";

        // The name of the method that creates all the animations.
        protected const string CreateAnimationsMethod = "CreateAnimations";

        // The name of the method that destroys all the animations.
        protected const string DestroyAnimationsMethod = "DestroyAnimations";

        // The proportion of a frame by which markers will be nudged. This compensates
        // for loss of precision in floating point math that can cause the progress
        // value that corresponds to the start of a Lottie frame to refer to the end
        // of the previous frame. Without this compensation there can be flashing when
        // playing from a particular frame, as the previous frame shows briefly. By
        // nudging the progress value forward slightly, the progress value is guaranteed
        // to not point to the previous frame.
        const double NudgeFrameProportion = 0.05;

        // The name of the IAnimatedVisualSource class.
        readonly string _className;
        readonly string _namespace;
        readonly Vector2 _compositionDeclaredSize;
        readonly TimeSpan _compositionDuration;
        readonly bool _setCommentProperties;
        readonly bool _disableFieldOptimization;
        readonly bool _generateDependencyObject;
        readonly bool _generatePublicClass;
        readonly Version _winUIVersion;
        readonly Stringifier _s;
        readonly IReadOnlyList<AnimatedVisualGenerator> _animatedVisualGenerators;
        readonly LoadedImageSurfaceInfo[] _loadedImageSurfaceInfos;
        readonly Dictionary<ObjectData, LoadedImageSurfaceInfo> _loadedImageSurfaceInfosByNode;
        readonly SourceMetadata _sourceMetadata;
        readonly bool _isThemed;
        readonly IReadOnlyList<string> _toolInfo;
        readonly IReadOnlyList<TypeName> _additionalInterfaces;
        readonly IReadOnlyList<MarkerInfo> _markers;
        readonly IReadOnlyList<NamedConstant> _internalConstants;

        AnimatedVisualGenerator? _currentAnimatedVisualGenerator;

        private protected InstantiatorGeneratorBase(
            CodegenConfiguration configuration,
            bool setCommentProperties,
            Stringifier stringifier)
        {
            _className = configuration.ClassName;
            _namespace = configuration.Namespace;
            _compositionDeclaredSize = new Vector2((float)configuration.Width, (float)configuration.Height);
            _sourceMetadata = new SourceMetadata(configuration.SourceMetadata);
            _compositionDuration = configuration.Duration;
            _setCommentProperties = setCommentProperties;

            // We should disableFieldOptimization if configuration.ImplementCreateAndDestroyMethods is true.
            _disableFieldOptimization = configuration.DisableOptimization || configuration.ImplementCreateAndDestroyMethods;
            _generateDependencyObject = configuration.GenerateDependencyObject;
            _generatePublicClass = configuration.Public;
            _winUIVersion = configuration.WinUIVersion;
            _s = stringifier;
            _toolInfo = configuration.ToolInfo;
            _additionalInterfaces = configuration.AdditionalInterfaces.Select(n => new TypeName(n)).ToArray();
            _markers = MarkerInfo.GetMarkerInfos(
                _sourceMetadata.LottieMetadata.FilteredMarkers,
                NudgeFrameProportion).ToArray();
            _internalConstants = GetInternalConstants().ToArray();

            var graphs = configuration.ObjectGraphs;

            _animatedVisualGenerators = graphs.Select(g => new AnimatedVisualGenerator(this, g.graphRoot, g.requiredUapVersion, graphs.Count > 1, configuration)).ToArray();

            // Determine whether theming is enabled.
            _isThemed = _animatedVisualGenerators.Any(avg => avg.IsThemed);

            // Deal with the nodes that are shared between multiple AnimatedVisual classes.
            // The nodes need naming, and some other adjustments.
            var sharedNodes = _animatedVisualGenerators.SelectMany(a => a.GetSharedNodes()).ToArray();

            // Canonicalize the loaded images surfaces.
            var sharedNodeGroups =
                (from n in sharedNodes
                 where n.IsLoadedImageSurface
                 let obj = (Wmd.LoadedImageSurface)n.Object
                 let key = obj.Type == Wmd.LoadedImageSurface.LoadedImageSurfaceType.FromUri
                             ? (object)((Wmd.LoadedImageSurfaceFromUri)obj).Uri
                             : ((Wmd.LoadedImageSurfaceFromStream)obj).Bytes
                 group n by key into g
                 select new SharedNodeGroup(g)).ToArray();

            // Generate names for each of the canonical nodes of the shared nodes (i.e. the first node in each group).
            foreach ((var n, var name) in NodeNamer<ObjectData>.GenerateNodeNames(sharedNodeGroups.Select(g => g.CanonicalNode)))
            {
                n.Name = name;
            }

            // Apply the name from the canonical node to the other nodes in its group so they will be
            // treated during generation as if they are the same object.
            foreach (var sharedNodeGroup in sharedNodeGroups)
            {
                var canonicalNode = sharedNodeGroup.CanonicalNode;
                if (canonicalNode.UsesAssetFile)
                {
                    // Set the Uri of the image file for LoadedImageSurfaceFromUri to $"ms-appx:///Assets/<className>/<filePath>/<fileName>.
                    var loadedImageSurfaceObj = (Wmd.LoadedImageSurfaceFromUri)canonicalNode.Object;
                    var imageUri = loadedImageSurfaceObj.Uri;

                    if (imageUri.IsFile)
                    {
                        canonicalNode.LoadedImageSurfaceImageUri = new Uri($"ms-appx:///Assets/{_className}{imageUri.AbsolutePath}");
                    }
                }

                // Propagate the name and Uri to the other nodes in the group.
                foreach (var n in sharedNodeGroup.Rest)
                {
                    n.Name = canonicalNode.Name;
                    n.LoadedImageSurfaceImageUri = canonicalNode.LoadedImageSurfaceImageUri;
                }
            }

            var sharedLoadedImageSurfaceInfos = (from g in sharedNodeGroups
                                                 where g.CanonicalNode.IsLoadedImageSurface
                                                 let loadedImageSurfaceNode = LoadedImageSurfaceInfoFromObjectData(g.CanonicalNode)
                                                 from node in OrderByName(g.All)
                                                 select (node, loadedImageSurfaceNode)).ToArray();

            _loadedImageSurfaceInfos = sharedLoadedImageSurfaceInfos.
                                            Select(n => n.loadedImageSurfaceNode).
                                            Distinct().
                                            OrderBy(lisi => lisi.Name, AlphanumericStringComparer.Instance).
                                            ToArray();

            _loadedImageSurfaceInfosByNode = sharedLoadedImageSurfaceInfos.ToDictionary(n => n.node, n => n.loadedImageSurfaceNode);
        }

        /// <summary>
        /// Well-known interface type.
        /// </summary>
        protected TypeName Interface_IAnimatedVisual { get; } = new TypeName("Microsoft.UI.Xaml.Controls.IAnimatedVisual");

        /// <summary>
        /// Well-known interface type.
        /// Extension of IAnimatedVisual that adds CreateAnimations and DestroyAnimations methods.
        /// </summary>
        protected TypeName Interface_IAnimatedVisual2 { get; } = new TypeName("Microsoft.UI.Xaml.Controls.IAnimatedVisual2");

        /// <summary>
        /// Well-known interface type.
        /// </summary>
        protected TypeName Interface_IAnimatedVisualSource { get; } = new TypeName("Microsoft.UI.Xaml.Controls.IAnimatedVisualSource");

        /// <summary>
        /// Well-known interface type.
        /// </summary>
        protected TypeName Interface_IDynamicAnimatedVisualSource { get; } = new TypeName("Microsoft.UI.Xaml.Controls.IDynamicAnimatedVisualSource");

        /// <summary>
        /// Well-known interface type.
        /// </summary>
        protected TypeName Interface_IAnimatedVisualSource2 { get; } = new TypeName("Microsoft.UI.Xaml.Controls.IAnimatedVisualSource2");

        /// <summary>
        /// Well-known interface type.
        /// Extension of IAnimatedVisualSource2 that allows to create IAnimatedVisual2 without animations instantiated at first.
        /// </summary>
        protected TypeName Interface_IAnimatedVisualSource3 { get; } = new TypeName("Microsoft.UI.Xaml.Controls.IAnimatedVisualSource3");

        /// <summary>
        /// Information about the IAnimatedVisualSourceInfo implementation.
        /// </summary>
        protected IAnimatedVisualSourceInfo AnimatedVisualSourceInfo => this;

        /// <summary>
        /// Gets the standard header text used to indicate that a file contains auto-generated content.
        /// </summary>
        protected IReadOnlyList<string> AutoGeneratedHeaderText
        {
            get
            {
                var builder = new CodeBuilder();
                builder.WriteLine("//------------------------------------------------------------------------------");
                builder.WriteLine("// <auto-generated>");
                builder.WriteLine("//     This code was generated by a tool.");

                if (_toolInfo is not null)
                {
                    builder.WriteLine("//");
                    foreach (var line in _toolInfo)
                    {
                        builder.WriteLine($"//       {line}");
                    }
                }

                builder.WriteLine("//");
                builder.WriteLine("//     Changes to this file may cause incorrect behavior and will be lost if");
                builder.WriteLine("//     the code is regenerated.");
                builder.WriteLine("// </auto-generated>");
                builder.WriteLine("//------------------------------------------------------------------------------");

                return builder.ToLines(0).ToArray();
            }
        }

        /// <summary>
        /// Writes the start of the file, e.g. using namespace statements and includes at the top of the file.
        /// </summary>
        protected abstract void WriteImplementationFileStart(CodeBuilder builder);

        /// <summary>
        /// Writes the start of the IAnimatedVisual implementation class.
        /// </summary>
        protected abstract void WriteAnimatedVisualStart(
            CodeBuilder builder,
            IAnimatedVisualInfo info);

        /// <summary>
        /// Writes the end of the IAnimatedVisual implementation class.
        /// </summary>
        protected abstract void WriteAnimatedVisualEnd(
            CodeBuilder builder,
            IAnimatedVisualInfo info,
            CodeBuilder createAnimations,
            CodeBuilder destroyAnimations);

        /// <summary>
        /// Write a byte array field.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="fieldName">The name of the field to be written.</param>
        /// <param name="bytes">The bytes in the array.</param>
        protected abstract void WriteByteArrayField(CodeBuilder builder, string fieldName, IReadOnlyList<byte> bytes);

        /// <summary>
        /// Writes the end of the file.
        /// </summary>
        protected abstract void WriteImplementationFileEnd(CodeBuilder builder);

        /// <summary>
        /// Writes CanvasGeometery.Combination factory code.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="obj">Describes the object that should be instantiated by the factory code.</param>
        /// <param name="typeName">The type of the result.</param>
        /// <param name="fieldName">If not null, the name of the field in which the result is stored.</param>
        protected abstract void WriteCanvasGeometryCombinationFactory(
            CodeBuilder builder,
            CanvasGeometry.Combination obj,
            string typeName,
            string fieldName);

        /// <summary>
        /// Writes CanvasGeometery.Ellipse factory code.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="obj">Describes the object that should be instantiated by the factory code.</param>
        /// <param name="typeName">The type of the result.</param>
        /// <param name="fieldName">If not null, the name of the field in which the result is stored.</param>
        protected abstract void WriteCanvasGeometryEllipseFactory(
            CodeBuilder builder,
            CanvasGeometry.Ellipse obj,
            string typeName,
            string fieldName);

        /// <summary>
        /// Writes CanvasGeometery.Ellipse factory code.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="obj">Describes the object that should be instantiated by the factory code.</param>
        /// <param name="typeName">The type of the result.</param>
        /// <param name="fieldName">If not null, the name of the field in which the result is stored.</param>
        protected abstract void WriteCanvasGeometryGroupFactory(
            CodeBuilder builder,
            CanvasGeometry.Group obj,
            string typeName,
            string fieldName);

        /// <summary>
        /// Writes CanvasGeometery.Path factory code.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="obj">Describes the object that should be instantiated by the factory code.</param>
        /// <param name="typeName">The type of the result.</param>
        /// <param name="fieldName">If not null, the name of the field in which the result is stored.</param>
        protected abstract void WriteCanvasGeometryPathFactory(
            CodeBuilder builder,
            CanvasGeometry.Path obj,
            string typeName,
            string fieldName);

        /// <summary>
        /// Writes CanvasGeometery.RoundedRectangle factory code.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="obj">Describes the object that should be instantiated by the factory code.</param>
        /// <param name="typeName">The type of the result.</param>
        /// <param name="fieldName">If not null, the name of the field in which the result is stored.</param>
        protected abstract void WriteCanvasGeometryRoundedRectangleFactory(
            CodeBuilder builder,
            CanvasGeometry.RoundedRectangle obj,
            string typeName,
            string fieldName);

        /// <summary>
        /// Writes CanvasGeometery.TransformedGeometry factory code.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="obj">Describes the object that should be instantiated by the factory code.</param>
        /// <param name="typeName">The type of the result.</param>
        /// <param name="fieldName">If not null, the name of the field in which the result is stored.</param>
        protected abstract void WriteCanvasGeometryTransformedGeometryFactory(
            CodeBuilder builder,
            CanvasGeometry.TransformedGeometry obj,
            string typeName,
            string fieldName);

        /// <summary>
        /// Write the CompositeEffect factory code.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="effect">Composite effect object.</param>
        /// <returns>String that should be used as the parameter for CreateEffectFactory.</returns>
        protected abstract string WriteCompositeEffectFactory(
            CodeBuilder builder,
            Mgce.CompositeEffect effect);

        /// <summary>
        /// Write the GaussianBlurEffect factory code.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="effect">Gaussian blur effect object.</param>
        /// <returns>String that should be used as the parameter for CreateEffectFactory.</returns>
        protected abstract string WriteGaussianBlurEffectFactory(
            CodeBuilder builder,
            Mgce.GaussianBlurEffect effect);

        /// <summary>
        /// Writes code that initializes a theme property value in the theme property set.
        /// </summary>
        private protected void WriteThemePropertyInitialization(
            CodeBuilder builder,
            string propertySetVariableName,
            PropertyBinding prop)
            => WriteThemePropertyInitialization(
                builder,
                propertySetVariableName,
                prop,
                $"_theme{prop.BindingName}");

        /// <summary>
        /// Writes code that initializes a theme property value in the theme property set.
        /// </summary>
        private protected void WriteThemePropertyInitialization(
            CodeBuilder builder,
            string propertySetVariableName,
            PropertyBinding prop,
            string themePropertyAccessor)
        {
            var propertyValueAccessor = GetThemePropertyAccessor(themePropertyAccessor, prop);
            builder.WriteLine($"{propertySetVariableName}{Deref}Insert{PropertySetValueTypeName(prop.ActualType)}({String(prop.BindingName)}, {propertyValueAccessor});");
        }

        /// <summary>
        /// Gets code to access a theme property.
        /// </summary>
        /// <returns>
        /// An expression that gets a theme property value.
        /// </returns>
        private protected string GetThemePropertyAccessor(string accessor, PropertyBinding prop)
            => prop.ExposedType switch
            {
                // Colors are stored as Vector4 because Composition cannot animate
                // subchannels of colors.
                // The cast to Color is necessary if the accessor returns Object (for
                // example if the value is coming from a DependencyPropertyChangedEventArgs.
                PropertySetValueType.Color => $"ColorAsVector4((Color){accessor})",

                // Scalars are stored as float, but exposed as double because
                // XAML markup prefers floats.
                PropertySetValueType.Scalar => $"(float){accessor}",
                _ => accessor,
            };

        void WritePropertySetInitialization(CodeBuilder builder, CompositionPropertySet propertySet, string variableName)
        {
            foreach (var (name, type) in propertySet.Names)
            {
                var valueInitializer = PropertySetValueInitializer(propertySet, name, type);
                builder.WriteLine($"{variableName}{Deref}Insert{PropertySetValueTypeName(type)}({String(name)}, {valueInitializer});");
            }
        }

        IEnumerable<NamedConstant> GetInternalConstants()
        {
            // Get the duration in ticks.
            yield return new NamedConstant(
                DurationTicksFieldName,
                $"Animation duration: {_compositionDuration.Ticks / (double)TimeSpan.TicksPerSecond,-1:N3} seconds.",
                ConstantType.Int64,
                _compositionDuration.Ticks);

            // Get the markers.
            foreach (var marker in _markers)
            {
                yield return new NamedConstant(marker.StartConstant, $"Marker: {marker.Name}.", ConstantType.Float, (float)marker.StartProgress);
                if (marker.DurationInFrames > 0)
                {
                    yield return new NamedConstant(marker.EndConstant!, $"Marker: {marker.Name}.", ConstantType.Float, (float)marker.EndProgress);
                }
            }

            // Get the theme properties.
            foreach (var themeProperty in _sourceMetadata.PropertyBindings)
            {
                switch (themeProperty.ExposedType)
                {
                    case PropertySetValueType.Color:
                        yield return new NamedConstant($"c_theme{themeProperty.BindingName}", $"Theme property: {themeProperty.BindingName}.", ConstantType.Color, (Wui.Color)themeProperty.DefaultValue);
                        break;
                    case PropertySetValueType.Scalar:
                        yield return new NamedConstant($"c_theme{themeProperty.BindingName}", $"Theme property: {themeProperty.BindingName}.", ConstantType.Float, (float)themeProperty.DefaultValue);
                        break;
                    case PropertySetValueType.Vector2:
                    case PropertySetValueType.Vector3:
                    case PropertySetValueType.Vector4:
                    default:
                        // For now we only support some of the possible property types as constants.
                        continue;
                }
            }
        }

        /// <summary>
        /// Returns text that describes the contents of the source metadata.
        /// </summary>
        /// <returns>A list of strings describing the source.</returns>
        protected IEnumerable<string> GetSourceDescriptionLines()
        {
            // Describe the source. Currently this handles only Lottie sources.
            var metadata = _sourceMetadata.LottieMetadata;
            if (metadata is not null)
            {
                var compositionName = SanitizeCommentLine(metadata.CompositionName);

                if (!string.IsNullOrWhiteSpace(compositionName))
                {
                    yield return $"Name:        {compositionName}";
                }

                yield return $"Frame rate:  {metadata.Duration.FPS} fps";
                yield return $"Frame count: {metadata.Duration.Frames}";
                yield return $"Duration:    {metadata.Duration.Time.TotalMilliseconds:0.0} mS";

                if (_markers.Count > 0)
                {
                    foreach (var line in LottieMarkersMonospaceTableFormatter.GetMarkersDescriptionLines(_s, _markers))
                    {
                        yield return line;
                    }
                }
            }

            // If there are property bindings, output information about them.
            // But only do this if we're NOT generating a DependencyObject because
            // the property bindings available on a DependencyObject are obvious
            // from the code and repeating them here would just be noise.
            if (!_generateDependencyObject && _sourceMetadata.PropertyBindings.Count > 0)
            {
                foreach (var line in ThemePropertiesMonospaceTableFormatter.GetThemePropertyDescriptionLines(_sourceMetadata.PropertyBindings))
                {
                    yield return line;
                }
            }
        }

        /// <summary>
        /// Returns text that describes the graph statistics. This graph shows the
        /// number of objects instantiated and is designed to help with investigations
        /// or performance.
        /// </summary>
        /// <returns>A list of strings describing the graph statistics.</returns>
        IEnumerable<string> GetGraphStatsLines() =>
            GraphStatsMonospaceTableFormatter.GetGraphStatsLines(
                                    _animatedVisualGenerators.Select(avg => (avg.StatsName, avg.Objects)));

        /// <summary>
        /// Call this to get a list of the asset files referenced by the generated code.
        /// </summary>
        /// <returns>
        /// List of asset files and their relative path to the Asset folder in a UWP that are referenced by the generated code.
        /// An item in the returned list has format "ms-appx:///Assets/subFolder/fileName", which the generated code
        /// will use to load the file from.
        /// </returns>
        protected IReadOnlyList<Uri> GetAssetsList() => _loadedImageSurfaceInfos.Where(n => n.ImageUri is not null).Select(n => n.ImageUri).ToArray();

        /// <summary>
        /// Call this to generate the code. Returns a string containing the generated code.
        /// </summary>
        /// <returns>The code.</returns>
        protected string GenerateCode()
        {
            var builder = new CodeBuilder();

            // Write the auto-generated warning comment.
            foreach (var line in AutoGeneratedHeaderText)
            {
                builder.WriteLine(line);
            }

            // Write a description of the graph stats.
            builder.WritePreformattedCommentLines(GetGraphStatsLines());

            // Write the start of the file. This is everything up to the start of the AnimatedVisual class.
            WriteImplementationFileStart(builder);

            // Write the LoadedImageSurface byte arrays into the outer (IAnimatedVisualSource) class.
            WriteLoadedImageSurfaceArrays(builder);

            // Write each AnimatedVisual class.
            var firstAnimatedVisualWritten = false;
            foreach (var animatedVisualGenerator in _animatedVisualGenerators)
            {
                if (firstAnimatedVisualWritten)
                {
                    // Put a blank line between each AnimatedVisual class.
                    builder.WriteLine();
                }

                var animatedVisualBuilder = new CodeBuilder();
                animatedVisualGenerator.WriteAnimatedVisualCode(animatedVisualBuilder);
                builder.WriteCodeBuilder(animatedVisualBuilder);

                firstAnimatedVisualWritten = true;
            }

            // Write the end of the file.
            WriteImplementationFileEnd(builder);

            return builder.ToString();
        }

        /// <summary>
        /// Returns the code to call the factory for the given object.
        /// </summary>
        /// <returns>The code to call the factory for the given object.</returns>
        protected string CallFactoryFor(CanvasGeometry obj)
        {
            return _currentAnimatedVisualGenerator!.CallFactoryFor(obj);
        }

        // Makes the given text suitable for use as a single line comment.
        static string? SanitizeCommentLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Replace any new lines.
            text = text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
            text = RemoveControlCharacters(text);
            return text.Trim();
        }

        static string RemoveControlCharacters(string text)
        {
            IEnumerable<char> Sanitize()
            {
                foreach (var ch in text)
                {
                    if (char.IsControl(ch))
                    {
                        continue;
                    }

                    yield return ch;
                }
            }

            return new string(Sanitize().ToArray());
        }

        protected void WriteInitializedField(CodeBuilder builder, string typeName, string fieldName, string initialization)
            => builder.WriteLine($"{typeName} {fieldName}{initialization};");

        void WriteDefaultInitializedField(CodeBuilder builder, string typeName, string fieldName)
            => WriteInitializedField(builder, typeName, fieldName, _s.DefaultInitialize);

        // Returns true iff the given sequence has exactly one item in it.
        // This is equivalent to Count() == 1, but doesn't require the whole
        // sequence to be enumerated.
        static bool IsEqualToOne<T>(IEnumerable<T> items)
        {
            var seenOne = false;
            foreach (var item in items)
            {
                if (seenOne)
                {
                    // Already seen one item - the sequence has more than one item.
                    return false;
                }

                seenOne = true;
            }

            return seenOne;
        }

        // Returns true iff the given sequence has more than one item in it.
        // This is equivalent to Count() > 1, but doesn't require the whole
        // sequence to be enumerated.
        static bool IsGreaterThanOne<T>(IEnumerable<T> items)
        {
            var seenOne = false;
            foreach (var item in items)
            {
                if (seenOne)
                {
                    // Already seen one item - the sequence has at least one item.
                    return true;
                }

                seenOne = true;
            }

            // The sequence is empty.
            return false;
        }

        // The InReferences on a node are used to determine whether a node needs storage (it does
        // if multiple other nodes reference it), however in one case a node with multiple
        // InReferences does not need storage:
        // * If the references are only from an ExpressionAnimation that is created in the factory
        //   for the node.
        // This method gets the InReferences, filtering out those which can be ignored.
        static IEnumerable<ObjectData> FilteredInRefs(ObjectData node)
        {
            // Examine all of the inrefs to the node.
            foreach (var vertex in node.InReferences)
            {
                var from = vertex.Node;

                // If the inref is from an ExpressionAnimation ...
                if (from.Object is ExpressionAnimation exprAnim)
                {
                    // ... is the animation shared?
                    if (from.InReferences.Length > 1)
                    {
                        yield return from;
                        continue;
                    }

                    // ... is the animation animating a property on the current node or its property set?
                    var isExpressionOnThisNode = false;

                    var compObject = (CompositionObject)node.Object;

                    // Search the animators to find the animator for this ExpressionAnimation.
                    // It will be found iff the ExpressionAnimation is animating this node.
                    foreach (var animator in compObject.Animators.Concat(compObject.Properties.Animators))
                    {
                        if (animator.Animation is ExpressionAnimation animatorExpression &&
                            animatorExpression.Expression == exprAnim.Expression)
                        {
                            isExpressionOnThisNode = true;
                            break;
                        }
                    }

                    if (!isExpressionOnThisNode)
                    {
                        yield return from;
                    }
                }
                else
                {
                    yield return from;
                }
            }
        }

        string String(GenericDataObject value) =>
            value.Type switch
            {
                GenericDataObjectType.Bool => Bool(((GenericDataBool)value).Value),
                GenericDataObjectType.Number => _s.Double(((GenericDataNumber)value).Value),
                GenericDataObjectType.String => _s.String(((GenericDataString)value).Value),
                _ => throw new InvalidOperationException(),
            };

        string Bool(bool value) => value ? "true" : "false";

        string Deref => _s.Deref;

        string New(string typeName) => _s.New(typeName);

#pragma warning disable CA1033 // Interface methods should be callable by child types

        string IAnimatedVisualSourceInfo.ClassName => _className;

        string IAnimatedVisualSourceInfo.Namespace => _namespace;

        IReadOnlyList<TypeName> IAnimatedVisualSourceInfo.AdditionalInterfaces => _additionalInterfaces;

        string IAnimatedVisualSourceInfo.ReusableExpressionAnimationFieldName => SingletonExpressionAnimationName;

        string IAnimatedVisualSourceInfo.DurationTicksFieldName => DurationTicksFieldName;

        bool IAnimatedVisualSourceInfo.GenerateDependencyObject => _generateDependencyObject;

        bool IAnimatedVisualSourceInfo.Public => _generatePublicClass;

        Version IAnimatedVisualSourceInfo.WinUIVersion => _winUIVersion;

        string IAnimatedVisualSourceInfo.ThemePropertiesFieldName => ThemePropertiesFieldName;

        bool IAnimatedVisualSourceInfo.IsThemed => _isThemed;

        Vector2 IAnimatedVisualSourceInfo.CompositionDeclaredSize => _compositionDeclaredSize;

        bool IAnimatedVisualSourceInfo.UsesCanvas => Any(f => f.UsesCanvas);

        bool IAnimatedVisualSourceInfo.UsesCanvasEffects => Any(f => f.UsesCanvasEffects);

        bool IAnimatedVisualSourceInfo.UsesCanvasGeometry => Any(f => f.UsesCanvasGeometry);

        bool IAnimatedVisualSourceInfo.UsesNamespaceWindowsUIXamlMedia => Any(f => f.UsesNamespaceWindowsUIXamlMedia);

        bool IAnimatedVisualSourceInfo.UsesStreams => Any(f => f.UsesStreams);

        IReadOnlyList<IAnimatedVisualInfo> IAnimatedVisualSourceInfo.AnimatedVisualInfos => _animatedVisualGenerators;

        bool IAnimatedVisualSourceInfo.UsesCompositeEffect => Any(f => f.UsesEffect(Mgce.GraphicsEffectType.CompositeEffect));

        bool IAnimatedVisualSourceInfo.UsesGaussianBlurEffect => Any(f => f.UsesEffect(Mgce.GraphicsEffectType.GaussianBlurEffect));

        IReadOnlyList<MarkerInfo> IAnimatedVisualSourceInfo.Markers => _markers;

        IReadOnlyList<NamedConstant> IAnimatedVisualSourceInfo.InternalConstants => _internalConstants;

        IReadOnlyList<LoadedImageSurfaceInfo> IAnimatedVisualSourceInfo.LoadedImageSurfaces => _loadedImageSurfaceInfos;

        SourceMetadata IAnimatedVisualSourceInfo.SourceMetadata => _sourceMetadata;

#pragma warning restore CA1033 // Interface methods should be callable by child types

        // Return true if any of the AnimatedVisualGenerators match the given predicate.
        bool Any(Func<AnimatedVisualGenerator, bool> predicate) => _animatedVisualGenerators.Any(predicate);

        // Writes code that will return the given GenericDataMap as Windows.Data.Json.
        void WriteJsonFactory(CodeBuilder builder, GenericDataMap jsonData, string factoryName)
        {
            builder.WriteLine($"{_s.ReferenceTypeName("JsonObject")} {factoryName}()");
            builder.OpenScope();
            builder.WriteLine($"{_s.Var} result = {New("JsonObject")}();");
            WritePopulateJsonObject(builder, jsonData, "result", 0);
            builder.WriteLine($"return result;");
            builder.CloseScope();
            builder.WriteLine();
        }

        void WritePopulateJsonArray(CodeBuilder builder, GenericDataList jsonData, string arrayName, int recursionLevel)
        {
            foreach (var value in jsonData)
            {
                if (value is null)
                {
                    builder.WriteLine($"{arrayName}{Deref}Append(JsonValue{Deref}CreateNullValue());");
                }
                else
                {
                    switch (value.Type)
                    {
                        case GenericDataObjectType.Bool:
                            builder.WriteLine($"{arrayName}{Deref}Append(JsonValue{Deref}CreateBooleanValue({String(value)}));");
                            break;
                        case GenericDataObjectType.Number:
                            builder.WriteLine($"{arrayName}{Deref}Append(JsonValue{Deref}CreateNumberValue({String(value)}));");
                            break;
                        case GenericDataObjectType.String:
                            builder.WriteLine($"{arrayName}{Deref}Append(JsonValue{Deref}CreateStringValue({String(value)}));");
                            break;
                        case GenericDataObjectType.List:
                            if (((GenericDataList)value).Count == 0)
                            {
                                builder.WriteLine($"{arrayName}{Deref}Append({New("JsonArray")}());");
                            }
                            else
                            {
                                var subArrayName = $"jarray_{recursionLevel}";
                                builder.OpenScope();
                                builder.WriteLine($"{_s.Var} {subArrayName} = {New("JsonArray")}();");
                                builder.WriteLine($"result{Deref}Append({subArrayName});");
                                WritePopulateJsonArray(builder, (GenericDataList)value, subArrayName, recursionLevel + 1);
                                builder.CloseScope();
                            }

                            break;
                        case GenericDataObjectType.Map:
                            if (((GenericDataMap)value).Count == 0)
                            {
                                builder.WriteLine($"{arrayName}{Deref}Append({New("JsonObject")}());");
                            }
                            else
                            {
                                var subObjectName = $"jobject_{recursionLevel}";
                                builder.OpenScope();
                                builder.WriteLine($"{_s.Var} {subObjectName} = {New("JsonObject")}();");
                                builder.WriteLine($"result{Deref}Append({subObjectName});");
                                WritePopulateJsonObject(builder, (GenericDataMap)value, subObjectName, recursionLevel + 1);
                                builder.CloseScope();
                            }

                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
        }

        void WritePopulateJsonObject(CodeBuilder builder, GenericDataMap jsonData, string objectName, int recursionLevel)
        {
            foreach (var pair in jsonData)
            {
                var k = _s.String(pair.Key);
                var value = pair.Value;

                if (value is null)
                {
                    builder.WriteLine($"{objectName}{Deref}Add({k}, JsonValue{Deref}CreateNullValue());");
                }
                else
                {
                    switch (value.Type)
                    {
                        case GenericDataObjectType.Bool:
                            builder.WriteLine($"{objectName}{Deref}Add({k}, JsonValue{Deref}CreateBooleanValue({String(value)}));");
                            break;
                        case GenericDataObjectType.Number:
                            builder.WriteLine($"{objectName}{Deref}Add({k}, JsonValue{Deref}CreateNumberValue({String(value)}));");
                            break;
                        case GenericDataObjectType.String:
                            builder.WriteLine($"{objectName}{Deref}Add({k}, JsonValue{Deref}CreateStringValue({String(value)}));");
                            break;
                        case GenericDataObjectType.List:
                            if (((GenericDataList)value).Count == 0)
                            {
                                builder.WriteLine($"{objectName}{Deref}Add({k}, {New("JsonArray")}());");
                            }
                            else
                            {
                                var subArrayName = $"jarray_{recursionLevel}";
                                builder.OpenScope();
                                builder.WriteLine($"{_s.Var} {subArrayName} = {New("JsonArray")}();");
                                builder.WriteLine($"result{Deref}Add({k}, {subArrayName});");
                                WritePopulateJsonArray(builder, (GenericDataList)value, subArrayName, recursionLevel + 1);
                                builder.CloseScope();
                            }

                            break;
                        case GenericDataObjectType.Map:
                            if (((GenericDataMap)value).Count == 0)
                            {
                                builder.WriteLine($"{objectName}{Deref}Add({k}, {New("JsonObject")}());");
                            }
                            else
                            {
                                var subObjectName = $"jobject_{recursionLevel}";
                                builder.OpenScope();
                                builder.WriteLine($"{_s.Var} {subObjectName} = {New("JsonObject")}();");
                                builder.WriteLine($"result{Deref}Add({k}, {subObjectName});");
                                WritePopulateJsonObject(builder, (GenericDataMap)value, subObjectName, recursionLevel + 1);
                                builder.CloseScope();
                            }

                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
        }

        // Write the LoadedImageSurface byte arrays into the outer (IAnimatedVisualSource) class.
        void WriteLoadedImageSurfaceArrays(CodeBuilder builder)
        {
            foreach (var loadedImageSurface in _loadedImageSurfaceInfos)
            {
                if (loadedImageSurface.Bytes is not null)
                {
                    builder.WriteComment(loadedImageSurface.Comment);
                    WriteByteArrayField(builder, loadedImageSurface.BytesFieldName, loadedImageSurface.Bytes);
                    builder.WriteLine();
                }
            }
        }

        static LoadedImageSurfaceInfo LoadedImageSurfaceInfoFromObjectData(ObjectData node)
        {
            if (!node.IsLoadedImageSurface)
            {
                throw new InvalidOperationException();
            }

            var bytes = (node.Object as Wmd.LoadedImageSurfaceFromStream)?.Bytes;
            return new LoadedImageSurfaceInfo(
                                node.TypeName,
                                node.Name!,
                                node.ShortComment,
                                node.FieldName!,
                                node.LoadedImageSurfaceBytesFieldName!,
                                node.LoadedImageSurfaceImageUri!,
                                ((Wmd.LoadedImageSurface)node.Object).Type,
                                bytes: bytes);
        }

        // Orders nodes by their types, then their names using alpha-numeric ordering (which
        // is the most natural ordering for code names that contain embedded numbers).
        static IEnumerable<ObjectData> OrderByName(IEnumerable<ObjectData> nodes) =>
            nodes.OrderBy(n => n.TypeName).ThenBy(n => n.Name, AlphanumericStringComparer.Instance);

        // Orders nodes by their type name, then by their name using alpha-numeric ordering
        // (which is the most natural ordering for code names that contain embedded numbers).
        static IEnumerable<ObjectData> OrderByTypeThenName(IEnumerable<ObjectData> nodes) =>
            nodes.OrderBy(n => n.TypeName).ThenBy(n => n.Name, AlphanumericStringComparer.Instance);

        private protected static string PropertySetValueTypeName(PropertySetValueType value)
            => value switch
            {
                PropertySetValueType.Color => "Color",
                PropertySetValueType.Scalar => "Scalar",
                PropertySetValueType.Vector2 => "Vector2",
                PropertySetValueType.Vector3 => "Vector3",
                PropertySetValueType.Vector4 => "Vector4",
                _ => throw new InvalidOperationException(),
            };

        string PropertySetValueInitializer(CompositionPropertySet propertySet, string propertyName, PropertySetValueType propertyType)
            => propertyType switch
            {
                PropertySetValueType.Color => PropertySetColorValueInitializer(propertySet, propertyName),
                PropertySetValueType.Scalar => PropertySetScalarValueInitializer(propertySet, propertyName),
                PropertySetValueType.Vector2 => PropertySetVector2ValueInitializer(propertySet, propertyName),
                PropertySetValueType.Vector3 => PropertySetVector3ValueInitializer(propertySet, propertyName),
                PropertySetValueType.Vector4 => PropertySetVector4ValueInitializer(propertySet, propertyName),
                _ => throw new InvalidOperationException(),
            };

        string PropertySetColorValueInitializer(CompositionPropertySet propertySet, string propertyName)
            => propertySet.TryGetColor(propertyName, out var value) == CompositionGetValueStatus.Succeeded
                    ? _s.Color(value!.Value)
                    : throw new InvalidOperationException();

        string PropertySetScalarValueInitializer(CompositionPropertySet propertySet, string propertyName)
            => propertySet.TryGetScalar(propertyName, out var value) == CompositionGetValueStatus.Succeeded
                    ? _s.Float(value!.Value)
                    : throw new InvalidOperationException();

        string PropertySetVector2ValueInitializer(CompositionPropertySet propertySet, string propertyName)
            => propertySet.TryGetVector2(propertyName, out var value) == CompositionGetValueStatus.Succeeded
                    ? _s.Vector2(value!.Value)
                    : throw new InvalidOperationException();

        string PropertySetVector3ValueInitializer(CompositionPropertySet propertySet, string propertyName)
            => propertySet.TryGetVector3(propertyName, out var value) == CompositionGetValueStatus.Succeeded
                    ? _s.Vector3(value!.Value)
                    : throw new InvalidOperationException();

        string PropertySetVector4ValueInitializer(CompositionPropertySet propertySet, string propertyName)
            => propertySet.TryGetVector4(propertyName, out var value) == CompositionGetValueStatus.Succeeded
                    ? _s.Vector4(value!.Value)
                    : throw new InvalidOperationException();

        /// <summary>
        /// Generates an IAnimatedVisual implementation.
        /// </summary>
        sealed class AnimatedVisualGenerator : IAnimatedVisualInfo
        {
            readonly HashSet<(ObjectData, ObjectData)> _factoriesAlreadyCalled = new HashSet<(ObjectData, ObjectData)>();
            readonly InstantiatorGeneratorBase _owner;
            readonly Stringifier _s;
            readonly ObjectData _rootNode;
            readonly ObjectGraph<ObjectData> _objectGraph;
            readonly uint _requiredUapVersion;
            readonly bool _isPartOfMultiVersionSource;

            // The subset of the object graph for which factories will be generated.
            readonly ObjectData[] _nodes;

            private CodeBuilder? _rootCodeBuilder = null;

            bool controllerCreatedInCreateAnimationsMethod = false;

            IReadOnlyList<LoadedImageSurfaceInfo>? _loadedImageSurfaceInfos;

            // Holds the node for which a factory is currently being written.
            ObjectData? _currentObjectFactoryNode;

            private CodeBuilder _createAnimationsCodeBuilder = new CodeBuilder();
            private CodeBuilder _destroyAnimationsCodeBuilder = new CodeBuilder();

            private CodegenConfiguration _configuration;

            internal AnimatedVisualGenerator(
                InstantiatorGeneratorBase owner,
                CompositionObject graphRoot,
                uint requiredUapVersion,
                bool isPartOfMultiVersionSource,
                CodegenConfiguration configuration)
            {
                _owner = owner;
                _s = _owner._s;
                _requiredUapVersion = requiredUapVersion;
                _isPartOfMultiVersionSource = isPartOfMultiVersionSource;
                _configuration = configuration;

                // Build the object graph.
                _objectGraph = ObjectGraph<ObjectData>.FromCompositionObject(graphRoot, includeVertices: true);

                // Force inlining on CompositionPath nodes that are only referenced once, because their factories
                // are always very simple.
                foreach (var node in _objectGraph.Nodes.Where(
                                        n => n.Type == Graph.NodeType.CompositionPath &&
                                        IsEqualToOne(FilteredInRefs(n))))
                {
                    node.ForceInline(() =>
                    {
                        var inlinedFactoryCode = CallFactoryFromFor(node, ((CompositionPath)node.Object).Source);
                        return $"{New("CompositionPath")}({_s.CanvasGeometryFactoryCall(inlinedFactoryCode)})";
                    });
                }

                // Force inlining on CubicBezierEasingFunction nodes that are only referenced once, because their factories
                // are always very simple.
                foreach (var (node, obj) in _objectGraph.CompositionObjectNodes.Where(
                                        n => n.Object is CubicBezierEasingFunction &&
                                            IsEqualToOne(FilteredInRefs(n.Node))))
                {
                    node.ForceInline(() =>
                    {
                        return CallCreateCubicBezierEasingFunction((CubicBezierEasingFunction)node.Object);
                    });
                }

                // If there is a theme property set, give it a special name and
                // mark it as shared. The theme property set is the only unowned property set.
                foreach (var (node, obj) in _objectGraph.CompositionObjectNodes.Where(
                        n => n.Object is CompositionPropertySet cps && cps.Owner is null))
                {
                    node.Name = "ThemeProperties";
                    node.IsSharedNode = true;

                    // If there's a theme property set, this IAnimatedVisual is themed.
                    IsThemed = true;
                }

                // Mark all the LoadedImageSurface nodes as shared and ensure they have storage.
                foreach (var (node, _) in _objectGraph.LoadedImageSurfaceNodes)
                {
                    node.IsSharedNode = true;
                }

                // Get the nodes that will produce factory methods.
                var factoryNodes = _objectGraph.Nodes.Where(n => n.NeedsAFactory).ToArray();

                // Give names to each node, except the nodes that may be shared by multiple IAnimatedVisuals.
                foreach ((var n, var name) in NodeNamer<ObjectData>.GenerateNodeNames(factoryNodes.Where(n => !n.IsSharedNode)))
                {
                    n.Name = name;
                }

                // Force storage to be allocated for nodes that have multiple references to them,
                // or are LoadedImageSurfaces.
                foreach (var node in _objectGraph.Nodes)
                {
                    if (node.IsSharedNode)
                    {
                        // Shared nodes are cached and shared between IAnimatedVisual instances, so
                        // they require storage.
                        node.RequiresStorage = true;
                        node.RequiresReadonlyStorage = true;
                    }
                    else if (IsGreaterThanOne(FilteredInRefs(node)))
                    {
                        // Node is referenced more than once so it requires storage.
                        if (node.Object is CompositionPropertySet propertySet)
                        {
                            // The node is a CompositionPropertySet. Rather than storing
                            // it, store the owner of the CompositionPropertySet. The
                            // CompositionPropertySet can be reached from its owner.
                            if (propertySet.Owner is not null)
                            {
                                var propertySetOwner = NodeFor(propertySet.Owner);
                                propertySetOwner.RequiresStorage = true;
                            }
                        }
                        else
                        {
                            node.RequiresStorage = true;
                        }
                    }
                    else if ((configuration.ImplementCreateAndDestroyMethods && node.Object is CompositionObject obj && obj.Animators.Count() > 0) || (node.Object is AnimationController c && c.IsCustom))
                    {
                        // If we are implementing IAnimatedVisual2 interface we need to store all the composition objects that have animators.
                        node.RequiresStorage = true;
                    }
                }

                // Find the root node.
                _rootNode = NodeFor(graphRoot);

                _rootNode.IsRootNode = true;

                // Ensure the root object has storage because it is referenced from IAnimatedVisual::RootVisual.
                _rootNode.RequiresStorage = true;

                // Save the nodes, ordered by name.
                _nodes = OrderByName(factoryNodes).ToArray();
            }

            internal IEnumerable<object> Objects =>
                _objectGraph.CompositionObjectNodes.Select(n => n.Object).
                Cast<object>().
                Concat(_objectGraph.CanvasGeometryNodes.Select(n => n.Object)).
                Concat(_objectGraph.CompositionPathNodes.Select(n => n.Object));

            // Returns the node for the theme CompositionPropertySet, or null if the
            // IAnimatedVisual does not support theming.
            internal bool IsThemed { get; }

            // Returns the nodes that are shared between multiple IAnimatedVisuals.
            // The fields for these are stored on the IAnimatedVisualSource.
            internal IEnumerable<ObjectData> GetSharedNodes() => _objectGraph.Nodes.Where(n => n.IsSharedNode);

            // Returns the node for the given object.
            ObjectData NodeFor(CompositionObject obj) => _objectGraph[obj];

            ObjectData NodeFor(CompositionPath obj) => _objectGraph[obj];

            ObjectData NodeFor(Wg.IGeometrySource2D obj) => _objectGraph[obj];

            ObjectData NodeFor(Wmd.LoadedImageSurface obj) => _objectGraph[obj];

            internal bool UsesCanvas => _nodes.Where(n => n.UsesCanvas).Any();

            internal bool UsesCanvasEffects => _nodes.Where(n => n.UsesCanvasEffects).Any();

            internal bool UsesCanvasGeometry => _nodes.Where(n => n.UsesCanvasGeometry).Any();

            internal bool UsesNamespaceWindowsUIXamlMedia => _nodes.Where(n => n.UsesNamespaceWindowsUIXamlMedia).Any();

            internal bool UsesStreams => _nodes.Where(n => n.UsesStream).Any();

            internal bool HasLoadedImageSurface => _nodes.Where(n => n.IsLoadedImageSurface).Any();

            internal bool UsesEffect(Mgce.GraphicsEffectType effectType) => _nodes.Where(n => n.UsesEffect(effectType)).Any();

            string ConstExprField(string type, string name, string value) => _s.ConstExprField(type, name, value);

            string Deref => _s.Deref;

            string New(string typeName) => _s.New(typeName);

            string Null => _s.Null;

            string ReferenceTypeName(string value) => _s.ReferenceTypeName(value);

            string ConstVar => _s.ConstVar;

            string Var => _s.Var;

            string Bool(bool value) => value ? "true" : "false";

            string Color(Wui.Color value) => _s.Color(value);

            string IListAdd => _s.IListAdd;

            string Float(float value) => _s.Float(value);

            string Int(int value) => _s.Int32(value);

            string Matrix3x2(Sn.Matrix3x2 value) => _s.Matrix3x2(value);

            string Matrix4x4(Matrix4x4 value) => _s.Matrix4x4(value);

            // readonly on C#, const on C++.
            string Readonly(string value) => _s.Readonly(value);

            string String(WinCompData.Expressions.Expression value) => String(value.ToText());

            string String(string value) => _s.String(value);

            string Vector2(Sn.Vector2 value) => _s.Vector2(value);

            string Vector3(Sn.Vector3 value) => _s.Vector3(value);

            string Vector4(Sn.Vector4 value) => _s.Vector4(value);

            string BorderMode(CompositionBorderMode value) => _s.BorderMode(value);

            string ColorSpace(CompositionColorSpace value) => _s.ColorSpace(value);

            string DropShadowSourcePolicy(CompositionDropShadowSourcePolicy value) => _s.DropShadowSourcePolicy(value);

            string ExtendMode(CompositionGradientExtendMode value) => _s.ExtendMode(value);

            string MappingMode(CompositionMappingMode value) => _s.MappingMode(value);

            string StrokeCap(CompositionStrokeCap value) => _s.StrokeCap(value);

            string StrokeLineJoin(CompositionStrokeLineJoin value) => _s.StrokeLineJoin(value);

            string TimeSpan(TimeSpan value) => value == _owner._compositionDuration ? _s.TimeSpan(DurationTicksFieldName) : _s.TimeSpan(value);

            /// <summary>
            /// Returns the code to call the factory for the given object.
            /// </summary>
            /// <returns>The code to call the factory for the given object.</returns>
            internal string CallFactoryFor(CanvasGeometry obj)
                => CallFactoryFromFor(_currentObjectFactoryNode!, obj);

            // Returns the code to call the factory for the given node from the given node.
            string CallFactoryFromFor(ObjectData callerNode, ObjectData calleeNode)
            {
                if (callerNode.CallFactoryFromForCache.TryGetValue(calleeNode, out string? result))
                {
                    // Return the factory from the cache.
                    return result;
                }

                // Get the factory call code.
                result = CallFactoryFromFor_UnCached(callerNode, calleeNode);

                // Save the factory call code in the cache on the caller for next time.
                if (calleeNode.RequiresStorage && !_owner._disableFieldOptimization)
                {
                    // The node has storage for its result. Next time just return the field.
                    callerNode.CallFactoryFromForCache.Add(calleeNode, calleeNode.FieldName!);
                }
                else
                {
                    callerNode.CallFactoryFromForCache.Add(calleeNode, result);
                }

                return result;
            }

            // Returns the code to call the factory for the given node from the given node.
            string CallFactoryFromFor_UnCached(ObjectData callerNode, ObjectData calleeNode)
            {
                // Calling into the root node is handled specially. The root node is always
                // created before the first vertex to it, so it is sufficient to just get
                // it from its field.
                if (calleeNode == _rootNode)
                {
                    Debug.Assert(calleeNode.RequiresStorage, "Root node is not stored in a field");
                    return calleeNode.FieldName!;
                }

                if (calleeNode.Object is CompositionPropertySet propertySet)
                {
                    // CompositionPropertySets do not have factories unless they are
                    // unowned. The call to the factory is therefore a call to the owner's
                    // factory, then a dereference of the ".Properties" property on the owner.
                    if (propertySet.Owner is not null)
                    {
                        return _s.PropertyGet(CallFactoryFromFor(callerNode, NodeFor(propertySet.Owner)), "Properties");
                    }
                }

                if (_owner._disableFieldOptimization)
                {
                    // When field optimization is disabled, always return a call to the factory.
                    // If the factory has been called already, it will return the value from
                    // its storage.
                    return calleeNode.FactoryCall();
                }

                // Find the vertex from caller to callee.
                var firstVertexFromCallerToCallee =
                        (from inref in calleeNode.InReferences
                         where inref.Node == callerNode
                         orderby inref.Position
                         select inref).FirstOrDefault();

                if (firstVertexFromCallerToCallee.Node is null &&
                    calleeNode.Object is CompositionObject calleeCompositionObject)
                {
                    // Didn't find a reference from caller to callee. The reference may be to
                    // the property set of the callee.
                    var propertySetNode = NodeFor(calleeCompositionObject.Properties);
                    firstVertexFromCallerToCallee =
                        (from inref in propertySetNode.InReferences
                         where inref.Node == callerNode
                         orderby inref.Position
                         select inref).First();
                }

                // Find the first vertex to the callee from any caller.
                var firstVertexToCallee = calleeNode.InReferences.First();

                // If the object has a vertex with a lower position then the object
                // will have already been created by the time the caller needs the object.
                if (firstVertexToCallee.Position < firstVertexFromCallerToCallee.Position)
                {
                    // The object was created by another caller. Just access the field.
                    Debug.Assert(calleeNode.RequiresStorage, "Expecting to access a field containing a previously cached value, but the callee has no field");
                    return calleeNode.FieldName!;
                }
                else if (calleeNode.RequiresStorage && _factoriesAlreadyCalled.Contains((callerNode, calleeNode)))
                {
                    return calleeNode.FieldName!;
                }
                else
                {
                    // Keep track of the fact that the caller called the factory
                    // already. If the caller asks for the factory twice and the factory
                    // does not have a cache, then the caller was expected to store the
                    // result in a local.
                    // NOTE: currently there is no generated code that is known to hit this case,
                    // so this is just here to ensure we find it if it happens.
                    if (!_factoriesAlreadyCalled.Add((callerNode, calleeNode)))
                    {
                        throw new InvalidOperationException();
                    }

                    return calleeNode.FactoryCall();
                }
            }

            // Returns the code to call the factory for the given object from the given node.
            string CallFactoryFromFor(ObjectData callerNode, CompositionObject? obj) =>
                obj is null
                ? _s.Null
                : CallFactoryFromFor(callerNode, NodeFor(obj));

            string CallFactoryFromFor(ObjectData callerNode, CompositionPath obj) => CallFactoryFromFor(callerNode, NodeFor(obj));

            string CallFactoryFromFor(ObjectData callerNode, Wg.IGeometrySource2D obj) => CallFactoryFromFor(callerNode, NodeFor(obj));

            bool GenerateCompositionPathFactory(CodeBuilder builder, CompositionPath obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                var canvasGeometry = _objectGraph[(CanvasGeometry)obj.Source];
                WriteCreateAssignment(builder, node, $"{New("CompositionPath")}({_s.CanvasGeometryFactoryCall(canvasGeometry.FactoryCall())})");
                WriteObjectFactoryEnd(builder);
                return true;
            }

            void WriteObjectFactoryStart(CodeBuilder builder, ObjectData node, IEnumerable<string>? parameters = null)
            {
                // Save the node as the current node while the factory is being written.
                _currentObjectFactoryNode = node;
                builder.WriteComment(node.LongComment);

                // Write the signature of the method.
                builder.WriteLine($"{_owner._s.ReferenceTypeName(node.TypeName)} {node.Name}({(parameters is null ? string.Empty : string.Join(", ", parameters))})");
                builder.OpenScope();
            }

            void WriteObjectFactoryEnd(CodeBuilder builder)
            {
                builder.WriteLine("return result;");
                builder.CloseScope();
                builder.WriteLine();
                _currentObjectFactoryNode = null;
            }

            /// <summary>
            /// Combines the calls to <see cref="StartAnimations(CodeBuilder, CompositionObject, ObjectData, string)"/>
            /// with <see cref="WriteObjectFactoryEnd(CodeBuilder)"/>.
            /// </summary>
            void WriteCompositionObjectFactoryEnd(CodeBuilder builder, CompositionObject obj, ObjectData node)
            {
                if (_configuration.ImplementCreateAndDestroyMethods)
                {
                    // Use FieldName as the reference name in case if we are implementing IAnimatedVisual2.
                    // We can't use local name "result" since animations will be started from different place.
                    StartAnimations(builder, obj, node, node.FieldName ?? string.Empty, ref controllerCreatedInCreateAnimationsMethod);
                }
                else
                {
                    StartAnimationsOnResult(builder, obj, node);
                }

                WriteObjectFactoryEnd(builder);
            }

            // Writes a factory that just creates an object but doesn't parameterize it before it is returned.
            void WriteSimpleObjectFactory(CodeBuilder builder, ObjectData node, string createCallText)
            {
                WriteObjectFactoryStart(builder, node);
                if (node.RequiresStorage)
                {
                    if (_owner._disableFieldOptimization)
                    {
                        // Create the object unless it has already been created.
                        builder.WriteLine($"return ({node.FieldName} == {Null})");
                        builder.Indent();
                        builder.WriteLine($"? {node.FieldName} = {createCallText}");
                        builder.WriteLine($": {node.FieldName};");
                        builder.UnIndent();
                    }
                    else
                    {
                        // If field optimization is enabled, the method will only get called once.
                        builder.WriteLine($"return {node.FieldName} = {createCallText};");
                    }
                }
                else
                {
                    // The object is only used once.
                    builder.WriteLine($"return {createCallText};");
                }

                builder.CloseScope();
                builder.WriteLine();
                _currentObjectFactoryNode = null;
            }

            void WriteCreateAssignment(CodeBuilder builder, ObjectData node, string createCallText)
            {
                if (node.RequiresStorage)
                {
                    if (_owner._disableFieldOptimization)
                    {
                        // If the field has already been assigned, return its value.
                        builder.WriteLine($"if ({node.FieldName} != {Null}) {{ return {node.FieldName}; }}");
                    }

                    builder.WriteLine($"{ConstVar} result = {node.FieldName} = {createCallText};");
                }
                else
                {
                    builder.WriteLine($"{ConstVar} result = {createCallText};");
                }
            }

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, bool? value, string target = "result")
                => WriteSetPropertyStatement(builder, propertyName, value, formatter: Bool, target);

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, int? value, string target = "result")
                => WriteSetPropertyStatement(builder, propertyName, value, formatter: Int, target);

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, float? value, string target = "result")
                => WriteSetPropertyStatement(builder, propertyName, value, formatter: Float, target);

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, CompositionStrokeCap? value, string target = "result")
                => WriteSetPropertyStatement(builder, propertyName, value, formatter: StrokeCap, target);

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, Vector2? value, string target = "result")
                => WriteSetPropertyStatement(builder, propertyName, value, formatter: Vector2, target);

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, Vector3? value, string target = "result")
                => WriteSetPropertyStatement(builder, propertyName, value, formatter: Vector3, target);

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, Matrix3x2? value, string target = "result")
            {
                if (value.HasValue)
                {
                    WriteMatrixComment(builder, value.Value);
                    WriteSetPropertyStatement(builder, propertyName, value, formatter: Matrix3x2, target);
                }
            }

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, Matrix4x4? value, string target = "result")
            {
                if (value.HasValue)
                {
                    WriteMatrixComment(builder, value.Value);
                    WriteSetPropertyStatement(builder, propertyName, value, formatter: Matrix4x4, target);
                }
            }

            void WriteSetPropertyStatement<T>(CodeBuilder builder, string propertyName, T? value, Func<T, string> formatter, string target = "result")
                where T : struct
            {
                if (value.HasValue)
                {
                    WriteSetPropertyStatement(builder, propertyName, formatter(value.Value), target);
                }
            }

            void WriteSetPropertyStatementDefaultIsNullOrWhitespace(
                CodeBuilder builder,
                string propertyName,
                string? value,
                string target = "result")
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    WriteSetPropertyStatement(builder, propertyName, String(value), target);
                }
            }

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, string value, string target = "result")
            {
                builder.WriteLine($"{_s.PropertySet(target, propertyName, value)};");
            }

            void WriteSetPropertyStatement(CodeBuilder builder, string propertyName, CompositionObject? value, ObjectData callerNode, string target = "result")
            {
                if (value is not null)
                {
                    WriteSetPropertyStatement(builder, propertyName, CallFactoryFromFor(callerNode, value), target);
                }
            }

            void WritePopulateShapesCollection(CodeBuilder builder, IList<CompositionShape> shapes, ObjectData node)
            {
                switch (shapes.Count)
                {
                    case 0:
                        // No items, nothing to do.
                        break;

                    case 1:
                        {
                            // A single item. We can add the shape in a single line.
                            var shape = shapes[0];
                            WriteShortDescriptionComment(builder, shape);
                            builder.WriteLine($"{_s.PropertyGet("result", "Shapes")}{Deref}{IListAdd}({CallFactoryFromFor(node, shape)});");
                            break;
                        }

                    default:
                        {
                            // Multiple items requires the use of a local.
                            builder.WriteLine($"{ConstVar} shapes = {_s.PropertyGet("result", "Shapes")};");
                            foreach (var shape in shapes)
                            {
                                WriteShortDescriptionComment(builder, shape);
                                builder.WriteLine($"shapes{Deref}{IListAdd}({CallFactoryFromFor(node, shape)});");
                            }

                            break;
                        }
                }
            }

            void WriteFrameNumberComment(CodeBuilder builder, double progress)
            {
                builder.WriteComment($"Frame {_owner._sourceMetadata.ProgressToFrameNumber(progress):0.##}.");
            }

            internal void WriteAnimatedVisualCode(CodeBuilder builder)
            {
                _owner._currentAnimatedVisualGenerator = this;

                // Write the body of the AnimatedVisual class.
                _owner.WriteAnimatedVisualStart(builder, this);

                _rootCodeBuilder = builder;

                // Write fields for constant values.
                builder.WriteLine(ConstExprField(_s.TypeInt64, DurationTicksFieldName, $"{_s.Int64(_owner._compositionDuration.Ticks)}"));

                // Write fields for each object that needs storage (i.e. objects that are referenced more than once).
                // Write read-only fields first.
                _owner.WriteDefaultInitializedField(builder, Readonly(_s.ReferenceTypeName("Compositor")), "_c");
                _owner.WriteDefaultInitializedField(builder, Readonly(_s.ReferenceTypeName("ExpressionAnimation")), SingletonExpressionAnimationName);

                if (_owner._isThemed)
                {
                    _owner.WriteDefaultInitializedField(builder, Readonly(_s.ReferenceTypeName("CompositionPropertySet")), ThemePropertiesFieldName);
                }

                WriteFields(builder);

                builder.WriteLine();

                builder.WriteSubBuilder("StartProgressBoundAnimation");
                builder.WriteSubBuilder("BindProperty");
                builder.WriteSubBuilder("BindProperty2");
                builder.WriteSubBuilder("CreateBooleanKeyFrameAnimation");
                builder.WriteSubBuilder("CreateColorKeyFrameAnimation");
                builder.WriteSubBuilder("CreatePathKeyFrameAnimation");
                builder.WriteSubBuilder("CreateScalarKeyFrameAnimation");
                builder.WriteSubBuilder("CreateVector2KeyFrameAnimation");
                builder.WriteSubBuilder("CreateVector3KeyFrameAnimation");
                builder.WriteSubBuilder("CreateVector4KeyFrameAnimation");
                builder.WriteSubBuilder("CreateSpriteShape");
                builder.WriteSubBuilder("CreateSpriteShapeWithFillBrush");

                if (_configuration.ImplementCreateAndDestroyMethods)
                {
                    _createAnimationsCodeBuilder = new CodeBuilder();
                    _destroyAnimationsCodeBuilder = new CodeBuilder();
                }

                // Write factory methods for each node.
                foreach (var node in _nodes)
                {
                    // Only generate a factory method if the node is not inlined into the caller.
                    if (!node.Inlined)
                    {
                        WriteFactoryForNode(builder, node);
                    }
                }

                // Write the end of the AnimatedVisual class.
                _owner.WriteAnimatedVisualEnd(builder, this, _createAnimationsCodeBuilder, _destroyAnimationsCodeBuilder);

                _owner._currentAnimatedVisualGenerator = null;
            }

            void WriteFields(CodeBuilder builder)
            {
                foreach (var node in OrderByTypeThenName(_nodes.Where(n => n.RequiresReadonlyStorage)))
                {
                    // Generate a field for the read-only storage.
                    _owner.WriteDefaultInitializedField(builder, Readonly(_s.ReferenceTypeName(node.TypeName)), node.FieldName!);
                }

                foreach (var node in OrderByTypeThenName(_nodes.Where(n => n.RequiresStorage && !n.RequiresReadonlyStorage)))
                {
                    // Generate a field for the non-read-only storage.
                    _owner.WriteDefaultInitializedField(builder, _s.ReferenceTypeName(node.TypeName), node.FieldName!);
                }
            }

            // Generates a factory method for the given node. The code is written into the given CodeBuilder.
            void WriteFactoryForNode(CodeBuilder builder, ObjectData node)
            {
                switch (node.Type)
                {
                    case Graph.NodeType.CompositionObject:
                        GenerateObjectFactory(builder, (CompositionObject)node.Object, node);
                        break;
                    case Graph.NodeType.CompositionPath:
                        GenerateCompositionPathFactory(builder, (CompositionPath)node.Object, node);
                        break;
                    case Graph.NodeType.CanvasGeometry:
                        GenerateCanvasGeometryFactory(builder, (CanvasGeometry)node.Object, node);
                        break;
                    case Graph.NodeType.LoadedImageSurface:
                        // LoadedImageSurface is written out in the IDynamicAnimatedVisualSource class, so does not need to do anything here.
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            string CallCreateCubicBezierEasingFunction(CubicBezierEasingFunction obj)
                => $"_c{Deref}CreateCubicBezierEasingFunction({Vector2(obj.ControlPoint1)}, {Vector2(obj.ControlPoint2)})";

            bool GenerateCanvasGeometryFactory(CodeBuilder builder, CanvasGeometry obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                var typeName = _s.ReferenceTypeName(node.TypeName);
                var fieldName = node.FieldName!;

                switch (obj.Type)
                {
                    case CanvasGeometry.GeometryType.Combination:
                        _owner.WriteCanvasGeometryCombinationFactory(builder, (CanvasGeometry.Combination)obj, typeName, fieldName);
                        break;
                    case CanvasGeometry.GeometryType.Ellipse:
                        _owner.WriteCanvasGeometryEllipseFactory(builder, (CanvasGeometry.Ellipse)obj, typeName, fieldName);
                        break;
                    case CanvasGeometry.GeometryType.Group:
                        _owner.WriteCanvasGeometryGroupFactory(builder, (CanvasGeometry.Group)obj, typeName, fieldName);
                        break;
                    case CanvasGeometry.GeometryType.Path:
                        _owner.WriteCanvasGeometryPathFactory(builder, (CanvasGeometry.Path)obj, typeName, fieldName);
                        break;
                    case CanvasGeometry.GeometryType.RoundedRectangle:
                        _owner.WriteCanvasGeometryRoundedRectangleFactory(builder, (CanvasGeometry.RoundedRectangle)obj, typeName, fieldName);
                        break;
                    case CanvasGeometry.GeometryType.TransformedGeometry:
                        _owner.WriteCanvasGeometryTransformedGeometryFactory(builder, (CanvasGeometry.TransformedGeometry)obj, typeName, fieldName);
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                WriteObjectFactoryEnd(builder);
                return true;
            }

            bool GenerateObjectFactory(CodeBuilder builder, CompositionObject obj, ObjectData node)
            {
                // Uncomment to see the order of creation.
                //builder.WriteComment($"Traversal order: {node.Position}");
                return obj.Type switch
                {
                    // Do not generate code for animation controllers. It is done inline in the CompositionObject initialization.
                    CompositionObjectType.AnimationController => GenerateCustomAnimationController(builder, (AnimationController)obj, node),
                    CompositionObjectType.BooleanKeyFrameAnimation => GenerateBooleanKeyFrameAnimationFactory(builder, (BooleanKeyFrameAnimation)obj, node),
                    CompositionObjectType.ColorKeyFrameAnimation => GenerateColorKeyFrameAnimationFactory(builder, (ColorKeyFrameAnimation)obj, node),
                    CompositionObjectType.CompositionColorBrush => GenerateCompositionColorBrushFactory(builder, (CompositionColorBrush)obj, node),
                    CompositionObjectType.CompositionColorGradientStop => GenerateCompositionColorGradientStopFactory(builder, (CompositionColorGradientStop)obj, node),
                    CompositionObjectType.CompositionContainerShape => GenerateContainerShapeFactory(builder, (CompositionContainerShape)obj, node),
                    CompositionObjectType.CompositionEffectBrush => GenerateCompositionEffectBrushFactory(builder, (CompositionEffectBrush)obj, node),
                    CompositionObjectType.CompositionEllipseGeometry => GenerateCompositionEllipseGeometryFactory(builder, (CompositionEllipseGeometry)obj, node),
                    CompositionObjectType.CompositionGeometricClip => GenerateCompositionGeometricClipFactory(builder, (CompositionGeometricClip)obj, node),
                    CompositionObjectType.CompositionLinearGradientBrush => GenerateCompositionLinearGradientBrushFactory(builder, (CompositionLinearGradientBrush)obj, node),
                    CompositionObjectType.CompositionMaskBrush => GenerateCompositionMaskBrushFactory(builder, (CompositionMaskBrush)obj, node),
                    CompositionObjectType.CompositionPathGeometry => GenerateCompositionPathGeometryFactory(builder, (CompositionPathGeometry)obj, node),

                    // Do not generate code for property sets. It is done inline in the CompositionObject initialization.
                    CompositionObjectType.CompositionPropertySet => true,
                    CompositionObjectType.CompositionRadialGradientBrush => GenerateCompositionRadialGradientBrushFactory(builder, (CompositionRadialGradientBrush)obj, node),
                    CompositionObjectType.CompositionRectangleGeometry => GenerateCompositionRectangleGeometryFactory(builder, (CompositionRectangleGeometry)obj, node),
                    CompositionObjectType.CompositionRoundedRectangleGeometry => GenerateCompositionRoundedRectangleGeometryFactory(builder, (CompositionRoundedRectangleGeometry)obj, node),
                    CompositionObjectType.CompositionSpriteShape => GenerateSpriteShapeFactory(builder, (CompositionSpriteShape)obj, node),
                    CompositionObjectType.CompositionSurfaceBrush => GenerateCompositionSurfaceBrushFactory(builder, (CompositionSurfaceBrush)obj, node),
                    CompositionObjectType.CompositionViewBox => GenerateCompositionViewBoxFactory(builder, (CompositionViewBox)obj, node),
                    CompositionObjectType.CompositionVisualSurface => GenerateCompositionVisualSurfaceFactory(builder, (CompositionVisualSurface)obj, node),
                    CompositionObjectType.ContainerVisual => GenerateContainerVisualFactory(builder, (ContainerVisual)obj, node),
                    CompositionObjectType.CubicBezierEasingFunction => GenerateCubicBezierEasingFunctionFactory(builder, (CubicBezierEasingFunction)obj, node),
                    CompositionObjectType.DropShadow => GenerateDropShadowFactory(builder, (DropShadow)obj, node),
                    CompositionObjectType.ExpressionAnimation => GenerateExpressionAnimationFactory(builder, (ExpressionAnimation)obj, node),
                    CompositionObjectType.InsetClip => GenerateInsetClipFactory(builder, (InsetClip)obj, node),
                    CompositionObjectType.LayerVisual => GenerateLayerVisualFactory(builder, (LayerVisual)obj, node),
                    CompositionObjectType.LinearEasingFunction => GenerateLinearEasingFunctionFactory(builder, (LinearEasingFunction)obj, node),
                    CompositionObjectType.PathKeyFrameAnimation => GeneratePathKeyFrameAnimationFactory(builder, (PathKeyFrameAnimation)obj, node),
                    CompositionObjectType.ScalarKeyFrameAnimation => GenerateScalarKeyFrameAnimationFactory(builder, (ScalarKeyFrameAnimation)obj, node),
                    CompositionObjectType.ShapeVisual => GenerateShapeVisualFactory(builder, (ShapeVisual)obj, node),
                    CompositionObjectType.SpriteVisual => GenerateSpriteVisualFactory(builder, (SpriteVisual)obj, node),
                    CompositionObjectType.StepEasingFunction => GenerateStepEasingFunctionFactory(builder, (StepEasingFunction)obj, node),
                    CompositionObjectType.Vector2KeyFrameAnimation => GenerateVector2KeyFrameAnimationFactory(builder, (Vector2KeyFrameAnimation)obj, node),
                    CompositionObjectType.Vector3KeyFrameAnimation => GenerateVector3KeyFrameAnimationFactory(builder, (Vector3KeyFrameAnimation)obj, node),
                    CompositionObjectType.Vector4KeyFrameAnimation => GenerateVector4KeyFrameAnimationFactory(builder, (Vector4KeyFrameAnimation)obj, node),
                    CompositionObjectType.CompositionEffectFactory => GenerateCompositionEffectFactory(builder, (CompositionEffectFactory)obj, node),
                    _ => throw new InvalidOperationException(),
                };
            }

            bool GenerateInsetClipFactory(CodeBuilder builder, InsetClip obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateInsetClip()");
                InitializeCompositionClip(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.LeftInset), obj.LeftInset);
                WriteSetPropertyStatement(builder, nameof(obj.RightInset), obj.RightInset);
                WriteSetPropertyStatement(builder, nameof(obj.TopInset), obj.TopInset);
                WriteSetPropertyStatement(builder, nameof(obj.BottomInset), obj.BottomInset);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateLayerVisualFactory(CodeBuilder builder, LayerVisual obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateLayerVisual()");
                InitializeContainerVisual(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.Shadow), obj.Shadow, node);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionGeometricClipFactory(CodeBuilder builder, CompositionGeometricClip obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateGeometricClip()");
                InitializeCompositionClip(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.Geometry), obj.Geometry, node);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionLinearGradientBrushFactory(CodeBuilder builder, CompositionLinearGradientBrush obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateLinearGradientBrush()");
                InitializeCompositionGradientBrush(builder, obj, node);

                WriteSetPropertyStatement(builder, "StartPoint", obj.StartPoint);
                WriteSetPropertyStatement(builder, "EndPoint", obj.EndPoint);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionMaskBrushFactory(CodeBuilder builder, CompositionMaskBrush obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateMaskBrush()");
                InitializeCompositionBrush(builder, obj, node);

                WriteSetPropertyStatement(builder, "Mask", CallFactoryFromFor(node, obj.Mask));
                WriteSetPropertyStatement(builder, "Source", CallFactoryFromFor(node, obj.Source));

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionRadialGradientBrushFactory(CodeBuilder builder, CompositionRadialGradientBrush obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateRadialGradientBrush()");
                InitializeCompositionGradientBrush(builder, obj, node);

                WriteSetPropertyStatement(builder, "EllipseCenter", obj.EllipseCenter);
                WriteSetPropertyStatement(builder, "EllipseRadius", obj.EllipseRadius);
                WriteSetPropertyStatement(builder, "GradientOriginOffset", obj.GradientOriginOffset);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateLinearEasingFunctionFactory(CodeBuilder builder, LinearEasingFunction obj, ObjectData node)
            {
                WriteSimpleObjectFactory(builder, node, $"_c{Deref}CreateLinearEasingFunction()");
                return true;
            }

            bool GenerateCubicBezierEasingFunctionFactory(CodeBuilder builder, CubicBezierEasingFunction obj, ObjectData node)
            {
                WriteSimpleObjectFactory(builder, node, CallCreateCubicBezierEasingFunction(obj));
                return true;
            }

            bool GenerateDropShadowFactory(CodeBuilder builder, DropShadow obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateDropShadow();");
                InitializeCompositionShadow(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.BlurRadius), obj.BlurRadius);
                WriteSetPropertyStatement(builder, nameof(obj.Color), obj.Color, formatter: Color);
                WriteSetPropertyStatement(builder, nameof(obj.Mask), obj.Mask, node);
                WriteSetPropertyStatement(builder, nameof(obj.Offset), obj.Offset);
                WriteSetPropertyStatement(builder, nameof(obj.Opacity), obj.Opacity);
                WriteSetPropertyStatement(builder, nameof(obj.SourcePolicy), obj.SourcePolicy, formatter: DropShadowSourcePolicy);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateStepEasingFunctionFactory(CodeBuilder builder, StepEasingFunction obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateStepEasingFunction()");
                InitializeCompositionEasingFunction(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.FinalStep), obj.FinalStep);
                WriteSetPropertyStatement(builder, nameof(obj.InitialStep), obj.InitialStep);
                WriteSetPropertyStatement(builder, nameof(obj.IsFinalStepSingleFrame), obj.IsFinalStepSingleFrame);
                WriteSetPropertyStatement(builder, nameof(obj.IsInitialStepSingleFrame), obj.IsInitialStepSingleFrame);
                WriteSetPropertyStatement(builder, nameof(obj.StepCount), obj.StepCount);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateContainerVisualFactory(CodeBuilder builder, ContainerVisual obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateContainerVisual()");
                InitializeContainerVisual(builder, obj, node);
                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateExpressionAnimationFactory(CodeBuilder builder, ExpressionAnimation obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateExpressionAnimation({String(obj.Expression)})");
                InitializeCompositionAnimation(builder, obj, node);
                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            void StartAnimationsOnResult(CodeBuilder builder, CompositionObject obj, ObjectData node)
                => StartAnimations(builder, obj, node, "result");

            void StartAnimations(CodeBuilder builder, CompositionObject obj, ObjectData node, string localName)
            {
                var controllerVariableAdded = false;
                StartAnimations(builder, obj, node, localName, ref controllerVariableAdded);
            }

            void StartAnimations(CodeBuilder builder, CompositionObject obj, ObjectData node, string localName, ref bool controllerVariableAdded)
            {
                // Start the animations for properties on the object.
                foreach (var animator in obj.Animators)
                {
                    StartAnimation(builder, obj, node, localName, ref controllerVariableAdded, animator);
                }

                // Start the animations for the properties on the property set on the object.
                // Prevent infinite recursion - the Properties on a CompositionPropertySet is itself.
                if (obj.Type != CompositionObjectType.CompositionPropertySet)
                {
                    // Start the animations for properties on the property set.
                    StartAnimations(builder, obj.Properties, NodeFor(obj.Properties), _s.PropertyGet(localName, "Properties"), ref controllerVariableAdded);
                }
            }

            void StartAnimation(CodeBuilder builder, CompositionObject obj, ObjectData node, string localName, ref bool controllerVariableAdded, CompositionObject.Animator animator)
            {
                // ExpressionAnimations are treated specially - a singleton
                // ExpressionAnimation is reset before each use, unless the animation
                // is shared.
                var animationNode = NodeFor(animator.Animation);
                if (!animationNode.RequiresStorage && animator.Animation is ExpressionAnimation expressionAnimation)
                {
                    StartSingletonExpressionAnimation(builder, obj, localName, animator, animationNode, expressionAnimation);
                    ConfigureAnimationController(builder, localName, ref controllerVariableAdded, animator);
                }
                else
                {
                    // KeyFrameAnimation or a shared ExpressionAnimation
                    var animationFactoryCall = CallFactoryFromFor(node, animationNode);

                    if (animator.Controller is not null)
                    {
                        // The animation has a controller.
                        var controller = animator.Controller;

                        var controllerAnimators = controller.Animators;

                        if (controllerAnimators.Count() == 1)
                        {
                            // The controller has only one property being animated.
                            var controllerAnimator = controllerAnimators.ElementAt(0);
                            if (controllerAnimator.AnimatedProperty == "Progress" &&
                                controllerAnimator.Animation is ExpressionAnimation controllerExpressionAnimation &&
                                controller.IsPaused)
                            {
                                // The controller has only its Progress property animated, and it's animated by
                                // an expression animation.
                                var controllerExpressionAnimationNode = NodeFor(controllerExpressionAnimation);

                                if (controllerExpressionAnimationNode.NeedsAFactory)
                                {
                                    EnsureStartProgressBoundAnimationWritten(_rootCodeBuilder ?? builder);

                                    // Special-case for a paused controller that has only its Progress property animated by
                                    // an ExpressionAnimation that has a factory. Generate a call to a helper that will do the work.
                                    // Note that this is the common case for Lottie.
                                    if (_configuration.ImplementCreateAndDestroyMethods)
                                    {
                                        // If we are implementing IAnimatedVisual2 we should create these animation not in the tree initialization code,
                                        // but inside CreateAnimations method.
                                        _createAnimationsCodeBuilder
                                            .WriteLine(
                                            $"StartProgressBoundAnimation({localName}, " +
                                            $"{String(animator.AnimatedProperty)}, " +
                                            $"{animationFactoryCall}, " +
                                            $"{CallFactoryFromFor(NodeFor(animator.Controller), controllerExpressionAnimationNode)});");

                                        // If we are implementing IAnimatedVisual2 we should also write a destruction call.
                                        _destroyAnimationsCodeBuilder
                                            .WriteLine($"{localName}{Deref}StopAnimation({String(animator.AnimatedProperty)});");
                                    }
                                    else
                                    {
                                        builder.WriteLine(
                                            $"StartProgressBoundAnimation({localName}, " +
                                            $"{String(animator.AnimatedProperty)}, " +
                                            $"{animationFactoryCall}, " +
                                            $"{CallFactoryFromFor(NodeFor(animator.Controller), controllerExpressionAnimationNode)});");
                                    }

                                    return;
                                }
                            }
                        }
                    }

                    if (_configuration.ImplementCreateAndDestroyMethods)
                    {
                        if (animator.Controller is not null && animator.Controller.IsCustom)
                        {
                            _createAnimationsCodeBuilder
                                .WriteLine($"{localName}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {animationFactoryCall}, {CallFactoryFromFor(node, NodeFor(animator.Controller))});");
                        }
                        else
                        {
                            _createAnimationsCodeBuilder
                                .WriteLine($"{localName}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {animationFactoryCall});");
                            ConfigureAnimationController(_createAnimationsCodeBuilder, localName, ref controllerVariableAdded, animator);
                        }

                        // If we are implementing IAnimatedVisual2 we should also write a destruction call.
                        _destroyAnimationsCodeBuilder
                            .WriteLine($"{localName}{Deref}StopAnimation({String(animator.AnimatedProperty)});");
                    }
                    else
                    {
                        if (animator.Controller is not null && animator.Controller.IsCustom)
                        {
                            builder.WriteLine($"{localName}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {animationFactoryCall}, {CallFactoryFromFor(node, NodeFor(animator.Controller))});");
                        }
                        else
                        {
                            builder.WriteLine($"{localName}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {animationFactoryCall});");
                            ConfigureAnimationController(builder, localName, ref controllerVariableAdded, animator);
                        }
                    }
                }
            }

            void ConfigureAnimationController(CodeBuilder builder, string localName, ref bool controllerVariableAdded, CompositionObject.Animator animator)
            {
                // If the animation has a controller, get the controller, optionally pause it, and recurse to start the animations
                // on the controller.
                if (animator.Controller is not null)
                {
                    var controller = animator.Controller;

                    if (!controllerVariableAdded)
                    {
                        // Declare and initialize the controller variable.
                        builder.WriteLine($"{Var} controller = {localName}{Deref}TryGetAnimationController({String(animator.AnimatedProperty)});");
                        controllerVariableAdded = true;
                    }
                    else
                    {
                        // Initialize the controller variable.
                        builder.WriteLine($"controller = {localName}{Deref}TryGetAnimationController({String(animator.AnimatedProperty)});");
                    }

                    if (controller.IsPaused)
                    {
                        builder.WriteLine($"controller{Deref}Pause();");
                    }

                    // Recurse to start animations on the controller.
                    StartAnimations(builder, controller, NodeFor(controller), "controller");
                }
            }

            // Helper method that starts an animation and binds its AnimationController.Progress to an expression.
            void EnsureStartProgressBoundAnimationWritten(CodeBuilder builder)
            {
                // Write a static method that starts an animation, then binds the Progress property of its
                // AnimationController for that animation to an expression. This is used to start animations
                // that have their progress bound to the progress of another property.
                var b = builder.GetSubBuilder("StartProgressBoundAnimation");
                if (b.IsEmpty)
                {
                    b.WriteLine("static void StartProgressBoundAnimation(");
                    b.Indent();
                    b.WriteLine($"{ReferenceTypeName("CompositionObject")} target,");
                    b.WriteLine($"{_s.TypeString} animatedPropertyName,");
                    b.WriteLine($"{ReferenceTypeName("CompositionAnimation")} animation,");
                    b.WriteLine($"{ReferenceTypeName("ExpressionAnimation")} controllerProgressExpression)");
                    b.UnIndent();
                    b.OpenScope();
                    b.WriteLine($"target{Deref}StartAnimation(animatedPropertyName, animation);");
                    b.WriteLine($"{ConstVar} controller = target{Deref}TryGetAnimationController(animatedPropertyName);");
                    b.WriteLine($"controller{Deref}Pause();");
                    b.WriteLine($"controller{Deref}StartAnimation({String("Progress")}, controllerProgressExpression);");
                    b.CloseScope();
                    b.WriteLine();
                }
            }

            void EnsureBindPropertyWritten(CodeBuilder builder)
            {
                // Write the method that binds an expression to an object using the singleton ExpressionAnimation object.
                var b = builder.GetSubBuilder("BindProperty");
                if (b.IsEmpty)
                {
                    // 1 reference parameter version.
                    b.WriteLine("void BindProperty(");
                    b.Indent();
                    b.WriteLine($"{ReferenceTypeName("CompositionObject")} target,");
                    b.WriteLine($"{_s.TypeString} animatedPropertyName,");
                    b.WriteLine($"{_s.TypeString} expression,");
                    b.WriteLine($"{_s.TypeString} referenceParameterName,");
                    b.WriteLine($"{ReferenceTypeName("CompositionObject")} referencedObject)");
                    b.UnIndent();
                    b.OpenScope();
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}ClearAllParameters();");
                    WriteSetPropertyStatement(b, "Expression", "expression", SingletonExpressionAnimationName);
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}SetReferenceParameter(referenceParameterName, referencedObject);");
                    b.WriteLine($"target{Deref}StartAnimation(animatedPropertyName, {SingletonExpressionAnimationName});");
                    b.CloseScope();
                    b.WriteLine();
                }
            }

            void EnsureBindProperty2Written(CodeBuilder builder)
            {
                // Write the method that binds an expression to an object using the singleton ExpressionAnimation object.
                var b = builder.GetSubBuilder("BindProperty2");

                if (b.IsEmpty)
                {
                    // 2 reference parameter version.
                    b.WriteLine($"void BindProperty2(");
                    b.Indent();
                    b.WriteLine($"{ReferenceTypeName("CompositionObject")} target,");
                    b.WriteLine($"{_s.TypeString} animatedPropertyName,");
                    b.WriteLine($"{_s.TypeString} expression,");
                    b.WriteLine($"{_s.TypeString} referenceParameterName0,");
                    b.WriteLine($"{ReferenceTypeName("CompositionObject")} referencedObject0,");
                    b.WriteLine($"{_s.TypeString} referenceParameterName1,");
                    b.WriteLine($"{ReferenceTypeName("CompositionObject")} referencedObject1)");
                    b.UnIndent();
                    b.OpenScope();
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}ClearAllParameters();");
                    WriteSetPropertyStatement(b, "Expression", "expression", SingletonExpressionAnimationName);
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}SetReferenceParameter(referenceParameterName0, referencedObject0);");
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}SetReferenceParameter(referenceParameterName1, referencedObject1);");
                    b.WriteLine($"target{Deref}StartAnimation(animatedPropertyName, {SingletonExpressionAnimationName});");
                    b.CloseScope();
                    b.WriteLine();
                }
            }

            void EnsureKeyFrameAnimationHelperWritten(CodeBuilder builder, CompositionObjectType animationType)
            {
                var methodName = $"Create{animationType}";

                var valueType = animationType switch
                {
                    CompositionObjectType.BooleanKeyFrameAnimation => "bool",
                    CompositionObjectType.ColorKeyFrameAnimation => "Color",
                    CompositionObjectType.PathKeyFrameAnimation => ReferenceTypeName("CompositionPath"),
                    CompositionObjectType.ScalarKeyFrameAnimation => _s.TypeFloat32,
                    CompositionObjectType.Vector2KeyFrameAnimation => _s.TypeVector2,
                    CompositionObjectType.Vector3KeyFrameAnimation => _s.TypeVector3,
                    CompositionObjectType.Vector4KeyFrameAnimation => _s.TypeVector4,
                    _ => throw new InvalidOperationException(),
                };

                // Write the method that creates a KeyFrameAnimation with duration set to the duration of
                // the composition.
                var b = builder.GetSubBuilder(methodName);
                if (b.IsEmpty)
                {
                    // BooleanKeyFrameAnimations never take an easing function.
                    var easingParameter =
                        animationType == CompositionObjectType.BooleanKeyFrameAnimation
                            ? string.Empty
                            : $", {ReferenceTypeName("CompositionEasingFunction")} initialEasingFunction";

                    var easingArgument =
                        animationType == CompositionObjectType.BooleanKeyFrameAnimation
                            ? string.Empty
                            : $", initialEasingFunction";

                    b.WriteLine($"{ReferenceTypeName(animationType.ToString())} {methodName}(float initialProgress, {valueType} initialValue{easingParameter})");
                    b.OpenScope();
                    b.WriteLine($"{ConstVar} result = _c{Deref}{methodName}();");
                    WriteSetPropertyStatement(b, "Duration", TimeSpan(_owner._compositionDuration));
                    if (animationType == CompositionObjectType.ColorKeyFrameAnimation)
                    {
                        WriteSetPropertyStatement(b, "InterpolationColorSpace", ColorSpace(CompositionColorSpace.Rgb));
                    }

                    b.WriteLine($"result{Deref}InsertKeyFrame(initialProgress, initialValue{easingArgument});");
                    b.WriteLine("return result;");
                    b.CloseScope();
                    b.WriteLine();
                }
            }

            /// <summary>
            /// Writes a call the helper method that creates a SpriteShape.
            /// Creates the helper method if it hasn't been created yet.
            /// </summary>
            void WriteCallHelperCreateSpriteShape(
                CodeBuilder builder,
                CompositionSpriteShape obj,
                ObjectData node,
                Matrix3x2 transformMatrix)
            {
                var b = builder.GetSubBuilder("CreateSpriteShape");
                if (b.IsEmpty)
                {
                    b.WriteLine($"{ReferenceTypeName("CompositionSpriteShape")} CreateSpriteShape({ReferenceTypeName("CompositionGeometry")} geometry, {_s.TypeMatrix3x2} transformMatrix)");
                    b.OpenScope();
                    b.WriteLine($"{ConstVar} result = _c{Deref}CreateSpriteShape(geometry);");
                    WriteSetPropertyStatement(b, "TransformMatrix", "transformMatrix");
                    b.WriteLine("return result;");
                    b.CloseScope();
                    b.WriteLine();
                }

                // Call the helper and initialize the remaining CompositionShape properties.
                WriteMatrixComment(builder, transformMatrix);
                WriteCreateAssignment(builder, node, $"CreateSpriteShape({CallFactoryFromFor(node, obj.Geometry)}, {Matrix3x2(transformMatrix)});");
                InitializeCompositionObject(builder, obj, node);
                WriteSetPropertyStatement(builder, nameof(obj.CenterPoint), obj.CenterPoint);
                WriteSetPropertyStatement(builder, nameof(obj.Offset), obj.Offset);
                WriteSetPropertyStatement(builder, nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees);
                WriteSetPropertyStatement(builder, nameof(obj.Scale), obj.Scale);
            }

            /// <summary>
            /// Writes a call the helper method that creates a SpriteShape with a fill brush.
            /// Creates the helper method if it hasn't been created yet.
            /// </summary>
            void WriteCallHelperCreateSpriteShapeWithFillBrush(
                CodeBuilder builder,
                CompositionSpriteShape obj,
                ObjectData node,
                Matrix3x2 transformMatrix)
            {
                var b = builder.GetSubBuilder("CreateSpriteShapeWithFillBrush");
                if (b.IsEmpty)
                {
                    b.WriteLine($"{ReferenceTypeName("CompositionSpriteShape")} CreateSpriteShape({ReferenceTypeName("CompositionGeometry")} geometry, {_s.TypeMatrix3x2} transformMatrix, {ReferenceTypeName("CompositionBrush")} fillBrush)");
                    b.OpenScope();
                    b.WriteLine($"{ConstVar} result = _c{Deref}CreateSpriteShape(geometry);");
                    WriteSetPropertyStatement(b, "TransformMatrix", "transformMatrix");
                    WriteSetPropertyStatement(b, "FillBrush", "fillBrush");
                    b.WriteLine("return result;");
                    b.CloseScope();
                    b.WriteLine();
                }

                // Call the helper and initialize the remaining CompositionShape properties.
                WriteMatrixComment(builder, obj.TransformMatrix);

                // We need to instantiate geometry first because sometimes it initializes fields
                // that are used in FillBrush, but CreateSpriteShape(GetGeometry(), ..., GetFillBrush()) code
                // will result in evaluating GetFillBrush() first which may cause null dereferencing
                builder.WriteLine($"{ConstVar} geometry = {CallFactoryFromFor(node, obj.Geometry)};");
                WriteCreateAssignment(builder, node, $"CreateSpriteShape(geometry, {Matrix3x2(transformMatrix)}, {CallFactoryFromFor(node, obj.FillBrush)});");
                InitializeCompositionObject(builder, obj, node);
                WriteSetPropertyStatement(builder, nameof(obj.CenterPoint), obj.CenterPoint);
                WriteSetPropertyStatement(builder, nameof(obj.Offset), obj.Offset);
                WriteSetPropertyStatement(builder, nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees);
                WriteSetPropertyStatement(builder, nameof(obj.Scale), obj.Scale);
            }

            // Starts an ExpressionAnimation that uses the shared singleton ExpressionAnimation.
            // This reparameterizes the singleton each time it is called, and therefore avoids the
            // cost of creating a new ExpressionAnimation. However, because it gets reparameterized
            // for each use, it cannot be used if the ExpressionAnimation is shared by multiple nodes.
            void StartSingletonExpressionAnimation(
                    CodeBuilder builder,
                    CompositionObject obj,
                    string localName,
                    CompositionObject.Animator animator,
                    ObjectData animationNode,
                    ExpressionAnimation animation)
            {
                Debug.Assert(animator.Animation == animation, "Precondition");

                var referenceParameters = animator.Animation.ReferenceParameters.ToArray();
                if (referenceParameters.Length == 1 &&
                    string.IsNullOrWhiteSpace(animation.Target))
                {
                    EnsureBindPropertyWritten(_rootCodeBuilder ?? builder);

                    var rp0 = referenceParameters[0];
                    var rp0Name = GetReferenceParameterName(obj, localName, animationNode, rp0);

                    // Special-case where there is exactly one reference parameter. Call a helper.
                    builder.WriteLine(
                        $"BindProperty({localName}, " + // target
                        $"{String(animator.AnimatedProperty)}, " + // property on target
                        $"{String(animation.Expression.ToText())}, " + // expression
                        $"{String(rp0.Key)}, " + // reference property name
                        $"{rp0Name});"); // reference object
                }
                else if (referenceParameters.Length == 2 &&
                    string.IsNullOrWhiteSpace(animation.Target))
                {
                    EnsureBindProperty2Written(_rootCodeBuilder ?? builder);

                    var rp0 = referenceParameters[0];
                    var rp0Name = GetReferenceParameterName(obj, localName, animationNode, rp0);

                    var rp1 = referenceParameters[1];
                    var rp1Name = GetReferenceParameterName(obj, localName, animationNode, rp1);

                    // Special-case where there are exactly two reference parameters. Call a helper.
                    builder.WriteLine(
                        $"BindProperty2({localName}, " + // target
                        $"{String(animator.AnimatedProperty)}, " + // property on target
                        $"{String(animation.Expression.ToText())}, " + // expression
                        $"{String(rp0.Key)}, " + // reference property name
                        $"{rp0Name}, " + // reference object
                        $"{String(rp1.Key)}, " + // reference property name
                        $"{rp1Name});"); // reference object
                }
                else
                {
                    builder.WriteLine($"{SingletonExpressionAnimationName}{Deref}ClearAllParameters();");
                    builder.WriteLine($"{_s.PropertySet(SingletonExpressionAnimationName, "Expression", String(animation.Expression))};");

                    // If there is a Target set it. Note however that the Target isn't used for anything
                    // interesting in this scenario, and there is no way to reset the Target to an
                    // empty string (the Target API disallows empty). In reality, for all our uses
                    // the Target will not be set and it doesn't matter if it was set previously.
                    if (!string.IsNullOrWhiteSpace(animation.Target))
                    {
                        builder.WriteLine($"{SingletonExpressionAnimationName}{Deref}Target = {String(animation.Target)};");
                    }

                    foreach (var rp in animation.ReferenceParameters)
                    {
                        var referenceParameterName = GetReferenceParameterName(obj, localName, animationNode, rp);

                        builder.WriteLine($"{SingletonExpressionAnimationName}{Deref}SetReferenceParameter({String(rp.Key)}, {referenceParameterName});");
                    }

                    builder.WriteLine($"{localName}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {SingletonExpressionAnimationName});");
                }
            }

            string GetReferenceParameterName(
                CompositionObject obj,
                string localName,
                ObjectData animationNode,
                KeyValuePair<string, CompositionObject> referenceParameter)
            {
                if (referenceParameter.Value == obj)
                {
                    return localName;
                }

                if (referenceParameter.Value.Type == CompositionObjectType.CompositionPropertySet)
                {
                    var propSet = (CompositionPropertySet)referenceParameter.Value;
                    var propSetOwner = propSet.Owner;
                    if (propSetOwner == obj)
                    {
                        // Use the name of the local that is holding the property set.
                        return "propertySet";
                    }

                    if (propSetOwner is null)
                    {
                        // It's an unowned property set. Currently these are:
                        // * only used for themes.
                        // * placed in a field by the constructor of the IAnimatedVisual.
                        Debug.Assert(_owner._isThemed, "Precondition");
                        return ThemePropertiesFieldName;
                    }

                    // Get the factory for the owner of the property set, and get the Properties object from it.
                    return CallFactoryFromFor(animationNode, propSetOwner);
                }

                return CallFactoryFromFor(animationNode, referenceParameter.Value);
            }

            void InitializeCompositionObject(CodeBuilder builder, CompositionObject obj, ObjectData node, string localName = "result")
            {
                if (_owner._setCommentProperties)
                {
                    WriteSetPropertyStatementDefaultIsNullOrWhitespace(builder, nameof(obj.Comment), obj.Comment, localName);
                }

                var propertySet = obj.Properties;

                if (propertySet.Names.Count > 0)
                {
                    builder.WriteLine($"{ConstVar} propertySet = {_s.PropertyGet(localName, "Properties")};");
                    _owner.WritePropertySetInitialization(builder, propertySet, "propertySet");
                }
            }

            void InitializeCompositionBrush(CodeBuilder builder, CompositionBrush obj, ObjectData node) =>
                InitializeCompositionObject(builder, obj, node);

            void InitializeCompositionEasingFunction(CodeBuilder builder, CompositionEasingFunction obj, ObjectData node) =>
                InitializeCompositionObject(builder, obj, node);

            void InitializeCompositionShadow(CodeBuilder builder, CompositionShadow obj, ObjectData node) =>
                InitializeCompositionObject(builder, obj, node);

            void InitializeVisual(CodeBuilder builder, Visual obj, ObjectData node)
            {
                InitializeCompositionObject(builder, obj, node);

                if (obj.BorderMode.HasValue && obj.BorderMode != CompositionBorderMode.Inherit)
                {
                    WriteSetPropertyStatement(builder, nameof(obj.BorderMode), BorderMode(obj.BorderMode.Value));
                }

                WriteSetPropertyStatement(builder, nameof(obj.CenterPoint), obj.CenterPoint);
                WriteSetPropertyStatement(builder, nameof(obj.Clip), obj.Clip, node);
                WriteSetPropertyStatement(builder, nameof(obj.IsVisible), obj.IsVisible);
                WriteSetPropertyStatement(builder, nameof(obj.Offset), obj.Offset);
                WriteSetPropertyStatement(builder, nameof(obj.Opacity), obj.Opacity);
                WriteSetPropertyStatement(builder, nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees);
                WriteSetPropertyStatement(builder, nameof(obj.RotationAxis), obj.RotationAxis);
                WriteSetPropertyStatement(builder, nameof(obj.Scale), obj.Scale);
                WriteSetPropertyStatement(builder, nameof(obj.Size), obj.Size);
                WriteSetPropertyStatement(builder, nameof(obj.TransformMatrix), obj.TransformMatrix);
            }

            void InitializeCompositionClip(CodeBuilder builder, CompositionClip obj, ObjectData node)
            {
                InitializeCompositionObject(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.CenterPoint), obj.CenterPoint);
                WriteSetPropertyStatement(builder, nameof(obj.Scale), obj.Scale);
            }

            void InitializeCompositionGradientBrush(CodeBuilder builder, CompositionGradientBrush obj, ObjectData node)
            {
                InitializeCompositionObject(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.AnchorPoint), obj.AnchorPoint);
                WriteSetPropertyStatement(builder, nameof(obj.CenterPoint), obj.CenterPoint);

                if (obj.ColorStops.Count > 0)
                {
                    builder.WriteLine($"{ConstVar} colorStops = {_s.PropertyGet("result", "ColorStops")};");
                    foreach (var colorStop in obj.ColorStops)
                    {
                        builder.WriteLine($"colorStops{Deref}{IListAdd}({CallFactoryFromFor(node, colorStop)});");
                    }
                }

                WriteSetPropertyStatement(builder, nameof(obj.ExtendMode), obj.ExtendMode, formatter: ExtendMode);
                WriteSetPropertyStatement(builder, nameof(obj.InterpolationSpace), obj.InterpolationSpace, formatter: ColorSpace);
                WriteSetPropertyStatement(builder, nameof(obj.MappingMode), obj.MappingMode, formatter: MappingMode);
                WriteSetPropertyStatement(builder, nameof(obj.Offset), obj.Offset);
                WriteSetPropertyStatement(builder, nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees);
                WriteSetPropertyStatement(builder, nameof(obj.Scale), obj.Scale);
                WriteSetPropertyStatement(builder, nameof(obj.TransformMatrix), obj.TransformMatrix);
            }

            void InitializeCompositionShape(CodeBuilder builder, CompositionShape obj, ObjectData node)
            {
                InitializeCompositionObject(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.CenterPoint), obj.CenterPoint);
                WriteSetPropertyStatement(builder, nameof(obj.Offset), obj.Offset);
                WriteSetPropertyStatement(builder, nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees);
                WriteSetPropertyStatement(builder, nameof(obj.Scale), obj.Scale);
                WriteSetPropertyStatement(builder, nameof(obj.TransformMatrix), obj.TransformMatrix);
            }

            void InitializeContainerVisual(CodeBuilder builder, ContainerVisual obj, ObjectData node)
            {
                InitializeVisual(builder, obj, node);

                switch (obj.Children.Count)
                {
                    case 0:
                        // No children, nothing to do.
                        break;

                    case 1:
                        {
                            // A single child. We can add the child in a single line.
                            var child = obj.Children[0];
                            WriteShortDescriptionComment(builder, child);
                            builder.WriteLine($"{_s.PropertyGet("result", "Children")}{Deref}InsertAtTop({CallFactoryFromFor(node, child)});");
                            break;
                        }

                    default:
                        {
                            // Multiple children requires the use of a local.
                            builder.WriteLine($"{ConstVar} children = {_s.PropertyGet("result", nameof(obj.Children))};");
                            foreach (var child in obj.Children)
                            {
                                WriteShortDescriptionComment(builder, child);
                                builder.WriteLine($"children{Deref}InsertAtTop({CallFactoryFromFor(node, child)});");
                            }

                            break;
                        }
                }
            }

            void InitializeCompositionGeometry(CodeBuilder builder, CompositionGeometry obj, ObjectData node)
            {
                InitializeCompositionObject(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.TrimEnd), obj.TrimEnd);
                WriteSetPropertyStatement(builder, nameof(obj.TrimOffset), obj.TrimOffset);
                WriteSetPropertyStatement(builder, nameof(obj.TrimStart), obj.TrimStart);
            }

            void InitializeCompositionAnimation(CodeBuilder builder, CompositionAnimation obj, ObjectData node)
            {
                InitializeCompositionAnimationWithParameters(
                    builder,
                    obj,
                    node,
                    obj.ReferenceParameters.Select(p => new KeyValuePair<string, string>(p.Key, $"{CallFactoryFromFor(node, p.Value)}")));
            }

            void InitializeCompositionAnimationWithParameters(
                CodeBuilder builder,
                CompositionAnimation obj,
                ObjectData node,
                IEnumerable<KeyValuePair<string, string>> parameters)
            {
                InitializeCompositionObject(builder, obj, node);
                WriteSetPropertyStatementDefaultIsNullOrWhitespace(builder, nameof(obj.Target), obj.Target);

                foreach (var parameter in parameters)
                {
                    builder.WriteLine($"result{Deref}SetReferenceParameter({String(parameter.Key)}, {parameter.Value});");
                }
            }

            void WriteCreateAndAssignKeyFrameAnimation(CodeBuilder builder, KeyFrameAnimation_ animation, ObjectData node)
            {
                WriteCreateAssignment(builder, node, $"_c{Deref}Create{animation.Type}()");

                Debug.Assert(animation.Duration.Ticks > 0, "Invariant");
                WriteSetPropertyStatement(builder, nameof(animation.Duration), TimeSpan(animation.Duration));

                InitializeCompositionAnimation(builder, animation, node);
            }

            bool GenerateCustomAnimationController(CodeBuilder builder, AnimationController obj, ObjectData node)
            {
                if (!obj.IsCustom)
                {
                    throw new InvalidOperationException();
                }

                WriteObjectFactoryStart(builder, node);

                WriteCreateAssignment(builder, node, $"_c{Deref}Create{obj.Type}()");

                if (obj.IsPaused)
                {
                    builder.WriteLine($"result{Deref}Pause();");
                }

                WriteCompositionObjectFactoryEnd(builder, obj, node);

                return true;
            }

            bool GenerateBooleanKeyFrameAnimationFactory(CodeBuilder builder, BooleanKeyFrameAnimation obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);

                var keyFrames = obj.KeyFrames;
                var firstKeyFrame = keyFrames.First();

                // If the duration is equal to the duration of the composition, use a helper.
                if (obj.Duration == _owner._compositionDuration &&
                    firstKeyFrame.Type == KeyFrameType.Value)
                {
                    EnsureKeyFrameAnimationHelperWritten(builder, obj.Type);

                    // Call the helper to create the animation. This will also set the duration and
                    // take the first key frame.
                    var kf = (KeyFrameAnimation<bool, Expr.Boolean>.ValueKeyFrame)firstKeyFrame;
                    WriteFrameNumberComment(builder, kf.Progress);
                    WriteCreateAssignment(builder, node, $"Create{obj.Type}({Float(kf.Progress)}, {Bool(kf.Value)})");
                    InitializeCompositionAnimation(builder, obj, node);
                    keyFrames = keyFrames.Skip(1);
                }
                else
                {
                    WriteCreateAndAssignKeyFrameAnimation(builder, obj, node);
                }

                foreach (var kf in keyFrames)
                {
                    WriteFrameNumberComment(builder, kf.Progress);

                    switch (kf.Type)
                    {
                        case KeyFrameType.Expression:
                            var expressionKeyFrame = (KeyFrameAnimation<bool, Expr.Boolean>.ExpressionKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertExpressionKeyFrame({Float(kf.Progress)}, {String(expressionKeyFrame.Expression)}, {_s.Null});");
                            break;
                        case KeyFrameType.Value:
                            var valueKeyFrame = (KeyFrameAnimation<bool, Expr.Boolean>.ValueKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertKeyFrame({Float(kf.Progress)}, {Bool(valueKeyFrame.Value)});");
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateColorKeyFrameAnimationFactory(CodeBuilder builder, ColorKeyFrameAnimation obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);

                var keyFrames = obj.KeyFrames;
                var firstKeyFrame = keyFrames.First();

                // If the duration is equal to the duration of the composition, use a helper.
                if (obj.Duration == _owner._compositionDuration &&
                    firstKeyFrame.Type == KeyFrameType.Value &&
                    obj.InterpolationColorSpace == CompositionColorSpace.Rgb)
                {
                    EnsureKeyFrameAnimationHelperWritten(builder, obj.Type);

                    // Call the helper to create the animation. This will also set the duration and
                    // take the first key frame.
                    var kf = (KeyFrameAnimation<Wui.Color, Expr.Color>.ValueKeyFrame)firstKeyFrame;
                    WriteFrameNumberComment(builder, kf.Progress);
                    WriteCreateAssignment(builder, node, $"Create{obj.Type}({Float(kf.Progress)}, {Color(kf.Value)}, {CallFactoryFromFor(node, kf.Easing)})");
                    InitializeCompositionAnimation(builder, obj, node);
                    keyFrames = keyFrames.Skip(1);
                }
                else
                {
                    WriteCreateAndAssignKeyFrameAnimation(builder, obj, node);

                    if (obj.InterpolationColorSpace != CompositionColorSpace.Auto)
                    {
                        WriteSetPropertyStatement(builder, nameof(obj.InterpolationColorSpace), ColorSpace(obj.InterpolationColorSpace));
                    }
                }

                foreach (var kf in keyFrames)
                {
                    WriteFrameNumberComment(builder, kf.Progress);

                    switch (kf.Type)
                    {
                        case KeyFrameType.Expression:
                            var expressionKeyFrame = (KeyFrameAnimation<Wui.Color, Expr.Color>.ExpressionKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertExpressionKeyFrame({Float(kf.Progress)}, {String(expressionKeyFrame.Expression)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        case KeyFrameType.Value:
                            var valueKeyFrame = (KeyFrameAnimation<Wui.Color, Expr.Color>.ValueKeyFrame)kf;
                            builder.WriteComment(valueKeyFrame.Value.Name);
                            builder.WriteLine($"result{Deref}InsertKeyFrame({Float(kf.Progress)}, {Color(valueKeyFrame.Value)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateVector2KeyFrameAnimationFactory(CodeBuilder builder, Vector2KeyFrameAnimation obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);

                var keyFrames = obj.KeyFrames;
                var firstKeyFrame = keyFrames.First();

                // If the duration is equal to the duration of the composition, use a helper.
                if (obj.Duration == _owner._compositionDuration &&
                    firstKeyFrame.Type == KeyFrameType.Value)
                {
                    EnsureKeyFrameAnimationHelperWritten(builder, obj.Type);

                    // Call the helper to create the animation. This will also set the duration and
                    // take the first key frame.
                    var kf = (KeyFrameAnimation<Vector2, Expr.Vector2>.ValueKeyFrame)firstKeyFrame;
                    WriteFrameNumberComment(builder, kf.Progress);
                    WriteCreateAssignment(builder, node, $"Create{obj.Type}({Float(kf.Progress)}, {Vector2(kf.Value)}, {CallFactoryFromFor(node, kf.Easing)})");
                    InitializeCompositionAnimation(builder, obj, node);
                    keyFrames = keyFrames.Skip(1);
                }
                else
                {
                    WriteCreateAndAssignKeyFrameAnimation(builder, obj, node);
                }

                foreach (var kf in keyFrames)
                {
                    WriteFrameNumberComment(builder, kf.Progress);

                    switch (kf.Type)
                    {
                        case KeyFrameType.Expression:
                            var expressionKeyFrame = (KeyFrameAnimation<Vector2, Expr.Vector2>.ExpressionKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertExpressionKeyFrame({Float(kf.Progress)}, {String(expressionKeyFrame.Expression)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        case KeyFrameType.Value:
                            var valueKeyFrame = (KeyFrameAnimation<Vector2, Expr.Vector2>.ValueKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertKeyFrame({Float(kf.Progress)}, {Vector2(valueKeyFrame.Value)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateVector3KeyFrameAnimationFactory(CodeBuilder builder, Vector3KeyFrameAnimation obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                var keyFrames = obj.KeyFrames;
                var firstKeyFrame = keyFrames.First();

                // If the duration is equal to the duration of the composition, use a helper.
                if (obj.Duration == _owner._compositionDuration &&
                    firstKeyFrame.Type == KeyFrameType.Value)
                {
                    EnsureKeyFrameAnimationHelperWritten(builder, obj.Type);

                    // Call the helper to create the animation. This will also set the duration and
                    // take the first key frame.
                    var kf = (KeyFrameAnimation<Vector3, Expr.Vector3>.ValueKeyFrame)firstKeyFrame;
                    WriteFrameNumberComment(builder, kf.Progress);
                    WriteCreateAssignment(builder, node, $"Create{obj.Type}({Float(kf.Progress)}, {Vector3(kf.Value)}, {CallFactoryFromFor(node, kf.Easing)})");
                    InitializeCompositionAnimation(builder, obj, node);
                    keyFrames = keyFrames.Skip(1);
                }
                else
                {
                    WriteCreateAndAssignKeyFrameAnimation(builder, obj, node);
                }

                foreach (var kf in keyFrames)
                {
                    WriteFrameNumberComment(builder, kf.Progress);

                    switch (kf.Type)
                    {
                        case KeyFrameType.Expression:
                            var expressionKeyFrame = (KeyFrameAnimation<Vector3, Expr.Vector3>.ExpressionKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertExpressionKeyFrame({Float(kf.Progress)}, {String(expressionKeyFrame.Expression)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        case KeyFrameType.Value:
                            var valueKeyFrame = (KeyFrameAnimation<Vector3, Expr.Vector3>.ValueKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertKeyFrame({Float(kf.Progress)}, {Vector3(valueKeyFrame.Value)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateVector4KeyFrameAnimationFactory(CodeBuilder builder, Vector4KeyFrameAnimation obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                var keyFrames = obj.KeyFrames;
                var firstKeyFrame = keyFrames.First();

                // If the duration is equal to the duration of the composition, use a helper.
                if (obj.Duration == _owner._compositionDuration &&
                    firstKeyFrame.Type == KeyFrameType.Value)
                {
                    EnsureKeyFrameAnimationHelperWritten(builder, obj.Type);

                    // Call the helper to create the animation. This will also set the duration and
                    // take the first key frame.
                    var kf = (KeyFrameAnimation<Vector4, Expr.Vector4>.ValueKeyFrame)firstKeyFrame;
                    WriteFrameNumberComment(builder, kf.Progress);
                    WriteCreateAssignment(builder, node, $"Create{obj.Type}({Float(kf.Progress)}, {Vector4(kf.Value)}, {CallFactoryFromFor(node, kf.Easing)})");
                    InitializeCompositionAnimation(builder, obj, node);
                    keyFrames = keyFrames.Skip(1);
                }
                else
                {
                    WriteCreateAndAssignKeyFrameAnimation(builder, obj, node);
                }

                foreach (var kf in keyFrames)
                {
                    WriteFrameNumberComment(builder, kf.Progress);

                    switch (kf.Type)
                    {
                        case KeyFrameType.Expression:
                            var expressionKeyFrame = (KeyFrameAnimation<Sn.Vector4, Expr.Vector4>.ExpressionKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertExpressionKeyFrame({Float(kf.Progress)}, {String(expressionKeyFrame.Expression)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        case KeyFrameType.Value:
                            var valueKeyFrame = (KeyFrameAnimation<Sn.Vector4, Expr.Vector4>.ValueKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertKeyFrame({Float(kf.Progress)}, {Vector4(valueKeyFrame.Value)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionEffectFactory(CodeBuilder builder, CompositionEffectFactory obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);

                var effectCreationString = obj.Effect.Type switch
                {
                    GraphicsEffectType.CompositeEffect => _owner.WriteCompositeEffectFactory(builder, (CompositeEffect)obj.Effect),
                    GraphicsEffectType.GaussianBlurEffect => _owner.WriteGaussianBlurEffectFactory(builder, (GaussianBlurEffect)obj.Effect),
                    _ => throw new InvalidOperationException()
                };

                WriteCreateAssignment(builder, node, $"_c{Deref}CreateEffectFactory({effectCreationString})");

                WriteCompositionObjectFactoryEnd(builder, obj, node);

                return true;
            }

            bool GeneratePathKeyFrameAnimationFactory(CodeBuilder builder, PathKeyFrameAnimation obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                var keyFrames = obj.KeyFrames;
                var firstKeyFrame = keyFrames.First();

                // If the duration is equal to the duration of the composition, use a helper.
                if (obj.Duration == _owner._compositionDuration)
                {
                    EnsureKeyFrameAnimationHelperWritten(builder, obj.Type);

                    // Call the helper to create the animation. This will also set the duration and
                    // take the first key frame.
                    var kf = (PathKeyFrameAnimation.ValueKeyFrame)firstKeyFrame;
                    WriteFrameNumberComment(builder, kf.Progress);
                    WriteCreateAssignment(builder, node, $"Create{obj.Type}({Float(kf.Progress)}, {CallFactoryFromFor(node, kf.Value)}, {CallFactoryFromFor(node, kf.Easing)})");
                    InitializeCompositionAnimation(builder, obj, node);
                    keyFrames = keyFrames.Skip(1);
                }
                else
                {
                    WriteCreateAndAssignKeyFrameAnimation(builder, obj, node);
                }

                foreach (var kf in keyFrames)
                {
                    WriteFrameNumberComment(builder, kf.Progress);
                    var valueKeyFrame = (PathKeyFrameAnimation.ValueKeyFrame)kf;
                    builder.WriteLine($"result{Deref}InsertKeyFrame({Float(kf.Progress)}, {CallFactoryFromFor(node, valueKeyFrame.Value)}, {CallFactoryFromFor(node, kf.Easing)});");
                }

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateScalarKeyFrameAnimationFactory(CodeBuilder builder, ScalarKeyFrameAnimation obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                var keyFrames = obj.KeyFrames;
                var firstKeyFrame = keyFrames.First();

                // If the duration is equal to the duration of the composition, use a helper.
                if (obj.Duration == _owner._compositionDuration &&
                    firstKeyFrame.Type == KeyFrameType.Value)
                {
                    EnsureKeyFrameAnimationHelperWritten(builder, obj.Type);

                    // Call the helper to create the animation. This will also set the duration and
                    // take the first key frame.
                    var kf = (KeyFrameAnimation<float, Expr.Scalar>.ValueKeyFrame)firstKeyFrame;
                    WriteFrameNumberComment(builder, kf.Progress);
                    WriteCreateAssignment(builder, node, $"Create{obj.Type}({Float(kf.Progress)}, {Float(kf.Value)}, {CallFactoryFromFor(node, kf.Easing)})");
                    InitializeCompositionAnimation(builder, obj, node);
                    keyFrames = keyFrames.Skip(1);
                }
                else
                {
                    WriteCreateAndAssignKeyFrameAnimation(builder, obj, node);
                }

                foreach (var kf in keyFrames)
                {
                    WriteFrameNumberComment(builder, kf.Progress);

                    switch (kf.Type)
                    {
                        case KeyFrameType.Expression:
                            var expressionKeyFrame = (KeyFrameAnimation<float, Expr.Scalar>.ExpressionKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertExpressionKeyFrame({Float(kf.Progress)}, {String(expressionKeyFrame.Expression)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        case KeyFrameType.Value:
                            var valueKeyFrame = (KeyFrameAnimation<float, Expr.Scalar>.ValueKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertKeyFrame({Float(kf.Progress)}, {Float(valueKeyFrame.Value)}, {CallFactoryFromFor(node, kf.Easing)});");
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionRectangleGeometryFactory(CodeBuilder builder, CompositionRectangleGeometry obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateRectangleGeometry()");
                InitializeCompositionGeometry(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.Offset), obj.Offset);
                WriteSetPropertyStatement(builder, nameof(obj.Size), obj.Size);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionRoundedRectangleGeometryFactory(CodeBuilder builder, CompositionRoundedRectangleGeometry obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateRoundedRectangleGeometry()");
                InitializeCompositionGeometry(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.CornerRadius), obj.CornerRadius);
                WriteSetPropertyStatement(builder, nameof(obj.Offset), obj.Offset);
                WriteSetPropertyStatement(builder, nameof(obj.Size), obj.Size);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionEllipseGeometryFactory(CodeBuilder builder, CompositionEllipseGeometry obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateEllipseGeometry()");
                InitializeCompositionGeometry(builder, obj, node);

                if (obj.Center != Sn.Vector2.Zero)
                {
                    WriteSetPropertyStatement(builder, nameof(obj.Center), obj.Center);
                }

                WriteSetPropertyStatement(builder, nameof(obj.Radius), obj.Radius);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionPathGeometryFactory(CodeBuilder builder, CompositionPathGeometry obj, ObjectData node)
            {
                var path = obj.Path is null ? null : _objectGraph[obj.Path];
                var createPathText = path is null ? string.Empty : CallFactoryFromFor(node, path);
                var createPathGeometryText = $"_c{Deref}CreatePathGeometry({createPathText})";

                if (obj.Animators.Count() == 0 &&
                    obj.Properties.Names.Count == 0 &&
                    !obj.TrimEnd.HasValue &&
                    !obj.TrimOffset.HasValue &&
                    !obj.TrimStart.HasValue &&
                    !node.IsReachableFrom(path))
                {
                    WriteSimpleObjectFactory(builder, node, createPathGeometryText);
                }
                else
                {
                    WriteObjectFactoryStart(builder, node);
                    WriteCreateAssignment(builder, node, createPathGeometryText);
                    InitializeCompositionGeometry(builder, obj, node);
                    WriteCompositionObjectFactoryEnd(builder, obj, node);
                }

                return true;
            }

            bool GenerateCompositionColorBrushFactory(CodeBuilder builder, CompositionColorBrush obj, ObjectData node)
            {
                var createCallText = obj.Color.HasValue
                                        ? $"_c{Deref}CreateColorBrush({Color(obj.Color.Value)})"
                                        : $"_c{Deref}CreateColorBrush()";

                if (obj.Animators.Count() > 0)
                {
                    WriteObjectFactoryStart(builder, node);
                    WriteCreateAssignment(builder, node, createCallText);
                    InitializeCompositionBrush(builder, obj, node);
                    WriteCompositionObjectFactoryEnd(builder, obj, node);
                }
                else
                {
                    WriteSimpleObjectFactory(builder, node, createCallText);
                }

                return true;
            }

            bool GenerateCompositionColorGradientStopFactory(CodeBuilder builder, CompositionColorGradientStop obj, ObjectData node)
            {
                if (obj.Animators.Count() > 0)
                {
                    WriteObjectFactoryStart(builder, node);
                    WriteCreateAssignment(builder, node, $"_c{Deref}CreateColorGradientStop({Float(obj.Offset)}, {Color(obj.Color)})");
                    InitializeCompositionObject(builder, obj, node);
                    WriteCompositionObjectFactoryEnd(builder, obj, node);
                }
                else
                {
                    WriteSimpleObjectFactory(builder, node, $"_c{Deref}CreateColorGradientStop({Float(obj.Offset)}, {Color(obj.Color)})");
                }

                return true;
            }

            bool GenerateShapeVisualFactory(CodeBuilder builder, ShapeVisual obj, ObjectData node)
            {
                // Sanity check: A ShapeVisual's size is its clip. If it's not set, nothing will display.
                Debug.Assert(obj.Size.HasValue && obj.Size.Value.Length() > 0, "ShapeVisuals need a size");
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateShapeVisual()");
                InitializeContainerVisual(builder, obj, node);
                WritePopulateShapesCollection(builder, obj.Shapes, node);
                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateSpriteVisualFactory(CodeBuilder builder, SpriteVisual obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateSpriteVisual()");
                InitializeContainerVisual(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.Brush), obj.Brush, node);
                WriteSetPropertyStatement(builder, nameof(obj.Shadow), obj.Shadow, node);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateContainerShapeFactory(CodeBuilder builder, CompositionContainerShape obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateContainerShape()");
                InitializeCompositionShape(builder, obj, node);
                WritePopulateShapesCollection(builder, obj.Shapes, node);
                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionEffectBrushFactory( CodeBuilder builder, CompositionEffectBrush obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);

                builder.WriteLine($"{ConstVar} effectFactory = {CallFactoryFromFor(node, obj.GetEffectFactory())};");

                WriteCreateAssignment(builder, node, $"effectFactory{Deref}CreateBrush()");
                InitializeCompositionBrush(builder, obj, node);

                // Perform brush initialization.
                foreach (var source in obj.GetEffectFactory().Effect.Sources)
                {
                    builder.WriteLine($"result{Deref}SetSourceParameter({String(source.Name)}, {CallFactoryFromFor(node, obj.GetSourceParameter(source.Name))});");
                }

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateSpriteShapeFactory(CodeBuilder builder, CompositionSpriteShape obj, ObjectData node)
            {
                var setFillBrush = true;
                WriteObjectFactoryStart(builder, node);
                if (obj.Geometry is null)
                {
                    WriteCreateAssignment(builder, node, $"_c{Deref}CreateSpriteShape()");
                    InitializeCompositionShape(builder, obj, node);
                }
                else
                {
                    if (obj.TransformMatrix.HasValue)
                    {
                        // Use the helper.
                        setFillBrush = false;

                        if (obj.FillBrush is null)
                        {
                            WriteCallHelperCreateSpriteShape(builder, obj, node, obj.TransformMatrix.Value);
                        }
                        else
                        {
                            WriteCallHelperCreateSpriteShapeWithFillBrush(builder, obj, node, obj.TransformMatrix.Value);
                        }
                    }
                    else
                    {
                        WriteCreateAssignment(builder, node, $"_c{Deref}CreateSpriteShape({CallFactoryFromFor(node, obj.Geometry)})");
                        InitializeCompositionShape(builder, obj, node);
                    }
                }

                // The CompositionShape properties are now initialized. Initialize the
                // properties that are specific to CompositionSpriteShape properties.
                if (setFillBrush)
                {
                    WriteSetPropertyStatement(builder, nameof(obj.FillBrush), obj.FillBrush, node);
                }

                WriteSetPropertyStatement(builder, nameof(obj.IsStrokeNonScaling), obj.IsStrokeNonScaling);
                WriteSetPropertyStatement(builder, nameof(obj.StrokeBrush), obj.StrokeBrush, node);
                WriteSetPropertyStatement(builder, nameof(obj.StrokeDashCap), obj.StrokeDashCap);
                WriteSetPropertyStatement(builder, nameof(obj.StrokeDashOffset), obj.StrokeDashOffset);

                if (obj.StrokeDashArray.Count > 0)
                {
                    builder.WriteLine($"{ConstVar} strokeDashArray = {_s.PropertyGet("result", "StrokeDashArray")};");
                    foreach (var strokeDash in obj.StrokeDashArray)
                    {
                        builder.WriteLine($"strokeDashArray{Deref}{IListAdd}({Float(strokeDash)});");
                    }
                }

                WriteSetPropertyStatement(builder, nameof(obj.StrokeStartCap), obj.StrokeStartCap);
                WriteSetPropertyStatement(builder, nameof(obj.StrokeEndCap), obj.StrokeEndCap);
                WriteSetPropertyStatement(builder, nameof(obj.StrokeLineJoin), obj.StrokeLineJoin, formatter: StrokeLineJoin);
                WriteSetPropertyStatement(builder, nameof(obj.StrokeMiterLimit), obj.StrokeMiterLimit);
                WriteSetPropertyStatement(builder, nameof(obj.StrokeThickness), obj.StrokeThickness);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionSurfaceBrushFactory(CodeBuilder builder, CompositionSurfaceBrush obj, ObjectData node)
            {
                var surfaceNode = obj.Surface switch
                {
                    CompositionObject compositionObject => NodeFor(compositionObject),
                    Wmd.LoadedImageSurface loadedImageSurface => NodeFor(loadedImageSurface),
                    _ => null,
                };

                // Create the code that initializes the Surface.
                var surfaceInitializationText = obj.Surface switch
                {
                    CompositionObject compositionObject => CallFactoryFromFor(node, compositionObject),
                    Wmd.LoadedImageSurface _ => surfaceNode!.FieldName!,
                    null => string.Empty,
                    _ => throw new InvalidOperationException(),
                };

                var isReachableFromSurfaceNode = node.IsReachableFrom(surfaceNode);

                if (obj.Animators.Count() == 0 &&
                    obj.Properties.Names.Count == 0 &&
                    !isReachableFromSurfaceNode)
                {
                    WriteSimpleObjectFactory(builder, node, $"_c{Deref}CreateSurfaceBrush({surfaceInitializationText})");
                }
                else
                {
                    WriteObjectFactoryStart(builder, node);

                    if (isReachableFromSurfaceNode)
                    {
                        // The Surface depends on the brush, so the brush needs to be created and assigned
                        // before the Surface.
                        WriteCreateAssignment(builder, node, $"_c{Deref}CreateSurfaceBrush()");
                        WriteSetPropertyStatement(builder, "Surface", surfaceInitializationText);
                    }
                    else
                    {
                        WriteCreateAssignment(builder, node, $"_c{Deref}CreateSurfaceBrush({surfaceInitializationText})");
                    }

                    InitializeCompositionBrush(builder, obj, node);
                    WriteCompositionObjectFactoryEnd(builder, obj, node);
                }

                return true;
            }

            bool GenerateCompositionViewBoxFactory(CodeBuilder builder, CompositionViewBox obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateViewBox()");
                InitializeCompositionObject(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.Size), obj.Size);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            bool GenerateCompositionVisualSurfaceFactory(CodeBuilder builder, CompositionVisualSurface obj, ObjectData node)
            {
                WriteObjectFactoryStart(builder, node);
                WriteCreateAssignment(builder, node, $"_c{Deref}CreateVisualSurface()");
                InitializeCompositionObject(builder, obj, node);

                WriteSetPropertyStatement(builder, nameof(obj.SourceVisual), obj.SourceVisual, node);
                WriteSetPropertyStatement(builder, nameof(obj.SourceSize), obj.SourceSize);
                WriteSetPropertyStatement(builder, nameof(obj.SourceOffset), obj.SourceOffset);

                WriteCompositionObjectFactoryEnd(builder, obj, node);
                return true;
            }

            // Name used in stats reports to identify the class without using its class name.
            internal string? StatsName => _isPartOfMultiVersionSource ? $"UAP v{_requiredUapVersion}" : null;

            string IAnimatedVisualInfo.ClassName =>
                $"{_owner._className}_AnimatedVisual{(_isPartOfMultiVersionSource ? $"_UAPv{_requiredUapVersion}" : string.Empty)}";

            IReadOnlyList<LoadedImageSurfaceInfo> IAnimatedVisualInfo.LoadedImageSurfaceNodes
            {
                get
                {
                    if (_loadedImageSurfaceInfos is null)
                    {
                        _loadedImageSurfaceInfos =
                            (from n in _nodes
                             where n.IsLoadedImageSurface
                             select _owner._loadedImageSurfaceInfosByNode[n])
                                .OrderBy(n => n.Name, AlphanumericStringComparer.Instance)
                                .ToArray();
                    }

                    return _loadedImageSurfaceInfos;
                }
            }

            uint IAnimatedVisualInfo.RequiredUapVersion => _requiredUapVersion;

            public bool ImplementCreateAndDestroyMethods => _configuration.ImplementCreateAndDestroyMethods;

            static void WriteMatrixComment(CodeBuilder builder, Matrix3x2? matrix)
            {
                if (matrix.HasValue)
                {
                    var (translation, rotationDegrees, scale, skew) = MatrixDecomposer.Decompose(matrix.Value);

                    // Use the word "Offset" to be consistent with Composition APIs.
                    var t = translation.HasValue ? $"Offset:{translation}" : string.Empty;
                    var r = rotationDegrees.HasValue ? $"Rotation:{rotationDegrees} degrees" : string.Empty;
                    var sc = scale.HasValue ? $"Scale:{scale}" : string.Empty;
                    var sk = skew.HasValue ? $"Skew:{skew}" : string.Empty;

                    builder.WriteComment(string.Join(", ", new[] { t, r, sc, sk }.Where(str => str.Length > 0)));
                }
            }

            static void WriteMatrixComment(CodeBuilder builder, Matrix4x4? matrix)
            {
                if (matrix.HasValue)
                {
                    var (translation, rotation, scale) = MatrixDecomposer.Decompose(matrix.Value);

                    // Use the word "Offset" to be consistent with Composition APIs.
                    // Show only the x and y offsets and scales because we don't suport 3D Lottie.
                    var t = translation.HasValue ? $"Offset:<{translation.Value.X}, {translation.Value.Y}>" : string.Empty;
                    var r = rotation.HasValue ? $"Rotation:{rotation}" : string.Empty;
                    var sc = scale.HasValue ? $"Scale:<{scale.Value.X}, {scale.Value.Y}>" : string.Empty;

                    builder.WriteComment(string.Join(", ", new[] { t, r, sc }.Where(str => str.Length > 0)));
                }
            }

            static void WriteShortDescriptionComment(CodeBuilder builder, IDescribable obj) =>
                builder.WriteComment(obj.ShortDescription);
        }

        // Aggregates ObjectData nodes that are shared between different IAnimatedVisual instances,
        // for example, LoadedImageSurface objects. Such nodes describe factories that are
        // scoped to the IAnimatedVisualSource implementation rather than the IAnimatedVisual implementation.
        sealed class SharedNodeGroup
        {
            readonly ObjectData[] _items;

            internal SharedNodeGroup(IEnumerable<ObjectData> items)
            {
                _items = items.ToArray();
            }

            /// <summary>
            /// An <see cref="ObjectData"/> object that will be treated as the canonical object.
            /// </summary>
            internal ObjectData CanonicalNode => _items[0];

            /// <summary>
            /// The <see cref="ObjectData"/> objects except the <see cref="CanonicalNode"/> object.
            /// </summary>
            internal IEnumerable<ObjectData> Rest => _items.Skip(1);

            /// <summary>
            ///  All of the <see cref="ObjectData"/> objects that are sharing this group.
            /// </summary>
            internal IReadOnlyList<ObjectData> All => _items;
        }

        // A node in the object graph, annotated with extra stuff to assist in code generation.
        sealed class ObjectData : Graph.Node<ObjectData>
        {
            Func<string>? _overriddenFactoryCall;
            Dictionary<ObjectData, string>? _callFactoryFromForCache;

            public Dictionary<ObjectData, string> CallFactoryFromForCache
            {
                get
                {
                    // Lazy initialization because not all nodes need the cache.
                    if (_callFactoryFromForCache is null)
                    {
                        _callFactoryFromForCache = new Dictionary<ObjectData, string>();
                    }

                    return _callFactoryFromForCache;
                }
            }

            // The name that is given to the node by the NodeNamer. This name is used to generate factory method
            // names and field names.
            public string? Name { get; set; }

            public string? FieldName => RequiresStorage ? CamelCase(Name!) : null;

            // Returns text for obtaining the value for this node. If the node has
            // been inlined, this can generate the code into the returned string, otherwise
            // it returns code for calling the factory.
            internal string FactoryCall()
                 => Inlined ? _overriddenFactoryCall!() : $"{Name}()";

            IEnumerable<string> GetAncestorShortComments()
            {
                // Prevent the root node from looking for ancestors. This could cause
                // infinite recursion.
                if (IsRootNode)
                {
                    yield break;
                }

                // Get the nodes that reference this node.
                var parents = InReferences.Select(v => v.Node).ToArray();
                if (parents.Length == 1)
                {
                    // There is exactly one parent. Get its comments.
                    foreach (var ancestorShortcomment in parents[0].GetAncestorShortComments())
                    {
                        if (!string.IsNullOrWhiteSpace(ancestorShortcomment))
                        {
                            yield return $"- {ancestorShortcomment}";
                        }
                    }

                    var parentShortComment = parents[0].ShortComment;
                    if (!string.IsNullOrWhiteSpace(parentShortComment))
                    {
                        yield return parentShortComment;
                    }
                }
            }

            internal string LongComment
            {
                get
                {
                    // Prepend the ancestor nodes.
                    var sb = new StringBuilder();
                    var ancestorIndent = 0;
                    foreach (var ancestorComment in GetAncestorShortComments())
                    {
                        sb.Append(new string(' ', ancestorIndent));
                        sb.AppendLine(ancestorComment);
                        ancestorIndent += 2;
                    }

                    sb.Append(((IDescribable)Object).LongDescription);

                    return sb.ToString();
                }
            }

            internal string? ShortComment => ((IDescribable)Object).ShortDescription;

            // True if this is the root node. This information is used when walking the
            // graph to create ancestor comments to prevent infinite recursion.
            internal bool IsRootNode { get; set; }

            // True if the object is referenced from more than one method and
            // therefore must be stored after it is created.
            internal bool RequiresStorage { get; set; }

            // True if the object must be stored as read-only after it is created.
            internal bool RequiresReadonlyStorage { get; set; }

            // Set to indicate that the node relies on Microsoft.Graphics.Canvas namespace
            internal bool UsesCanvas => Object is CompositionEffectBrush;

            // Set to indicate that the node relies on Microsoft.Graphics.Canvas.Effects namespace
            internal bool UsesCanvasEffects => Object is CompositionEffectBrush;

            // Set to indicate that the node relies on Microsoft.Graphics.Canvas.Geometry namespace
            internal bool UsesCanvasGeometry => Object is CanvasGeometry;

            // Set to indicate that the node is a LoadedImageSurface.
            internal bool IsLoadedImageSurface => Object is Wmd.LoadedImageSurface;

            // True if the node describes an object that can be shared between
            // multiple IAnimatedVisual classes, and thus will be associated with the
            // IAnimatedVisualSource implementation rather than the IAnimatedVisual implementation.
            internal bool IsSharedNode { get; set; }

            // Set to indicate that the node uses the Windows.UI.Xaml.Media namespace.
            internal bool UsesNamespaceWindowsUIXamlMedia => IsLoadedImageSurface;

            // Set to indicate that the node uses stream(s).
            internal bool UsesStream => Object is Wmd.LoadedImageSurface lis && lis.Type == Wmd.LoadedImageSurface.LoadedImageSurfaceType.FromStream;

            // Set to indicate that the node uses asset file(s).
            internal bool UsesAssetFile => Object is Wmd.LoadedImageSurface lis && lis.Type == Wmd.LoadedImageSurface.LoadedImageSurfaceType.FromUri;

            // Set to indicate that the composition depends on the given effect type.
            internal bool UsesEffect(Mgce.GraphicsEffectType effectType) => Object is CompositionEffectBrush compositeEffectBrush && compositeEffectBrush.GetEffectFactory().Effect.Type == effectType;

            // Identifies the byte array of a LoadedImageSurface.
            internal string? LoadedImageSurfaceBytesFieldName => IsLoadedImageSurface ? $"s_{Name}_Bytes" : null;

            internal Uri? LoadedImageSurfaceImageUri { get; set; }

            // True if the code to create the object will be generated inline.
            internal bool Inlined => _overriddenFactoryCall is not null;

            internal void ForceInline(Func<string> replacementFactoryCall)
            {
                _overriddenFactoryCall = replacementFactoryCall;
                RequiresStorage = false;
                RequiresReadonlyStorage = false;
            }

            // The name of the type of the object described by this node.
            // This is the name used as the return type of a factory method.
            internal string TypeName
                => Type switch
                {
                    Graph.NodeType.CanvasGeometry => "CanvasGeometry",
                    Graph.NodeType.CompositionObject => ((CompositionObject)Object).Type.ToString(),
                    Graph.NodeType.CompositionPath => "CompositionPath",
                    Graph.NodeType.LoadedImageSurface => "LoadedImageSurface",
                    _ => throw new InvalidOperationException(),
                };

            // True iff a factory should be created for the given node.
            internal bool NeedsAFactory
            {
                get
                {
                    return !Inlined && Type switch
                    {
                        Graph.NodeType.CanvasGeometry => true,
                        Graph.NodeType.CompositionPath => true,
                        Graph.NodeType.LoadedImageSurface => true,
                        Graph.NodeType.CompositionObject => NeedsAFactory((CompositionObject)Object),
                        _ => throw new InvalidOperationException(),
                    };

                    bool NeedsAFactory(CompositionObject obj)
                    {
                        return obj.Type switch
                        {
                            // AnimationController is never created explicitly - they result from
                            // calling TryGetAnimationController(...).
                            CompositionObjectType.AnimationController => ((AnimationController)obj).IsCustom,

                            // CompositionPropertySet is never created explicitly - they just exist
                            // on the Properties property of every CompositionObject.
                            CompositionObjectType.CompositionPropertySet => false,

                            // ExpressionAnimations that are not shared will use the "_reusableExpressionAnimation"
                            // so there is no need for a factory for them. Detect the shared case by counting the
                            // InReferences to the node.
                            CompositionObjectType.ExpressionAnimation => InReferences.Length > 1,

                            // All other CompositionObjects need factories.
                            _ => true,
                        };
                    }
                }
            }

            // For debugging purposes only.
            public override string ToString() => Name is null ? $"{TypeName} {Position}" : $"{Name} {Position}";

            // Sets the first character to lower case.
            static string CamelCase(string value) => $"_{char.ToLowerInvariant(value[0])}{value.Substring(1)}";
        }
    }
}
