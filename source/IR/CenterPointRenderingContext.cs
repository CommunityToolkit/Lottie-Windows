// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class CenterPointRenderingContext : RenderingContext
    {
        CenterPointRenderingContext()
        {
        }

        public static CenterPointRenderingContext Create(IAnimatableVector2 centerPoint)
            => centerPoint.IsAnimated ? new Animated(centerPoint) : new Static(centerPoint.InitialValue);

        public static RenderingContext WithoutRedundants(RenderingContext context)
        {
            if (context.SubContexts.Count == 0)
            {
                return context;
            }

            CenterPointRenderingContext? currentCenterPoint = null;

            // Once we see a center point, hold onto it, and accumulate contexts until
            // a rotation, scale, or centerpoint is seen. If a subsequent center point
            // is seen without any intervening rotation or scale, don't output the
            // centerpoint.
            var accumulator = new List<RenderingContext>(context.SubContexts.Count / 2);

            var centerPointWasOutput = false;

            foreach (var subContext in context.SubContexts)
            {
                switch (subContext)
                {
                    case CenterPointRenderingContext centerPoint:
                        currentCenterPoint = centerPoint;
                        centerPointWasOutput = false;
                        break;

                    case RotationRenderingContext bar:
                    case ScaleRenderingContext foo:
                        if (currentCenterPoint is not null && !centerPointWasOutput)
                        {
                            accumulator.Add(currentCenterPoint);
                            centerPointWasOutput = true;
                        }

                        goto default;

                    default:
                        accumulator.Add(subContext);
                        break;
                }
            }

            return Compose(accumulator);
        }

        public static RenderingContext WithCenterPointInsteadOfAnchor(RenderingContext context)
        {
            if (context.SubContexts.Count == 0)
            {
                return context;
            }

            return Compose(ReplaceAnchorWithCenterPoint(context.SubContexts));
        }

        static IEnumerable<RenderingContext> ReplaceAnchorWithCenterPoint(IEnumerable<RenderingContext> subContexts)
        {
            AnchorRenderingContext? currentAnchor = null;
            var centerPointIsCurrent = false;

            foreach (var subContext in subContexts)
            {
                switch (subContext)
                {
                    case AnchorRenderingContext anchor:
                        currentAnchor = anchor;
                        centerPointIsCurrent = false;
                        break;

                    case PositionRenderingContext.Animated position:
                        if (currentAnchor is null)
                        {
                            yield return position;
                        }
                        else
                        {
                            // Adjust the position.
                            switch (currentAnchor)
                            {
                                case AnchorRenderingContext.Animated _:
                                    throw TODO;

                                case AnchorRenderingContext.Static _:
                                    yield return position.WithOffset(Vector2.Zero - ((AnchorRenderingContext.Static)currentAnchor).Anchor);
                                    break;

                                default: throw Unreachable;
                            }
                        }

                        break;

                    case PositionRenderingContext.Static position:
                        if (currentAnchor is null)
                        {
                            yield return position;
                        }
                        else
                        {
                            // Adjust the position.
                            switch (currentAnchor)
                            {
                                case AnchorRenderingContext.Animated _:
                                    yield return PositionRenderingContext.Create(((AnchorRenderingContext.Animated)currentAnchor.WithOffset(Vector2.Zero - position.Position)).Anchor);
                                    break;

                                case AnchorRenderingContext.Static _:
                                    yield return position.WithOffset(Vector2.Zero - ((AnchorRenderingContext.Static)currentAnchor).Anchor);
                                    break;

                                default: throw Unreachable;
                            }
                        }

                        break;

                    case RotationRenderingContext rotation:
                        if (currentAnchor is not null && !centerPointIsCurrent)
                        {
                            // Ensure a center point is in the context.
                            switch (currentAnchor)
                            {
                                case AnchorRenderingContext.Animated _:
                                    yield return new Animated(((AnchorRenderingContext.Animated)currentAnchor).Anchor);
                                    break;

                                case AnchorRenderingContext.Static _:
                                    yield return new Static(((AnchorRenderingContext.Static)currentAnchor).Anchor);
                                    break;

                                default: throw Unreachable;
                            }

                            centerPointIsCurrent = true;
                        }

                        yield return rotation;
                        break;

                    case ScaleRenderingContext scale:
                        if (currentAnchor is not null && !centerPointIsCurrent)
                        {
                            // Ensure a center point is in the context.
                            switch (currentAnchor)
                            {
                                case AnchorRenderingContext.Animated _:
                                    yield return new Animated(((AnchorRenderingContext.Animated)currentAnchor).Anchor);
                                    break;

                                case AnchorRenderingContext.Static _:
                                    yield return new Static(((AnchorRenderingContext.Static)currentAnchor).Anchor);
                                    break;

                                default: throw Unreachable;
                            }

                            centerPointIsCurrent = true;
                        }

                        yield return scale;
                        break;

                    default:
                        yield return subContext;
                        break;
                }
            }
        }

        public sealed class Animated : CenterPointRenderingContext
        {
            internal Animated(IAnimatableVector2 centerPoint) => CenterPoint = centerPoint;

            public IAnimatableVector2 CenterPoint { get; }

            public override bool IsAnimated => true;

            public override RenderingContext WithOffset(Vector2 offset)
            {
                if (offset.X == 0 && offset.Y == 0)
                {
                    return this;
                }

                return new Animated(CenterPoint.Type switch
                {
                    AnimatableVector2Type.Vector2 => ((AnimatableVector2)CenterPoint).WithOffset(offset),
                    AnimatableVector2Type.XY => ((AnimatableXY)CenterPoint).WithOffset(offset),
                    _ => throw Unreachable,
                });
            }

            public override RenderingContext WithTimeOffset(double timeOffset)
                 => new Animated(CenterPoint.WithTimeOffset(timeOffset));

            public override string ToString() =>
                $"Animated CenterPoint {CenterPoint}";
        }

        public sealed class Static : CenterPointRenderingContext
        {
            internal Static(Vector2 centerPoint) => CenterPoint = centerPoint;

            public Vector2 CenterPoint { get; }

            public override bool IsAnimated => false;

            public override RenderingContext WithOffset(Vector2 offset)
                => offset.X == 0 && offset.Y == 0
                    ? this
                    : new Static(CenterPoint + offset);

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() =>
                $"Static CenterPoint {CenterPoint}";
        }
    }
}