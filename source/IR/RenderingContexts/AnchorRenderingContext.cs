﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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