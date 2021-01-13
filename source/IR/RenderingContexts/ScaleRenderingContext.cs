// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class ScaleRenderingContext : RenderingContext
    {
        internal ScaleRenderingContext(IAnimatableVector3 scalePercent)
            => ScalePercent = scalePercent;

        public IAnimatableVector3 ScalePercent { get; }

        public override string ToString() => $"Scale {ScalePercent}";
    }
}
