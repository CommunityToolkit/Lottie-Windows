// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgc;

namespace CommunityToolkit.WinUI.Lottie.WinCompData.Mgce
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositeEffect : GraphicsEffectBase
    {
        public CanvasComposite Mode { get; set; }

        public IList<CompositionEffectSourceParameter> Sources { get; } = new List<CompositionEffectSourceParameter>();

        public override GraphicsEffectType Type => GraphicsEffectType.CompositeEffect;
    }
}
