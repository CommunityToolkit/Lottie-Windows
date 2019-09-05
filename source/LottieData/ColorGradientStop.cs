// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    readonly struct ColorGradientStop
    {
        public ColorGradientStop(double offset, Color color)
        {
            if (color == null)
            {
                throw new ArgumentException("Color must be specified");
            }

            if (color.A != 1)
            {
                throw new ArgumentException("Color must have an alpha of 1");
            }

            Offset = offset;
            Color = color;
        }

        public readonly Color Color;
        public readonly double Offset;

        /// <inheritdoc/>
        public override string ToString()
            => $"#{ToHex(Color.R)}{ToHex(Color.G)}{ToHex(Color.B)}@{Offset}";

        static string ToHex(double value) => ((byte)(value * 255)).ToString("X2");
    }
}