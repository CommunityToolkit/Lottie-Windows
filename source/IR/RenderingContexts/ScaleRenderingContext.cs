// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class ScaleRenderingContext : RenderingContext
    {
        ScaleRenderingContext()
        {
        }

        public sealed override RenderingContext WithOffset(Vector2 offset) => this;

        public static RenderingContext WithoutRedundants(RenderingContext context)
            => context.Filter((Static c) => c.ScalePercent.X != 100 || c.ScalePercent.Y != 100);

        public static ScaleRenderingContext Create(IAnimatableVector2 scalePercent)
            => scalePercent.IsAnimated
                ? new Animated(scalePercent)
                : new Static(scalePercent.InitialValue);

        /// <summary>
        /// Returns the result of scaling <paramref name="point"/> by <paramref name="scale"/> around
        /// <paramref name="origin"/>.
        /// </summary>
        /// <returns>The resulting point.</returns>
        internal static Vector2 ScalePointAroundOrigin(Vector2 point, Vector2 origin, Vector2 scale)
        {
            var centerPoint = new Vector2(point.X - origin.X, point.Y - origin.Y);
            return centerPoint * scale;
        }

        public sealed class Animated : ScaleRenderingContext
        {
            internal Animated(IAnimatableVector2 scalePercent)
                => ScalePercent = scalePercent;

            public IAnimatableVector2 ScalePercent { get; }

            public override bool IsAnimated => true;

            public override RenderingContext WithTimeOffset(double timeOffset)
                => new Animated(ScalePercent.WithTimeOffset(timeOffset));

            public override string ToString() =>
                $"Animated Scale {ScalePercent}";
        }

        public sealed class Static : ScaleRenderingContext
        {
            internal Static(Vector2 scalePercent)
                => ScalePercent = scalePercent;

            public Vector2 ScalePercent { get; }

            public override bool IsAnimated => false;

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() =>
                $"Static Scale {ScalePercent}";
        }
    }
}
