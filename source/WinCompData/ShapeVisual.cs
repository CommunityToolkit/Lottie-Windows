// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
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
        public IList<CompositionShape> Shapes { get; } = new ListOfNeverNull<CompositionShape>();

        public CompositionViewBox ViewBox { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.ShapeVisual;
    }
}
