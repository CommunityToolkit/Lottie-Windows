// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CommunityToolkit.WinUI.Lottie.CompMetadata;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.MetaData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;
using CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;
using Mgce = CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen.CSharp
{
    /// <summary>
    /// Generates C# code that instantiates a Composition graph.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class CSharpInstantiatorGenerator : InstantiatorGeneratorBase
    {
        readonly Stringifier _s;
        readonly string _winUiNamespace;

        CSharpInstantiatorGenerator(
            CodegenConfiguration configuration,
            Stringifier stringifier)
            : base(
                  configuration,
                  setCommentProperties: false,
                  stringifier: stringifier)
        {
            _s = stringifier;

            if (AnimatedVisualSourceInfo.WinUIVersion.Major >= 3)
            {
                _winUiNamespace = "Microsoft.UI";
            }
            else
            {
                _winUiNamespace = "Windows.UI";
            }
        }

        IAnimatedVisualSourceInfo SourceInfo => AnimatedVisualSourceInfo;

        /// <summary>
        /// Returns the C# code for a factory that will instantiate the given <see cref="Visual"/> as a
        /// Windows.UI.Composition Visual.
        /// </summary>
        /// <returns>A tuple containing the C# code and list of referenced asset files.</returns>
        public static CSharpCodegenResult CreateFactoryCode(
            CodegenConfiguration configuration)
        {
            var generator = new CSharpInstantiatorGenerator(
                                configuration: configuration,
                                stringifier: new CSharpStringifier());

            return new CSharpCodegenResult(
                csText: generator.GenerateCode(),
                assets: generator.GetAssetsList()
            );
        }

        static string FieldAssignment(string fieldName) => fieldName is not null ? $"{fieldName} = " : string.Empty;

        /// <inheritdoc/>
        // Called by the base class to write the start of the file (i.e. everything up to the body of the Instantiator class).
        protected override void WriteImplementationFileStart(CodeBuilder builder)
        {
            // A sorted set to hold the namespaces that the generated code will use. The set is maintained in sorted order.
            var namespaces = new SortedSet<string>();

            if (SourceInfo.UsesCanvas)
            {
                namespaces.Add("Microsoft.Graphics.Canvas");
            }

            if (SourceInfo.UsesCanvasEffects)
            {
                namespaces.Add("Microsoft.Graphics.Canvas.Effects");
            }

            if (SourceInfo.UsesCanvasGeometry)
            {
                // Windows.Graphics is needed for IGeometrySource2D.
                if (SourceInfo.WinUIVersion.Major < 3)
                {
                    namespaces.Add("Windows.Graphics");
                }
                else
                {
                    namespaces.Add("Microsoft.Graphics");
                }

                namespaces.Add("Microsoft.Graphics.Canvas.Geometry");
            }

            if (SourceInfo.UsesNamespaceWindowsUIXamlMedia)
            {
                namespaces.Add("System.ComponentModel");
                namespaces.Add("System.Runtime.InteropServices.WindowsRuntime");
                namespaces.Add("Windows.Foundation");
                namespaces.Add($"{_winUiNamespace}.Xaml.Media");
            }

            if (SourceInfo.GenerateDependencyObject)
            {
                namespaces.Add($"{_winUiNamespace}.Xaml");
            }

            if (SourceInfo.UsesStreams)
            {
                namespaces.Add("System.IO");
            }

            namespaces.Add("System");
            namespaces.Add("System.Collections.Generic");
            namespaces.Add("System.Numerics");

            // Windows.UI is needed for Color.
            namespaces.Add("Windows.UI");
            namespaces.Add($"{_winUiNamespace}.Composition");

            if (SourceInfo.LoadedImageSurfaces.Count > 0)
            {
                // IDynamicAnimatedVisualSource is output in multiple places without
                // namespace qualification.
                namespaces.Add("Microsoft.UI.Xaml.Controls");
            }

            // Write out each namespace using.
            foreach (var n in namespaces)
            {
                builder.WriteLine($"using {n};");
            }

            builder.WriteLine();

            builder.WriteLine($"namespace {SourceInfo.Namespace}");
            builder.OpenScope();

            // Write a description of the source as comments.
            builder.WritePreformattedCommentLines(GetSourceDescriptionLines());

            WriteIAnimatedVisualSource(builder, SourceInfo.LoadedImageSurfaces.Count > 0);

            builder.WriteLine();

            WriteFrameCountPropertyImpl(builder);
            builder.WriteLine();

            WriteFrameratePropertyImpl(builder);
            builder.WriteLine();

            WriteDurationPropertyImpl(builder);
            builder.WriteLine();

            WriteFrameToProgressImpl(builder);
            builder.WriteLine();

            WriteMarkersPropertyImpl(builder);
            builder.WriteLine();

            WriteSetColorPropertyImpl(builder);
            builder.WriteLine();

            WriteSetScalarPropertyImpl(builder);
            builder.WriteLine();
        }

        static string ExposedType(PropertyBinding binding)
            => binding.ExposedType switch
            {
                PropertySetValueType.Color => "Color",
                PropertySetValueType.Scalar => "double",
                PropertySetValueType.Vector2 => "Vector2",
                PropertySetValueType.Vector3 => "Vector3",
                PropertySetValueType.Vector4 => "Vector4",
                _ => throw new InvalidOperationException(),
            };

        void WriteThemeProperties(CodeBuilder builder)
        {
            foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
            {
                if (SourceInfo.GenerateDependencyObject)
                {
                    builder.WriteLine($"public {ExposedType(prop)} {prop.BindingName}");
                    builder.OpenScope();
                    builder.WriteLine($"get => ({ExposedType(prop)})GetValue({prop.BindingName}Property);");
                    builder.WriteLine($"set => SetValue({prop.BindingName}Property, value);");
                }
                else
                {
                    builder.WriteLine($"public {ExposedType(prop)} {prop.BindingName}");
                    builder.OpenScope();
                    builder.WriteLine($"get => _theme{prop.BindingName};");
                    builder.WriteLine("set");
                    builder.OpenScope();
                    builder.WriteLine($"_theme{prop.BindingName} = value;");
                    builder.WriteLine($"if ({SourceInfo.ThemePropertiesFieldName} != null)");
                    builder.OpenScope();
                    WriteThemePropertyInitialization(builder, SourceInfo.ThemePropertiesFieldName, prop);
                    builder.CloseScope();
                    builder.CloseScope();
                }

                builder.CloseScope();
                builder.WriteLine();
            }
        }

        // Writes the static dependency property fields and their initializers.
        void WriteDependencyPropertyFields(CodeBuilder builder)
        {
            if (SourceInfo.GenerateDependencyObject)
            {
                foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
                {
                    builder.WriteSummaryComment($"Dependency property for {prop.BindingName}.");
                    builder.WriteLine($"public static readonly DependencyProperty {prop.BindingName}Property =");
                    builder.Indent();
                    builder.WriteLine($"DependencyProperty.Register({_s.String(prop.BindingName)}, typeof({ExposedType(prop)}), typeof({SourceInfo.ClassName}),");
                    builder.Indent();
                    builder.WriteLine($"new PropertyMetadata({GetDefaultPropertyBindingValue(prop)}, On{prop.BindingName}Changed));");
                    builder.UnIndent();
                    builder.UnIndent();
                    builder.WriteLine();
                }
            }
        }

        // Writes the methods that handle dependency property changes.
        void WriteDependencyPropertyChangeHandlers(CodeBuilder builder)
        {
            // Add the dependency property change handler methods.
            if (SourceInfo.GenerateDependencyObject)
            {
                foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
                {
                    builder.WriteLine($"static void On{prop.BindingName}Changed(DependencyObject d, DependencyPropertyChangedEventArgs args)");
                    builder.OpenScope();
                    WriteThemePropertyInitialization(builder, $"(({SourceInfo.ClassName})d).{SourceInfo.ThemePropertiesFieldName}?", prop, $"({ExposedType(prop)})args.NewValue");
                    builder.CloseScope();
                    builder.WriteLine();
                }
            }
        }

        /// <summary>
        /// Writes a class that implements IAnimatedVisualSourceX interfaces or optional IDynamicAnimatedVisualSource.
        /// </summary>
        void WriteIAnimatedVisualSource(CodeBuilder builder, bool implementDynamicAVS)
        {
            var visibility = SourceInfo.Public ? "public " : string.Empty;

            builder.WriteLine($"{visibility}sealed class {SourceInfo.ClassName}");

            builder.Indent();
            if (SourceInfo.GenerateDependencyObject)
            {
                builder.WriteLine($": DependencyObject");
                builder.WriteLine($", {Interface_IAnimatedVisualSource.GetQualifiedName(_s)}");
            }
            else
            {
                builder.WriteLine($": {Interface_IAnimatedVisualSource.GetQualifiedName(_s)}");
            }

            if (SourceInfo.WinUIVersion >= new Version(2, 6))
            {
                builder.WriteLine($", {Interface_IAnimatedVisualSource2.GetQualifiedName(_s)}");
            }

            if (implementDynamicAVS)
            {
                builder.WriteLine($", {Interface_IDynamicAnimatedVisualSource.GetQualifiedName(_s)}");
                builder.WriteLine(", INotifyPropertyChanged");
            }

            foreach (var additionalInterface in SourceInfo.AdditionalInterfaces)
            {
                builder.WriteLine($", {additionalInterface.GetQualifiedName(_s)}");
            }

            builder.UnIndent();

            builder.OpenScope();

            if (implementDynamicAVS)
            {
                // Declare variables.
                builder.WriteLine($"const int c_loadedImageSurfaceCount = {SourceInfo.LoadedImageSurfaces.Count};");
                builder.WriteLine($"int _loadCompleteEventCount;");
                builder.WriteLine("bool _isImageLoadingAsynchronous;");
                builder.WriteLine("bool _isTryCreateAnimatedVisualCalled;");
                builder.WriteLine("bool _isImageLoadingStarted;");

                if (AnimatedVisualSourceInfo.WinUIVersion.Major >= 3)
                {
                    builder.WriteLine("HashSet<TypedEventHandler<IDynamicAnimatedVisualSource, object>> _animatedVisualInvalidatedEventTokenTable = new HashSet<TypedEventHandler<IDynamicAnimatedVisualSource, object>>();");
                }
                else
                {
                    builder.WriteLine("EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>> _animatedVisualInvalidatedEventTokenTable;");
                }

                // Declare the variables to hold the LoadedImageSurfaces.
                foreach (var n in SourceInfo.LoadedImageSurfaces)
                {
                    builder.WriteLine($"{_s.ReferenceTypeName(n.TypeName)} {n.FieldName};");
                }

                builder.WriteLine();
            }

            // Add any internal constants.
            foreach (var c in SourceInfo.InternalConstants)
            {
                builder.WriteComment(c.Description);
                switch (c.Type)
                {
                    case ConstantType.Color:
                        var color = (WinCompData.Wui.Color)c.Value;
                        builder.WriteLine($"internal static readonly Color {c.Name} = {_s.Color(color)};");
                        break;
                    case ConstantType.Int64:
                        builder.WriteLine($"internal const long {c.Name} = {_s.Int64((long)c.Value)};");
                        break;
                    case ConstantType.Float:
                        builder.WriteLine($"internal const float {c.Name} = {_s.Float((float)c.Value)};");
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                builder.WriteLine();
            }

            // Add the methods and fields needed for theming.
            WriteThemeMethodsAndFields(builder);

            if (implementDynamicAVS)
            {
                // Implement the INotifyPropertyChanged.PropertyChanged event.
                builder.WriteSummaryComment("This implementation of the INotifyPropertyChanged.PropertyChanged event is specific to C# and does not work on WinRT.");
                builder.WriteLine("public event PropertyChangedEventHandler PropertyChanged;");
                builder.WriteLine();

                // Implement the AnimatedVisualInvalidated event.
                WriteAnimatedVisualInvalidatedEvent(builder);

                // Define properties.
                builder.WriteSummaryComment("If this property is set to true, <see cref=\"TryCreateAnimatedVisual\"/> will return null" +
                    " until all images have loaded. When all images have loaded, <see cref=\"TryCreateAnimatedVisual\"/> will return" +
                    " the AnimatedVisual. To use, set it when instantiating the AnimatedVisualSource. Once <see cref=\"TryCreateAnimatedVisual\"/>" +
                    " is called, changes made to this property will be ignored. Default value is false.");
                builder.WriteLine("public bool IsImageLoadingAsynchronous");
                builder.OpenScope();
                builder.WriteLine("get { return _isImageLoadingAsynchronous; }");
                builder.WriteLine("set");
                builder.OpenScope();
                builder.WriteLine("if (!_isTryCreateAnimatedVisualCalled && _isImageLoadingAsynchronous != value)");
                builder.OpenScope();
                builder.WriteLine("_isImageLoadingAsynchronous = value;");
                builder.WriteLine("NotifyPropertyChanged(nameof(IsImageLoadingAsynchronous));");
                builder.CloseScope();
                builder.CloseScope();
                builder.CloseScope();
                builder.WriteLine();
                builder.WriteSummaryComment("Returns true if all images have finished loading.");
                builder.WriteLine("public bool IsImageLoadingCompleted { get; private set; }");
                builder.WriteLine();
            }

            // Generate the overload of TryCreateAnimatedVisual that doesn't return diagnostics.
            builder.WriteLine($"public {Interface_IAnimatedVisual.GetQualifiedName(_s)} TryCreateAnimatedVisual(Compositor compositor)");
            builder.OpenScope();
            builder.WriteLine("object ignored = null;");
            builder.WriteLine($"return TryCreateAnimatedVisual(compositor, out ignored);");
            builder.CloseScope();
            builder.WriteLine();

            // Generate the method that creates an instance of the animated visual.
            builder.WriteLine($"public {Interface_IAnimatedVisual.GetQualifiedName(_s)} TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)");
            builder.OpenScope();
            if (implementDynamicAVS)
            {
                builder.WriteLine("_isTryCreateAnimatedVisualCalled = true;");
            }

            builder.WriteLine("diagnostics = null;");
            if (SourceInfo.IsThemed)
            {
                builder.WriteLine("EnsureThemeProperties(compositor);");
            }

            builder.WriteLine();

            var animatedVisualInfos = SourceInfo.AnimatedVisualInfos.OrderByDescending(avi => avi.RequiredUapVersion).ToArray();

            if (implementDynamicAVS)
            {
                // WinUI3 doesn't ever do a version check. It's up to the user to make sure
                // the version they're using is compatible.
                if (SourceInfo.WinUIVersion.Major < 3)
                {
                    // Check whether the runtime will support the lowest UAP version required.
                    builder.WriteLine($"if (!{animatedVisualInfos[^1].ClassName}.IsRuntimeCompatible())");
                    builder.OpenScope();
                    builder.WriteLine("return null;");
                    builder.CloseScope();
                    builder.WriteLine();
                }

                builder.WriteLine("EnsureImageLoadingStarted();");
                builder.WriteLine();
                builder.WriteLine("if (_isImageLoadingAsynchronous && _loadCompleteEventCount != c_loadedImageSurfaceCount)");
                builder.OpenScope();
                builder.WriteLine("return null;");
                builder.CloseScope();
                builder.WriteLine();
            }

            // WinUI3 doesn't ever do a version check. It's up to the user to make sure
            // the version they're using is compatible.
            if (SourceInfo.WinUIVersion.Major >= 3)
            {
                WriteInstantiateAndReturnAnimatedVisual(builder, SourceInfo.AnimatedVisualInfos.First());
            }
            else
            {
                // Check the runtime version and instantiate the highest compatible IAnimatedVisual class.
                for (var i = 0; i < animatedVisualInfos.Length; i++)
                {
                    var current = animatedVisualInfos[i];
                    builder.WriteLine($"if ({current.ClassName}.IsRuntimeCompatible())");
                    builder.OpenScope();
                    WriteInstantiateAndReturnAnimatedVisual(builder, current);
                    builder.CloseScope();
                    builder.WriteLine();
                }

                builder.WriteLine("return null;");
            }

            builder.CloseScope();

            if (implementDynamicAVS)
            {
                // Generate the method that load all the LoadedImageSurfaces.
                WriteEnsureImageLoadingStarted(builder);

                // Generate the method that handle the LoadCompleted event of the LoadedImageSurface objects.
                WriteHandleLoadCompleted(builder);

                // Generate the method that raise the PropertyChanged event.
                builder.WriteLine("void NotifyPropertyChanged(string name)");
                builder.OpenScope();
                builder.WriteLine("PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));");
                builder.CloseScope();
                builder.WriteLine();

                // Generate the method that get or create the EventRegistrationTokenTable.
                if (AnimatedVisualSourceInfo.WinUIVersion.Major < 3)
                {
                    builder.WriteLine("EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>> GetAnimatedVisualInvalidatedEventRegistrationTokenTable()");
                    builder.OpenScope();
                    builder.WriteLine("return EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>>.GetOrCreateEventRegistrationTokenTable(ref _animatedVisualInvalidatedEventTokenTable);");
                    builder.CloseScope();
                }
            }
        }

        void WriteThemeMethodsAndFields(CodeBuilder builder)
        {
            if (SourceInfo.IsThemed)
            {
                builder.WriteLine($"CompositionPropertySet {SourceInfo.ThemePropertiesFieldName};");

                // Add fields for each of the theme properties.
                // Not needed if generating a DependencyObject - the values will be stored in DependencyPropertys.
                if (!SourceInfo.GenerateDependencyObject)
                {
                    foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
                    {
                        var defaultValue = GetDefaultPropertyBindingValue(prop);

                        WriteInitializedField(builder, ExposedType(prop), $"_theme{prop.BindingName}", _s.VariableInitialization($"c_theme{prop.BindingName}"));
                    }
                }

                builder.WriteLine();

                // Add the public static dependency property fields for each theme property.
                WriteDependencyPropertyFields(builder);

                // Add properties for each of the theme properties.
                if (SourceInfo.IsThemed)
                {
                    builder.WriteComment("Theme properties.");
                    WriteThemeProperties(builder);
                }

                if (SourceInfo.SourceMetadata.PropertyBindings.Any(pb => pb.ExposedType == PropertySetValueType.Color))
                {
                    // There's at least one themed color. They will need a helper method to convert to Vector4.
                    builder.WriteLine("static Vector4 ColorAsVector4(Color color) => new Vector4(color.R, color.G, color.B, color.A);");
                    builder.WriteLine();
                }

                // Add the dependency property change handler methods.
                WriteDependencyPropertyChangeHandlers(builder);

                // EnsureThemeProperties(...) method implementation.
                builder.WriteLine("CompositionPropertySet EnsureThemeProperties(Compositor compositor)");
                builder.OpenScope();
                builder.WriteLine($"if ({SourceInfo.ThemePropertiesFieldName} == null)");
                builder.OpenScope();
                builder.WriteLine($"{SourceInfo.ThemePropertiesFieldName} = compositor.CreatePropertySet();");

                // Initialize the values in the property set.
                foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
                {
                    WriteThemePropertyInitialization(builder, SourceInfo.ThemePropertiesFieldName, prop, prop.BindingName);
                }

                builder.CloseScope();
                builder.WriteLine($"return {SourceInfo.ThemePropertiesFieldName};");
                builder.CloseScope();
                builder.WriteLine();
            }
        }

        string GetDefaultPropertyBindingValue(PropertyBinding prop)
             => prop.ExposedType switch
             {
                 PropertySetValueType.Color => _s.Color((WinCompData.Wui.Color)prop.DefaultValue),

                 // Scalars are stored as floats, but exposed as doubles as XAML markup prefers doubles.
                 PropertySetValueType.Scalar => _s.Double((float)prop.DefaultValue),
                 PropertySetValueType.Vector2 => _s.Vector2((Vector2)prop.DefaultValue),
                 PropertySetValueType.Vector3 => _s.Vector3((Vector3)prop.DefaultValue),
                 PropertySetValueType.Vector4 => _s.Vector4((Vector4)prop.DefaultValue),
                 _ => throw new InvalidOperationException(),
             };

        /// <summary>
        /// Generates the FrameCount property implementation.
        /// </summary>
        void WriteFrameCountPropertyImpl(CodeBuilder builder)
        {
            builder.WriteSummaryComment("Gets the number of frames in the animation.");
            builder.WriteLine($"public double FrameCount => {_s.Double(SourceInfo.SourceMetadata.LottieMetadata.Duration.Frames)};");
        }

        /// <summary>
        /// Generates the Framerate property implementation.
        /// </summary>
        void WriteFrameratePropertyImpl(CodeBuilder builder)
        {
            builder.WriteSummaryComment("Gets the frame rate of the animation.");
            builder.WriteLine($"public double Framerate => {_s.Double(SourceInfo.SourceMetadata.LottieMetadata.Duration.FPS)};");
        }

        /// <summary>
        /// Generates the Duration property implementation.
        /// </summary>
        void WriteDurationPropertyImpl(CodeBuilder builder)
        {
            builder.WriteSummaryComment("Gets the duration of the animation.");
            builder.WriteLine($"public TimeSpan Duration => TimeSpan.FromTicks({SourceInfo.SourceMetadata.LottieMetadata.Duration.Time.Ticks});");
        }

        /// <summary>
        /// Generates the FrameToProgress(...) implementation.
        /// </summary>
        void WriteFrameToProgressImpl(CodeBuilder builder)
        {
            builder.WriteSummaryComment("Converts a zero-based frame number to the corresponding progress value denoting the start of the frame.");
            builder.WriteLine($"public double FrameToProgress(double frameNumber)");
            builder.OpenScope();
            builder.WriteLine($"return frameNumber / {_s.Double(SourceInfo.SourceMetadata.LottieMetadata.Duration.Frames)};");
            builder.CloseScope();
        }

        /// <summary>
        /// Generates the Markers property implementation.
        /// </summary>
        void WriteMarkersPropertyImpl(CodeBuilder builder)
        {
            builder.WriteSummaryComment("Returns a map from marker names to corresponding progress values.");
            builder.WriteLine($"public IReadOnlyDictionary<string, double> Markers =>");
            builder.Indent();
            builder.WriteLine("new Dictionary<string, double>");
            builder.OpenScope();
            foreach (var marker in SourceInfo.Markers)
            {
                builder.WriteLine($"{{ {_s.String(marker.Name)}, {_s.Double(marker.StartProgress)} }},");
            }

            builder.CloseScopeWithSemicolon();
            builder.UnIndent();
        }

        /// <summary>
        /// Generates the SetColorProperty(...) method implementation.
        /// </summary>
        void WriteSetColorPropertyImpl(CodeBuilder builder) =>
            WriteSetPropertyImpl(
                builder,
                PropertySetValueType.Color,
                "Color",
                "Sets the color property with the given name, or does nothing if no such property exists.");

        /// <summary>
        /// Generates the SetScalarProperty(...) method implementation.
        /// </summary>
        void WriteSetScalarPropertyImpl(CodeBuilder builder) =>
            WriteSetPropertyImpl(
                builder,
                PropertySetValueType.Scalar,
                "double",
                "Sets the scalar property with the given name, or does nothing if no such property exists.");

        void WriteSetPropertyImpl(
            CodeBuilder builder,
            PropertySetValueType propertyType,
            string typeName,
            string comment)
        {
            builder.WriteSummaryComment(comment);
            var properties = SourceInfo.SourceMetadata.PropertyBindings.Where(p => p.ExposedType == propertyType).ToArray();

            builder.WriteLine($"public void Set{propertyType}Property(string propertyName, {typeName} value)");
            if (properties.Length == 0)
            {
                // There are no properties. The method doesn't need to do anything.
                builder.OpenScope();
                builder.CloseScope();
            }
            else
            {
                var propertySetTypeName = PropertySetValueTypeName(properties[0].ActualType);
                var valueInitializer = properties[0].ExposedType == PropertySetValueType.Color ? "ColorAsVector4(value)" : "value";

                builder.OpenScope();

                var firstSeen = false;
                foreach (var prop in properties)
                {
                    // If the propertyName is a known name, save it into its backing field.
                    builder.WriteLine($"{(firstSeen ? "else " : string.Empty)}if (propertyName == {_s.String(prop.BindingName)})");
                    firstSeen = true;
                    builder.OpenScope();
                    builder.WriteLine($"{(SourceInfo.GenerateDependencyObject ? string.Empty : "_theme")}{prop.BindingName} = value;");
                    builder.CloseScope();
                }

                builder.WriteLine("else");
                builder.OpenScope();

                // Ignore the name if it doesn't refer to a known property.
                builder.WriteLine("return;");
                builder.CloseScope();
                builder.WriteLine();

                // Update the CompositionPropertySet if it has been created.
                builder.WriteLine($"if ({SourceInfo.ThemePropertiesFieldName} != null)");
                builder.OpenScope();

                builder.WriteLine($"{SourceInfo.ThemePropertiesFieldName}.Insert{propertySetTypeName}(propertyName, {valueInitializer});");
                builder.CloseScope();
                builder.CloseScope();
            }
        }

        /// <inheritdoc/>
        protected override void WriteAnimatedVisualStart(CodeBuilder builder, IAnimatedVisualInfo info)
        {
            // Start the instantiator class.
            builder.WriteLine($"sealed class {info.ClassName}");
            builder.Indent();
            builder.WriteLine($": {Interface_IAnimatedVisual.GetQualifiedName(_s)}");

            if (info.ImplementCreateAndDestroyMethods)
            {
                builder.WriteLine($", {Interface_IAnimatedVisual2.GetQualifiedName(_s)}");
            }

            builder.UnIndent();

            builder.OpenScope();
        }

        IEnumerable<string> GetConstructorParameters(IAnimatedVisualInfo info)
        {
            yield return "Compositor compositor";

            if (SourceInfo.IsThemed)
            {
                yield return "CompositionPropertySet themeProperties";
            }

            foreach (var loadedImageSurfaceNode in info.LoadedImageSurfaceNodes)
            {
                yield return $"{_s.ReferenceTypeName(loadedImageSurfaceNode.TypeName)} {_s.CamelCase(loadedImageSurfaceNode.Name)}";
            }
        }

        /// <inheritdoc/>
        // Called by the base class to write the end of the AnimatedVisual class.
        protected override void WriteAnimatedVisualEnd(
            CodeBuilder builder,
            IAnimatedVisualInfo info,
            CodeBuilder createAnimations,
            CodeBuilder destroyAnimations)
        {
            // Write the constructor for the AnimatedVisual class.
            builder.WriteLine($"internal {info.ClassName}(");
            builder.Indent();
            builder.WriteCommaSeparatedLines(GetConstructorParameters(info));
            builder.WriteLine(")");
            builder.UnIndent();
            builder.OpenScope();

            // Copy constructor parameters into fields.
            builder.WriteLine("_c = compositor;");

            if (SourceInfo.IsThemed)
            {
                builder.WriteLine($"{SourceInfo.ThemePropertiesFieldName} = themeProperties;");
            }

            var loadedImageSurfaceNodes = info.LoadedImageSurfaceNodes;
            foreach (var n in loadedImageSurfaceNodes)
            {
                builder.WriteLine($"{n.FieldName} = {_s.CamelCase(n.Name)};");
            }

            builder.WriteLine($"{SourceInfo.ReusableExpressionAnimationFieldName} = compositor.CreateExpressionAnimation();");

            builder.WriteLine("Root();");
            builder.CloseScope();
            builder.WriteLine();

            // Write the IAnimatedVisual implementation.
            builder.WriteLine($"public Visual RootVisual => _root;");
            builder.WriteLine($"public TimeSpan Duration => TimeSpan.FromTicks({SourceInfo.DurationTicksFieldName});");
            builder.WriteLine($"public Vector2 Size => {_s.Vector2(SourceInfo.CompositionDeclaredSize)};");
            builder.WriteLine("void IDisposable.Dispose() => _root?.Dispose();");
            builder.WriteLine();

            if (info.ImplementCreateAndDestroyMethods)
            {
                builder.WriteLine($"public void {CreateAnimationsMethod}()");
                builder.OpenScope();
                builder.WriteCodeBuilder(createAnimations);
                builder.CloseScope();
                builder.WriteLine();

                builder.WriteLine($"public void {DestroyAnimationsMethod}()");
                builder.OpenScope();
                builder.WriteCodeBuilder(destroyAnimations);
                builder.CloseScope();
                builder.WriteLine();
            }

            // WinUI3 doesn't ever do a version check. It's up to the user to make sure
            // the version they're using is compatible.
            if (SourceInfo.WinUIVersion.Major < 3)
            {
                // Write the IsRuntimeCompatible static method.
                builder.WriteLine("internal static bool IsRuntimeCompatible()");
                builder.OpenScope();
                builder.WriteLine($"return Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent(\"Windows.Foundation.UniversalApiContract\", {info.RequiredUapVersion});");
                builder.CloseScope();
            }

            // Close the scope for the instantiator class.
            builder.CloseScope();
        }

        /// <inheritdoc/>
        // Called by the base class to write the end of the file (i.e. everything after the body of the AnimatedVisual class).
        protected override void WriteImplementationFileEnd(CodeBuilder builder)
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
            builder.WriteLine($"{_s.Float(obj.X)}, {_s.Float(obj.Y)}, {_s.Float(obj.RadiusX)}, {_s.Float(obj.RadiusY)});");
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
                        builder.WriteLine($"builder.BeginFigure({_s.Vector2(((CanvasPathBuilder.Command.BeginFigure)command).StartPoint)});");
                        break;
                    case CanvasPathBuilder.CommandType.EndFigure:
                        builder.WriteLine($"builder.EndFigure({_s.CanvasFigureLoop(((CanvasPathBuilder.Command.EndFigure)command).FigureLoop)});");
                        break;
                    case CanvasPathBuilder.CommandType.AddLine:
                        builder.WriteLine($"builder.AddLine({_s.Vector2(((CanvasPathBuilder.Command.AddLine)command).EndPoint)});");
                        break;
                    case CanvasPathBuilder.CommandType.AddCubicBezier:
                        var cb = (CanvasPathBuilder.Command.AddCubicBezier)command;
                        builder.WriteLine($"builder.AddCubicBezier({_s.Vector2(cb.ControlPoint1)}, {_s.Vector2(cb.ControlPoint2)}, {_s.Vector2(cb.EndPoint)});");
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
            builder.WriteLine($"{_s.Float(obj.X)},");
            builder.WriteLine($"{_s.Float(obj.Y)},");
            builder.WriteLine($"{_s.Float(obj.W)},");
            builder.WriteLine($"{_s.Float(obj.H)},");
            builder.WriteLine($"{_s.Float(obj.RadiusX)},");
            builder.WriteLine($"{_s.Float(obj.RadiusY)});");
            builder.UnIndent();
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryTransformedGeometryFactory(CodeBuilder builder, CanvasGeometry.TransformedGeometry obj, string typeName, string fieldName)
        {
            builder.WriteLine($"var result = {FieldAssignment(fieldName)}{CallFactoryFor(obj.SourceGeometry)}.Transform({_s.Matrix3x2(obj.TransformMatrix)});");
        }

        /// <inheritdoc/>
        protected override string WriteCompositeEffectFactory(CodeBuilder builder, Mgce.CompositeEffect effect)
        {
            var effectVariable = "compositeEffect";
            builder.WriteLine($"var {effectVariable} = new CompositeEffect();");
            builder.WriteLine($"{effectVariable}.Mode = {_s.CanvasCompositeMode(effect.Mode)};");
            foreach (var source in effect.Sources)
            {
                builder.WriteLine($"{effectVariable}.Sources.Add(new CompositionEffectSourceParameter({_s.String(source.Name)}));");
            }

            return effectVariable;
        }

        /// <inheritdoc/>
        protected override string WriteGaussianBlurEffectFactory(CodeBuilder builder, GaussianBlurEffect effect)
        {
            var effectVariable = "gaussianBlurEffect";
            builder.WriteLine($"var {effectVariable} = new GaussianBlurEffect();");

            builder.WriteLine($"{effectVariable}.BlurAmount = {_s.Float(effect.BlurAmount)};");

            builder.WriteLine($"{effectVariable}.Source = new CompositionEffectSourceParameter({_s.String(effect.Sources.First().Name)});");

            return effectVariable;
        }

        /// <inheritdoc/>
        protected override void WriteByteArrayField(CodeBuilder builder, string fieldName, IReadOnlyList<byte> bytes)
        {
            string base64 = Convert.ToBase64String(bytes.ToArray());
            builder.WriteLine($"static readonly string {fieldName}_base64 = ");
            builder.WriteLongStringLiteral(base64, 115);
            builder.WriteLine(";");
        }

        void WriteAnimatedVisualInvalidatedEvent(CodeBuilder builder)
        {
            builder.WriteLine("public event TypedEventHandler<IDynamicAnimatedVisualSource, object> AnimatedVisualInvalidated");
            builder.OpenScope();
            builder.WriteLine("add");
            builder.OpenScope();
            if (AnimatedVisualSourceInfo.WinUIVersion.Major >= 3)
            {
                builder.WriteLine("_animatedVisualInvalidatedEventTokenTable.Add(value);");
            }
            else
            {
                builder.WriteLine("return GetAnimatedVisualInvalidatedEventRegistrationTokenTable().AddEventHandler(value);");
            }

            builder.CloseScope();
            builder.WriteLine("remove");
            builder.OpenScope();

            if (AnimatedVisualSourceInfo.WinUIVersion.Major >= 3)
            {
                builder.WriteLine("_animatedVisualInvalidatedEventTokenTable.Remove(value);");
            }
            else
            {
                builder.WriteLine("GetAnimatedVisualInvalidatedEventRegistrationTokenTable().RemoveEventHandler(value);");
            }

            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();
        }

        void WriteEnsureImageLoadingStarted(CodeBuilder builder)
        {
            builder.WriteLine("void EnsureImageLoadingStarted()");
            builder.OpenScope();
            builder.WriteLine("if (!_isImageLoadingStarted)");
            builder.OpenScope();
            builder.WriteLine("var eventHandler = new TypedEventHandler<LoadedImageSurface, LoadedImageSourceLoadCompletedEventArgs>(HandleLoadCompleted);");

            foreach (var n in SourceInfo.LoadedImageSurfaces)
            {
                switch (n.LoadedImageSurfaceType)
                {
                    case LoadedImageSurface.LoadedImageSurfaceType.FromStream:
                        builder.WriteLine($"{n.FieldName} = LoadedImageSurface.StartLoadFromStream(Convert.FromBase64String({n.BytesFieldName}_base64).AsBuffer().AsStream().AsRandomAccessStream());");
                        break;
                    case LoadedImageSurface.LoadedImageSurfaceType.FromUri:
                        builder.WriteComment(n.Comment);
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
            builder.WriteLine("if (_loadCompleteEventCount == c_loadedImageSurfaceCount)");
            builder.OpenScope();
            builder.WriteLine("IsImageLoadingCompleted = true;");
            builder.WriteLine("NotifyPropertyChanged(nameof(IsImageLoadingCompleted));");

            // If asynchronouse image loading is enabled notify via IDynamicAnimatedVisualSource that
            // the previous result is now invalidated.
            builder.WriteLine("if (_isImageLoadingAsynchronous)");
            builder.OpenScope();
            if (AnimatedVisualSourceInfo.WinUIVersion.Major >= 3)
            {
                builder.WriteLine("foreach (var v in _animatedVisualInvalidatedEventTokenTable) v.Invoke(this, null);");
            }
            else
            {
                builder.WriteLine("_animatedVisualInvalidatedEventTokenTable?.InvocationList?.Invoke(this, null);");
            }

            builder.CloseScope();
            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();
        }

        IEnumerable<string> GetConstructorArguments(IAnimatedVisualInfo info)
        {
            yield return "compositor";

            if (SourceInfo.IsThemed)
            {
                yield return SourceInfo.ThemePropertiesFieldName;
            }

            foreach (var loadedImageSurfaceNode in info.LoadedImageSurfaceNodes)
            {
                yield return loadedImageSurfaceNode.FieldName;
            }
        }

        void WriteInstantiateAndReturnAnimatedVisual(CodeBuilder builder, IAnimatedVisualInfo info)
        {
            builder.WriteLine("var res = ");
            builder.Indent();
            builder.WriteLine($"new {info.ClassName}(");
            builder.Indent();
            builder.WriteCommaSeparatedLines(GetConstructorArguments(info));
            builder.WriteLine(");");
            builder.UnIndent();

            if (info.ImplementCreateAndDestroyMethods)
            {
                builder.WriteLine($"res.{CreateAnimationsMethod}();");
            }

            builder.WriteLine("return res;");
            builder.UnIndent();
        }
    }
}
