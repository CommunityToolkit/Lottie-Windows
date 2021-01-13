// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes
{
    sealed class RadialGradientBrush : GradientBrush
    {
        public RadialGradientBrush(
           IAnimatableVector3 startPoint,
           IAnimatableVector3 endPoint,
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

        public override string ToString() => $"Radial gradient";
    }
}