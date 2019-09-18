// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// Mask class representing the Lottie mask properties.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class Mask
    {
        public Mask(
            bool inverted,
            string name,
            Animatable<Sequence<BezierSegment>> points,
            Animatable<double> opacityPercent,
            MaskMode mode
        )
        {
            Inverted = inverted;
            Name = name;
            Points = points;
            OpacityPercent = opacityPercent;
            Mode = mode;
        }

        public bool Inverted { get; }

        public string Name { get; }

        public Animatable<Sequence<BezierSegment>> Points { get; }

        public Animatable<double> OpacityPercent { get; }

        public MaskMode Mode { get; }

        public enum MaskMode
        {
            None = 0,
            Add,
            Darken,
            Difference,
            Intersect,
            Lighten,
            Subtract,
        }
    }
}
