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