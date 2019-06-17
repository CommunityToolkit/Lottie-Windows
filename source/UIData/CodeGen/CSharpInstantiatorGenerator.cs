// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData;
using Mgcg = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIData
    public
#endif
    sealed class CSharpInstantiatorGenerator : InstantiatorGeneratorBase
    {
        readonly CSharpStringifier _stringifier;

        CSharpInstantiatorGenerator(
            string className,
            CompositionObject graphRoot,
            TimeSpan duration,
            bool setCommentProperties,
            bool disableFieldOptimization,
            CSharpStringifier stringifier)
            : base(
                  className: className,
                  graphRoot: graphRoot,
                  duration: duration,
                  setCommentProperties: setCommentProperties,
                  disableFieldOptimization: disableFieldOptimization,
                  stringifier: stringifier)
        {
            _stringifier = stringifier;
        }

        /// <summary>
        /// Returns the C# code for a factory that will instantiate the given <see cref="Visual"/> as a
        /// Windows.UI.Composition Visual.
        /// </summary>
        /// <returns>A tuple containing the C# code and list of referenced asset files.</returns>
        public static (string csText, IEnumerable<Uri> assetList) CreateFactoryCode(
            string className,
            Visual rootVisual,
            float width,
            float height,
            TimeSpan duration,
            bool disableFieldOptimization)
        {
            var generator = new CSharpInstantiatorGenerator(
                                className: className,
                                graphRoot: rootVisual,
                                duration: duration,
                                setCommentProperties: false,
                                disableFieldOptimization: disableFieldOptimization,
                                stringifier: new CSharpStringifier());

            var csText = generator.GenerateCode(className, width, height);

            var assetList = generator.GetAssetsList();

            return (csText, assetList);
        }

        /// <inheritdoc/>
        // Called by the base class to write the start of the file (i.e. everything up to the body of the Instantiator class).
        protected override void WriteFileStart(CodeBuilder builder, CodeGenInfo info)
        {
            // A list to hold the namespaces that the generated code will use. The list will be ordered and written out after all namespaces are added.
            List<string> namepaceList = new List<string>();

            if (info.UsesCanvas)
            {
                namepaceList.Add("Microsoft.Graphics.Canvas");
            }

            if (info.UsesCanvasEffects)
            {
                namepaceList.Add("Microsoft.Graphics.Canvas.Effects");
            }

            if (info.UsesCanvasGeometry)
            {
                namepaceList.Add("Microsoft.Graphics.Canvas.Geometry");
            }

            if (info.UsesNamespaceWindowsUIXamlMedia)
            {
                namepaceList.Add("Windows.UI.Xaml.Media");
                namepaceList.Add("System.Runtime.InteropServices.WindowsRuntime");
                namepaceList.Add("Windows.Foundation");
                namepaceList.Add("System.ComponentModel");
            }

            if (info.UsesStreams)
            {
                namepaceList.Add("System.IO");
            }

            namepaceList.Add("Microsoft.UI.Xaml.Controls");
            namepaceList.Add("System");
            namepaceList.Add("System.Numerics");
            namepaceList.Add("Windows.UI");
            namepaceList.Add("Windows.UI.Composition");

            // Order the list alphabetically and write each namespace using out.
            var orderList = namepaceList.OrderBy(n => n).ToArray();
            foreach (var n in orderList)
            {
                builder.WriteLine($"using {n.ToString()};");
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
        void WriteIAnimatedVisualSource(CodeBuilder builder, CodeGenInfo info)
        {
            builder.WriteLine("namespace AnimatedVisuals");
            builder.OpenScope();
            builder.WriteLine($"sealed class {info.ClassName} : IAnimatedVisualSource");
            builder.OpenScope();

            // Generate the method that creates an instance of the animated visual.
            builder.WriteLine("public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)");
            builder.OpenScope();
            builder.WriteLine("diagnostics = null;");
            builder.WriteLine("if (!IsRuntimeCompatible())");
            builder.OpenScope();
            builder.WriteLine("return null;");
            builder.CloseScope();
            builder.WriteLine("return new AnimatedVisual(compositor);");
            builder.CloseScope();
            builder.WriteLine();
        }

        /// <summary>
        /// Write a class that implements the IDynamicAnimatedVisualSource interface.
        /// </summary>
        void WriteIDynamicAnimatedVisualSource(CodeBuilder builder, CodeGenInfo info)
        {
            var nodes = info.LoadedImageSurfaceNodes.ToArray();
            builder.WriteLine("namespace AnimatedVisuals");
            builder.OpenScope();
            builder.WriteLine($"sealed class {info.ClassName} : IDynamicAnimatedVisualSource, INotifyPropertyChanged");
            builder.OpenScope();

            // Declare variables.
            builder.WriteLine($"{_stringifier.Const(_stringifier.Int32TypeName)} c_loadedImageSurfaceCount = {nodes.Distinct().Count()};");
            builder.WriteLine($"{_stringifier.Int32TypeName} _loadCompleteEventCount;");
            builder.WriteLine("bool _isAnimatedVisualSourceDynamic = true;");
            builder.WriteLine("bool _tryCreateAnimatedVisualCalled;");
            builder.WriteLine("bool _imageLoadingStarted;");
            builder.WriteLine("EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>> _animatedVisualInvalidatedEventTokenTable;");

            // Declare the variables to hold the surfaces.
            foreach (var n in nodes)
            {
                builder.WriteLine($"{_stringifier.ReferenceTypeName(n.TypeName)} {_stringifier.MakeVariableName(n.Name)};");
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
            builder.WriteLine("if (!_tryCreateAnimatedVisualCalled && _isAnimatedVisualSourceDynamic != value)");
            builder.OpenScope();
            builder.WriteLine("_isAnimatedVisualSourceDynamic = value;");
            builder.WriteLine("NotifyPropertyChanged(nameof(IsAnimatedVisualSourceDynamic));");
            builder.CloseScope();
            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();

            builder.WriteSummaryComment("Returns true if all images have loaded. To see if the images succeeded to load, see <see cref=\"ImageSuccessfulLoadingProgress\"/>.");
            builder.WriteLine("public bool ImageLoadingCompleted { get; private set; }");
            builder.WriteLine();

            builder.WriteSummaryComment("Represents the progress of the image loading. Returns value between 0 and 1. 0 means none of the images finished loading. 1 means all images finished loading.");
            builder.WriteLine("public double ImageSuccessfulLoadingProgress { get; private set; }");
            builder.WriteLine();

            // Generate the method that creates an instance of the animated visual.
            builder.WriteLine("public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)");
            builder.OpenScope();
            builder.WriteLine("_tryCreateAnimatedVisualCalled = true;");
            builder.WriteLine();
            builder.WriteLine("diagnostics = null;");
            builder.WriteLine("if (!IsRuntimeCompatible())");
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
            builder.WriteLine("return");
            builder.Indent();
            WriteAnimatedVisualCall(builder, info);
            builder.WriteLine(";");
            builder.UnIndent();
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
        protected override void WriteInstantiatorStart(CodeBuilder builder, CodeGenInfo info)
        {
            // Start the instantiator class.
            builder.WriteLine("sealed class AnimatedVisual : IAnimatedVisual");
            builder.OpenScope();
        }

        /// <inheritdoc/>
        // Called by the base class to write the end of the file (i.e. everything after the body of the Instantiator class).
        protected override void WriteFileEnd(
            CodeBuilder builder,
            CodeGenInfo info)
        {
            // Write the constructor for the instantiator.
            if (info.HasLoadedImageSurface)
            {
                builder.WriteLine($"internal AnimatedVisual(Compositor compositor,");
                builder.Indent();

                // Pass in the image surfaces to AnimatedVisual() constructor.
                var nodes = GetLoadedImageSurfacesNodes().ToArray();
                for (var i = 0; i < nodes.Length; i++)
                {
                    var parameterString = $"{_stringifier.ReferenceTypeName(nodes[i].TypeName)} {_stringifier.CamelCase(nodes[i].Name)}";
                    if (i < nodes.Length - 1)
                    {
                        // Append "," to each parameter except the last one.
                        builder.WriteLine($"{parameterString},");
                    }
                    else
                    {
                        // Close the parenthesis after the last parameter.
                        builder.WriteLine($"{parameterString})");
                    }
                }

                builder.UnIndent();
                builder.OpenScope();

                builder.WriteLine("_c = compositor;");
                builder.WriteLine($"{info.ReusableExpressionAnimationFieldName} = compositor.CreateExpressionAnimation();");

                // Initialize the private image surface variables with the input parameters of the constructor.
                foreach (var n in nodes)
                {
                    builder.WriteLine($"{_stringifier.MakeVariableName(n.Name)} = {_stringifier.CamelCase(n.Name)};");
                }
            }
            else
            {
                builder.WriteLine("internal AnimatedVisual(Compositor compositor)");
                builder.OpenScope();
                builder.WriteLine("_c = compositor;");
                builder.WriteLine($"{info.ReusableExpressionAnimationFieldName} = compositor.CreateExpressionAnimation();");
            }

            builder.WriteLine("Root();");
            builder.CloseScope();
            builder.WriteLine();

            // Write the IAnimatedVisual implementation.
            builder.WriteLine("Visual IAnimatedVisual.RootVisual => _root;");
            builder.WriteLine($"TimeSpan IAnimatedVisual.Duration => TimeSpan.FromTicks({info.DurationTicksFieldName});");
            builder.WriteLine($"Vector2 IAnimatedVisual.Size => {Vector2(info.CompositionDeclaredSize)};");
            builder.WriteLine("void IDisposable.Dispose() => _root?.Dispose();");

            // Close the scope for the instantiator class.
            builder.CloseScope();

            // Close the scope for the class.
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
                builder.WriteLine($"{_stringifier.Matrix3x2(obj.Matrix)},");
            }

            builder.WriteLine($"{_stringifier.CanvasGeometryCombine(obj.CombineMode)});");
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
            builder.WriteLine($"new CanvasGeometry[] {{ {string.Join(", ", obj.Geometries.Select(g => CallFactoryFor(g)) ) } }},");
            builder.WriteLine($"{_stringifier.FilledRegionDetermination(obj.FilledRegionDetermination)});");
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
                builder.WriteLine($"builder.SetFilledRegionDetermination({_stringifier.FilledRegionDetermination(obj.FilledRegionDetermination)});");
            }

            foreach (var command in obj.Commands)
            {
                switch (command.Type)
                {
                    case CanvasPathBuilder.CommandType.BeginFigure:
                        builder.WriteLine($"builder.BeginFigure({Vector2(((CanvasPathBuilder.Command.BeginFigure)command).StartPoint)});");
                        break;
                    case CanvasPathBuilder.CommandType.EndFigure:
                        builder.WriteLine($"builder.EndFigure({_stringifier.CanvasFigureLoop(((CanvasPathBuilder.Command.EndFigure)command).FigureLoop)});");
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
            builder.WriteLine($"var result = {FieldAssignment(fieldName)}{CallFactoryFor(obj.SourceGeometry)}.Transform({_stringifier.Matrix3x2(obj.TransformMatrix)});");
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

        void WriteAnimatedVisualCall(CodeBuilder builder, CodeGenInfo info)
        {
            builder.WriteLine("new AnimatedVisual(compositor,");
            builder.Indent();

            var n = info.LoadedImageSurfaceNodes.ToArray();
            for (var i = 0; i < n.Length; i++)
            {
                var parameterString = $"{_stringifier.MakeVariableName(n[i].Name)}";
                if (i < n.Length - 1)
                {
                    // Append "," to each parameter except the last one.
                    builder.WriteLine($"{parameterString},");
                }
                else
                {
                    // Close the parenthesis after the last parameter.
                    builder.WriteLine($"{parameterString})");
                }
            }

            builder.UnIndent();
        }

        void WriteEnsureImageLoadingStarted(CodeBuilder builder, CodeGenInfo info)
        {
            builder.WriteLine("void EnsureImageLoadingStarted()");
            builder.OpenScope();
            builder.WriteLine("if (!_imageLoadingStarted)");
            builder.OpenScope();
            builder.WriteLine("var eventHandler = new TypedEventHandler<LoadedImageSurface, LoadedImageSourceLoadCompletedEventArgs>(HandleLoadCompleted);");

            var nodes = info.LoadedImageSurfaceNodes.ToArray();
            foreach (var n in nodes)
            {
                switch (n.LoadedImageSurfaceType)
                {
                    case LoadedImageSurface.LoadedImageSurfaceType.FromStream:
                        builder.WriteLine($"{_stringifier.MakeVariableName(n.Name)} = LoadedImageSurface.StartLoadFromStream({n.BytesFieldName}.AsBuffer().AsStream().AsRandomAccessStream());");
                        break;
                    case LoadedImageSurface.LoadedImageSurfaceType.FromUri:
                        builder.WriteLine($"{_stringifier.MakeVariableName(n.Name)} = LoadedImageSurface.StartLoadFromUri(new Uri(\"{n.ImageUri}\"));");
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                builder.WriteLine($"{_stringifier.MakeVariableName(n.Name)}.LoadCompleted += eventHandler;");
            }

            builder.WriteLine("_imageLoadingStarted = true;");
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
            builder.WriteLine("ImageLoadingCompleted = true;");
            builder.WriteLine("NotifyPropertyChanged(nameof(ImageLoadingCompleted));");
            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();
        }

        static string FieldAssignment(string fieldName) => fieldName != null ? $"{fieldName} = " : string.Empty;

        string Float(float value) => _stringifier.Float(value);

        string Vector2(Vector2 value) => _stringifier.Vector2(value);

        sealed class CSharpStringifier : StringifierBase
        {
            public override string Deref => ".";

            public override string ScopeResolve => ".";

            public override string Var => "var";

            public override string New => "new";

            public override string Null => "null";

            public override string IListAdd => "Add";

            public override string FactoryCall(string value) => value;

            public override string CanvasFigureLoop(CanvasFigureLoop value)
            {
                switch (value)
                {
                    case Mgcg.CanvasFigureLoop.Open:
                        return "CanvasFigureLoop.Open";
                    case Mgcg.CanvasFigureLoop.Closed:
                        return "CanvasFigureLoop.Closed";
                    default:
                        throw new InvalidOperationException();
                }
            }

            public override string CanvasGeometryCombine(CanvasGeometryCombine value)
            {
                switch (value)
                {
                    case Mgcg.CanvasGeometryCombine.Union:
                        return "CanvasGeometryCombine.Union";
                    case Mgcg.CanvasGeometryCombine.Exclude:
                        return "CanvasGeometryCombine.Exclude";
                    case Mgcg.CanvasGeometryCombine.Intersect:
                        return "CanvasGeometryCombine.Intersect";
                    case Mgcg.CanvasGeometryCombine.Xor:
                        return "CanvasGeometryCombine.Xor";
                    default:
                        throw new InvalidOperationException();
                }
            }

            public override string Color(Color value) => $"Color.FromArgb({Hex(value.A)}, {Hex(value.R)}, {Hex(value.G)}, {Hex(value.B)})";

            public override string FilledRegionDetermination(CanvasFilledRegionDetermination value)
            {
                switch (value)
                {
                    case CanvasFilledRegionDetermination.Alternate:
                        return "CanvasFilledRegionDetermination.Alternate";
                    case CanvasFilledRegionDetermination.Winding:
                        return "CanvasFilledRegionDetermination.Winding";
                    default:
                        throw new InvalidOperationException();
                }
            }

            public override string Int64(long value) => value.ToString();

            public override string Matrix3x2(Matrix3x2 value)
            {
                return $"new Matrix3x2({Float(value.M11)}, {Float(value.M12)}, {Float(value.M21)}, {Float(value.M22)}, {Float(value.M31)}, {Float(value.M32)})";
            }

            public override string Matrix4x4(Matrix4x4 value)
            {
                return $"new Matrix4x4({Float(value.M11)}, {Float(value.M12)}, {Float(value.M13)}, {Float(value.M14)}, {Float(value.M21)}, {Float(value.M22)}, {Float(value.M23)}, {Float(value.M24)}, {Float(value.M31)}, {Float(value.M32)}, {Float(value.M33)}, {Float(value.M34)}, {Float(value.M41)}, {Float(value.M42)}, {Float(value.M43)}, {Float(value.M44)})";
            }

            public override string Readonly(string value) => $"readonly {value}";

            public override string Int64TypeName => "long";

            public override string ReferenceTypeName(string value) => value;

            public override string TimeSpan(TimeSpan value) => TimeSpan(Int64(value.Ticks));

            public override string TimeSpan(string ticks) => $"TimeSpan.FromTicks({ticks})";

            public override string Vector2(Vector2 value) => $"new Vector2({Float(value.X)}, {Float(value.Y)})";

            public override string Vector3(Vector3 value) => $"new Vector3({Float(value.X)}, {Float(value.Y)}, {Float(value.Z)})";

            public override string ByteArray => "byte[]";
        }
    }
}
