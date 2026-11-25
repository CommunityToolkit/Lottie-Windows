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
        /// Gets the code that sets a property declared by a base class of the type of the
        /// target, or null to set the property directly.
        /// </summary>
        /// <param name="declaringType">The name of the class that declares the property.</param>
        /// <param name="valueType">The name of the type of the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="target">The code for the object whose property is set.</param>
        /// <param name="value">The code for the value.</param>
        /// <returns>The code of the call, without a terminating semicolon, or null.</returns>
        private protected virtual string? GetInheritedPropertySetterCall(string declaringType, string valueType, string propertyName, string target, string value) => null;

        /// <summary>
        /// Gets the code that adds a color stop to the ColorStops of a gradient brush, or null to
        /// add it directly to the ColorStops collection.
        /// </summary>
        /// <param name="target">The code for the gradient brush.</param>
        /// <param name="colorStop">The code for the color stop.</param>
        /// <returns>The code of the call, without a terminating semicolon, or null.</returns>
        private protected virtual string? GetAddColorStopCall(string target, string colorStop) => null;

        /// <summary>
        /// Gets the code that adds a shape to the Shapes of a ShapeVisual or a CompositionContainerShape,
        /// or null to add it directly to the Shapes collection.
        /// </summary>
        /// <param name="targetType">The name of the type of the object that has the Shapes.</param>
        /// <param name="target">The code for the object that has the Shapes.</param>
        /// <param name="shape">The code for the shape.</param>
        /// <returns>The code of the call, without a terminating semicolon, or null.</returns>
        private protected virtual string? GetAddShapeCall(string targetType, string target, string shape) => null;

        /// <summary>
        /// Gets the code that inserts a visual at the top of the Children of a ContainerVisual,
        /// or null to insert it directly into the Children collection.
        /// </summary>
        /// <param name="target">The code for the ContainerVisual.</param>
        /// <param name="child">The code for the child visual.</param>
        /// <returns>The code of the call, without a terminating semicolon, or null.</returns>
        private protected virtual string? GetInsertChildAtTopCall(string target, string child) => null;

        /// <summary>
        /// Gets the attribute to write before the helper methods that the factories call, or null
        /// for no attribute.
        /// </summary>
        private protected virtual string? HelperMethodAttribute => null;

        /// <summary>
        /// Gets a value indicating whether the language supports the BindTrim helper, which
        /// initializes the TStart and TEnd values of a geometry, starts their animations, and binds
        /// the trim of the geometry to them.
        /// </summary>
        private protected virtual bool IsBindTrimHelperSupported => false;

        /// <summary>
        /// Writes code that inserts the given value key frames into the animation in the "result"
        /// variable from a table of data, which is more compact than a call per key frame.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="animationType">The type of the animation.</param>
        /// <param name="keyFrames">The key frames, in the order they must be inserted.</param>
        /// <returns><see langword="true"/> if the code was written, or <see langword="false"/> if
        /// the language does not support it, in which case nothing was written.</returns>
        private protected virtual bool TryWriteInsertKeyFramesFromData(
            CodeBuilder builder,
            CompositionObjectType animationType,
            IReadOnlyList<KeyFrameData> keyFrames) => false;

        /// <summary>
        /// Describes a value key frame for <see cref="TryWriteInsertKeyFramesFromData"/>.
        /// </summary>
        private protected sealed class KeyFrameData
        {
            internal KeyFrameData(float progress, IReadOnlyList<float> value, object? easing, string? easingCode, string comment)
            {
                Progress = progress;
                Value = value;
                Easing = easing;
                EasingCode = easingCode;
                Comment = comment;
            }

            /// <summary>
            /// The progress of the key frame.
            /// </summary>
            internal float Progress { get; }

            /// <summary>
            /// The components of the value of the key frame.
            /// </summary>
            internal IReadOnlyList<float> Value { get; }

            /// <summary>
            /// The easing function of the key frame. Key frames with the same easing function
            /// use the same object.
            /// </summary>
            internal object? Easing { get; }

            /// <summary>
            /// The code that gets the easing function, or null if the type of animation has no easing.
            /// The code of the first key frame using an easing function may create it, so it must
            /// be evaluated before the code of the other key frames that use the same easing function.
            /// </summary>
            internal string? EasingCode { get; }

            /// <summary>
            /// A description of the key frame.
            /// </summary>
            internal string Comment { get; }
        }

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

            // Generate each AnimatedVisual class.
            var animatedVisualClasses = _animatedVisualGenerators.Select(g => g.GenerateClass()).ToArray();

            WriteAnimatedVisualClasses(builder, animatedVisualClasses);

            // Write the end of the file.
            WriteImplementationFileEnd(builder);

            return builder.ToString();
        }

        /// <summary>
        /// Writes the AnimatedVisual classes. Override to change how the classes are written, for
        /// example to share code between them.
        /// </summary>
        /// <param name="builder">A <see cref="CodeBuilder"/> used to create the code.</param>
        /// <param name="classes">The AnimatedVisual classes.</param>
        private protected virtual void WriteAnimatedVisualClasses(
            CodeBuilder builder,
            IReadOnlyList<AnimatedVisualClass> classes)
        {
            var firstAnimatedVisualWritten = false;
            foreach (var c in classes)
            {
                if (firstAnimatedVisualWritten)
                {
                    // Put a blank line between each AnimatedVisual class.
                    builder.WriteLine();
                }

                c.WriteStart(builder);
                foreach (var field in c.Members.Fields)
                {
                    builder.WriteLine(field.Declaration);
                }

                foreach (var method in c.Members.Methods)
                {
                    builder.WriteLine();
                    method.Write(builder);
                }

                builder.WriteLine();
                c.WriteEnd(builder);
                firstAnimatedVisualWritten = true;
            }
        }

        /// <summary>
        /// An AnimatedVisual class that has been generated, and that is ready to be written.
        /// </summary>
        private protected sealed class AnimatedVisualClass
        {
            readonly InstantiatorGeneratorBase _owner;
            readonly CodeBuilder _createAnimations;
            readonly CodeBuilder _destroyAnimations;

            internal AnimatedVisualClass(
                InstantiatorGeneratorBase owner,
                IAnimatedVisualInfo info,
                AnimatedVisualClassMembers members,
                CodeBuilder createAnimations,
                CodeBuilder destroyAnimations)
            {
                _owner = owner;
                Info = info;
                Members = members;
                _createAnimations = createAnimations;
                _destroyAnimations = destroyAnimations;
            }

            /// <summary>
            /// Gets the description of the class.
            /// </summary>
            internal IAnimatedVisualInfo Info { get; }

            /// <summary>
            /// Gets the fields and methods of the class, in the order that they are written.
            /// </summary>
            internal AnimatedVisualClassMembers Members { get; }

            /// <summary>
            /// Writes the start of the class, up to and including its opening brace.
            /// </summary>
            internal void WriteStart(CodeBuilder builder)
            {
                _owner._currentAnimatedVisualGenerator = (AnimatedVisualGenerator)Info;
                _owner.WriteAnimatedVisualStart(builder, Info);
                _owner._currentAnimatedVisualGenerator = null;
            }

            /// <summary>
            /// Writes what is after the fields and methods of the class, including its closing brace.
            /// </summary>
            internal void WriteEnd(CodeBuilder builder)
            {
                _owner._currentAnimatedVisualGenerator = (AnimatedVisualGenerator)Info;
                _owner.WriteAnimatedVisualEnd(builder, Info, _createAnimations, _destroyAnimations);
                _owner._currentAnimatedVisualGenerator = null;
            }
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

        string FieldDeclaration(string typeName, string fieldName)
            => $"{typeName} {fieldName}{_s.DefaultInitialize};";

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

            // The helper methods that the factories call, by key. They are added when first needed.
            readonly Dictionary<string, AnimatedVisualMethod> _helperMethods = new Dictionary<string, AnimatedVisualMethod>();

            // The factory method that is being written, if any.
            AnimatedVisualMethod? _currentMethod;

            // What the code that is being generated uses, which is recorded in the method that is being written.
            AnimatedVisualMemberUses? _currentUses;

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

            string TimeSpan(TimeSpan value)
            {
                if (value != _owner._compositionDuration)
                {
                    return _s.TimeSpan(value);
                }

                // The duration of the composition is in a field.
                _currentUses?.UseField(DurationTicksFieldName);
                return _s.TimeSpan(DurationTicksFieldName);
            }

            /// <summary>
            /// Returns the code to call the factory for the given object.
            /// </summary>
            /// <returns>The code to call the factory for the given object.</returns>
            internal string CallFactoryFor(CanvasGeometry obj)
                => CallFactoryFromFor(_currentObjectFactoryNode!, obj);

            // Returns the code to call the factory for the given node from the given node.
            string CallFactoryFromFor(ObjectData callerNode, ObjectData calleeNode)
            {
                if (callerNode.CallFactoryFromForCache.TryGetValue(calleeNode, out var cached))
                {
                    // Return the factory from the cache. The code that uses it uses what it uses.
                    _currentUses?.Add(cached.Uses);
                    return cached.Code;
                }

                // Get the factory call code, and what it uses, which is recorded again when the code is
                // used from the cache.
                var outerUses = _currentUses;
                var uses = new AnimatedVisualMemberUses();
                _currentUses = uses;
                var result = CallFactoryFromFor_UnCached(callerNode, calleeNode);
                _currentUses = outerUses;
                outerUses?.Add(uses);

                // Save the factory call code in the cache on the caller for next time.
                if (calleeNode.RequiresStorage && !_owner._disableFieldOptimization)
                {
                    // The node has storage for its result. Next time just return the field.
                    var fieldUses = new AnimatedVisualMemberUses();
                    fieldUses.UseField(calleeNode.FieldName!);
                    callerNode.CallFactoryFromForCache.Add(calleeNode, (calleeNode.FieldName!, fieldUses));
                }
                else
                {
                    callerNode.CallFactoryFromForCache.Add(calleeNode, (result, uses));
                }

                return result;
            }

            // Returns the name of the field of the node, and records that the code being generated uses it.
            string UseField(ObjectData node)
            {
                _currentUses?.UseField(node.FieldName!);
                return node.FieldName!;
            }

            // Returns the code that refers to the object whose animations are started, and records that the code
            // being generated uses what it uses.
            string Use(LocalReference local)
            {
                if (local.Field is not null)
                {
                    UseField(local.Field);
                }

                return local.Code;
            }

            // Returns the name of the field that holds the singleton ExpressionAnimation, and records that the code
            // being generated uses it.
            string UseSingletonExpressionAnimation()
            {
                _currentUses?.UseField(SingletonExpressionAnimationName);
                return SingletonExpressionAnimationName;
            }

            // Records the uses of the code that is generated while the returned scope is alive in the given
            // uses, which are those of the method that the code is written into.
            UsesScope RecordUsesIn(AnimatedVisualMemberUses? uses) => new UsesScope(this, uses);

            // Returns the code that gets the value of the node, and records that the code being generated uses
            // what it uses. If the node is not inlined, that is the factory method of the node.
            string UseFactoryCall(ObjectData node)
            {
                if (!node.Inlined)
                {
                    _currentUses?.UseMethod(node.Name!);
                }

                return node.FactoryCall();
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
                    return UseField(calleeNode);
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
                    return UseFactoryCall(calleeNode);
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
                    return UseField(calleeNode);
                }
                else if (calleeNode.RequiresStorage && _factoriesAlreadyCalled.Contains((callerNode, calleeNode)))
                {
                    return UseField(calleeNode);
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

                    return UseFactoryCall(calleeNode);
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
                WriteCreateAssignment(builder, node, $"{New("CompositionPath")}({_s.CanvasGeometryFactoryCall(UseFactoryCall(canvasGeometry))})");
                WriteObjectFactoryEnd(builder);
                return true;
            }

            void WriteObjectFactoryStart(CodeBuilder builder, ObjectData node, IEnumerable<string>? parameters = null)
            {
                // Save the node as the current node while the factory is being written.
                _currentObjectFactoryNode = node;

                // The comment and the signature belong to the method, and the code of its body goes into the builder.
                var method = _currentMethod!;
                Debug.Assert(ReferenceEquals(builder, method.Body), "The factory must be written to the body of the current method");
                method.Comment.WriteComment(node.LongComment);
                method.Header.WriteLine($"{_owner._s.ReferenceTypeName(node.TypeName)} {node.Name}({(parameters is null ? string.Empty : string.Join(", ", parameters))})");
                builder.OpenScope();
            }

            void WriteObjectFactoryEnd(CodeBuilder builder)
            {
                builder.WriteLine("return result;");
                builder.CloseScope();
                _currentObjectFactoryNode = null;
            }

            /// <summary>
            /// Combines the calls to <see cref="StartAnimations(CodeBuilder, CompositionObject, ObjectData, LocalReference)"/>
            /// with <see cref="WriteObjectFactoryEnd(CodeBuilder)"/>.
            /// </summary>
            void WriteCompositionObjectFactoryEnd(CodeBuilder builder, CompositionObject obj, ObjectData node)
            {
                if (_configuration.ImplementCreateAndDestroyMethods)
                {
                    // Use FieldName as the reference name in case if we are implementing IAnimatedVisual2.
                    // We can't use local name "result" since animations will be started from different place.
                    StartAnimations(builder, obj, node, new LocalReference(node.FieldName ?? string.Empty, node.FieldName is null ? null : node), ref controllerCreatedInCreateAnimationsMethod);
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
                    var fieldName = UseField(node);
                    if (_owner._disableFieldOptimization)
                    {
                        // Create the object unless it has already been created.
                        builder.WriteLine($"return ({fieldName} == {Null})");
                        builder.Indent();
                        builder.WriteLine($"? {fieldName} = {createCallText}");
                        builder.WriteLine($": {fieldName};");
                        builder.UnIndent();
                    }
                    else
                    {
                        // If field optimization is enabled, the method will only get called once.
                        builder.WriteLine($"return {fieldName} = {createCallText};");
                    }
                }
                else
                {
                    // The object is only used once.
                    builder.WriteLine($"return {createCallText};");
                }

                builder.CloseScope();
                _currentObjectFactoryNode = null;
            }

            void WriteCreateAssignment(CodeBuilder builder, ObjectData node, string createCallText)
            {
                if (node.RequiresStorage)
                {
                    var fieldName = UseField(node);
                    if (_owner._disableFieldOptimization)
                    {
                        // If the field has already been assigned, return its value.
                        builder.WriteLine($"if ({fieldName} != {Null}) {{ return {fieldName}; }}");
                    }

                    builder.WriteLine($"{ConstVar} result = {fieldName} = {createCallText};");
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

            // Writes code that sets a property declared by a base class of the type of "result".
            void WriteSetInheritedPropertyStatement<T>(CodeBuilder builder, string declaringType, string valueType, string propertyName, T? value, Func<T, string> formatter)
                where T : struct
            {
                if (value.HasValue)
                {
                    switch (value.Value)
                    {
                        case Sn.Matrix3x2 matrix3x2:
                            WriteMatrixComment(builder, matrix3x2);
                            break;
                        case Matrix4x4 matrix4x4:
                            WriteMatrixComment(builder, matrix4x4);
                            break;
                    }

                    WriteSetInheritedPropertyStatement(builder, declaringType, valueType, propertyName, formatter(value.Value));
                }
            }

            // Writes code that sets a property declared by a base class of the type of "result".
            void WriteSetInheritedPropertyStatement(CodeBuilder builder, string declaringType, string valueType, string propertyName, string value)
            {
                var call = _owner.GetInheritedPropertySetterCall(declaringType, valueType, propertyName, "result", value);
                if (call is null)
                {
                    WriteSetPropertyStatement(builder, propertyName, value);
                }
                else
                {
                    builder.WriteLine($"{call};");
                }
            }

            void WritePopulateShapesCollection(CodeBuilder builder, IList<CompositionShape> shapes, ObjectData node)
            {
                // Get the code for the shapes in order, because the first use of a shape may create it.
                var shapeCalls = shapes.Select(shape => CallFactoryFromFor(node, shape)).ToArray();
                var addShapeCalls = shapeCalls.Select(shape => _owner.GetAddShapeCall(node.TypeName, "result", shape)).ToArray();
                if (addShapeCalls.Length > 0 && addShapeCalls.All(call => call is not null))
                {
                    // Add each shape with a call.
                    for (var i = 0; i < shapes.Count; i++)
                    {
                        WriteShortDescriptionComment(builder, shapes[i]);
                        builder.WriteLine($"{addShapeCalls[i]};");
                    }

                    return;
                }

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
                            builder.WriteLine($"{_s.PropertyGet("result", "Shapes")}{Deref}{IListAdd}({shapeCalls[0]});");
                            break;
                        }

                    default:
                        {
                            // Multiple items requires the use of a local.
                            builder.WriteLine($"{ConstVar} shapes = {_s.PropertyGet("result", "Shapes")};");
                            for (var i = 0; i < shapes.Count; i++)
                            {
                                WriteShortDescriptionComment(builder, shapes[i]);
                                builder.WriteLine($"shapes{Deref}{IListAdd}({shapeCalls[i]});");
                            }

                            break;
                        }
                }
            }

            void WriteFrameNumberComment(CodeBuilder builder, double progress)
            {
                builder.WriteComment(FrameNumberComment(progress));
            }

            string FrameNumberComment(double progress) => $"Frame {_owner._sourceMetadata.ProgressToFrameNumber(progress):0.##}.";

            // Writes the code that inserts the given key frames into the animation in the "result" variable.
            void WriteInsertKeyFrames<TValue, TExpression>(
                CodeBuilder builder,
                ObjectData node,
                KeyFrameAnimation<TValue, TExpression> animation,
                IEnumerable<KeyFrameAnimation_.KeyFrame> keyFrames,
                Func<TValue, string> valueCode,
                Func<TValue, float[]> valueData,
                Func<TValue, string?>? valueComment = null)
                where TExpression : Expr.Expression_<TExpression>
            {
                var animationType = animation.Type;

                // BooleanKeyFrameAnimations do not support easing functions.
                var hasEasing = animationType != CompositionObjectType.BooleanKeyFrameAnimation;
                var frames = keyFrames.ToArray();

                // Get the code for the easing functions in order, because the first use of an
                // easing function may create it.
                var easingCodes = frames.Select(kf => hasEasing ? CallFactoryFromFor(node, kf.Easing) : null).ToArray();

                // Key frames that all have values may be written as data.
                if (frames.Length > 1 && frames.All(kf => kf.Type == KeyFrameType.Value))
                {
                    var data = frames.Select((kf, i) =>
                    {
                        var value = ((KeyFrameAnimation<TValue, TExpression>.ValueKeyFrame)kf).Value;
                        var description = valueComment?.Invoke(value);
                        return new KeyFrameData(
                            kf.Progress,
                            valueData(value),
                            kf.Easing,
                            easingCodes[i],
                            string.IsNullOrWhiteSpace(description) ? FrameNumberComment(kf.Progress) : $"{FrameNumberComment(kf.Progress)} {description}");
                    }).ToArray();

                    if (_owner.TryWriteInsertKeyFramesFromData(builder, animationType, data))
                    {
                        return;
                    }
                }

                for (var i = 0; i < frames.Length; i++)
                {
                    var kf = frames[i];
                    WriteFrameNumberComment(builder, kf.Progress);

                    switch (kf.Type)
                    {
                        case KeyFrameType.Expression:
                            var expressionKeyFrame = (KeyFrameAnimation<TValue, TExpression>.ExpressionKeyFrame)kf;
                            builder.WriteLine($"result{Deref}InsertExpressionKeyFrame({Float(kf.Progress)}, {String(expressionKeyFrame.Expression)}, {easingCodes[i] ?? _s.Null});");
                            break;
                        case KeyFrameType.Value:
                            var valueKeyFrame = (KeyFrameAnimation<TValue, TExpression>.ValueKeyFrame)kf;
                            if (valueComment is not null)
                            {
                                builder.WriteComment(valueComment(valueKeyFrame.Value));
                            }

                            builder.WriteLine(hasEasing
                                ? $"result{Deref}InsertKeyFrame({Float(kf.Progress)}, {valueCode(valueKeyFrame.Value)}, {easingCodes[i]});"
                                : $"result{Deref}InsertKeyFrame({Float(kf.Progress)}, {valueCode(valueKeyFrame.Value)});");
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }

            // The names under which helper methods are kept, in the order that they are written. The
            // methods are only written if the code of an object needs them.
            static readonly string[] s_helperMethodKeys =
            {
                "StartProgressBoundAnimation",
                "BindProperty",
                "BindProperty2",
                "BindTrim",
                "CreateBooleanKeyFrameAnimation",
                "CreateColorKeyFrameAnimation",
                "CreatePathKeyFrameAnimation",
                "CreateScalarKeyFrameAnimation",
                "CreateVector2KeyFrameAnimation",
                "CreateVector3KeyFrameAnimation",
                "CreateVector4KeyFrameAnimation",
                "CreateSpriteShape",
                "CreateSpriteShapeWithFillBrush",
            };

            // Generates the fields and methods of the class, which are written by the AnimatedVisualClass.
            internal AnimatedVisualClass GenerateClass()
            {
                _owner._currentAnimatedVisualGenerator = this;

                if (_configuration.ImplementCreateAndDestroyMethods)
                {
                    _createAnimationsCodeBuilder = new CodeBuilder();
                    _destroyAnimationsCodeBuilder = new CodeBuilder();
                }

                var fields = GenerateFields();

                // Generate a factory method for each node.
                var factoryMethods = new List<AnimatedVisualMethod>();
                foreach (var node in _nodes)
                {
                    // Only generate a factory method if the node is not inlined into the caller.
                    if (!node.Inlined)
                    {
                        var method = GenerateFactoryMethodForNode(node);
                        if (method is not null)
                        {
                            factoryMethods.Add(method);
                        }
                    }
                }

                // The helper methods are written before the factory methods.
                var methods = s_helperMethodKeys
                    .Where(_helperMethods.ContainsKey)
                    .Select(key => _helperMethods[key])
                    .Concat(factoryMethods)
                    .ToArray();

                _owner._currentAnimatedVisualGenerator = null;

                return new AnimatedVisualClass(
                    _owner,
                    this,
                    new AnimatedVisualClassMembers(fields, methods),
                    _createAnimationsCodeBuilder,
                    _destroyAnimationsCodeBuilder);
            }

            // Gets the helper method with the given key, which is empty if it has not been written. The code
            // that is being generated uses the method.
            AnimatedVisualMethod HelperMethod(string key, string name)
            {
                Debug.Assert(Array.IndexOf(s_helperMethodKeys, key) >= 0, "The helper method would not be written");
                _currentUses?.UseMethod(name);
                if (!_helperMethods.TryGetValue(key, out var method))
                {
                    method = new AnimatedVisualMethod(name);
                    _helperMethods.Add(key, method);
                }

                return method;
            }

            IReadOnlyList<AnimatedVisualField> GenerateFields()
            {
                var fields = new List<AnimatedVisualField>();

                // Fields for constant values.
                fields.Add(new AnimatedVisualField(
                    DurationTicksFieldName,
                    ConstExprField(_s.TypeInt64, DurationTicksFieldName, $"{_s.Int64(_owner._compositionDuration.Ticks)}")));

                // Fields for each object that needs storage (i.e. objects that are referenced more than once).
                // Read-only fields come first. The fields that the constructor assigns are read-only.
                fields.Add(CompositorField());
                fields.Add(ConstructorAssignedField(_s.ReferenceTypeName("ExpressionAnimation"), SingletonExpressionAnimationName));

                if (_owner._isThemed)
                {
                    fields.Add(ConstructorAssignedField(_s.ReferenceTypeName("CompositionPropertySet"), ThemePropertiesFieldName));
                }

                foreach (var node in OrderByTypeThenName(_nodes.Where(n => n.RequiresReadonlyStorage)))
                {
                    // Generate a field for the read-only storage.
                    var typeName = _s.ReferenceTypeName(node.TypeName);
                    fields.Add(new AnimatedVisualField(
                        node.FieldName!,
                        _owner.FieldDeclaration(Readonly(typeName), node.FieldName!),
                        _owner.FieldDeclaration(typeName, node.FieldName!)));
                }

                foreach (var node in OrderByTypeThenName(_nodes.Where(n => n.RequiresStorage && !n.RequiresReadonlyStorage)))
                {
                    // Generate a field for the non-read-only storage.
                    fields.Add(new AnimatedVisualField(
                        node.FieldName!,
                        _owner.FieldDeclaration(_s.ReferenceTypeName(node.TypeName), node.FieldName!)));
                }

                return fields;
            }

            // A field that the constructor of the class assigns, and so cannot be read-only in a base class.
            AnimatedVisualField ConstructorAssignedField(string typeName, string fieldName)
                => new AnimatedVisualField(
                    fieldName,
                    _owner.FieldDeclaration(Readonly(typeName), fieldName),
                    _owner.FieldDeclaration(typeName, fieldName),
                    isAssignedByConstructor: true);

            // The Compositor that every class has, which any of its methods can use without recording it.
            AnimatedVisualField CompositorField()
                => new AnimatedVisualField(
                    "_c",
                    _owner.FieldDeclaration(Readonly(_s.ReferenceTypeName("Compositor")), "_c"),
                    _owner.FieldDeclaration(_s.ReferenceTypeName("Compositor"), "_c"),
                    isAssignedByConstructor: true,
                    isCommon: true);

            // Generates the factory method for the given node, or returns null if the node has no factory.
            AnimatedVisualMethod? GenerateFactoryMethodForNode(ObjectData node)
            {
                var method = new AnimatedVisualMethod(node.Name!);
                _currentMethod = method;
                _currentUses = method.Uses;
                WriteFactoryForNode(method.Body, node);
                _currentUses = null;
                _currentMethod = null;
                return method.IsEmpty ? null : method;
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
                var fieldName = node.RequiresStorage ? UseField(node) : node.FieldName!;

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
                => StartAnimations(builder, obj, node, LocalReference.Result);

            void StartAnimations(CodeBuilder builder, CompositionObject obj, ObjectData node, LocalReference localName)
            {
                var controllerVariableAdded = false;
                StartAnimations(builder, obj, node, localName, ref controllerVariableAdded);
            }

            void StartAnimations(CodeBuilder builder, CompositionObject obj, ObjectData node, LocalReference localName, ref bool controllerVariableAdded)
            {
                var animators = obj.Animators.ToArray();
                TryGetTrimBinding(obj, out var trimBindingIndex);

                // Start the animations for properties on the object.
                for (var i = 0; i < animators.Length; i++)
                {
                    if (i == trimBindingIndex)
                    {
                        // This animator and the next 3 are started by a helper.
                        WriteBindTrimCall(builder, obj, node, localName, animators[i], animators[i + 2]);
                        i += 3;
                        continue;
                    }

                    StartAnimation(builder, obj, node, localName, ref controllerVariableAdded, animators[i]);
                }

                // Start the animations for the properties on the property set on the object.
                // Prevent infinite recursion - the Properties on a CompositionPropertySet is itself.
                if (obj.Type != CompositionObjectType.CompositionPropertySet)
                {
                    // Start the animations for properties on the property set.
                    StartAnimations(builder, obj.Properties, NodeFor(obj.Properties), localName.Properties(_s), ref controllerVariableAdded);
                }
            }

            void StartAnimation(CodeBuilder builder, CompositionObject obj, ObjectData node, LocalReference localName, ref bool controllerVariableAdded, CompositionObject.Animator animator)
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
                    // If we are implementing IAnimatedVisual2, the code is written into the CreateAnimations and
                    // DestroyAnimations methods, so the method of the object does not use what the code uses.
                    using var uses = RecordUsesIn(_configuration.ImplementCreateAndDestroyMethods ? null : _currentUses);
                    StartAnimationWithFactory(builder, node, localName, ref controllerVariableAdded, animator, animationNode);
                }
            }

            // Starts a KeyFrameAnimation or a shared ExpressionAnimation. With IAnimatedVisual2, the code is
            // written into the CreateAnimations and DestroyAnimations methods, which is why it is not
            // recorded as used by the method of the object, and why it can use the code of the local name.
            void StartAnimationWithFactory(CodeBuilder builder, ObjectData node, LocalReference localName, ref bool controllerVariableAdded, CompositionObject.Animator animator, ObjectData animationNode)
            {
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
                                EnsureStartProgressBoundAnimationWritten();

                                // Special-case for a paused controller that has only its Progress property animated by
                                // an ExpressionAnimation that has a factory. Generate a call to a helper that will do the work.
                                // Note that this is the common case for Lottie.
                                if (_configuration.ImplementCreateAndDestroyMethods)
                                {
                                    // If we are implementing IAnimatedVisual2 we should create these animation not in the tree initialization code,
                                    // but inside CreateAnimations method.
                                    _createAnimationsCodeBuilder
                                        .WriteLine(
                                        $"StartProgressBoundAnimation({localName.Code}, " +
                                        $"{String(animator.AnimatedProperty)}, " +
                                        $"{animationFactoryCall}, " +
                                        $"{CallFactoryFromFor(NodeFor(animator.Controller), controllerExpressionAnimationNode)});");

                                    // If we are implementing IAnimatedVisual2 we should also write a destruction call.
                                    _destroyAnimationsCodeBuilder
                                        .WriteLine($"{localName.Code}{Deref}StopAnimation({String(animator.AnimatedProperty)});");
                                }
                                else
                                {
                                    builder.WriteLine(
                                        $"StartProgressBoundAnimation({localName.Code}, " +
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
                            .WriteLine($"{localName.Code}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {animationFactoryCall}, {CallFactoryFromFor(node, NodeFor(animator.Controller))});");
                    }
                    else
                    {
                        _createAnimationsCodeBuilder
                            .WriteLine($"{localName.Code}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {animationFactoryCall});");
                        ConfigureAnimationController(_createAnimationsCodeBuilder, localName, ref controllerVariableAdded, animator);
                    }

                    // If we are implementing IAnimatedVisual2 we should also write a destruction call.
                    _destroyAnimationsCodeBuilder
                        .WriteLine($"{localName.Code}{Deref}StopAnimation({String(animator.AnimatedProperty)});");
                }
                else
                {
                    if (animator.Controller is not null && animator.Controller.IsCustom)
                    {
                        builder.WriteLine($"{localName.Code}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {animationFactoryCall}, {CallFactoryFromFor(node, NodeFor(animator.Controller))});");
                    }
                    else
                    {
                        builder.WriteLine($"{localName.Code}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {animationFactoryCall});");
                        ConfigureAnimationController(builder, localName, ref controllerVariableAdded, animator);
                    }
                }
            }

            void ConfigureAnimationController(CodeBuilder builder, LocalReference localName, ref bool controllerVariableAdded, CompositionObject.Animator animator)
            {
                // If the animation has a controller, get the controller, optionally pause it, and recurse to start the animations
                // on the controller.
                if (animator.Controller is not null)
                {
                    var controller = animator.Controller;

                    if (!controllerVariableAdded)
                    {
                        // Declare and initialize the controller variable.
                        builder.WriteLine($"{Var} controller = {Use(localName)}{Deref}TryGetAnimationController({String(animator.AnimatedProperty)});");
                        controllerVariableAdded = true;
                    }
                    else
                    {
                        // Initialize the controller variable.
                        builder.WriteLine($"controller = {Use(localName)}{Deref}TryGetAnimationController({String(animator.AnimatedProperty)});");
                    }

                    if (controller.IsPaused)
                    {
                        builder.WriteLine($"controller{Deref}Pause();");
                    }

                    // Recurse to start animations on the controller.
                    StartAnimations(builder, controller, NodeFor(controller), LocalReference.Controller);
                }
            }

            // Finds the animators of a geometry that are started by the BindTrim helper, which is the
            // way the translator animates trims: TStart and TEnd values in the property set of the
            // geometry, with progress bound animations, and TrimStart and TrimEnd bound to their
            // minimum and maximum, as 4 consecutive animators. The helper also initializes the property
            // set, so the property set must have only these values. This has no side effects, so that
            // it gives the same result when the property set is initialized and when the animations
            // are started.
            bool TryGetTrimBinding(CompositionObject obj, out int index)
            {
                index = -1;

                // With IAnimatedVisual2, the progress bound animations are started by a different method.
                if (!_owner.IsBindTrimHelperSupported ||
                    _configuration.ImplementCreateAndDestroyMethods ||
                    obj is not CompositionGeometry)
                {
                    return false;
                }

                var names = obj.Properties.Names;
                if (!names.Keys.SequenceEqual(new[] { "TEnd", "TStart" }, StringComparer.Ordinal) ||
                    names.Values.Any(type => type != PropertySetValueType.Scalar))
                {
                    return false;
                }

                var animators = obj.Animators.ToArray();
                for (var i = 0; i + 4 <= animators.Length; i++)
                {
                    if (!IsProgressBoundAnimator(animators[i], "TStart", out var tStartProgress) ||
                        !IsTrimBindingAnimator(obj, animators[i + 1], "TrimStart", "Min(my.TStart,my.TEnd)") ||
                        !IsProgressBoundAnimator(animators[i + 2], "TEnd", out var tEndProgress) ||
                        !IsTrimBindingAnimator(obj, animators[i + 3], "TrimEnd", "Max(my.TStart,my.TEnd)") ||
                        tStartProgress != tEndProgress)
                    {
                        continue;
                    }

                    // No other animation may reference the property set, because it would be referenced
                    // through the local that holds it, which is not written.
                    if (animators.Take(i).Concat(animators.Skip(i + 4)).Concat(obj.Properties.Animators)
                        .Any(a => a.Animation.ReferenceParameters.Any(p => p.Value == obj.Properties)))
                    {
                        return false;
                    }

                    index = i;
                    return true;
                }

                return false;
            }

            // True if the animator starts a key frame animation with StartProgressBoundAnimation.
            bool IsProgressBoundAnimator(CompositionObject.Animator animator, string animatedProperty, out ExpressionAnimation? progressExpression)
            {
                progressExpression = null;
                if (animator.AnimatedProperty != animatedProperty ||
                    animator.Animation is not KeyFrameAnimation_ ||
                    animator.Controller is not { IsPaused: true } controller)
                {
                    return false;
                }

                var controllerAnimators = controller.Animators.ToArray();
                if (controllerAnimators.Length != 1 ||
                    controllerAnimators[0].AnimatedProperty != "Progress" ||
                    controllerAnimators[0].Animation is not ExpressionAnimation expression ||
                    !NodeFor(expression).NeedsAFactory)
                {
                    return false;
                }

                progressExpression = expression;
                return true;
            }

            // True if the animator binds a property of the object with BindProperty to the given
            // expression of the object.
            bool IsTrimBindingAnimator(CompositionObject obj, CompositionObject.Animator animator, string animatedProperty, string expression)
                => animator.AnimatedProperty == animatedProperty &&
                   animator.Controller is null &&
                   animator.Animation is ExpressionAnimation animation &&
                   !NodeFor(animation).RequiresStorage &&
                   string.IsNullOrWhiteSpace(animation.Target) &&
                   animation.ReferenceParameters.Count == 1 &&
                   animation.ReferenceParameters.First().Key == "my" &&
                   animation.ReferenceParameters.First().Value == obj &&
                   animation.Expression.ToText() == expression;

            void WriteBindTrimCall(
                CodeBuilder builder,
                CompositionObject obj,
                ObjectData node,
                LocalReference localName,
                CompositionObject.Animator tStart,
                CompositionObject.Animator tEnd)
            {
                EnsureStartProgressBoundAnimationWritten();
                EnsureBindPropertyWritten();
                EnsureBindTrimWritten();

                // The factories are called in the same order as when the animators are started one by one.
                var progressExpressionNode = NodeFor(tStart.Controller!.Animators.Single().Animation);
                var tStartAnimation = CallFactoryFromFor(node, NodeFor(tStart.Animation));
                var progressExpression = CallFactoryFromFor(NodeFor(tStart.Controller), progressExpressionNode);
                var tEndAnimation = CallFactoryFromFor(node, NodeFor(tEnd.Animation));

                // The progress expression is the same object for TStart and TEnd, so it is passed once.
                var tEndProgressExpression = CallFactoryFromFor(NodeFor(tEnd.Controller!), progressExpressionNode);
                Debug.Assert(
                    tEndProgressExpression == progressExpression || tEndProgressExpression == progressExpressionNode.FieldName,
                    "The progress expression of TEnd would be created by a different call");

                var properties = obj.Properties;
                builder.WriteLine(
                    $"BindTrim({Use(localName)}, " +
                    $"{_owner.PropertySetValueInitializer(properties, "TEnd", PropertySetValueType.Scalar)}, " +
                    $"{_owner.PropertySetValueInitializer(properties, "TStart", PropertySetValueType.Scalar)}, " +
                    $"{tStartAnimation}, {progressExpression}, {tEndAnimation});");
            }

            void EnsureBindTrimWritten()
            {
                // Write the method that does what the animators found by TryGetTrimBinding do.
                var method = HelperMethod("BindTrim", "BindTrim");
                if (method.IsEmpty)
                {
                    method.Attribute = _owner.HelperMethodAttribute;
                    using var uses = RecordUsesIn(method.Uses);

                    // The helper calls these helpers.
                    EnsureStartProgressBoundAnimationWritten();
                    EnsureBindPropertyWritten();
                    var h = method.Header;
                    h.WriteLine("void BindTrim(");
                    h.Indent();
                    h.WriteLine($"{ReferenceTypeName("CompositionGeometry")} target,");
                    h.WriteLine($"{_s.TypeFloat32} tEnd,");
                    h.WriteLine($"{_s.TypeFloat32} tStart,");
                    h.WriteLine($"{ReferenceTypeName("CompositionAnimation")} tStartAnimation,");
                    h.WriteLine($"{ReferenceTypeName("ExpressionAnimation")} progressExpression,");
                    h.WriteLine($"{ReferenceTypeName("CompositionAnimation")} tEndAnimation)");
                    h.UnIndent();
                    var b = method.Body;
                    b.OpenScope();
                    b.WriteLine($"{ConstVar} propertySet = {_s.PropertyGet("target", "Properties")};");
                    b.WriteLine($"propertySet{Deref}InsertScalar({String("TEnd")}, tEnd);");
                    b.WriteLine($"propertySet{Deref}InsertScalar({String("TStart")}, tStart);");
                    b.WriteLine($"StartProgressBoundAnimation(target, {String("TStart")}, tStartAnimation, progressExpression);");
                    b.WriteLine($"BindProperty(target, {String("TrimStart")}, {String("Min(my.TStart,my.TEnd)")}, {String("my")}, target);");
                    b.WriteLine($"StartProgressBoundAnimation(target, {String("TEnd")}, tEndAnimation, progressExpression);");
                    b.WriteLine($"BindProperty(target, {String("TrimEnd")}, {String("Max(my.TStart,my.TEnd)")}, {String("my")}, target);");
                    b.CloseScope();
                }
            }

            // Helper method that starts an animation and binds its AnimationController.Progress to an expression.
            void EnsureStartProgressBoundAnimationWritten()
            {
                // Write a static method that starts an animation, then binds the Progress property of its
                // AnimationController for that animation to an expression. This is used to start animations
                // that have their progress bound to the progress of another property.
                var method = HelperMethod("StartProgressBoundAnimation", "StartProgressBoundAnimation");
                if (method.IsEmpty)
                {
                    method.Attribute = _owner.HelperMethodAttribute;
                    using var uses = RecordUsesIn(method.Uses);
                    method.IsStatic = true;
                    var h = method.Header;
                    h.WriteLine("static void StartProgressBoundAnimation(");
                    h.Indent();
                    h.WriteLine($"{ReferenceTypeName("CompositionObject")} target,");
                    h.WriteLine($"{_s.TypeString} animatedPropertyName,");
                    h.WriteLine($"{ReferenceTypeName("CompositionAnimation")} animation,");
                    h.WriteLine($"{ReferenceTypeName("ExpressionAnimation")} controllerProgressExpression)");
                    h.UnIndent();
                    var b = method.Body;
                    b.OpenScope();
                    b.WriteLine($"target{Deref}StartAnimation(animatedPropertyName, animation);");
                    b.WriteLine($"{ConstVar} controller = target{Deref}TryGetAnimationController(animatedPropertyName);");
                    b.WriteLine($"controller{Deref}Pause();");
                    b.WriteLine($"controller{Deref}StartAnimation({String("Progress")}, controllerProgressExpression);");
                    b.CloseScope();
                }
            }

            void EnsureBindPropertyWritten()
            {
                // Write the method that binds an expression to an object using the singleton ExpressionAnimation object.
                var method = HelperMethod("BindProperty", "BindProperty");
                if (method.IsEmpty)
                {
                    // 1 reference parameter version.
                    method.Attribute = _owner.HelperMethodAttribute;
                    using var uses = RecordUsesIn(method.Uses);
                    UseSingletonExpressionAnimation();
                    var h = method.Header;
                    h.WriteLine("void BindProperty(");
                    h.Indent();
                    h.WriteLine($"{ReferenceTypeName("CompositionObject")} target,");
                    h.WriteLine($"{_s.TypeString} animatedPropertyName,");
                    h.WriteLine($"{_s.TypeString} expression,");
                    h.WriteLine($"{_s.TypeString} referenceParameterName,");
                    h.WriteLine($"{ReferenceTypeName("CompositionObject")} referencedObject)");
                    h.UnIndent();
                    var b = method.Body;
                    b.OpenScope();
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}ClearAllParameters();");
                    WriteSetPropertyStatement(b, "Expression", "expression", SingletonExpressionAnimationName);
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}SetReferenceParameter(referenceParameterName, referencedObject);");
                    b.WriteLine($"target{Deref}StartAnimation(animatedPropertyName, {SingletonExpressionAnimationName});");
                    b.CloseScope();
                }
            }

            void EnsureBindProperty2Written()
            {
                // Write the method that binds an expression to an object using the singleton ExpressionAnimation object.
                var method = HelperMethod("BindProperty2", "BindProperty2");
                if (method.IsEmpty)
                {
                    // 2 reference parameter version.
                    method.Attribute = _owner.HelperMethodAttribute;
                    using var uses = RecordUsesIn(method.Uses);
                    UseSingletonExpressionAnimation();
                    var h = method.Header;
                    h.WriteLine("void BindProperty2(");
                    h.Indent();
                    h.WriteLine($"{ReferenceTypeName("CompositionObject")} target,");
                    h.WriteLine($"{_s.TypeString} animatedPropertyName,");
                    h.WriteLine($"{_s.TypeString} expression,");
                    h.WriteLine($"{_s.TypeString} referenceParameterName0,");
                    h.WriteLine($"{ReferenceTypeName("CompositionObject")} referencedObject0,");
                    h.WriteLine($"{_s.TypeString} referenceParameterName1,");
                    h.WriteLine($"{ReferenceTypeName("CompositionObject")} referencedObject1)");
                    h.UnIndent();
                    var b = method.Body;
                    b.OpenScope();
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}ClearAllParameters();");
                    WriteSetPropertyStatement(b, "Expression", "expression", SingletonExpressionAnimationName);
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}SetReferenceParameter(referenceParameterName0, referencedObject0);");
                    b.WriteLine($"{SingletonExpressionAnimationName}{Deref}SetReferenceParameter(referenceParameterName1, referencedObject1);");
                    b.WriteLine($"target{Deref}StartAnimation(animatedPropertyName, {SingletonExpressionAnimationName});");
                    b.CloseScope();
                }
            }

            void EnsureKeyFrameAnimationHelperWritten(CompositionObjectType animationType)
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
                var method = HelperMethod(methodName, methodName);
                if (method.IsEmpty)
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

                    method.Attribute = _owner.HelperMethodAttribute;
                    using var uses = RecordUsesIn(method.Uses);
                    method.Header.WriteLine($"{ReferenceTypeName(animationType.ToString())} {methodName}(float initialProgress, {valueType} initialValue{easingParameter})");
                    var b = method.Body;
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
                var method = HelperMethod("CreateSpriteShape", "CreateSpriteShape");
                if (method.IsEmpty)
                {
                    method.Attribute = _owner.HelperMethodAttribute;
                    using var uses = RecordUsesIn(method.Uses);
                    method.Header.WriteLine($"{ReferenceTypeName("CompositionSpriteShape")} CreateSpriteShape({ReferenceTypeName("CompositionGeometry")} geometry, {_s.TypeMatrix3x2} transformMatrix)");
                    var b = method.Body;
                    b.OpenScope();
                    b.WriteLine($"{ConstVar} result = _c{Deref}CreateSpriteShape(geometry);");
                    WriteSetPropertyStatement(b, "TransformMatrix", "transformMatrix");
                    b.WriteLine("return result;");
                    b.CloseScope();
                }

                // Call the helper and initialize the remaining CompositionShape properties.
                WriteMatrixComment(builder, transformMatrix);
                WriteCreateAssignment(builder, node, $"CreateSpriteShape({CallFactoryFromFor(node, obj.Geometry)}, {Matrix3x2(transformMatrix)});");
                InitializeCompositionObject(builder, obj, node);
                WriteCompositionShapeProperties(builder, obj);
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
                // This is an overload of CreateSpriteShape, so its key is not the same as its name.
                var method = HelperMethod("CreateSpriteShapeWithFillBrush", "CreateSpriteShape");
                if (method.IsEmpty)
                {
                    method.Attribute = _owner.HelperMethodAttribute;
                    using var uses = RecordUsesIn(method.Uses);
                    method.Header.WriteLine($"{ReferenceTypeName("CompositionSpriteShape")} CreateSpriteShape({ReferenceTypeName("CompositionGeometry")} geometry, {_s.TypeMatrix3x2} transformMatrix, {ReferenceTypeName("CompositionBrush")} fillBrush)");
                    var b = method.Body;
                    b.OpenScope();
                    b.WriteLine($"{ConstVar} result = _c{Deref}CreateSpriteShape(geometry);");
                    WriteSetPropertyStatement(b, "TransformMatrix", "transformMatrix");
                    WriteSetPropertyStatement(b, "FillBrush", "fillBrush");
                    b.WriteLine("return result;");
                    b.CloseScope();
                }

                // Call the helper and initialize the remaining CompositionShape properties.
                WriteMatrixComment(builder, obj.TransformMatrix);

                // We need to instantiate geometry first because sometimes it initializes fields
                // that are used in FillBrush, but CreateSpriteShape(GetGeometry(), ..., GetFillBrush()) code
                // will result in evaluating GetFillBrush() first which may cause null dereferencing
                builder.WriteLine($"{ConstVar} geometry = {CallFactoryFromFor(node, obj.Geometry)};");
                WriteCreateAssignment(builder, node, $"CreateSpriteShape(geometry, {Matrix3x2(transformMatrix)}, {CallFactoryFromFor(node, obj.FillBrush)});");
                InitializeCompositionObject(builder, obj, node);
                WriteCompositionShapeProperties(builder, obj);
            }

            // Starts an ExpressionAnimation that uses the shared singleton ExpressionAnimation.
            // This reparameterizes the singleton each time it is called, and therefore avoids the
            // cost of creating a new ExpressionAnimation. However, because it gets reparameterized
            // for each use, it cannot be used if the ExpressionAnimation is shared by multiple nodes.
            void StartSingletonExpressionAnimation(
                    CodeBuilder builder,
                    CompositionObject obj,
                    LocalReference localName,
                    CompositionObject.Animator animator,
                    ObjectData animationNode,
                    ExpressionAnimation animation)
            {
                Debug.Assert(animator.Animation == animation, "Precondition");

                // All of the code that this writes is in the method of the object.
                var local = Use(localName);
                var referenceParameters = animator.Animation.ReferenceParameters.ToArray();
                if (referenceParameters.Length == 1 &&
                    string.IsNullOrWhiteSpace(animation.Target))
                {
                    EnsureBindPropertyWritten();

                    var rp0 = referenceParameters[0];
                    var rp0Name = GetReferenceParameterName(obj, localName, animationNode, rp0);

                    // Special-case where there is exactly one reference parameter. Call a helper.
                    builder.WriteLine(
                        $"BindProperty({local}, " + // target
                        $"{String(animator.AnimatedProperty)}, " + // property on target
                        $"{String(animation.Expression.ToText())}, " + // expression
                        $"{String(rp0.Key)}, " + // reference property name
                        $"{rp0Name});"); // reference object
                }
                else if (referenceParameters.Length == 2 &&
                    string.IsNullOrWhiteSpace(animation.Target))
                {
                    EnsureBindProperty2Written();

                    var rp0 = referenceParameters[0];
                    var rp0Name = GetReferenceParameterName(obj, localName, animationNode, rp0);

                    var rp1 = referenceParameters[1];
                    var rp1Name = GetReferenceParameterName(obj, localName, animationNode, rp1);

                    // Special-case where there are exactly two reference parameters. Call a helper.
                    builder.WriteLine(
                        $"BindProperty2({local}, " + // target
                        $"{String(animator.AnimatedProperty)}, " + // property on target
                        $"{String(animation.Expression.ToText())}, " + // expression
                        $"{String(rp0.Key)}, " + // reference property name
                        $"{rp0Name}, " + // reference object
                        $"{String(rp1.Key)}, " + // reference property name
                        $"{rp1Name});"); // reference object
                }
                else
                {
                    var singleton = UseSingletonExpressionAnimation();
                    builder.WriteLine($"{singleton}{Deref}ClearAllParameters();");
                    builder.WriteLine($"{_s.PropertySet(singleton, "Expression", String(animation.Expression))};");

                    // If there is a Target set it. Note however that the Target isn't used for anything
                    // interesting in this scenario, and there is no way to reset the Target to an
                    // empty string (the Target API disallows empty). In reality, for all our uses
                    // the Target will not be set and it doesn't matter if it was set previously.
                    if (!string.IsNullOrWhiteSpace(animation.Target))
                    {
                        builder.WriteLine($"{singleton}{Deref}Target = {String(animation.Target)};");
                    }

                    foreach (var rp in animation.ReferenceParameters)
                    {
                        var referenceParameterName = GetReferenceParameterName(obj, localName, animationNode, rp);

                        builder.WriteLine($"{singleton}{Deref}SetReferenceParameter({String(rp.Key)}, {referenceParameterName});");
                    }

                    builder.WriteLine($"{local}{Deref}StartAnimation({String(animator.AnimatedProperty)}, {singleton});");
                }
            }

            string GetReferenceParameterName(
                CompositionObject obj,
                LocalReference localName,
                ObjectData animationNode,
                KeyValuePair<string, CompositionObject> referenceParameter)
            {
                if (referenceParameter.Value == obj)
                {
                    return Use(localName);
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
                        _currentUses?.UseField(ThemePropertiesFieldName);
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

                // When the trim of a geometry is bound by the BindTrim helper, the helper initializes the property set.
                if (propertySet.Names.Count > 0 && !TryGetTrimBinding(obj, out _))
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

                // These properties are declared by Visual, which is a base class of the type of the result.
                const string visual = nameof(Visual);

                if (obj.BorderMode.HasValue && obj.BorderMode != CompositionBorderMode.Inherit)
                {
                    WriteSetInheritedPropertyStatement(builder, visual, nameof(CompositionBorderMode), nameof(obj.BorderMode), BorderMode(obj.BorderMode.Value));
                }

                WriteSetInheritedPropertyStatement(builder, visual, _s.TypeVector3, nameof(obj.CenterPoint), obj.CenterPoint, Vector3);
                if (obj.Clip is not null)
                {
                    WriteSetInheritedPropertyStatement(builder, visual, nameof(CompositionClip), nameof(obj.Clip), CallFactoryFromFor(node, obj.Clip));
                }

                WriteSetInheritedPropertyStatement(builder, visual, "bool", nameof(obj.IsVisible), obj.IsVisible, Bool);
                WriteSetInheritedPropertyStatement(builder, visual, _s.TypeVector3, nameof(obj.Offset), obj.Offset, Vector3);
                WriteSetInheritedPropertyStatement(builder, visual, _s.TypeFloat32, nameof(obj.Opacity), obj.Opacity, Float);
                WriteSetInheritedPropertyStatement(builder, visual, _s.TypeFloat32, nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees, Float);
                WriteSetInheritedPropertyStatement(builder, visual, _s.TypeVector3, nameof(obj.RotationAxis), obj.RotationAxis, Vector3);
                WriteSetInheritedPropertyStatement(builder, visual, _s.TypeVector3, nameof(obj.Scale), obj.Scale, Vector3);
                WriteSetInheritedPropertyStatement(builder, visual, _s.TypeVector2, nameof(obj.Size), obj.Size, Vector2);
                WriteSetInheritedPropertyStatement(builder, visual, nameof(Matrix4x4), nameof(obj.TransformMatrix), obj.TransformMatrix, Matrix4x4);
            }

            void InitializeCompositionClip(CodeBuilder builder, CompositionClip obj, ObjectData node)
            {
                InitializeCompositionObject(builder, obj, node);

                // These properties are declared by CompositionClip, which is a base class of the type of the result.
                const string clip = nameof(CompositionClip);
                WriteSetInheritedPropertyStatement(builder, clip, _s.TypeVector2, nameof(obj.CenterPoint), obj.CenterPoint, Vector2);
                WriteSetInheritedPropertyStatement(builder, clip, _s.TypeVector2, nameof(obj.Scale), obj.Scale, Vector2);
            }

            void InitializeCompositionGradientBrush(CodeBuilder builder, CompositionGradientBrush obj, ObjectData node)
            {
                InitializeCompositionObject(builder, obj, node);

                // These properties are declared by CompositionGradientBrush, which is a base class of the type of the result.
                const string gradientBrush = nameof(CompositionGradientBrush);
                WriteSetInheritedPropertyStatement(builder, gradientBrush, _s.TypeVector2, nameof(obj.AnchorPoint), obj.AnchorPoint, Vector2);
                WriteSetInheritedPropertyStatement(builder, gradientBrush, _s.TypeVector2, nameof(obj.CenterPoint), obj.CenterPoint, Vector2);

                if (obj.ColorStops.Count > 0)
                {
                    var colorStopCalls = obj.ColorStops.Select(colorStop => CallFactoryFromFor(node, colorStop)).ToArray();
                    var addColorStopCalls = colorStopCalls.Select(colorStop => _owner.GetAddColorStopCall("result", colorStop)).ToArray();
                    if (addColorStopCalls.All(call => call is not null))
                    {
                        foreach (var call in addColorStopCalls)
                        {
                            builder.WriteLine($"{call};");
                        }
                    }
                    else
                    {
                        builder.WriteLine($"{ConstVar} colorStops = {_s.PropertyGet("result", "ColorStops")};");
                        foreach (var colorStop in colorStopCalls)
                        {
                            builder.WriteLine($"colorStops{Deref}{IListAdd}({colorStop});");
                        }
                    }
                }

                WriteSetInheritedPropertyStatement(builder, gradientBrush, nameof(CompositionGradientExtendMode), nameof(obj.ExtendMode), obj.ExtendMode, ExtendMode);
                WriteSetInheritedPropertyStatement(builder, gradientBrush, nameof(CompositionColorSpace), nameof(obj.InterpolationSpace), obj.InterpolationSpace, ColorSpace);
                WriteSetInheritedPropertyStatement(builder, gradientBrush, nameof(CompositionMappingMode), nameof(obj.MappingMode), obj.MappingMode, MappingMode);
                WriteSetInheritedPropertyStatement(builder, gradientBrush, _s.TypeVector2, nameof(obj.Offset), obj.Offset, Vector2);
                WriteSetInheritedPropertyStatement(builder, gradientBrush, _s.TypeFloat32, nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees, Float);
                WriteSetInheritedPropertyStatement(builder, gradientBrush, _s.TypeVector2, nameof(obj.Scale), obj.Scale, Vector2);
                WriteSetInheritedPropertyStatement(builder, gradientBrush, _s.TypeMatrix3x2, nameof(obj.TransformMatrix), obj.TransformMatrix, Matrix3x2);
            }

            void InitializeCompositionShape(CodeBuilder builder, CompositionShape obj, ObjectData node)
            {
                InitializeCompositionObject(builder, obj, node);
                WriteCompositionShapeProperties(builder, obj);
                WriteSetInheritedPropertyStatement(builder, nameof(CompositionShape), _s.TypeMatrix3x2, nameof(obj.TransformMatrix), obj.TransformMatrix, Matrix3x2);
            }

            // Writes the properties of a CompositionShape, except its TransformMatrix.
            void WriteCompositionShapeProperties(CodeBuilder builder, CompositionShape obj)
            {
                // These properties are declared by CompositionShape, which is a base class of the type of the result.
                const string shape = nameof(CompositionShape);
                WriteSetInheritedPropertyStatement(builder, shape, _s.TypeVector2, nameof(obj.CenterPoint), obj.CenterPoint, Vector2);
                WriteSetInheritedPropertyStatement(builder, shape, _s.TypeVector2, nameof(obj.Offset), obj.Offset, Vector2);
                WriteSetInheritedPropertyStatement(builder, shape, _s.TypeFloat32, nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees, Float);
                WriteSetInheritedPropertyStatement(builder, shape, _s.TypeVector2, nameof(obj.Scale), obj.Scale, Vector2);
            }

            void InitializeContainerVisual(CodeBuilder builder, ContainerVisual obj, ObjectData node)
            {
                InitializeVisual(builder, obj, node);

                // Get the code for the children in order, because the first use of a child may create it.
                var childCalls = obj.Children.Select(child => CallFactoryFromFor(node, child)).ToArray();
                var insertChildCalls = childCalls.Select(child => _owner.GetInsertChildAtTopCall("result", child)).ToArray();
                if (insertChildCalls.Length > 0 && insertChildCalls.All(call => call is not null))
                {
                    // Insert each child with a call.
                    for (var i = 0; i < obj.Children.Count; i++)
                    {
                        WriteShortDescriptionComment(builder, obj.Children[i]);
                        builder.WriteLine($"{insertChildCalls[i]};");
                    }

                    return;
                }

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
                            builder.WriteLine($"{_s.PropertyGet("result", "Children")}{Deref}InsertAtTop({childCalls[0]});");
                            break;
                        }

                    default:
                        {
                            // Multiple children requires the use of a local.
                            builder.WriteLine($"{ConstVar} children = {_s.PropertyGet("result", nameof(obj.Children))};");
                            for (var i = 0; i < obj.Children.Count; i++)
                            {
                                WriteShortDescriptionComment(builder, obj.Children[i]);
                                builder.WriteLine($"children{Deref}InsertAtTop({childCalls[i]});");
                            }

                            break;
                        }
                }
            }

            void InitializeCompositionGeometry(CodeBuilder builder, CompositionGeometry obj, ObjectData node)
            {
                InitializeCompositionObject(builder, obj, node);

                // These properties are declared by CompositionGeometry, which is a base class of the type of the result.
                const string geometry = nameof(CompositionGeometry);
                WriteSetInheritedPropertyStatement(builder, geometry, _s.TypeFloat32, nameof(obj.TrimEnd), obj.TrimEnd, Float);
                WriteSetInheritedPropertyStatement(builder, geometry, _s.TypeFloat32, nameof(obj.TrimOffset), obj.TrimOffset, Float);
                WriteSetInheritedPropertyStatement(builder, geometry, _s.TypeFloat32, nameof(obj.TrimStart), obj.TrimStart, Float);
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
                WriteSetInheritedPropertyStatement(builder, "KeyFrameAnimation", "TimeSpan", nameof(animation.Duration), TimeSpan(animation.Duration));

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
                    EnsureKeyFrameAnimationHelperWritten(obj.Type);

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

                WriteInsertKeyFrames(builder, node, obj, keyFrames, Bool, v => new[] { v ? 1F : 0F });

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
                    EnsureKeyFrameAnimationHelperWritten(obj.Type);

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

                WriteInsertKeyFrames(builder, node, obj, keyFrames, Color, v => new float[] { v.A, v.R, v.G, v.B }, v => v.Name);

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
                    EnsureKeyFrameAnimationHelperWritten(obj.Type);

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

                WriteInsertKeyFrames(builder, node, obj, keyFrames, Vector2, v => new[] { v.X, v.Y });

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
                    EnsureKeyFrameAnimationHelperWritten(obj.Type);

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

                WriteInsertKeyFrames(builder, node, obj, keyFrames, Vector3, v => new[] { v.X, v.Y, v.Z });

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
                    EnsureKeyFrameAnimationHelperWritten(obj.Type);

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

                WriteInsertKeyFrames(builder, node, obj, keyFrames, Vector4, v => new[] { v.X, v.Y, v.Z, v.W });

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
                    EnsureKeyFrameAnimationHelperWritten(obj.Type);

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
                    EnsureKeyFrameAnimationHelperWritten(obj.Type);

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

                WriteInsertKeyFrames(builder, node, obj, keyFrames, Float, v => new[] { v });

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
                    Wmd.LoadedImageSurface _ => UseField(surfaceNode!),
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

            // Records the uses of the code that is generated into the given uses until it is disposed.
            readonly struct UsesScope : IDisposable
            {
                readonly AnimatedVisualGenerator _generator;
                readonly AnimatedVisualMemberUses? _outerUses;

                internal UsesScope(AnimatedVisualGenerator generator, AnimatedVisualMemberUses? uses)
                {
                    _generator = generator;
                    _outerUses = generator._currentUses;
                    generator._currentUses = uses;
                }

                public void Dispose() => _generator._currentUses = _outerUses;
            }
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

        // The code that refers to the object whose animations are started. This is its local variable, or its
        // field if the animations are started by a method other than the one that creates the object, as is
        // the case when IAnimatedVisual2 is implemented. The field is recorded as used where the code is written.
        sealed class LocalReference
        {
            internal LocalReference(string code, ObjectData? field = null)
            {
                Code = code;
                Field = field;
            }

            internal static LocalReference Result { get; } = new LocalReference("result");

            internal static LocalReference Controller { get; } = new LocalReference("controller");

            internal string Code { get; }

            // The node whose field the code refers to, or null if the code does not refer to a field.
            internal ObjectData? Field { get; }

            // Gets the reference to the Properties of the object.
            internal LocalReference Properties(Stringifier s) => new LocalReference(s.PropertyGet(Code, "Properties"), Field);
        }

        // A node in the object graph, annotated with extra stuff to assist in code generation.
        sealed class ObjectData : Graph.Node<ObjectData>
        {
            Func<string>? _overriddenFactoryCall;
            Dictionary<ObjectData, (string Code, AnimatedVisualMemberUses Uses)>? _callFactoryFromForCache;

            // The code that calls the factories of other nodes from this node, with what the code uses.
            public Dictionary<ObjectData, (string Code, AnimatedVisualMemberUses Uses)> CallFactoryFromForCache
            {
                get
                {
                    // Lazy initialization because not all nodes need the cache.
                    if (_callFactoryFromForCache is null)
                    {
                        _callFactoryFromForCache = new Dictionary<ObjectData, (string Code, AnimatedVisualMemberUses Uses)>();
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
