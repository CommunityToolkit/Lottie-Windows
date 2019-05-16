// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData
{
#if PUBLIC_WinUIXamlMediaData
    public
#endif
    abstract class LoadedImageSurface : ICompositionSurface, IDescribable
    {
        protected LoadedImageSurface()
        {
        }

        /// <inheritdoc/>
        public string LongDescription { get; set; }

        /// <inheritdoc/>
        public string ShortDescription { get; set; }

        public abstract LoadedImageSurfaceType Type { get; }

        public static LoadedImageSurfaceFromStream StartLoadFromStream(byte[] bytes)
        {
            return new LoadedImageSurfaceFromStream(bytes);
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
