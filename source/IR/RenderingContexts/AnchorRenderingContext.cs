// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class AnchorRenderingContext : RenderingContext
    {
        public AnchorRenderingContext(IAnimatableVector3 anchor)
            => Anchor = anchor;

        public IAnimatableVector3 Anchor { get; }

        public override string ToString() => $"Anchor {Anchor}";
    }
}