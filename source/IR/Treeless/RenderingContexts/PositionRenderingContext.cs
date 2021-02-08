﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless.RenderingContexts
{
    sealed class PositionRenderingContext : RenderingContext
    {
        internal PositionRenderingContext(IAnimatableVector3 position)
            => Position = position;

        public IAnimatableVector3 Position { get; }

        public override string ToString() => $"Postion {Position}";
    }
}
