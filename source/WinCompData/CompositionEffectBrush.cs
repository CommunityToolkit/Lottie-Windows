// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionEffectBrush : CompositionBrush
    {
        CompositionEffectFactory _factory;
        Dictionary<string, CompositionBrush> _sourceParameters = new Dictionary<string, CompositionBrush>();

        internal CompositionEffectBrush(CompositionEffectFactory factory)
        {
            _factory = factory;
        }

        public CompositionBrush GetSourceParameter(string name)
        {
            return _sourceParameters[name];
        }

        public void SetSourceParameter(string name, CompositionBrush source)
        {
            _sourceParameters.Add(name, source);
        }

        public CompositionEffectFactory GetFactory() => _factory;

        public override CompositionObjectType Type => CompositionObjectType.CompositionEffectBrush;
    }
}
