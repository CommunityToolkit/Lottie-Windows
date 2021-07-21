// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class RadialGradientStroke : ShapeStroke, IRadialGradient
    {
        public RadialGradientStroke(
            in ShapeLayerContentArgs args,
            Animatable<Opacity> opacity,
            Animatable<double> strokeWidth,
            LineCapType capType,
            LineJoinType joinType,
            double miterLimit,
            IAnimatableVector2 startPoint,
            IAnimatableVector2 endPoint,
            Animatable<Sequence<GradientStop>> gradientStops,
            Animatable<double> highlightLength,
            Animatable<double> highlightDegrees)
            : base(in args, opacity, strokeWidth, capType, joinType, miterLimit)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            GradientStops = gradientStops;
            HighlightLength = highlightLength;
            HighlightDegrees = highlightDegrees;
        }

        public IAnimatableVector2 StartPoint { get; }

        public IAnimatableVector2 EndPoint { get; }

        public Animatable<Sequence<GradientStop>> GradientStops { get; }

        public Animatable<double> HighlightLength { get; }

        public Animatable<double> HighlightDegrees { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.RadialGradientStroke;

        public override ShapeStrokeKind StrokeKind => ShapeStrokeKind.RadialGradient;

        public override ShapeLayerContent WithTimeOffset(double offset)
        {
            return new RadialGradientStroke(
                CopyArgs(),
                Opacity.WithTimeOffset(offset),
                StrokeWidth.WithTimeOffset(offset),
                CapType,
                JoinType,
                MiterLimit,
                StartPoint.WithTimeOffset(offset),
                EndPoint.WithTimeOffset(offset),
                GradientStops.WithTimeOffset(offset),
                HighlightLength.WithTimeOffset(offset),
                HighlightDegrees.WithTimeOffset(offset)
                );
        }
    }
}
