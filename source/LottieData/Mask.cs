// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Lottie.Animatables;

namespace CommunityToolkit.WinUI.Lottie.LottieData
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
            Animatable<PathGeometry> points,
            Animatable<Opacity> opacity,
            MaskMode mode
        )
        {
            Inverted = inverted;
            Name = name;
            Points = points;
            Opacity = opacity;
            Mode = mode;
        }

        public bool Inverted { get; }

        public string Name { get; }

        public Animatable<PathGeometry> Points { get; }

        public Animatable<Opacity> Opacity { get; }

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
