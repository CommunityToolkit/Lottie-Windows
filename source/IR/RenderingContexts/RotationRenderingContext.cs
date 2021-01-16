// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class RotationRenderingContext : RenderingContext
    {
        RotationRenderingContext()
        {
        }

        public static RenderingContext WithoutRedundants(RenderingContext context)
            => context.Filter((Static c) => c.Rotation != Rotation.None);

        public static RotationRenderingContext Create(Animatable<Rotation> rotation)
            => Create(rotation, new Animatable<Vector2>(Vector2.Zero));

        public static RotationRenderingContext Create(Animatable<Rotation> rotation, Animatable<Vector2> centerPoint)
            => rotation.IsAnimated || centerPoint.IsAnimated ? new Animated(rotation, centerPoint) : new Static(rotation.InitialValue, centerPoint.InitialValue);

        public sealed class Animated : RotationRenderingContext
        {
            internal Animated(Animatable<Rotation> rotation, Animatable<Vector2> centerPoint)
                => (Rotation, CenterPoint) = (rotation, centerPoint);

            public Animatable<Vector2> CenterPoint { get; }

            public Animatable<Rotation> Rotation { get; }

            public override bool IsAnimated => true;

            public override RenderingContext WithOffset(Vector3 offset)
                => offset == Vector3.Zero
                    ? this
                    : new Animated(Rotation, CenterPoint.Select(c => c + offset));

            public override RenderingContext WithTimeOffset(double timeOffset) =>
                new Animated(Rotation.WithTimeOffset(timeOffset), CenterPoint.WithTimeOffset(timeOffset));

            public override string ToString() =>
                $"Animated Rotation {Rotation} around {CenterPoint}";
        }

        public sealed class Static : RotationRenderingContext
        {
            internal Static(Rotation rotation, Vector2 centerPoint)
                => (Rotation, CenterPoint) = (rotation, centerPoint);

            public Vector2 CenterPoint { get; }

            public Rotation Rotation { get; }

            public override bool IsAnimated => false;

            public override RenderingContext WithOffset(Vector3 offset) =>
                offset == Vector3.Zero
                    ? this
                    : new Static(Rotation, CenterPoint + offset);

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() =>
                $"Static Rotation {Rotation} around {CenterPoint}";
        }
    }
}
