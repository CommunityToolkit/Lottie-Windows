// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    class ContainerVisual : Visual
    {
        internal ContainerVisual()
        {
        }

        public IList<Visual> Children { get; } = new List<Visual>();

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.ContainerVisual;
    }
}
