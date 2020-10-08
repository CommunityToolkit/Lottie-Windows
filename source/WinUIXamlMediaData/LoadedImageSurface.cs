// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinStorageStreamsData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData
{
#if PUBLIC_WinUIXamlMediaData
    public
#endif
    abstract class LoadedImageSurface : ICompositionSurface, IDescribable
    {
        private protected LoadedImageSurface()
        {
        }

        /// <inheritdoc/>
        string? IDescribable.LongDescription { get; set; }

        /// <inheritdoc/>
        string? IDescribable.ShortDescription { get; set; }

        /// <inheritdoc/>
        string? IDescribable.Name { get; set; }

        public abstract LoadedImageSurfaceType Type { get; }

        public static LoadedImageSurfaceFromStream StartLoadFromStream(byte[] bytes)
        {
            return new LoadedImageSurfaceFromStream(bytes);
        }

        public static LoadedImageSurface StartLoadFromStream(IRandomAccessStream stream)
        {
            // Implementation coming soon.
            throw new System.NotImplementedException();
        }

        public static LoadedImageSurfaceFromUri StartLoadFromUri(Uri uri)
        {
            return new LoadedImageSurfaceFromUri(uri);
        }

        public enum LoadedImageSurfaceType
        {
            FromStream,
            FromUri,
        }
    }
}
