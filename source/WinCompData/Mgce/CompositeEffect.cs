// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgc;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositeEffect : GraphicsEffectBase
    {
        public CanvasComposite Mode { get; set; }

        public IList<CompositionEffectSourceParameter> Sources { get; } = new ListOfNeverNull<CompositionEffectSourceParameter>();

        public override GraphicsEffectType Type => GraphicsEffectType.CompositeEffect;
    }
}
