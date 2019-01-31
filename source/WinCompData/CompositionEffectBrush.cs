// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    class CompositionEffectBrush : CompositionBrush
    {
        readonly Dictionary<string, CompositionBrush> _sourceParameters = new Dictionary<string, CompositionBrush>();

        internal CompositionEffectBrush(Mgce.CompositeEffect effect)
        {
            this.Effect = effect;
        }

        public Dictionary<string, CompositionBrush> SourceParameters => _sourceParameters;

        public Mgce.CompositeEffect Effect { get; }

        public override CompositionObjectType Type => CompositionObjectType.CompositionEffectBrush;
    }
}
