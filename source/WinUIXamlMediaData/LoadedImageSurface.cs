// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinStorageStreamsData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData
{
#if PUBLIC_WinUIXamlMediaData
    public
#endif
    sealed class LoadedImageSurface : ICompositionSurface
    {
        readonly byte[] _bytes;

        LoadedImageSurface(byte[] bytes)
        {
            _bytes = bytes;
        }

        public static LoadedImageSurface StartLoadFromStream(byte[] bytes)
        {
            return new LoadedImageSurface(bytes);
        }

        public static LoadedImageSurface StartLoadFromStream(IRandomAccessStream bytes)
        {
            // Implementation coming soon.
            throw new System.NotImplementedException();
        }

        public byte[] Bytes => _bytes;
    }
}
