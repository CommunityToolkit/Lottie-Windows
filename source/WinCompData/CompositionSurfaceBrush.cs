// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    class CompositionSurfaceBrush : CompositionBrush
    {
        internal CompositionSurfaceBrush()
        {
        }

        internal CompositionSurfaceBrush(ICompositionSurface surface)
        {
            Surface = surface;
        }

        public ICompositionSurface Surface { get; set; }

        // NOTE: Windows.UI.Composition.CompositionSurfaceBrush has more members. Only the members
        // that are needed has been added here.

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionSurfaceBrush;
    }
}
