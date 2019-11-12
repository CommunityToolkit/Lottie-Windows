// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class OpacityGradientStop : GradientStop
    {
        public OpacityGradientStop(double offset, double opacityPercent)
            : base(offset)
        {
            OpacityPercent = opacityPercent;
        }

        public double OpacityPercent { get; }

        /// <inheritdoc/>
        public override GradientStopKind Kind => GradientStopKind.Opacity;

        /// <inheritdoc/>
        public override string ToString() => $"{OpacityPercent}%@{Offset}";
    }
}