// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    /// <summary>
    /// Data representation of Windows.UI.Composition.CompositionPath.
    /// </summary>
    [MetaData.UapVersion(6)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionPath : IDescribable
    {
        public CompositionPath(Wg.IGeometrySource2D source)
        {
            Source = source;
        }

        public Wg.IGeometrySource2D Source { get; }

        /// <inheritdoc/>
        string? IDescribable.LongDescription { get; set; }

        /// <inheritdoc/>
        string? IDescribable.ShortDescription { get; set; }

        /// <inheritdoc/>
        string? IDescribable.Name { get; set; }
    }
}
