// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    interface IContainShapes
    {
        /// <summary>
        /// The <see cref="CompositionShape"/>s that are contained by this object.
        /// </summary>
        IList<CompositionShape> Shapes { get; }
    }
}
