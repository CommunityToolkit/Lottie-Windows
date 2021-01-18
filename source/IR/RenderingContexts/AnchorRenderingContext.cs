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

        internal static RenderingContext ReplaceAnchorsWithTranslations(RenderingContext context)
        {
            return context.SubContexts.Count == 0 ? context : Compose(Replace(context));

            static IEnumerable<RenderingContext> Replace(RenderingContext context)
            {
                AnchorRenderingContext currentAnchor = new Static(Vector2.Zero);

                foreach (var item in context.SubContexts)
                {
                    switch (item)
                    {
                        case AnchorRenderingContext anchor:
                            currentAnchor = anchor;
                            break;

                        case PositionRenderingContext position:
                            yield return currentAnchor.ToPositionRenderingContext(value => Vector2.Zero - value);
                            goto default;

                        case RotationRenderingContext.Animated rotation:
                            // If the rotation is animated and the anchor is animated then this is a lot more complicated.
                            // For now let's only handle static.
                            // Should just need to output 2 position contexts.
                            throw TODO;
                        case RotationRenderingContext.Static rotation:
                            yield return currentAnchor.ToPositionRenderingContext(value => RotationRenderingContext.RotatePointAroundOrigin(Vector2.Zero, value, rotation.Rotation.Radians));
                            goto default;

                        case ScaleRenderingContext.Animated scale:
                            // If the rotation is animated and the anchor is animated then this is a lot more complicated.
                            // For now let's only handle static.
                            // Should just need to output 2 position contexts.
                            throw TODO;

                        case ScaleRenderingContext.Static scale:
                            // todo - do this to each item in the anchor.
                            yield return currentAnchor.ToPositionRenderingContext(value => ScaleRenderingContext.ScalePointAroundOrigin(Vector2.Zero, value, scale.ScalePercent / 100));
                            goto default;

                        default:
                            yield return item;
                            break;
                    }
                }
            }
        }

        PositionRenderingContext ToPositionRenderingContext(Func<Vector2, Vector2> selector)
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