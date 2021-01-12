// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless.RenderingContexts
{
    sealed class VisibilityRenderingContext : RenderingContext
    {
        public bool IsHidden { get; set; }

        public double InPoint { get; set; }

        public double OutPoint { get; set; }

        public override string ToString()
            => IsHidden
                ? "Hidden"
                : $"Visible {InPoint}->{OutPoint}";
    }
}
