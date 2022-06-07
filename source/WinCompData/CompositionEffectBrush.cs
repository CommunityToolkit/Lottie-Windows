// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionEffectBrush : CompositionBrush
    {
        readonly CompositionEffectFactory _effectFactory;
        readonly Dictionary<string, CompositionBrush> _sourceParameters = new Dictionary<string, CompositionBrush>();

        internal CompositionEffectBrush(CompositionEffectFactory effectFactory)
        {
            _effectFactory = effectFactory;
        }

        public CompositionBrush GetSourceParameter(string name) => _sourceParameters[name];

        public void SetSourceParameter(string name, CompositionBrush source)
        {
            _sourceParameters.Add(name, source);
        }

        public CompositionEffectFactory GetEffectFactory() => _effectFactory;

        public override CompositionObjectType Type => CompositionObjectType.CompositionEffectBrush;
    }
}
