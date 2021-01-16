// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes
{
    sealed class LinearGradientBrush : GradientBrush
    {
        public LinearGradientBrush(
            IAnimatableVector2 startPoint,
            IAnimatableVector2 endPoint,
            Animatable<Sequence<GradientStop>> gradientStops,
            Animatable<Opacity> opacity)
            : base(startPoint, endPoint, gradientStops, opacity)
        {
        }

        public override Brush WithTimeOffset(double timeOffset)
             => IsAnimated
            ? new LinearGradientBrush(
                StartPoint.WithTimeOffset(timeOffset),
                EndPoint.WithTimeOffset(timeOffset),
                GradientStops.WithTimeOffset(timeOffset),
                Opacity.WithTimeOffset(timeOffset))
            : this;

        public override string ToString() => $"{(IsAnimated ? "Animated" : "Static")} Linear gradient";
    }
}