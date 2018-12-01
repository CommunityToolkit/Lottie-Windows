// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionEasingFunction : CompositionObject
    {
        // all protected private should be private protected.
        protected private CompositionEasingFunction()
        {
        }
    }
}
