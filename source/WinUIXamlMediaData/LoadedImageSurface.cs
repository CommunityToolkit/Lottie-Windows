// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData
{
#if PUBLIC_WinUIXamlMediaData
    public
#endif
    class LoadedImageSurface : ICompositionSurface, IDescribable
    {
        readonly byte[] _bytes;
        readonly string _filePath;
        readonly LoadedImageSurfaceLoadType _loadType;

        LoadedImageSurface(byte[] bytes)
        {
            _bytes = bytes;
            _loadType = LoadedImageSurfaceLoadType.FromStream;
        }

        LoadedImageSurface(string filePath)
        {
            _filePath = filePath;
            _loadType = LoadedImageSurfaceLoadType.FromUri;
        }

        public static LoadedImageSurface StartLoadFromStream(byte[] bytes)
        {
            return new LoadedImageSurface(bytes);
        }

        public static LoadedImageSurface StartLoadFromUri(string filePath)
        {
            return new LoadedImageSurface(filePath);
        }

        public byte[] Bytes => _bytes;

        public string FilePath => _filePath;

        public LoadedImageSurfaceLoadType LoadType => _loadType;

        /// <inheritdoc/>
        public string LongDescription { get; set; }

        /// <inheritdoc/>
        public string ShortDescription { get; set; }

        public enum LoadedImageSurfaceLoadType
        {
            FromStream,
            FromUri,
        }
    }
}
