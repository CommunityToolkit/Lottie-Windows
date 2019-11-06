// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class LinearGradientFill : ShapeFill, IGradient
    {
        public LinearGradientFill(
            in ShapeLayerContentArgs args,
            PathFillType fillType,
            Animatable<double> opacityPercent,
            IAnimatableVector3 startPoint,
            IAnimatableVector3 endPoint,
            Animatable<Sequence<ColorGradientStop>> colorStops,
            Animatable<Sequence<OpacityGradientStop>> opacityPercentStops)
            : base(in args, fillType, opacityPercent)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            ColorStops = colorStops;
            OpacityPercentStops = opacityPercentStops;
        }

        public IAnimatableVector3 StartPoint { get; }

        public IAnimatableVector3 EndPoint { get; }

        public Animatable<Sequence<ColorGradientStop>> ColorStops { get; }

        public Animatable<Sequence<OpacityGradientStop>> OpacityPercentStops { get; }

        /// <inheritdoc/>
        public override ShapeFillKind FillKind => ShapeFillKind.LinearGradient;

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.LinearGradientFill;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.LinearGradientFill;
    }
}
