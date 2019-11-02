// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class RadialGradientStroke : ShapeStroke, IRadialGradient
    {
        public RadialGradientStroke(
            in ShapeLayerContentArgs args,
            Animatable<double> opacityPercent,
            Animatable<double> strokeWidth,
            LineCapType capType,
            LineJoinType joinType,
            double miterLimit,
            IAnimatableVector3 startPoint,
            IAnimatableVector3 endPoint,
            Animatable<Sequence<ColorGradientStop>> colorStops,
            Animatable<Sequence<OpacityGradientStop>> opacityPercentStops,
            Animatable<double> highlightLength,
            Animatable<double> highlightDegrees)
            : base(in args, opacityPercent, strokeWidth, capType, joinType, miterLimit)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            ColorStops = colorStops;
            OpacityPercentStops = opacityPercentStops;
            HighlightLength = highlightLength;
            HighlightDegrees = highlightDegrees;
        }

        public IAnimatableVector3 StartPoint { get; }

        public IAnimatableVector3 EndPoint { get; }

        public Animatable<Sequence<ColorGradientStop>> ColorStops { get; }

        public Animatable<Sequence<OpacityGradientStop>> OpacityPercentStops { get; }

        public Animatable<double> HighlightLength { get; }

        public Animatable<double> HighlightDegrees { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.RadialGradientStroke;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.RadialGradientStroke;

        public override ShapeStrokeKind StrokeKind => ShapeStrokeKind.RadialGradient;
    }
}
