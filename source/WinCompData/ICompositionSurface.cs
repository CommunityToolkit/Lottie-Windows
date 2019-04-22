// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    interface ICompositionSurface
    {
        /// <summary>
        /// The name of the concrete type of the implementation.
        /// </summary>
        string TypeName { get; }
    }
}
