// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if !WINDOWS_UWP
    public
#endif
    interface IContainShapes
    {
        /// <summary>
        /// The <see cref="CompositionShape"/>s that are contained by this object.
        /// </summary>
        ListOfNeverNull<CompositionShape> Shapes { get; }
    }
}
