// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class ShapeVisual : ContainerVisual, IContainShapes
    {
        internal ShapeVisual()
        {
            Shapes = new ListOfNeverNull<CompositionShape>();
        }

        /// <inheritdoc/>
        public ListOfNeverNull<CompositionShape> Shapes { get; }

        public CompositionViewBox ViewBox { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.ShapeVisual;
    }
}
