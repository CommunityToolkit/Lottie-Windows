// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class DrawingDirectionRenderingContext : RenderingContext
    {
        public DrawingDirectionRenderingContext(bool isDrawingReversed)
            => IsDrawingReversed = isDrawingReversed;

        public bool IsDrawingReversed { get; }

        public override string ToString() => $"IsDrawingReversed {IsDrawingReversed}";
    }
}