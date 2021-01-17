// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.ShapeStroke;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class StrokeRenderingContext : RenderingContext
    {
        public StrokeRenderingContext(
            Brush brush,
            Animatable<double> strokeWidth,
            LineCapType capType,
            LineJoinType joinType,
            double miterLimit)
        {
            Brush = brush;
            StrokeWidth = strokeWidth;
            CapType = capType;
            JoinType = joinType;
            MiterLimit = miterLimit;
        }

        public Animatable<double> StrokeWidth { get; }

        public LineCapType CapType { get; }

        public LineJoinType JoinType { get; }

        public double MiterLimit { get; }

        public Brush Brush { get; }

        public override bool IsAnimated => StrokeWidth.IsAnimated || Brush.IsAnimated;

        public override sealed RenderingContext WithOffset(Vector2 offset) => this;

        public override RenderingContext WithTimeOffset(double timeOffset)
            => IsAnimated
                ? new StrokeRenderingContext(
                        Brush.WithTimeOffset(timeOffset),
                        StrokeWidth.WithTimeOffset(timeOffset),
                        CapType,
                        JoinType,
                        MiterLimit)
                : this;

        public override string ToString() => $"Stroke {Brush}";
    }
}