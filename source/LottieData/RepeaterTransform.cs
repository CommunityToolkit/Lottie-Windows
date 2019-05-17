// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
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
            Animatable<double> rotationDegrees,
            Animatable<double> opacityPercent,
            Animatable<double> startOpacityPercent,
            Animatable<double> endOpacityPercent)
            : base(in args, anchor, position, scalePercent, rotationDegrees, opacityPercent)
        {
            StartOpacityPercent = startOpacityPercent;
            EndOpacityPercent = endOpacityPercent;
        }

        /// <summary>
        /// Gets the opacity of the original shaped. Only used by <see cref="Repeater"/>.
        /// </summary>
        public Animatable<double> StartOpacityPercent { get; }

        /// <summary>
        /// Gets the opacity of the last copy of the original shape. Only used by <see cref="Repeater"/>.
        /// </summary>
        public Animatable<double> EndOpacityPercent { get; }
    }
}
