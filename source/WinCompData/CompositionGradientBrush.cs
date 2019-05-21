// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionGradientBrush : CompositionBrush
    {
        private protected CompositionGradientBrush()
        {
        }

        public IList<CompositionColorGradientStop> ColorStops { get; } = new ListOfNeverNull<CompositionColorGradientStop>();
    }
}
