// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Lottie.LottieData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using Sn = System.Numerics;

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
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
            public WinCompData.Expressions.Vector2 OffsetExpression { get; }

            // Use constant value if size is static
            public Sn.Vector2 OffsetValue { get; }

            // IsAnimated = true means that we have to use OffsetExpression.
            // IsAnimated = false means that we can use OffsetValue instead of OffsetExpression to optimize the code.
            public bool IsAnimated { get; }

            public OriginOffsetContainer(RectangleOrRoundedRectangleGeometry geometry, WinCompData.Expressions.Vector2 expression)
            {
                IsAnimated = true;
                Geometry = geometry;
                OffsetExpression = expression;
                OffsetValue = new Sn.Vector2(0, 0);
            }

            public OriginOffsetContainer(RectangleOrRoundedRectangleGeometry geometry, Sn.Vector2 value)
            {
                IsAnimated = false;
                Geometry = geometry;
                OffsetExpression = Expression.Vector2(value.X, value.Y);
                OffsetValue = value;
            }
        }

        internal OriginOffsetContainer? OriginOffset { get; set; }

        public new ShapeLayer Layer { get; }
    }
}