// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData
{
#if PUBLIC_WinUIXamlMediaData
    public
#endif
    sealed class LoadedImageSurfaceFromUri : LoadedImageSurface
    {
        private LoadedImageSurfaceFromUri(Uri imageUri)
        {
            ImageUri = imageUri;
        }

        public static LoadedImageSurfaceFromUri StartLoadFromUri(Uri imageUri)
        {
            return new LoadedImageSurfaceFromUri(imageUri);
        }

        public override LoadedImageSurfaceType Type => LoadedImageSurfaceType.FromUri;
    }
}
