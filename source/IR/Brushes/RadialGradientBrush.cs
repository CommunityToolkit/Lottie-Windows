// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes
{
    sealed class RadialGradientBrush : GradientBrush
    {
        public RadialGradientBrush(
           IAnimatableVector2 startPoint,
           IAnimatableVector2 endPoint,
           Animatable<Sequence<GradientStop>> gradientStops,
           Animatable<Opacity> opacity,
           Animatable<double> highlightLength,
           Animatable<double> highlightDegrees)
           : base(startPoint, endPoint, gradientStops, opacity)
        {
            HighlightLength = highlightLength;
            HighlightDegrees = highlightDegrees;
        }

        public Animatable<double> HighlightLength { get; }

        public Animatable<double> HighlightDegrees { get; }

        public override bool IsAnimated =>
            base.IsAnimated || HighlightDegrees.IsAnimated || HighlightLength.IsAnimated;

        public override Brush WithTimeOffset(double timeOffset)
            => IsAnimated
            ? new RadialGradientBrush(
                    StartPoint.WithTimeOffset(timeOffset),
                    EndPoint.WithTimeOffset(timeOffset),
                    GradientStops.WithTimeOffset(timeOffset),
                    Opacity.WithTimeOffset(timeOffset),
                    HighlightLength.WithTimeOffset(timeOffset),
                    HighlightDegrees.WithTimeOffset(timeOffset))
            : this;

        public override string ToString() => $"{(IsAnimated ? "Animated" : "Static")} Radial gradient";
    }
}