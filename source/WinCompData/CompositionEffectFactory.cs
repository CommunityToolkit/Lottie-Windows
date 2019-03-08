// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionEffectFactory
    {
        readonly GraphicsEffectBase _effect;

        internal CompositionEffectFactory(GraphicsEffectBase effect)
        {
            _effect = effect;
        }

        public CompositionEffectBrush CreateBrush()
        {
            return new CompositionEffectBrush(this);
        }

        public GraphicsEffectBase GetEffect() => _effect;
    }
}
