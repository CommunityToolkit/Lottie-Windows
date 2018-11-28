// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class RepeaterTransform : Transform
    {
        public RepeaterTransform(
            string name,
            IAnimatableVector3 anchor,
            IAnimatableVector3 position,
            IAnimatableVector3 scalePercent,
            Animatable<double> rotationDegrees,
            Animatable<double> opacityPercent,
            Animatable<double> startOpacityPercent,
            Animatable<double> endOpacityPercent)
            : base(name, anchor, position, scalePercent, rotationDegrees, opacityPercent)
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
