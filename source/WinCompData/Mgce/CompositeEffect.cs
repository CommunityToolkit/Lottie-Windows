// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgc;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositeEffect : GraphicsEffectBase
    {
        public CompositeEffect(CanvasComposite mode, IList<CompositionEffectSourceParameter> sources)
        {
            Mode = mode;
            _sources = sources;
        }

        public CanvasComposite Mode { get; set; }

        private IList<CompositionEffectSourceParameter> _sources = new List<CompositionEffectSourceParameter>();

        public override IList<CompositionEffectSourceParameter> Sources => _sources;

        public override GraphicsEffectType Type => GraphicsEffectType.CompositeEffect;
    }
}
