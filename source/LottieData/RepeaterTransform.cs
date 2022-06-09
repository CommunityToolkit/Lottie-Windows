// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Lottie.Animatables;

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class RepeaterTransform : Transform
    {
        public RepeaterTransform(
            in ShapeLayerContentArgs args,
            IAnimatableVector3 anchor,
            IAnimatableVector3 position,
            IAnimatableVector3 scalePercent,
            Animatable<Rotation> rotation,
            Animatable<Opacity> opacity,
            Animatable<Opacity> startOpacity,
            Animatable<Opacity> endOpacity)
            : base(in args, anchor, position, scalePercent, rotation, opacity)
        {
            StartOpacity = startOpacity;
            EndOpacity = endOpacity;
        }

        /// <summary>
        /// Gets the opacity of the original shaped. Only used by <see cref="Repeater"/>.
        /// </summary>
        public Animatable<Opacity> StartOpacity { get; }

        /// <summary>
        /// Gets the opacity of the last copy of the original shape. Only used by <see cref="Repeater"/>.
        /// </summary>
        public Animatable<Opacity> EndOpacity { get; }

        public override ShapeLayerContent WithTimeOffset(double offset)
        {
            return new RepeaterTransform(
                CopyArgs(),
                Anchor.WithTimeOffset(offset),
                Position.WithTimeOffset(offset),
                ScalePercent.WithTimeOffset(offset),
                Rotation.WithTimeOffset(offset),
                Opacity.WithTimeOffset(offset),
                StartOpacity.WithTimeOffset(offset),
                EndOpacity.WithTimeOffset(offset)
                );
        }
    }
}
