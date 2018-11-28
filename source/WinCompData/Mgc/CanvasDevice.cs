// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgc
{
#if !WINDOWS_UWP
    public
#endif
    sealed class CanvasDevice : IDisposable
    {
        public static CanvasDevice GetSharedDevice() => new CanvasDevice();

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
