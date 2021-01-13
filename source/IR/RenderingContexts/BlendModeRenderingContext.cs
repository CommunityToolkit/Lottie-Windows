// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class BlendModeRenderingContext : RenderingContext
    {
        internal BlendModeRenderingContext(BlendMode blendMode) => BlendMode = blendMode;

        public BlendMode BlendMode { get; }

        public override string ToString() => $"BlendMode {BlendMode}";
    }
}