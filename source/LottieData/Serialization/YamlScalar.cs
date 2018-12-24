// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    sealed class YamlScalar : YamlObject
    {
        readonly object _value;
        readonly string _presention;

        YamlScalar(object value, string presentation)
        {
            _value = value;
            _presention = presentation;
        }

        internal override YamlObjectKind Kind => YamlObjectKind.Scalar;

        public static implicit operator YamlScalar(string value)
        {
            var escapedValue = value;
            if (escapedValue == null)
            {
                escapedValue = "~";
            }
            else if (escapedValue.Length == 0)
            {
                escapedValue = "''";
            }
            else if (value.StartsWith(" ") || value.EndsWith(" ") || value.StartsWith("#"))
            {
                escapedValue = $"'{value}'";
            }

            return new YamlScalar(value, escapedValue);
        }

        public static implicit operator YamlScalar(double value) => new YamlScalar(value, value.ToString());

        public static implicit operator YamlScalar(bool value) => new YamlScalar(value, value.ToString());

        public static implicit operator YamlScalar(TimeSpan value) => new YamlScalar(value, value.ToString());

        public static implicit operator YamlScalar(Color value) => new YamlScalar(value, $"'{value.ToString()}'");

        public static implicit operator YamlScalar(Version value) => new YamlScalar(value, value.ToString());

        public static implicit operator YamlScalar(Easing.EasingType type) => new YamlScalar(type, type.ToString());

        public static implicit operator YamlScalar(Layer.LayerType type) => new YamlScalar(type, type.ToString());

        public static implicit operator YamlScalar(Mask.MaskMode type) => new YamlScalar(type, type.ToString());

        public static implicit operator YamlScalar(ShapeContentType type) => new YamlScalar(type, type.ToString());

        public override string ToString() => _presention;
    }
}