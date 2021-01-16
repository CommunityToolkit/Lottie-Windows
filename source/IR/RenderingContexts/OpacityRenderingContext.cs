// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class OpacityRenderingContext : RenderingContext
    {
        OpacityRenderingContext()
        {
        }

        public override sealed RenderingContext WithOffset(Vector3 offset) => this;

        public static RenderingContext WithoutRedundants(RenderingContext context)
            => context.SubContexts.Count > 0
                ? Compose(MoveOpacitiesUp(context.SubContexts))
                : context;

        static IEnumerable<RenderingContext> MoveOpacitiesUp(IReadOnlyList<RenderingContext> items)
        {
            var accumulator = new List<RenderingContext>(items.Count);
            var opacitiesAccumulator = new List<OpacityRenderingContext>();
            var timeOffset = 0.0;

            foreach (var item in items)
            {
                switch (item)
                {
                    case OpacityRenderingContext opacity:
                        opacitiesAccumulator.Add((OpacityRenderingContext)opacity.WithTimeOffset(timeOffset));
                        break;

                    case TimeOffsetRenderingContext t:
                        timeOffset += t.TimeOffset;
                        goto default;

                    default:
                        accumulator.Add(item);
                        break;
                }
            }

            foreach (var item in Combine(opacitiesAccumulator))
            {
                yield return item;
            }

            foreach (var item in accumulator)
            {
                yield return item;
            }
        }

        // Combines the given opacities. We could get more clever by interpolating
        // animations, but for now we'll only multiple with static values.
        static IEnumerable<OpacityRenderingContext> Combine(IEnumerable<OpacityRenderingContext> items)
        {
            Static? current = null;

            foreach (var item in items)
            {
                switch (item)
                {
                    case Animated a:
                        if (current != null)
                        {
                            yield return new Animated(a.Opacity.Select(o => o * current.Opacity));
                            current = null;
                        }
                        else
                        {
                            yield return item;
                        }

                        break;
                    case Static s:
                        if (current is null)
                        {
                            current = s;
                        }
                        else
                        {
                            current = new Static(current.Opacity * s.Opacity);
                        }

                        break;

                    default:
                        throw Unreachable;
                }
            }

            if (current != null && current.Opacity != Opacity.Opaque)
            {
                yield return current;
            }
        }

        public static OpacityRenderingContext Create(Animatable<Opacity> opacity)
            => opacity.IsAnimated ? new Animated(opacity) : new Static(opacity.InitialValue);

        public sealed class Animated : OpacityRenderingContext
        {
            internal Animated(Animatable<Opacity> opacity)
                => Opacity = opacity;

            public Animatable<Opacity> Opacity { get; }

            public override bool IsAnimated => true;

            public override RenderingContext WithTimeOffset(double timeOffset)
                => new Animated(Opacity.WithTimeOffset(timeOffset));

            public override string ToString() => $"Animated Opacity {Opacity}";
        }

        public sealed class Static : OpacityRenderingContext
        {
            internal Static(Opacity opacity)
                => Opacity = opacity;

            public Opacity Opacity { get; }

            public override bool IsAnimated => false;

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() => $"Static Opacity {Opacity}";
        }
    }
}
