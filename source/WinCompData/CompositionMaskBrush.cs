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
    sealed class CompositionMaskBrush : CompositionBrush
    {
        public CompositionBrush? Source { get; set; }

        public CompositionBrush? Mask { get; set; }

        internal CompositionMaskBrush()
        {
        }

        public override CompositionObjectType Type => CompositionObjectType.CompositionMaskBrush;
    }
}
