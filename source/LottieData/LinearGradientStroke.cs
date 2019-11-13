// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class LinearGradientStroke : ShapeStroke, IGradient
    {
        public LinearGradientStroke(
            in ShapeLayerContentArgs args,
            Animatable<double> opacityPercent,
            Animatable<double> strokeWidth,
            LineCapType capType,
            LineJoinType joinType,
            double miterLimit,
            IAnimatableVector3 startPoint,
            IAnimatableVector3 endPoint,
            Animatable<Sequence<GradientStop>> gradientStops)
            : base(in args, opacityPercent, strokeWidth, capType, joinType, miterLimit)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            GradientStops = gradientStops;
        }

        public IAnimatableVector3 StartPoint { get; }

        public IAnimatableVector3 EndPoint { get; }

        public Animatable<Sequence<GradientStop>> GradientStops { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.LinearGradientStroke;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.LinearGradientStroke;

        public override ShapeStrokeKind StrokeKind => ShapeStrokeKind.LinearGradient;
    }
}
