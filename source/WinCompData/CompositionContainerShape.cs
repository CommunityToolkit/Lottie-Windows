// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionContainerShape : CompositionShape, IContainShapes
    {
        internal CompositionContainerShape()
        {
            Shapes = new ListOfNeverNull<CompositionShape>();
        }

        /// <inheritdoc/>
        public ListOfNeverNull<CompositionShape> Shapes { get; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionContainerShape;
    }
}
