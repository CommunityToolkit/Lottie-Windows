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

        public sealed override RenderingContext WithOffset(Vector3 offset) => this;

        public static RenderingContext WithoutRedundants(RenderingContext context)
            => context.Filter((Static c) => c.Rotation != Rotation.None);

        public static RotationRenderingContext Create(Animatable<Rotation> rotation)
            => rotation.IsAnimated ? new Animated(rotation) : new Static(rotation.InitialValue);

        public sealed class Animated : RotationRenderingContext
        {
            internal Animated(Animatable<Rotation> rotation)
                => Rotation = rotation;

            public Animatable<Rotation> Rotation { get; }

            public override bool IsAnimated => true;

            public override RenderingContext WithTimeOffset(double timeOffset) =>
                new Animated(Rotation.WithTimeOffset(timeOffset));

            public override string ToString() =>
                $"Animated Rotation {Rotation}";
        }

        public sealed class Static : RotationRenderingContext
        {
            internal Static(Rotation rotation)
                => Rotation = rotation;

            public Rotation Rotation { get; }

            public override bool IsAnimated => false;

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() =>
                $"Static Rotation {Rotation}";
        }
    }
}
