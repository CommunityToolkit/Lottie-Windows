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
        private protected LoadedImageSurface()
        {
        }

        public byte[] Bytes { get; set; }

        public Uri ImageUri { get; set; }

        /// <inheritdoc/>
        public string LongDescription { get; set; }

        /// <inheritdoc/>
        public string ShortDescription { get; set; }

        public abstract LoadedImageSurfaceType Type { get; }

        public enum LoadedImageSurfaceType
        {
            FromStream,
            FromUri,
        }
    }
}
