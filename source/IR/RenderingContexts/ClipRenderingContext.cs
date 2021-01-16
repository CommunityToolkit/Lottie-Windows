// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class ClipRenderingContext : RenderingContext
    {
        internal ClipRenderingContext(Vector2 position, Vector2 size)
        {
            Position = position;
            Size = size;
        }

        public Vector2 Position { get; }

        public Vector2 Size { get; }

        public override bool IsAnimated => false;

        public override RenderingContext WithOffset(Vector2 offset)
            => offset.X == 0 && offset.Y == 0
                ? this
                : new ClipRenderingContext(Position + offset, Size);

        public override RenderingContext WithTimeOffset(double timeOffset) => this;

        public static RenderingContext WithoutRedundants(RenderingContext context)
            => context.SubContexts.Count > 0
                ? Compose(MoveClipsUp(context.SubContexts))
                : context;

        static IEnumerable<RenderingContext> MoveClipsUp(IReadOnlyList<RenderingContext> items)
        {
            var accumulator = new List<RenderingContext>(items.Count);
            ClipRenderingContext? currentClip = null;
            var yieldAccumulatorContents = false;
            foreach (var item in items)
            {
                switch (item)
                {
                    case ClipRenderingContext clip:
                        currentClip = clip.WithIntersection(currentClip);
                        break;

                    case PositionRenderingContext _:
                    case ScaleRenderingContext _:
                        yieldAccumulatorContents = true;
                        goto default;

                    default:
                        accumulator.Add(item);
                        break;
                }

                if (yieldAccumulatorContents)
                {
                    if (currentClip is not null)
                    {
                        yield return currentClip;
                    }

                    foreach (var c in accumulator)
                    {
                        yield return c;
                    }

                    currentClip = null;
                    accumulator.Clear();
                }
            }

            if (currentClip is not null)
            {
                yield return currentClip;
            }

            foreach (var c in accumulator)
            {
                yield return c;
            }
        }

        ClipRenderingContext WithIntersection(ClipRenderingContext? other)
        {
            if (other is null)
            {
                return this;
            }

            var leftX = Math.Max(Position.X, other.Position.X);
            var rightX = Math.Min(Position.X + Size.X, other.Position.X + other.Size.X);
            var topY = Math.Max(Position.Y, other.Position.Y);
            var bottomY = Math.Min(Position.Y + Size.Y, other.Position.Y + other.Size.Y);

            return new ClipRenderingContext(new Vector2(leftX, topY), new Vector2(rightX - leftX, bottomY - topY));
        }

        public override string ToString() => $"Clip: {Size.X}x{Size.Y}";
    }
}
