// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class MasksRenderingContext : RenderingContext
    {
        public MasksRenderingContext(IReadOnlyList<Mask> masks)
            => Masks = masks;

        public IReadOnlyList<Mask> Masks { get; }

        public override bool IsAnimated => Masks.Any(item => item.IsAnimated);

        // This needs implementing!
        public override sealed RenderingContext WithOffset(Vector2 offset) => throw new InvalidOperationException();

        public override RenderingContext WithTimeOffset(double timeOffset)
            => IsAnimated
                ? new MasksRenderingContext(Masks.Select(item => item.WithTimeOffset(timeOffset)).ToArray())
                : this;

        public override string ToString() => $"Mask {Masks}";
    }
}