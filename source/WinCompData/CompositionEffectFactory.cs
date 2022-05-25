// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionEffectFactory : CompositionObject
    {
        readonly GraphicsEffectBase _effect;

        internal CompositionEffectFactory(GraphicsEffectBase effect)
        {
            _effect = effect;
        }

        public CompositionEffectBrush CreateBrush()
        {
            return new CompositionEffectBrush(_effect);
        }

        public GraphicsEffectBase GetEffect() => _effect;

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionEffectFactory;
    }
}
