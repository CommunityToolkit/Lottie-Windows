// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Lottie.Animatables;

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class RadialGradientFill : ShapeFill, IRadialGradient
    {
        public RadialGradientFill(
            in ShapeLayerContentArgs args,
            PathFillType fillType,
            Animatable<Opacity> opacity,
            IAnimatableVector2 startPoint,
            IAnimatableVector2 endPoint,
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

        public IAnimatableVector2 StartPoint { get; }

        public IAnimatableVector2 EndPoint { get; }

        public Animatable<Sequence<GradientStop>> GradientStops { get; }

        public Animatable<double> HighlightLength { get; }

        public Animatable<double> HighlightDegrees { get; }

        /// <inheritdoc/>
        public override ShapeFillKind FillKind => ShapeFillKind.RadialGradient;

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.RadialGradientFill;

        public override ShapeLayerContent WithTimeOffset(double offset)
        {
            return new RadialGradientFill(
                CopyArgs(),
                FillType,
                Opacity.WithTimeOffset(offset),
                StartPoint.WithTimeOffset(offset),
                EndPoint.WithTimeOffset(offset),
                GradientStops.WithTimeOffset(offset),
                HighlightLength.WithTimeOffset(offset),
                HighlightDegrees.WithTimeOffset(offset)
                );
        }
    }
}
