// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class ShapeStroke : ShapeLayerContent
    {
        public ShapeStroke(
            in ShapeLayerContentArgs args,
            Animatable<Opacity> opacity,
            Animatable<double> strokeWidth,
            LineCapType capType,
            LineJoinType joinType,
            double miterLimit)
            : base(in args)
        {
            Opacity = opacity;
            StrokeWidth = strokeWidth;
            CapType = capType;
            JoinType = joinType;
            MiterLimit = miterLimit;
        }

        public Animatable<Opacity> Opacity { get; }

        public Animatable<double> StrokeWidth { get; }

        public LineCapType CapType { get; }

        public LineJoinType JoinType { get; }

        public double MiterLimit { get; }

        public abstract ShapeStrokeKind StrokeKind { get; }

        public enum LineCapType
        {
            Butt,
            Round,
            Projected,
        }

        public enum LineJoinType
        {
            Miter,
            Round,
            Bevel,
        }

        public enum ShapeStrokeKind
        {
            SolidColor,
            LinearGradient,
            RadialGradient,
        }
    }
}
