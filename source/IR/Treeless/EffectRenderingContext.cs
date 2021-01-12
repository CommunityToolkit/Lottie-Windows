// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless
{
    sealed class EffectRenderingContext : RenderingContext
    {
        internal EffectRenderingContext(Effect effect) => Effect = effect;

        public Effect Effect { get; }

        public override string ToString() => $"Effect: {Effect}";
    }
}