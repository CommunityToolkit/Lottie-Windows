// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class AnchorRenderingContext : RenderingContext
    {
        AnchorRenderingContext()
        {
        }

        public static AnchorRenderingContext Create(IAnimatableVector3 anchor)
            => anchor.IsAnimated ? new Animated(anchor) : new Static(anchor.InitialValue);

        // Removes all the static AnchorRenderingContexts by adjusting the subsequent
        // position, scale, and rotations.
        public static RenderingContext WithoutRedundants(RenderingContext context)
        {
            Debug.Assert(context.IsFlattened, "Precondition");

            var anchor = Vector3.Zero;

            var accumulator = new List<RenderingContext>();

            var index = 0;
            foreach (var subContext in context.SubContexts)
            {
                index++;

                switch (subContext)
                {
                    case Static anchorContext:
                        anchor = anchorContext.Anchor;
                        break;

                    case Animated _:
                        // We don't currently handle animation. Return what
                        // we have so far with the rest unchanged.
                        return Compose(accumulator) + Compose(context.SubContexts.Skip(index - 1));

                    case PositionRenderingContext position:
                        accumulator.Add(position.WithOffset(Vector3.Zero - anchor));
                        break;

                    case RotationRenderingContext rotation:
                        accumulator.Add(rotation.WithOffset(anchor));
                        break;

                    case ScaleRenderingContext scale:
                        accumulator.Add(scale.WithOffset(anchor));
                        break;

                    default:
                        accumulator.Add(subContext);
                        break;
                }
            }

            return Compose(accumulator);
        }

        public sealed class Animated : AnchorRenderingContext
        {
            internal Animated(IAnimatableVector3 anchor) => Anchor = anchor;

            public IAnimatableVector3 Anchor { get; }

            public override bool IsAnimated => true;

            public override RenderingContext WithOffset(Vector3 offset)
            {
                if (offset.X == 0 && offset.Y == 0)
                {
                    return this;
                }

                return new Animated(Anchor.Type switch
                {
                    AnimatableVector3Type.Vector3 => ((AnimatableVector3)Anchor).WithOffset(offset),
                    AnimatableVector3Type.XYZ => ((AnimatableXYZ)Anchor).WithOffset(offset),
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
            internal Static(Vector3 anchor) => Anchor = anchor;

            public Vector3 Anchor { get; }

            public override bool IsAnimated => false;

            public override RenderingContext WithOffset(Vector3 offset)
                => offset.X == 0 && offset.Y == 0
                    ? this
                    : new Static(Anchor + offset);

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() =>
                $"Static Anchor {Anchor}";
        }
    }
}