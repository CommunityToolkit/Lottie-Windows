// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Color : IEquatable<Color>
    {
        Color(double a, double r, double g, double b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public static Color FromArgb(double a, double r, double g, double b)
            => new Color(a, r, g, b);

        public static Color Black { get; } = new Color(1, 0, 0, 0);

        public static Color TransparentBlack { get; } = new Color(0, 0, 0, 0);

        /// <summary>
        /// The alpha value of this <see cref="Color"/>.
        /// </summary>
        public double A { get; }

        /// <summary>
        /// The red value of this <see cref="Color"/>.
        /// </summary>
        public double R { get; }

        /// <summary>
        /// The green value of this <see cref="Color"/>.
        /// </summary>
        public double G { get; }

        /// <summary>
        /// The blue value of this <see cref="Color"/>.
        /// </summary>
        public double B { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as Color);

        /// <inheritdoc/>
        public bool Equals(Color other) => A == other.A && R == other.R && G == other.G && B == other.B;

        /// <inheritdoc/>
        public override int GetHashCode() => (A * R * G * B).GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => $"#{ToHex(A)}{ToHex(R)}{ToHex(G)}{ToHex(B)}";

        /// <summary>
        /// Return a color with the given opacity multiplied into the alpha channel of the given color.
        /// </summary>
        public static Color operator *(Color color, Opacity opacity) => color?.MultipliedByOpacity(opacity);

        /// <summary>
        /// Return a color with the given opacity multiplied into the alpha channel of the given color.
        /// </summary>
        public static Color operator *(Opacity opacity, Color color) => color?.MultipliedByOpacity(opacity);

        Color MultipliedByOpacity(Opacity opacity) => opacity.IsOpaque ? this : new Color(opacity.Value * A, R, G, B);

        static string ToHex(double value) => ((byte)(value * 255)).ToString("X2");
    }
}
