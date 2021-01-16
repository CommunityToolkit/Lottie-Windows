// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class ScaleRenderingContext : RenderingContext
    {
        ScaleRenderingContext()
        {
        }

        public static RenderingContext WithoutRedundants(RenderingContext context)
            => context.Filter((Static c) => c.ScalePercent.X != 100 || c.ScalePercent.Y != 100);

        public static ScaleRenderingContext Create(IAnimatableVector3 scalePercent)
            => scalePercent.IsAnimated
                ? new Animated(scalePercent, new Animatable<Vector2>(Vector2.Zero))
                : new Static(scalePercent.InitialValue, Vector2.Zero);

        public static ScaleRenderingContext Create(IAnimatableVector3 scalePercent, Animatable<Vector2> centerPoint)
            => scalePercent.IsAnimated || centerPoint.IsAnimated
                ? new Animated(scalePercent, centerPoint)
                : new Static(scalePercent.InitialValue, centerPoint.InitialValue);

        public sealed class Animated : ScaleRenderingContext
        {
            internal Animated(IAnimatableVector3 scalePercent, Animatable<Vector2> centerPoint)
                => (ScalePercent, CenterPoint) = (scalePercent, centerPoint);

            public Animatable<Vector2> CenterPoint { get; }

            public IAnimatableVector3 ScalePercent { get; }

            public override bool IsAnimated => true;

            public override RenderingContext WithOffset(Vector3 offset)
                => offset == Vector3.Zero
                    ? this
                    : new Animated(ScalePercent, CenterPoint.Select(c => c + offset));

            public override RenderingContext WithTimeOffset(double timeOffset)
                => new Animated(ScalePercent.WithTimeOffset(timeOffset), CenterPoint.WithTimeOffset(timeOffset));

            public override string ToString() =>
                $"Animated Scale {ScalePercent} around {CenterPoint}";
        }

        public sealed class Static : ScaleRenderingContext
        {
            internal Static(Vector3 scalePercent, Vector2 centerPoint)
                => (ScalePercent, CenterPoint) = (scalePercent, centerPoint);

            public Vector2 CenterPoint { get; }

            public Vector3 ScalePercent { get; }

            public override bool IsAnimated => false;

            public override RenderingContext WithOffset(Vector3 offset) =>
                offset == Vector3.Zero
                    ? this
                    : new Static(ScalePercent, CenterPoint + offset);

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() =>
                $"Static Scale {ScalePercent} around {CenterPoint}";
        }
    }
}
