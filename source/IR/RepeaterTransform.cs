// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
#if PUBLIC_IR
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
    }
}
