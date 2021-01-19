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

        /// <summary>
        /// Replaces any <see cref="AnchorRenderingContext"/>s with <see cref="PositionRenderingContext"/>s
        /// and <see cref="CenterPointRenderingContext"/>s.
        /// </summary>
        /// <returns>A new <see cref="RenderingContext"/> that contains no
        /// <see cref="AnchorRenderingContext"/>s.</returns>
        internal static RenderingContext ReplaceAnchors(RenderingContext context)
        {
            return Compose(Replace(context));

            static IEnumerable<RenderingContext> Replace(RenderingContext context)
            {
                AnchorRenderingContext currentAnchor = new Static(Vector2.Zero);

                foreach (var item in context)
                {
                    switch (item)
                    {
                        case AnchorRenderingContext anchor:
                            // Store the anchor. It scale, and rotation.
                            currentAnchor = anchor;

                            // The anchor moves the position to the inverse of the anchor
                            // (i.e. the anchor is the point that is positioned by the position).
                            yield return currentAnchor.ToPositionRenderingContext(value => Vector2.Zero - value);
                            break;

                        case RotationRenderingContext.Animated rotation:
                            switch (currentAnchor)
                            {
                                case Animated animatedAnchor:
                                    // Animated anchor and animated rotation. Use an animated centerpoint.
                                    yield return new CenterPointRenderingContext.Animated(animatedAnchor.Anchor);
                                    yield return item;

                                    // Reset the centerpoint.
                                    yield return new CenterPointRenderingContext.Static(Vector2.Zero);
                                    break;
                                case Static staticAnchor:
                                    // Static anchor and animated rotation.
                                    yield return new PositionRenderingContext.Animated(new AnimatableVector2(rotation.Rotation.Select(r =>
                                        r.RotatePointAroundOrigin(point: Vector2.Zero, origin: staticAnchor.Anchor)).KeyFrames));
                                    yield return item;
                                    break;

                                default: throw Unreachable;
                            }

                            break;

                        case RotationRenderingContext.Static rotation:
                            // Static anchor and static rotation.
                            yield return currentAnchor.ToPositionRenderingContext(
                                value => rotation.Rotation.RotatePointAroundOrigin(point: Vector2.Zero, origin: value));
                            yield return item;
                            break;

                        case ScaleRenderingContext.Animated scale:
                            switch (currentAnchor)
                            {
                                case Animated animatedAnchor:
                                    // Animated anchor and animated scale. Use an animated centerpoint.
                                    yield return new CenterPointRenderingContext.Animated(animatedAnchor.Anchor);
                                    yield return item;

                                    // Reset the centerpoint.
                                    yield return new CenterPointRenderingContext.Static(Vector2.Zero);
                                    break;
                                case Static staticAnchor:
                                    // Static anchor and animated scale.
                                    switch (scale.ScalePercent)
                                    {
                                        case AnimatableVector2 scale2:
                                            yield return new PositionRenderingContext.Animated(new AnimatableVector2(scale2.Select(s =>
                                                ScaleRenderingContext.ScalePointAroundOrigin(point: Vector2.Zero, origin: staticAnchor.Anchor, scale: s / 100)).KeyFrames));
                                            break;

                                        case AnimatableXY scaleXY:
                                            // The scale has X and Y animated separately.
                                            yield return new PositionRenderingContext.Animated(scaleXY.Select(
                                                    sX => ScaleRenderingContext.ScalePointAroundOrigin(point: Vector2.Zero, origin: staticAnchor.Anchor, scale: new Vector2(sX / 100, 0)).X,
                                                    sY => ScaleRenderingContext.ScalePointAroundOrigin(point: Vector2.Zero, origin: staticAnchor.Anchor, scale: new Vector2(0, sY / 100)).Y));
                                            break;

                                        default: throw Unreachable;
                                    }

                                    yield return item;
                                    break;

                                default: throw Unreachable;
                            }

                            break;

                        case ScaleRenderingContext.Static scale:
                            yield return currentAnchor.ToPositionRenderingContext(
                                value => ScaleRenderingContext.ScalePointAroundOrigin(point: Vector2.Zero, origin: value, scale: scale.ScalePercent / 100));
                            yield return item;
                            break;

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