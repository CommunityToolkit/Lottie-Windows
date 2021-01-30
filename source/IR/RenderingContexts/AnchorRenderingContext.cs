// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class AnchorRenderingContext : RenderingContext
    {
        AnchorRenderingContext()
        {
        }

        public static AnchorRenderingContext Create(IAnimatableVector2 anchor)
            => anchor.IsAnimated ? new Animated(anchor) : new Static(anchor.InitialValue);

        public override sealed bool DependsOn(RenderingContext other)
            => other switch
            {
                CenterPointRenderingContext _ => true,
                PositionRenderingContext _ => true,
                RotationRenderingContext _ => true,
                ScaleRenderingContext _ => true,
                _ => false,
            };

        /// <summary>
        /// Converts the anchor to a position offset. <paramref name="selector"/> is called for
        /// each key frame, allowing the caller to translate the anchor value to an offset.
        /// </summary>
        /// <returns>A position offset.</returns>
        internal PositionRenderingContext ToPositionRenderingContext(Func<Vector2, Vector2> selector)
        {
            switch (this)
            {
                case Animated animated:
                    switch (animated.Anchor)
                    {
                        case AnimatableVector2 v2:
                            return new PositionRenderingContext.Animated(v2.Select(selector));
                        case AnimatableXY xy:
                            return new PositionRenderingContext.Animated(xy.Select(x => selector(new Vector2(x, 0)).X, y => selector(new Vector2(0, y)).Y));
                        default: throw Unreachable;
                    }

                case Static st:
                    return new PositionRenderingContext.Static(selector(st.Anchor));

                default: throw Unreachable;
            }
        }

        public sealed class Animated : AnchorRenderingContext
        {
            internal Animated(IAnimatableVector2 anchor) => Anchor = anchor;

            public IAnimatableVector2 Anchor { get; }

            public override bool IsAnimated => true;

            public override RenderingContext WithOffset(Vector2 offset)
            {
                if (offset.X == 0 && offset.Y == 0)
                {
                    return this;
                }

                return new Animated(Anchor.Type switch
                {
                    AnimatableVector2Type.Vector2 => ((AnimatableVector2)Anchor).WithOffset(offset),
                    AnimatableVector2Type.XY => ((AnimatableXY)Anchor).WithOffset(offset),
                    _ => throw Unreachable,
                });
            }

            public override RenderingContext WithTimeOffset(double timeOffset)
                 => new Animated(Anchor.WithTimeOffset(timeOffset));

            public override string ToString() =>
                $"Animated Anchor {Anchor}";
        }

        public sealed class Static : AnchorRenderingContext
        {
            internal Static(Vector2 anchor) => Anchor = anchor;

            public static Static Zero { get; } = new Static(Vector2.Zero);

            public Vector2 Anchor { get; }

            public override bool IsAnimated => false;

            public override RenderingContext WithOffset(Vector2 offset)
                => offset.X == 0 && offset.Y == 0
                    ? this
                    : new Static(Anchor + offset);

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() =>
                $"Static Anchor {Anchor}";
        }
    }
}