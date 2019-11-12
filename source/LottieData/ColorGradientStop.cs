// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class ColorGradientStop : GradientStop, IEquatable<ColorGradientStop>
    {
        public ColorGradientStop(double offset, Color color)
            : base(offset)
        {
            if (color == null)
            {
                throw new ArgumentException("Color must be specified");
            }

            Color = color;
        }

        public Color Color { get; }

        /// <inheritdoc/>
        public override GradientStopKind Kind => GradientStopKind.Color;

        /// <inheritdoc/>
        public override string ToString()
            => $"#{ToHex(Color.R)}{ToHex(Color.G)}{ToHex(Color.B)}@{Offset}";

        static string ToHex(double value) => ((byte)(value * 255)).ToString("X2");

        public bool Equals(ColorGradientStop other)
            => other != null && other.Offset == Offset && other.Color.Equals(Color);

        public override bool Equals(object obj)
            => Equals(obj as ColorGradientStop);

        public override int GetHashCode() => Color.GetHashCode() ^ Offset.GetHashCode();
    }
}