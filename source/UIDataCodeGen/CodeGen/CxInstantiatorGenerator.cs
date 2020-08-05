// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class CxInstantiatorGenerator : CppInstantiatorGeneratorBase
    {
        CxInstantiatorGenerator(
            CodegenConfiguration configuration,
            string headerFileName)
            : base(
                  configuration: configuration,
                  setCommentProperties: false,
                  new CxStringifier(),
                  headerFileName,
                  false)
        {
        }

        /// <summary>
        /// Returns the Cx code for a factory that will instantiate the given <see cref="Visual"/> as a
        /// Windows.UI.Composition Visual.
        /// </summary>
        /// <returns>A value tuple containing the cpp code, header code, and list of referenced asset files.</returns>
        public static (string cppText, string hText, IEnumerable<Uri> assetList) CreateFactoryCode(
            CodegenConfiguration configuration,
            string headerFileName)
        {
            var generator = new CxInstantiatorGenerator(
                configuration: configuration,
                headerFileName: headerFileName);

            var cppText = generator.GenerateCode();

            var hText = generator.GenerateHeaderText(generator.AnimatedVisualSourceInfo);

            var assetList = generator.GetAssetsList();

            return (cppText, hText, assetList);
        }

        private protected override void WriteHeaderClassStart(HeaderBuilder builder, IAnimatedVisualSourceInfo info, string inherits)
        {
            // NOTE: the CX class is always made public. This is necessary to allow CX
            // XAML projects to compile (the XAML compiler can't find the metadata for the
            // class if it isn't made public).
            builder.Preamble.WriteLine($"public ref class {SourceClassName} sealed");
            builder.Preamble.Indent();
            builder.Preamble.WriteLine($": public {inherits}");
            builder.Preamble.UnIndent();
        }

        private protected override void WriteThemeHeader(HeaderBuilder builder)
        {
            // Add a field to hold the theme property set.
            builder.Private.WriteLine($"{Wuc}::{T.CompositionPropertySet} {SourceInfo.ThemePropertiesFieldName}{{ nullptr }};");

            builder.Internal.WriteComment("Theme properties.");

            var hasColorProperty = false;

            // Add fields and proeprty declarations for each of the theme properties.
            foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
            {
                hasColorProperty |= prop.ExposedType == PropertySetValueType.Color;

                if (SourceInfo.GenerateDependencyObject)
                {
                    builder.Private.WriteLine($"static Windows::UI::Xaml::DependencyProperty^ _{S.CamelCase(prop.Name)}Property;");
                    builder.Private.WriteLine($"static void On{prop.Name}Changed(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);");
                    builder.Internal.WriteLine($"static Windows::UI::Xaml::DependencyProperty^ {prop.Name}Property();");
                    builder.Internal.WriteLine();
                }
                else
                {
                    var exposedTypeName = QualifiedTypeName(prop.ExposedType);

                    WriteInitializedField(builder.Private, exposedTypeName, $"_theme{prop.Name}", S.VariableInitialization($"c_theme{prop.Name}"));
                }

                builder.Internal.WriteLine($"property {QualifiedTypeName(prop.ExposedType)} {prop.Name}");
                builder.Internal.OpenScope();
                builder.Internal.WriteLine($"{QualifiedTypeName(prop.ExposedType)} get();");
                builder.Internal.WriteLine($"void set ({QualifiedTypeName(prop.ExposedType)} value);");
                builder.Internal.CloseScope();
                builder.Internal.WriteLine();
            }

            builder.Private.WriteLine();
            builder.Private.WriteLine($"{Wuc}::{T.CompositionPropertySet} EnsureThemeProperties({Wuc}::{T.Compositor} compositor);");
            builder.Private.WriteLine();

            if (hasColorProperty)
            {
                var b = IsInterfaceCustom ? builder.Internal : builder.Private;
                b.WriteLine("static Windows::Foundation::Numerics::float4 ColorAsVector4(Windows::UI::Color color);");
                b.WriteLine();
            }
        }

        protected override void WriteThemePropertyImpls(CodeBuilder builder)
        {
            var propertyBindings = SourceInfo.SourceMetadata.PropertyBindings;

            var sourceClassQualifier = $"{S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::";

            if (propertyBindings.Any(pb => pb.ExposedType == PropertySetValueType.Color))
            {
                // Write the helper for converting a color to a vector 4.
                builder.WriteLine($"float4 {sourceClassQualifier}ColorAsVector4(Color color)");
                builder.OpenScope();
                builder.WriteLine("return { static_cast<float>(color.R), static_cast<float>(color.G), static_cast<float>(color.B), static_cast<float>(color.A) };");
                builder.CloseScope();
                builder.WriteLine();
            }

            builder.WriteLine($"{T.CompositionPropertySet} {sourceClassQualifier}EnsureThemeProperties({T.Compositor} compositor)");
            builder.OpenScope();
            builder.WriteLine($"if ({SourceInfo.ThemePropertiesFieldName} == nullptr)");
            builder.OpenScope();
            builder.WriteLine($"{SourceInfo.ThemePropertiesFieldName} = compositor{S.Deref}CreatePropertySet();");

            // Initialize the values in the property set.
            foreach (var prop in propertyBindings)
            {
                WriteThemePropertyInitialization(builder, SourceInfo.ThemePropertiesFieldName, prop, prop.Name);
            }

            builder.CloseScope();
            builder.WriteLine();
            builder.WriteLine($"return {SourceInfo.ThemePropertiesFieldName};");
            builder.CloseScope();
            builder.WriteLine();

            // The GetThemeProperties method is designed to allow setting of properties when the actual
            // type of the IAnimatedVisualSource is not known. It relies on a custom interface that declares
            // it, so if we're not generating code for a custom interface, there's no reason to generate
            // the method.
            if (IsInterfaceCustom)
            {
                builder.WriteLine($"{T.CompositionPropertySet} {sourceClassQualifier}GetThemeProperties({T.Compositor} compositor)");
                builder.OpenScope();
                builder.WriteLine("return EnsureThemeProperties(compositor);");
                builder.CloseScope();
                builder.WriteLine();
            }

            // Write property implementations for each theme property.
            foreach (var prop in propertyBindings)
            {
                if (SourceInfo.GenerateDependencyObject)
                {
                    // Write the dependency property accessor.
                    builder.WriteLine($"DependencyProperty^ {sourceClassQualifier}{prop.Name}Property()");
                    builder.OpenScope();
                    builder.WriteLine($"return _{S.CamelCase(prop.Name)}Property;");
                    builder.CloseScope();
                    builder.WriteLine();

                    // Write the dependency property change handler.
                    builder.WriteLine($"void {sourceClassQualifier}On{prop.Name}Changed(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)");
                    builder.OpenScope();
                    builder.WriteLine($"auto self = ({sourceClassQualifier}^)d;");
                    builder.WriteLine();
                    builder.WriteLine("if (self->_themeProperties != nullptr)");
                    builder.OpenScope();
                    WriteThemePropertyInitialization(builder, $"self->{SourceInfo.ThemePropertiesFieldName}", prop, "e->NewValue");
                    builder.CloseScope();
                    builder.CloseScope();
                    builder.WriteLine();

                    // Write the dependency property initializer.
                    builder.WriteLine($"DependencyProperty^ {sourceClassQualifier}_{S.CamelCase(prop.Name)}Property =");
                    builder.Indent();
                    builder.WriteLine($"DependencyProperty::Register(");
                    builder.Indent();
                    builder.WriteLine($"{S.String(prop.Name)},");
                    builder.WriteLine($"{TypeName(prop.ExposedType)}::typeid,");
                    builder.WriteLine($"{sourceClassQualifier}typeid,");
                    builder.WriteLine($"ref new PropertyMetadata(c_theme{prop.Name},");
                    builder.WriteLine($"ref new PropertyChangedCallback(&{sourceClassQualifier}On{prop.Name}Changed)));");
                    builder.UnIndent();
                    builder.UnIndent();
                    builder.WriteLine();
                }

                // Write the getter.
                builder.WriteLine($"{TypeName(prop.ExposedType)} {sourceClassQualifier}{prop.Name}::get()");
                builder.OpenScope();
                if (SourceInfo.GenerateDependencyObject)
                {
                    // Get the value from the dependency property.
                    builder.WriteLine($"return ({TypeName(prop.ExposedType)})GetValue(_{S.CamelCase(prop.Name)}Property);");
                }
                else
                {
                    // Get the value from the backing field.
                    builder.WriteLine($"return _theme{prop.Name};");
                }

                builder.CloseScope();
                builder.WriteLine();

                // Write the setter.
                builder.WriteLine($"void {sourceClassQualifier}{prop.Name}::set({TypeName(prop.ExposedType)} value)");
                builder.OpenScope();
                if (SourceInfo.GenerateDependencyObject)
                {
                    builder.WriteLine($"SetValue(_{S.CamelCase(prop.Name)}Property, value);");
                }
                else
                {
                    // This saves to the backing field, and updates the theme property
                    // set if one has been created.
                    builder.WriteLine($"_theme{prop.Name} = value;");
                    builder.WriteLine("if (_themeProperties != nullptr)");
                    builder.OpenScope();
                    WriteThemePropertyInitialization(builder, "_themeProperties", prop);
                    builder.CloseScope();
                }

                builder.CloseScope();
                builder.WriteLine();
            }
        }

        /// <inheritdoc/>
        protected override void WriteAnimatedVisualStart(
            CodeBuilder builder,
            IAnimatedVisualInfo info)
        {
            // Start writing the instantiator.
            builder.WriteLine($"ref class {info.ClassName} sealed");
            builder.Indent();
            builder.WriteLine($": public {AnimatedVisualTypeName}");
            builder.UnIndent();
            builder.OpenScope();

            if (info.AnimatedVisualSourceInfo.UsesCanvasEffects ||
                info.AnimatedVisualSourceInfo.UsesCanvasGeometry)
            {
                // D2D factory field.
                builder.WriteLine("ComPtr<ID2D1Factory> _d2dFactory;");
            }
        }
    }
}
