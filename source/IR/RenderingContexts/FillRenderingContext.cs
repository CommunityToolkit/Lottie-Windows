// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class FillRenderingContext : RenderingContext
    {
        public FillRenderingContext(Brush brush)
            => Brush = brush;

        public Brush Brush { get; }

        public override bool IsAnimated => Brush.IsAnimated;

        public override sealed RenderingContext WithOffset(Vector2 offset) => this;

        public override RenderingContext WithTimeOffset(double timeOffset)
            => IsAnimated
                ? new FillRenderingContext(Brush.WithTimeOffset(timeOffset))
                : this;

        public override string ToString() => $"Fill: {Brush}";
    }
}