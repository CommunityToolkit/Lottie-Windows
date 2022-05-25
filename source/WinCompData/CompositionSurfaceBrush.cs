// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionSurfaceBrush : CompositionBrush
    {
        internal CompositionSurfaceBrush(ICompositionSurface surface)
        {
            Surface = surface;
        }

        public ICompositionSurface Surface { get; set; }

        // NOTE: Windows.UI.Composition.CompositionSurfaceBrush has more members. Only the members
        // that are needed have been added here.

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionSurfaceBrush;
    }
}
