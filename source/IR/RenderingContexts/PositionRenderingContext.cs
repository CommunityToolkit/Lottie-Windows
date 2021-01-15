// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class PositionRenderingContext : RenderingContext
    {
        PositionRenderingContext()
        {
        }

        public static PositionRenderingContext Create(IAnimatableVector3 position)
            => position.IsAnimated ? new Animated(position) : new Static(position.InitialValue);

        public static RenderingContext WithoutRedundants(RenderingContext context)
            => context.Filter((Static c) => c.Position.X != 0 || c.Position.Y != 0);

        public sealed class Animated : PositionRenderingContext
        {
            internal Animated(IAnimatableVector3 position)
                => Position = position;

            public IAnimatableVector3 Position { get; }

            public override RenderingContext WithOffset(Vector3 offset)
            {
                if (offset.X == 0 && offset.Y == 0)
                {
                    return this;
                }

                return new Animated(Position.Type switch
                {
                    AnimatableVector3Type.Vector3 => ((AnimatableVector3)Position).WithOffset(offset),
                    AnimatableVector3Type.XYZ => ((AnimatableXYZ)Position).WithOffset(offset),
                    _ => throw new InvalidOperationException(),
                });
            }

            public override RenderingContext WithTimeOffset(double timeOffset)
                => new Animated(Position.WithTimeOffset(timeOffset));

            public override bool IsAnimated => true;

            public override string ToString() => $"Animated Position {Position}";
        }

        public sealed class Static : PositionRenderingContext
        {
            internal Static(Vector3 position)
                => Position = position;

            public Vector3 Position { get; }

            public override bool IsAnimated => false;

            public override RenderingContext WithOffset(Vector3 offset)
                => offset.X == 0 && offset.Y == 0
                    ? this
                    : new Static(Position + offset);

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() => $"Static Position {Position}";
        }
    }
}
