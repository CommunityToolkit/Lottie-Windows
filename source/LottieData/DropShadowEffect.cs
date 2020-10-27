// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// An effect applied to a layer.
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
