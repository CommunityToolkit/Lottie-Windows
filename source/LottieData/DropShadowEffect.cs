// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// A drop shadow effect.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class DropShadowEffect : Effect
    {
        internal DropShadowEffect(string name)
            : base(name)
        {
        }

        public override EffectType Type => EffectType.DropShadow;
    }
}
