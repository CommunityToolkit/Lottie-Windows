// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(6)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class ShapeVisual : ContainerVisual, IContainShapes
    {
        internal ShapeVisual()
        {
        }

        /// <inheritdoc/>
        public IList<CompositionShape> Shapes { get; } = new List<CompositionShape>();

        public CompositionViewBox? ViewBox { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.ShapeVisual;
    }
}
