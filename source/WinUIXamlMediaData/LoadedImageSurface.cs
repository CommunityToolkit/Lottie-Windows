// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData
{
#if PUBLIC_WinUIXamlMedia
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

        string ICompositionSurface.TypeName => nameof(LoadedImageSurface);
    }
}
