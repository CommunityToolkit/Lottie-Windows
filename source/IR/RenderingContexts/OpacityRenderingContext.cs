// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    abstract class OpacityRenderingContext : RenderingContext
    {
        OpacityRenderingContext()
        {
        }

        public override sealed bool DependsOn(RenderingContext other)
            => other switch
            {
                FillRenderingContext _ => true,
                StrokeRenderingContext _ => true,
                _ => false,
            };

        public override sealed RenderingContext WithOffset(Vector2 offset) => this;

        // Combines the given opacities. We could get more clever by interpolating
        // animations, but for now we'll only multiply with static values.
        public static IEnumerable<OpacityRenderingContext> Combine(IEnumerable<OpacityRenderingContext> items)
        {
            // Partition into static and animated.
            var staticOpacties = items.OfType<Static>().ToArray();
            var animatedOpacties = items.OfType<Animated>().ToArray();

            if (staticOpacties.Length + animatedOpacties.Length == 0)
            {
                yield break;
            }

            var staticOpacity = staticOpacties.Length > 0
                ? staticOpacties.Select(op => op.Opacity).Aggregate((a, b) => a * b)
                : Opacity.Opaque;

            if (animatedOpacties.Length == 0)
            {
                yield return new Static(staticOpacity);
            }
            else
            {
                // Multiply the first opacity by the static opacity.
                yield return new Animated(animatedOpacties[0].Opacity.Select(o => o * staticOpacity));

                // And output the rest unchanged.
                foreach (var opacity in animatedOpacties.Skip(1))
                {
                    yield return opacity;
                }
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
