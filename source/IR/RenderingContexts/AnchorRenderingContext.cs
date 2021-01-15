// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class AnchorRenderingContext : RenderingContext
    {
        AnchorRenderingContext()
        {
        }

        public static RenderingContext WithoutRedundants(RenderingContext context)
            => context.Filter((Static c) => c.Anchor.X != 0 || c.Anchor.Y != 0);

        public static AnchorRenderingContext Create(IAnimatableVector3 anchor)
            => anchor.IsAnimated ? new Animated(anchor) : new Static(anchor.InitialValue);

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
                    _ => throw new InvalidOperationException(),
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