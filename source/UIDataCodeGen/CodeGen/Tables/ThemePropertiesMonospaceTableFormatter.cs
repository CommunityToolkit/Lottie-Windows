// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.CompMetadata;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    sealed class ThemePropertiesMonospaceTableFormatter : MonospaceTableFormatter
    {
        internal static IEnumerable<string> GetThemePropertyDescriptionLines(IEnumerable<PropertyBinding> themeProperty)
        {
            if (themeProperty is null)
            {
                return Array.Empty<string>();
            }

            var header = new[] {
                Row.HeaderTop,
                new Row.ColumnData(
                        ColumnData.Create("Theme property"),
                        ColumnData.Create("Accessor"),
                        ColumnData.Create("Type"),
                        ColumnData.Create("Default value")
                        ),
                Row.HeaderBottom,
                };

            var records =
                (from property in themeProperty
                 select new Row.ColumnData(
                     ColumnData.Create(property.DisplayName, TextAlignment.Left, 1),
                     ColumnData.Create(property.BindingName, TextAlignment.Left, 1),
                     ColumnData.Create(property.ExposedType.ToString()),
                     ColumnData.Create(GetDefaultValueString(property))
                 )).ToArray();

            var rows = header.Concat(records).Append(Row.BodyBottom);

            return GetTableLines(rows);
        }

        static string GetDefaultValueString(PropertyBinding propertyBinding)
        {
            switch (propertyBinding.ExposedType)
            {
                case WinCompData.MetaData.PropertySetValueType.Color:
                    var color = (WinCompData.Wui.Color)propertyBinding.DefaultValue;
                    var colorHex = $"#{color.Hex}";
                    return color.TryGetFriendlyName(out var colorName)
                        ? $"{colorHex} {colorName}"
                        : colorHex;
                case WinCompData.MetaData.PropertySetValueType.Scalar:
                    return ((float)propertyBinding.DefaultValue).ToString();
                case WinCompData.MetaData.PropertySetValueType.Vector2:
                    return ((Vector2)propertyBinding.DefaultValue).ToString();
                case WinCompData.MetaData.PropertySetValueType.Vector3:
                    return ((Vector3)propertyBinding.DefaultValue).ToString();
                case WinCompData.MetaData.PropertySetValueType.Vector4:
                    return ((Vector4)propertyBinding.DefaultValue).ToString();
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
