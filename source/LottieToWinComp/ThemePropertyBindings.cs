// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates themable properties into propertyset values.
    /// </summary>
    static class ThemePropertyBindings
    {
        /// <summary>
        /// The name used in <see cref="ExpressionAnimation"/> expressions to bind to
        /// the <see cref="CompositionPropertySet"/> that contains the theme properties.
        /// </summary>
        public const string ThemePropertiesName = "_theme";

        /// <summary>
        /// Parses the given bindingSpec, and returns the name of the property in the theme
        /// <see cref="CompositionPropertySet"/> that should be used for binding to, or null if the property bindings
        /// are currently disabled, or the bindingSpec doesn't mention the given property name.
        /// </summary>
        /// <returns>The name of the corresponding property in the theme <see cref="CompositionPropertySet"/>
        /// or null.</returns>
        public static string? GetThemeBindingNameForLottieProperty(TranslationContext context, string bindingSpec, string propertyName)
            => context.TranslatePropertyBindings
                ? PropertyBindings.FindFirstBindingNameForProperty(bindingSpec, propertyName)
                : null;

        /// <summary>
        /// Gets the theme <see cref="CompositionPropertySet"/> for the given translation.
        /// </summary>
        /// <returns>The theme <see cref="CompositionPropertySet"/>.</returns>
        public static CompositionPropertySet GetThemePropertySet(TranslationContext context)
        {
            var cache = context.GetStateCache<StateCache>();
            return cache.GetThemePropertySet(context);
        }

        /// <summary>
        /// Ensures there is a property in the theme property set with the given name and default value.
        /// </summary>
        public static void EnsureColorThemePropertyExists(LayerContext context, string bindingName, string displayName, Color defaultValue)
        {
            var defaultValueAsWinUIColor = ConvertTo.Color(defaultValue);
            var defaultValueAsVector4 = ConvertTo.Vector4(defaultValueAsWinUIColor);
            var themePropertySet = GetThemePropertySet(context);

            // Insert a property set value for the scalar if one hasn't yet been added.
            switch (themePropertySet.TryGetVector4(bindingName, out var existingColorAsVector4))
            {
                case CompositionGetValueStatus.NotFound:
                    // The property hasn't been added yet. Add it.
                    themePropertySet.InsertVector4(bindingName, ConvertTo.Vector4(defaultValueAsWinUIColor));
                    context.Translation.PropertyBindings.AddPropertyBinding(new CompMetadata.PropertyBinding(
                        bindingName : bindingName,
                        displayName : displayName,
                        actualType : PropertySetValueType.Vector4,
                        exposedType : PropertySetValueType.Color,
                        defaultValue : defaultValueAsWinUIColor));
                    break;

                case CompositionGetValueStatus.Succeeded:
                    // The property has already been added.
                    var existingValue = ConvertTo.Color(ConvertTo.Color(existingColorAsVector4!.Value));

                    if (defaultValueAsVector4 != existingColorAsVector4)
                    {
                        context.Issues.ThemePropertyValuesAreInconsistent(bindingName, existingValue.ToString(), ConvertTo.Color(ConvertTo.Color(defaultValueAsVector4)).ToString());
                    }

                    break;

                case CompositionGetValueStatus.TypeMismatch:
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Ensures there is a property in the theme property set with the given name and default value.
        /// </summary>
        static void EnsureScalarThemePropertyExists(TranslationContext context, string bindingName, string displayName, double defaultValue)
        {
            var defaultValueAsFloat = ConvertTo.Float(defaultValue);
            var themePropertySet = GetThemePropertySet(context);

            // Insert a property set value for the scalar if one hasn't yet been added.
            switch (themePropertySet.TryGetScalar(bindingName, out var existingValueAsFloat))
            {
                case CompositionGetValueStatus.NotFound:
                    // The property hasn't been added yet. Add it.
                    themePropertySet.InsertScalar(bindingName, defaultValueAsFloat);
                    context.PropertyBindings.AddPropertyBinding(new CompMetadata.PropertyBinding(
                        bindingName : bindingName,
                        displayName : displayName,
                        actualType : PropertySetValueType.Scalar,
                        exposedType : PropertySetValueType.Scalar,
                        defaultValue : ConvertTo.Float(defaultValue)));
                    break;

                case CompositionGetValueStatus.Succeeded:
                    // The property has already been added.
                    if (existingValueAsFloat != defaultValueAsFloat)
                    {
                        context.Issues.ThemePropertyValuesAreInconsistent(bindingName, existingValueAsFloat.ToString(), defaultValueAsFloat.ToString());
                    }

                    break;

                case CompositionGetValueStatus.TypeMismatch:
                default:
                    throw new InvalidOperationException();
            }
        }

        public static bool TryBindScalarPropertyToTheme(
            TranslationContext context,
            CompositionObject target,
            string bindingSpec,
            string lottiePropertyName,
            string compositionPropertyName,
            double defaultValue)
        {
            var bindingName = GetThemeBindingNameForLottieProperty(context, bindingSpec, lottiePropertyName);

            if (bindingName is null)
            {
                return false;
            }
            else
            {
                // Ensure there is a property in the theme property set for this binding name.
                EnsureScalarThemePropertyExists(context, bindingName, displayName: bindingName, defaultValue);

                // Create an expression that binds property to the theme property set.
                var anim = context.ObjectFactory.CreateExpressionAnimation(ExpressionFactory.ThemedScalar(bindingName));
                anim.SetReferenceParameter(ThemePropertiesName, GetThemePropertySet(context));
                target.StartAnimation(compositionPropertyName, anim);
                return true;
            }
        }

        sealed class StateCache
        {
            CompositionPropertySet? _themePropertySet;

            /// <summary>
            /// <see cref="CompositionPropertySet"/> used for property bindings for themed Lotties.
            /// </summary>
            public CompositionPropertySet GetThemePropertySet(TranslationContext context) =>
                _themePropertySet ??= context.ObjectFactory.CreatePropertySet();
        }
    }
}