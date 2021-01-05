// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
#if PUBLIC_IR
    public
#endif
    sealed class RadialGradientFill : ShapeFill, IRadialGradient
    {
        public RadialGradientFill(
            in ShapeLayerContentArgs args,
            PathFillType fillType,
            Animatable<Opacity> opacity,
            IAnimatableVector3 startPoint,
            IAnimatableVector3 endPoint,
            Animatable<Sequence<GradientStop>> gradientStops,
            Animatable<double> highlightLength,
            Animatable<double> highlightDegrees)
            : base(in args, fillType, opacity)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            GradientStops = gradientStops;
            HighlightLength = highlightLength;
            HighlightDegrees = highlightDegrees;
        }

        public IAnimatableVector3 StartPoint { get; }

        public IAnimatableVector3 EndPoint { get; }

        public Animatable<Sequence<GradientStop>> GradientStops { get; }

        public Animatable<double> HighlightLength { get; }

        public Animatable<double> HighlightDegrees { get; }

        /// <inheritdoc/>
        public override ShapeFillKind FillKind => ShapeFillKind.RadialGradient;

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.RadialGradientFill;
    }
}
