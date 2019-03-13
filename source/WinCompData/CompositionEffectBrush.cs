// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionEffectBrush : CompositionBrush
    {
        readonly GraphicsEffectBase _effect;
        readonly Dictionary<string, CompositionBrush> _sourceParameters = new Dictionary<string, CompositionBrush>();

        internal CompositionEffectBrush(GraphicsEffectBase effect)
        {
            _effect = effect;
        }

        public CompositionBrush GetSourceParameter(string name) => _sourceParameters[name];

        public void SetSourceParameter(string name, CompositionBrush source)
        {
            _sourceParameters.Add(name, source);
        }

        public GraphicsEffectBase GetEffect() => _effect;

        public override CompositionObjectType Type => CompositionObjectType.CompositionEffectBrush;
    }
}
