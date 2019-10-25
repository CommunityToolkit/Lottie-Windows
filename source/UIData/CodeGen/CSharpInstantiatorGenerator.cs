// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData;
using Mgce = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIData
    public
#endif
    sealed class CSharpInstantiatorGenerator : InstantiatorGeneratorBase
    {
        readonly Stringifier _s;

        CSharpInstantiatorGenerator(
            string className,
            Vector2 size,
            IReadOnlyList<(CompositionObject graphRoot, uint requiredUapVersion)> graphs,
            TimeSpan duration,
            bool setCommentProperties,
            bool disableFieldOptimization,
            Stringifier stringifier)
            : base(
                  className: className,
                  compositionDeclaredSize: size,
                  graphs: graphs,
                  duration: duration,
                  setCommentProperties: setCommentProperties,
                  disableFieldOptimization: disableFieldOptimization,
                  stringifier: stringifier)
        {
            _s = stringifier;
        }

        /// <summary>
        /// Returns the C# code for a factory that will instantiate the given <see cref="Visual"/> as a
        /// Windows.UI.Composition Visual.
        /// </summary>
        /// <returns>A tuple containing the C# code and list of referenced asset files.</returns>
        public static (string csText, IEnumerable<Uri> assetList) CreateFactoryCode(
            string className,
            IReadOnlyList<(CompositionObject graphRoot, uint requiredUapVersion)> graphs,
            float width,
            float height,
            TimeSpan duration,
            bool disableFieldOptimization)
        {
            var generator = new CSharpInstantiatorGenerator(
                                className: className,
                                size: new Vector2(width, height),
                                graphs: graphs,
                                duration: duration,
                                setCommentProperties: false,
                                disableFieldOptimization: disableFieldOptimization,
                                stringifier: new Stringifier());

            var csText = generator.GenerateCode();

            var assetList = generator.GetAssetsList();

            return (csText, assetList);
        }

        /// <inheritdoc/>
        // Called by the base class to write the start of the file (i.e. everything up to the body of the Instantiator class).
        protected override void WriteFileStart(CodeBuilder builder, IAnimatedVisualSourceInfo info)
        {
            // A sorted set to hold the namespaces that the generated code will use. The set is maintained in sorted order.
            var namepaces = new SortedSet<string>();

            if (info.UsesCanvas)
            {
                namepaces.Add("Microsoft.Graphics.Canvas");
            }

            if (info.UsesCanvasEffects)
            {
                namepaces.Add("Microsoft.Graphics.Canvas.Effects");
            }

            if (info.UsesCanvasGeometry)
            {
                namepaces.Add("Microsoft.Graphics.Canvas.Geometry");
            }

            if (info.UsesNamespaceWindowsUIXamlMedia)
            {
                namepaces.Add("Windows.UI.Xaml.Media");
                namepaces.Add("System.Runtime.InteropServices.WindowsRuntime");
                namepaces.Add("Windows.Foundation");
                namepaces.Add("System.ComponentModel");
            }

            if (info.UsesStreams)
            {
                namepaces.Add("System.IO");
            }

            namepaces.Add("Microsoft.UI.Xaml.Controls");
            namepaces.Add("System");
            namepaces.Add("System.Numerics");
            namepaces.Add("Windows.UI");
            namepaces.Add("Windows.UI.Composition");

            // Write out each namespace using.
            foreach (var n in namepaces)
            {
                builder.WriteLine($"using {n};");
            }

            builder.WriteLine();

            // If the composition has LoadedImageSurface, write a class that implements the IDynamicAnimatedVisualSource interface.
            // Otherwise, implement the IAnimatedVisualSource interface.
            if (info.HasLoadedImageSurface)
            {
                WriteIDynamicAnimatedVisualSource(builder, info);
            }
            else
            {
                WriteIAnimatedVisualSource(builder, info);
            }
        }

        /// <summary>
        /// Write a class that implements the IAnimatedVisualSource interface.
        /// </summary>
        void WriteIAnimatedVisualSource(CodeBuilder builder, IAnimatedVisualSourceInfo info)
        {
            builder.WriteLine("namespace AnimatedVisuals");
            builder.OpenScope();
            builder.WriteLine($"sealed class {info.ClassName} : IAnimatedVisualSource");
            builder.OpenScope();

            // Generate the method that creates an instance of the animated visual.
            builder.WriteLine("public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)");
            builder.OpenScope();
            builder.WriteLine("diagnostics = null;");
            builder.WriteLine();

            // Check the runtime version and instantiate the highest compatible IAnimatedVisual class.
            var animatedVisualInfos = info.AnimatedVisualInfos.OrderByDescending(avi => avi.RequiredUapVersion).ToArray();
            for (var i = 0; i < animatedVisualInfos.Length; i++)
            {
                var current = animatedVisualInfos[i];
                builder.WriteLine($"if ({current.ClassName}.IsRuntimeCompatible())");
                builder.OpenScope();
                builder.WriteLine($"return new {current.ClassName}(compositor);");
                builder.CloseScope();
                builder.WriteLine();
            }

            builder.WriteLine("return null;");
            builder.CloseScope();
            builder.WriteLine();
        }

        /// <summary>
        /// Write a class that implements the IDynamicAnimatedVisualSource interface.
        /// </summary>
        void WriteIDynamicAnimatedVisualSource(CodeBuilder builder, IAnimatedVisualSourceInfo info)
        {
            builder.WriteLine("namespace AnimatedVisuals");
            builder.OpenScope();
            builder.WriteLine($"sealed class {info.ClassName} : IDynamicAnimatedVisualSource, INotifyPropertyChanged");
            builder.OpenScope();

            // Declare variables.
            builder.WriteLine($"{_s.Const(_s.Int32TypeName)} c_loadedImageSurfaceCount = {info.LoadedImageSurfaceNodes.Count};");
            builder.WriteLine($"{_s.Int32TypeName} _loadCompleteEventCount;");
            builder.WriteLine("bool _isAnimatedVisualSourceDynamic = true;");
            builder.WriteLine("bool _isTryCreateAnimatedVisualCalled;");
            builder.WriteLine("bool _isImageLoadingStarted;");
            builder.WriteLine("EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>> _animatedVisualInvalidatedEventTokenTable;");

            // Declare the variables to hold the LoadedImageSurfaces.
            foreach (var n in info.LoadedImageSurfaceNodes)
            {
                builder.WriteLine($"{_s.ReferenceTypeName(n.TypeName)} {n.FieldName};");
            }

            builder.WriteLine();

            // Implement the INotifyPropertyChanged.PropertyChanged event.
            builder.WriteSummaryComment("This implementation of the INotifyPropertyChanged.PropertyChanged event is specific to C# and does not work on WinRT.");
            builder.WriteLine("public event PropertyChangedEventHandler PropertyChanged;");
            builder.WriteLine();

            // Implement the AnimatedVisualInvalidated event.
            WriteAnimatedVisualInvalidatedEvent(builder);

            // Define properties.
            builder.WriteSummaryComment("If this property is set to true, <see cref=\"TryCreateAnimatedVisual\"/> will return null until all images have loaded. When all images have loaded, <see cref=\"TryCreateAnimatedVisual\"/> will return the AnimatedVisual. To use, set it when declaring the AnimatedVisualSource. Once <see cref=\"TryCreateAnimatedVisual\"/> is called, changes made to this property will be ignored. Default value is true.");
            builder.WriteLine("public bool IsAnimatedVisualSourceDynamic");
            builder.OpenScope();
            builder.WriteLine("get { return _isAnimatedVisualSourceDynamic; }");
            builder.WriteLine("set");
            builder.OpenScope();
            builder.WriteLine("if (!_isTryCreateAnimatedVisualCalled && _isAnimatedVisualSourceDynamic != value)");
            builder.OpenScope();
            builder.WriteLine("_isAnimatedVisualSourceDynamic = value;");
            builder.WriteLine("NotifyPropertyChanged(nameof(IsAnimatedVisualSourceDynamic));");
            builder.CloseScope();
            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();

            builder.WriteSummaryComment("Returns true if all images have loaded. To see if the images succeeded to load, see <see cref=\"ImageSuccessfulLoadingProgress\"/>.");
            builder.WriteLine("public bool IsImageLoadingCompleted { get; private set; }");
            builder.WriteLine();

            builder.WriteSummaryComment("Represents the progress of the image loading. Returns value between 0 and 1. 0 means none of the images finished loading. 1 means all images finished loading.");
            builder.WriteLine("public double ImageSuccessfulLoadingProgress { get; private set; }");
            builder.WriteLine();

            // Generate the method that creates an instance of the animated visual.
            builder.WriteLine("public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)");
            builder.OpenScope();
            builder.WriteLine("_isTryCreateAnimatedVisualCalled = true;");
            builder.WriteLine("diagnostics = null;");
            builder.WriteLine();

            // Check whether the runtime will support the lowest UAP version required.
            var animatedVisualInfos = info.AnimatedVisualInfos.OrderByDescending(avi => avi.RequiredUapVersion).ToArray();
            builder.WriteLine($"if (!{animatedVisualInfos[animatedVisualInfos.Length - 1].ClassName}.IsRuntimeCompatible())");
            builder.OpenScope();
            builder.WriteLine("return null;");
            builder.CloseScope();
            builder.WriteLine();
            builder.WriteLine("EnsureImageLoadingStarted();");
            builder.WriteLine();
            builder.WriteLine("if (_isAnimatedVisualSourceDynamic && _loadCompleteEventCount != c_loadedImageSurfaceCount)");
            builder.OpenScope();
            builder.WriteLine("return null;");
            builder.CloseScope();
            builder.WriteLine();

            // Check the runtime version and instantiate the highest compatible IAnimatedVisual class.
            for (var i = 0; i < animatedVisualInfos.Length; i++)
            {
                var current = animatedVisualInfos[i];
                var versionTestRequired = i < animatedVisualInfos.Length - 1;

                if (i > 0)
                {
                    builder.WriteLine();
                }

                if (versionTestRequired)
                {
                    builder.WriteLine($"if ({current.ClassName}.IsRuntimeCompatible())");
                    builder.OpenScope();
                }

                builder.WriteLine("return");
                builder.Indent();
                builder.WriteLine($"new {current.ClassName}(");
                builder.Indent();
                builder.WriteLine("compositor,");
                builder.WriteCommaSeparatedLines(info.LoadedImageSurfaceNodes.Select(n => n.FieldName));
                builder.WriteLine(");");
                builder.UnIndent();
                builder.UnIndent();
                if (versionTestRequired)
                {
                    builder.CloseScope();
                }
            }

            builder.CloseScope();
            builder.WriteLine();

            // Generate the method that load all the LoadedImageSurfaces.
            WriteEnsureImageLoadingStarted(builder, info);

            // Generate the method that handle the LoadCompleted event of the LoadedImageSurface objects.
            WriteHandleLoadCompleted(builder);

            // Generate the method that raise the PropertyChanged event.
            builder.WriteLine("void NotifyPropertyChanged(string name)");
            builder.OpenScope();
            builder.WriteLine("PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));");
            builder.CloseScope();
            builder.WriteLine();

            // Generate the method that get or create the EventRegistrationTokenTable.
            builder.WriteLine("EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>> GetAnimatedVisualInvalidatedEventRegistrationTokenTable()");
            builder.OpenScope();
            builder.WriteLine("return EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>>.GetOrCreateEventRegistrationTokenTable(ref _animatedVisualInvalidatedEventTokenTable);");
            builder.CloseScope();
            builder.WriteLine();
        }

        /// <inheritdoc/>
        protected override void WriteAnimatedVisualStart(CodeBuilder builder, IAnimatedVisualInfo info)
        {
            // Start the instantiator class.
            builder.WriteLine($"sealed class {info.ClassName} : IAnimatedVisual");
            builder.OpenScope();
        }

        /// <inheritdoc/>
        // Called by the base class to write the end of the AnimatedVisual class.
        protected override void WriteAnimatedVisualEnd(
            CodeBuilder builder,
            IAnimatedVisualInfo info)
        {
            // Write the constructor for the AnimatedVisual class.
            if (info.HasLoadedImageSurface)
            {
                builder.WriteLine($"internal {info.ClassName}(");
                builder.Indent();

                builder.WriteLine("Compositor compositor,");

                // Define the image surface parameters of the AnimatedVisual() constructor.
                builder.WriteCommaSeparatedLines(info.LoadedImageSurfaceNodes.Select(n => $"{_s.ReferenceTypeName(n.TypeName)} {_s.CamelCase(n.Name)}"));
                builder.WriteLine(")");
                builder.UnIndent();
                builder.OpenScope();

                builder.WriteLine("_c = compositor;");
                builder.WriteLine($"{info.AnimatedVisualSourceInfo.ReusableExpressionAnimationFieldName} = compositor.CreateExpressionAnimation();");

                // Initialize the private image surface variables with the input parameters of the constructor.
                var loadedImageSurfaceNodes = info.LoadedImageSurfaceNodes;
                foreach (var n in loadedImageSurfaceNodes)
                {
                    builder.WriteLine($"{n.FieldName} = {_s.CamelCase(n.Name)};");
                }
            }
            else
            {
                builder.WriteLine($"internal {info.ClassName}(Compositor compositor)");
                builder.OpenScope();
                builder.WriteLine("_c = compositor;");
                builder.WriteLine($"{info.AnimatedVisualSourceInfo.ReusableExpressionAnimationFieldName} = compositor.CreateExpressionAnimation();");
            }

            builder.WriteLine("Root();");
            builder.CloseScope();
            builder.WriteLine();

            // Write the IAnimatedVisual implementation.
            builder.WriteLine("Visual IAnimatedVisual.RootVisual => _root;");
            builder.WriteLine($"TimeSpan IAnimatedVisual.Duration => TimeSpan.FromTicks({info.AnimatedVisualSourceInfo.DurationTicksFieldName});");
            builder.WriteLine($"Vector2 IAnimatedVisual.Size => {Vector2(info.AnimatedVisualSourceInfo.CompositionDeclaredSize)};");
            builder.WriteLine("void IDisposable.Dispose() => _root?.Dispose();");
            builder.WriteLine();

            // Write the IsRuntimeCompatible static method.
            builder.WriteLine("internal static bool IsRuntimeCompatible()");
            builder.OpenScope();
            builder.WriteLine($"return Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent(\"Windows.Foundation.UniversalApiContract\", {info.RequiredUapVersion});");
            builder.CloseScope();

            // Close the scope for the instantiator class.
            builder.CloseScope();
        }

        /// <inheritdoc/>
        // Called by the base class to write the end of the file (i.e. everything after the body of the AnimatedVisual class).
        protected override void WriteFileEnd(
            CodeBuilder builder,
            IAnimatedVisualSourceInfo info)
        {
            // Close the scope for the IAnimatedVisualSource class.
            builder.CloseScope();

            // Close the scope for the namespace
            builder.CloseScope();
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryCombinationFactory(CodeBuilder builder, CanvasGeometry.Combination obj, string typeName, string fieldName)
        {
            builder.WriteLine($"var result = {FieldAssignment(fieldName)}{CallFactoryFor(obj.A)}.");
            builder.Indent();
            builder.WriteLine($"CombineWith({CallFactoryFor(obj.B)},");
            if (obj.Matrix.IsIdentity)
            {
                builder.WriteLine("Matrix3x2.Identity,");
            }
            else
            {
                builder.WriteLine($"{_s.Matrix3x2(obj.Matrix)},");
            }

            builder.WriteLine($"{_s.CanvasGeometryCombine(obj.CombineMode)});");
            builder.UnIndent();
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryEllipseFactory(CodeBuilder builder, CanvasGeometry.Ellipse obj, string typeName, string fieldName)
        {
            builder.WriteLine($"var result = {FieldAssignment(fieldName)}CanvasGeometry.CreateEllipse(");
            builder.Indent();
            builder.WriteLine($"null,");
            builder.WriteLine($"{Float(obj.X)}, {Float(obj.Y)}, {Float(obj.RadiusX)}, {Float(obj.RadiusY)});");
            builder.UnIndent();
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryGroupFactory(CodeBuilder builder, CanvasGeometry.Group obj, string typeName, string fieldName)
        {
            builder.WriteLine($"var result = {FieldAssignment(fieldName)}CanvasGeometry.CreateGroup(");
            builder.Indent();
            builder.WriteLine($"null,");
            builder.WriteLine($"new CanvasGeometry[] {{ {string.Join(", ", obj.Geometries.Select(g => CallFactoryFor(g))) } }},");
            builder.WriteLine($"{_s.FilledRegionDetermination(obj.FilledRegionDetermination)});");
            builder.UnIndent();
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryPathFactory(CodeBuilder builder, CanvasGeometry.Path obj, string typeName, string fieldName)
        {
            builder.WriteLine($"{typeName} result;");
            builder.WriteLine($"using (var builder = new CanvasPathBuilder(null))");
            builder.OpenScope();
            if (obj.FilledRegionDetermination != CanvasFilledRegionDetermination.Alternate)
            {
                builder.WriteLine($"builder.SetFilledRegionDetermination({_s.FilledRegionDetermination(obj.FilledRegionDetermination)});");
            }

            foreach (var command in obj.Commands)
            {
                switch (command.Type)
                {
                    case CanvasPathBuilder.CommandType.BeginFigure:
                        builder.WriteLine($"builder.BeginFigure({Vector2(((CanvasPathBuilder.Command.BeginFigure)command).StartPoint)});");
                        break;
                    case CanvasPathBuilder.CommandType.EndFigure:
                        builder.WriteLine($"builder.EndFigure({_s.CanvasFigureLoop(((CanvasPathBuilder.Command.EndFigure)command).FigureLoop)});");
                        break;
                    case CanvasPathBuilder.CommandType.AddLine:
                        builder.WriteLine($"builder.AddLine({Vector2(((CanvasPathBuilder.Command.AddLine)command).EndPoint)});");
                        break;
                    case CanvasPathBuilder.CommandType.AddCubicBezier:
                        var cb = (CanvasPathBuilder.Command.AddCubicBezier)command;
                        builder.WriteLine($"builder.AddCubicBezier({Vector2(cb.ControlPoint1)}, {Vector2(cb.ControlPoint2)}, {Vector2(cb.EndPoint)});");
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            builder.WriteLine($"result = {FieldAssignment(fieldName)}CanvasGeometry.CreatePath(builder);");
            builder.CloseScope();
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryRoundedRectangleFactory(CodeBuilder builder, CanvasGeometry.RoundedRectangle obj, string typeName, string fieldName)
        {
            builder.WriteLine($"var result = {FieldAssignment(fieldName)}CanvasGeometry.CreateRoundedRectangle(");
            builder.Indent();
            builder.WriteLine("null,");
            builder.WriteLine($"{Float(obj.X)},");
            builder.WriteLine($"{Float(obj.Y)},");
            builder.WriteLine($"{Float(obj.W)},");
            builder.WriteLine($"{Float(obj.H)},");
            builder.WriteLine($"{Float(obj.RadiusX)},");
            builder.WriteLine($"{Float(obj.RadiusY)};");
            builder.UnIndent();
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryTransformedGeometryFactory(CodeBuilder builder, CanvasGeometry.TransformedGeometry obj, string typeName, string fieldName)
        {
            builder.WriteLine($"var result = {FieldAssignment(fieldName)}{CallFactoryFor(obj.SourceGeometry)}.Transform({_s.Matrix3x2(obj.TransformMatrix)});");
        }

        void WriteAnimatedVisualInvalidatedEvent(CodeBuilder builder)
        {
            builder.WriteLine("public event TypedEventHandler<IDynamicAnimatedVisualSource, object> AnimatedVisualInvalidated");
            builder.OpenScope();
            builder.WriteLine("add");
            builder.OpenScope();
            builder.WriteLine("return GetAnimatedVisualInvalidatedEventRegistrationTokenTable().AddEventHandler(value);");
            builder.CloseScope();
            builder.WriteLine("remove");
            builder.OpenScope();
            builder.WriteLine("GetAnimatedVisualInvalidatedEventRegistrationTokenTable().RemoveEventHandler(value);");
            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();
        }

        void WriteEnsureImageLoadingStarted(CodeBuilder builder, IAnimatedVisualSourceInfo info)
        {
            builder.WriteLine("void EnsureImageLoadingStarted()");
            builder.OpenScope();
            builder.WriteLine("if (!_isImageLoadingStarted)");
            builder.OpenScope();
            builder.WriteLine("var eventHandler = new TypedEventHandler<LoadedImageSurface, LoadedImageSourceLoadCompletedEventArgs>(HandleLoadCompleted);");

            foreach (var n in info.LoadedImageSurfaceNodes)
            {
                switch (n.LoadedImageSurfaceType)
                {
                    case LoadedImageSurface.LoadedImageSurfaceType.FromStream:
                        builder.WriteLine($"{n.FieldName} = LoadedImageSurface.StartLoadFromStream({n.BytesFieldName}.AsBuffer().AsStream().AsRandomAccessStream());");
                        break;
                    case LoadedImageSurface.LoadedImageSurfaceType.FromUri:
                        builder.WriteLine($"{n.FieldName} = LoadedImageSurface.StartLoadFromUri(new Uri(\"{n.ImageUri}\"));");
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                builder.WriteLine($"{n.FieldName}.LoadCompleted += eventHandler;");
            }

            builder.WriteLine("_isImageLoadingStarted = true;");
            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();
        }

        void WriteHandleLoadCompleted(CodeBuilder builder)
        {
            builder.WriteLine("void HandleLoadCompleted(LoadedImageSurface sender, LoadedImageSourceLoadCompletedEventArgs e)");
            builder.OpenScope();
            builder.WriteLine("_loadCompleteEventCount++;");
            builder.WriteLine("sender.LoadCompleted -= HandleLoadCompleted;");
            builder.WriteLine();
            builder.WriteLine("if (e.Status == LoadedImageSourceLoadStatus.Success)");
            builder.OpenScope();
            builder.WriteLine("if (_isAnimatedVisualSourceDynamic && _loadCompleteEventCount == c_loadedImageSurfaceCount)");
            builder.OpenScope();
            builder.WriteLine("_animatedVisualInvalidatedEventTokenTable?.InvocationList?.Invoke(this, null);");
            builder.CloseScope();
            builder.WriteLine("ImageSuccessfulLoadingProgress = (double)_loadCompleteEventCount / c_loadedImageSurfaceCount;");
            builder.WriteLine("NotifyPropertyChanged(nameof(ImageSuccessfulLoadingProgress));");
            builder.CloseScope();
            builder.WriteLine();
            builder.WriteLine("if (_loadCompleteEventCount == c_loadedImageSurfaceCount)");
            builder.OpenScope();
            builder.WriteLine("IsImageLoadingCompleted = true;");
            builder.WriteLine("NotifyPropertyChanged(nameof(IsImageLoadingCompleted));");
            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();
        }

        /// <inheritdoc/>
        protected override string WriteCompositeEffectFactory(CodeBuilder builder, Mgce.CompositeEffect compositeEffect)
        {
            var compositeEffectString = "compositeEffect";
            builder.WriteLine($"var {compositeEffectString} = new CompositeEffect();");
            builder.WriteLine($"{compositeEffectString}.Mode = {_s.CanvasCompositeMode(compositeEffect.Mode)};");
            foreach (var source in compositeEffect.Sources)
            {
                builder.WriteLine($"{compositeEffectString}.Sources.Add(new CompositionEffectSourceParameter({String(source.Name)}));");
            }

            return compositeEffectString;
        }

        static string FieldAssignment(string fieldName) => fieldName != null ? $"{fieldName} = " : string.Empty;

        string Float(float value) => _s.Float(value);

        string Vector2(Vector2 value) => _s.Vector2(value);

        string String(string value) => _s.String(value);
    }
}
