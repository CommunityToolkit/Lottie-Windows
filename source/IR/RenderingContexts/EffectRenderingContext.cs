// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class EffectRenderingContext : RenderingContext
    {
        internal EffectRenderingContext(Effect effect) => Effect = effect;

        public Effect Effect { get; }

        // For now assume true. Need to look at whether the effect is animated.
        public override bool IsAnimated => true;

        public override sealed bool DependsOn(RenderingContext other) => true;

        // This needs implementing!
        public override sealed RenderingContext WithOffset(Vector2 offset) => throw new InvalidOperationException();

        // This needs implementing!
        public override RenderingContext WithTimeOffset(double timeOffset) => throw new InvalidOperationException();

        public override string ToString() => $"Effect: {Effect}";
    }
}