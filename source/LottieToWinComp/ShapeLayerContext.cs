// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    sealed class ShapeLayerContext : LayerContext
    {
        internal ShapeLayerContext(CompositionContext compositionContext, ShapeLayer layer)
            : base(compositionContext, layer)
        {
            Layer = layer;
        }

        // Rectangles are implemented differently in WinComp API
        // and Lottie. In WinComp API coordinates inside rectangle start in
        // top left corner and in Lottie they start in the middle
        // To account for this we need to offset all the points inside
        // the rectangle for (Rectangle.Size / 2).
        // This class represents this offset (static or animated)
        public class OriginOffsetContainer
        {
            public RectangleOrRoundedRectangleGeometry Geometry { get; }

            // Use expression if size is animated
            public WinCompData.Expressions.Vector2? OffsetExpression { get; }

            // Use constant value if size is static
            public Sn.Vector2? OffsetValue { get; }

            public bool IsAnimated => OffsetValue is null;

            public OriginOffsetContainer(RectangleOrRoundedRectangleGeometry geometry, WinCompData.Expressions.Vector2 expression)
            {
                Geometry = geometry;
                OffsetExpression = expression;
                OffsetValue = null;
            }

            public OriginOffsetContainer(RectangleOrRoundedRectangleGeometry geometry, Sn.Vector2 value)
            {
                Geometry = geometry;
                OffsetExpression = null;
                OffsetValue = value;
            }
        }

        internal OriginOffsetContainer? OriginOffset { get; set; }

        public new ShapeLayer Layer { get; }
    }
}