// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
#if PUBLIC_IR
    public
#endif
    sealed class LinearGradientStroke : ShapeStroke, IGradient
    {
        public LinearGradientStroke(
            in ShapeLayerContentArgs args,
            Animatable<Opacity> opacity,
            Animatable<double> strokeWidth,
            LineCapType capType,
            LineJoinType joinType,
            double miterLimit,
            IAnimatableVector2 startPoint,
            IAnimatableVector2 endPoint,
            Animatable<Sequence<GradientStop>> gradientStops)
            : base(in args, opacity, strokeWidth, capType, joinType, miterLimit)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            GradientStops = gradientStops;
        }

        public IAnimatableVector2 StartPoint { get; }

        public IAnimatableVector2 EndPoint { get; }

        public Animatable<Sequence<GradientStop>> GradientStops { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.LinearGradientStroke;

        public override ShapeStrokeKind StrokeKind => ShapeStrokeKind.LinearGradient;
    }
}
