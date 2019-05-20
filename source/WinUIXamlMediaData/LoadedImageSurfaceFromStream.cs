// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData
{
#if PUBLIC_WinUIXamlMediaData
    public
#endif
    sealed class LoadedImageSurfaceFromStream : LoadedImageSurface
    {
        internal LoadedImageSurfaceFromStream(byte[] bytes)
        {
            Bytes = bytes;
        }

        public byte[] Bytes { get; }

        public override LoadedImageSurfaceType Type => LoadedImageSurfaceType.FromStream;
    }
}
