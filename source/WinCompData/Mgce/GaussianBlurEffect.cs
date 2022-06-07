// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class GaussianBlurEffect : GraphicsEffectBase
    {
        // Default is 3.0.
        public float? BlurAmount { get; set; }

        public CompositionEffectSourceParameter? Source { get; set; }

        public override IList<CompositionEffectSourceParameter> Sources => Source is null ? new List<CompositionEffectSourceParameter>() : new List<CompositionEffectSourceParameter>() { Source! };

        public override GraphicsEffectType Type => GraphicsEffectType.GaussianBlurEffect;
    }
}
