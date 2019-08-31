// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    readonly struct GradientStop
    {
        public GradientStop(double offset, Color color, double? opacityPercent)
        {
            if (color != null)
            {
                if (color.A != 1)
                {
                    throw new ArgumentException("Color must have an alpha of 1");
                }
            }
            else if (opacityPercent == null)
            {
                throw new ArgumentException("Color or opacity must be specified");
            }

            Offset = offset;
            Color = color;
            OpacityPercent = opacityPercent;
        }

        public static GradientStop FromColor(double offset, Color color) => new GradientStop(offset, color, null);

        public static GradientStop FromOpacityPercent(double offset, double opacityPercent) => new GradientStop(offset, null, opacityPercent);

        public readonly Color Color;
        public readonly double Offset;
        public readonly double? OpacityPercent;

        /// <summary>
        /// Returns the first gradient stop in a sequence that is a color. Opacity will always be
        /// set to 1.
        /// </summary>
        /// <returns>The first gradient stop in the sequence.</returns>
        public static Color GetFirstColor(ReadOnlySpan<GradientStop> gradientStops)
        {
            foreach (var stop in gradientStops)
            {
                if (stop.Color != null)
                {
                    return stop.Color;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var rgb = Color == null
                ? "??????"
                : $"{ToHex(Color.R)}{ToHex(Color.G)}{ToHex(Color.B)}";

            var opacity = OpacityPercent.HasValue
                ? ToHex(OpacityPercent.Value / 100)
                : "??";

            return $"#{opacity}{rgb}@{Offset}";
        }

        static string ToHex(double value) => ((byte)(value * 255)).ToString("X2");
    }
}