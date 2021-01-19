// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    /// <summary>
    /// Defines the size of some content. The content is clipped to this size.
    /// </summary>
    sealed class SizeRenderingContext : RenderingContext
    {
        internal SizeRenderingContext(Vector2 size)
        {
            Size = size;
        }

        public Vector2 Size { get; }

        public override bool IsAnimated => false;

        public override RenderingContext WithOffset(Vector2 offset) => this;

        public override RenderingContext WithTimeOffset(double timeOffset) => this;

        public static RenderingContext WithoutRedundants(RenderingContext context)
        {
            if (context.Any(item => item is AnchorRenderingContext))
            {
                // Don't try to remove anything while there are anchors.
                return context;
            }

            return Compose(Without(context));

            static IEnumerable<RenderingContext> Without(RenderingContext items)
            {
                // Keep track of the bounding box.
                var topLeft = Vector2.Zero;
                var bottomRight = new Vector2(double.PositiveInfinity, double.PositiveInfinity);

                foreach (var item in items)
                {
                    switch (item)
                    {
                        case SizeRenderingContext size:
                            var newBottomRight = new Vector2(Math.Min(bottomRight.X, size.Size.X), Math.Min(bottomRight.Y, size.Size.Y));
                            if (newBottomRight != bottomRight)
                            {
                                yield return size;
                                bottomRight = newBottomRight;
                            }

                            continue;

                        case ScaleRenderingContext.Static staticScale:
                            // Update the bounding box.
                            topLeft *= staticScale.ScalePercent / 100;
                            bottomRight *= staticScale.ScalePercent / 100;
                            break;

                        case PositionRenderingContext.Static staticPosition:
                            // Update the bounding box.
                            topLeft += staticPosition.Position;
                            bottomRight += staticPosition.Position;
                            break;

                        case RotationRenderingContext _:
                        case ScaleRenderingContext.Animated _:
                        case PositionRenderingContext.Animated _:
                            // Reset the bounding box.
                            topLeft = Vector2.Zero;
                            bottomRight = new Vector2(double.PositiveInfinity, double.PositiveInfinity);
                            break;
                    }

                    yield return item;
                }
            }
        }

        public override string ToString() => $"Size: {Size.X}x{Size.Y}";
    }
}
