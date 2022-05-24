// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

#if WINAPPSDK 
using Microsoft.UI.Composition;
using CommunityToolkit.WinUI.Lottie;
#else
using Windows.UI.Composition;
#endif

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    /// <summary>
    /// Loads files from a <see cref="StorageFile"/>. Supports raw
    /// JSON files and .lottie files.
    /// </summary>
    sealed class StorageFileLoader : Loader
    {
        readonly ImageAssetHandler? _imageLoader;
        readonly StorageFile _storageFile;

        StorageFileLoader(ImageAssetHandler? imageLoader, StorageFile storageFile)
        {
            _imageLoader = imageLoader;
            _storageFile = storageFile;
        }

        internal static async Task<AnimatedVisualFactory?> LoadAsync(
            ImageAssetHandler? imageLoader,
            StorageFile file,
            LottieVisualOptions options)
        {
            if (file is null)
            {
                return null;
            }

            if (file.Name.EndsWith(".lottie", StringComparison.OrdinalIgnoreCase))
            {
                // It's a .lottie file. Defer to the DotLottieLoader.
                return await DotLottieLoader.LoadAsync(file, options);
            }

            var loader = new StorageFileLoader(imageLoader, file);
            return await Loader.LoadAsync(
                loader.GetJsonStreamAsync,
                loader,
                options);
        }

        // Starts loading from an ms-appx asset file. This loads embedded assets.
        internal static async Task<AnimatedVisualFactory?> LoadAsync(
            ImageAssetHandler? imageLoader,
            Uri applicationUri,
            LottieVisualOptions options)
        {
            Debug.Assert(applicationUri.AbsoluteUri.StartsWith("ms-"), "Precondition");
            var file = await StorageFile.GetFileFromApplicationUriAsync(applicationUri);
            return await LoadAsync(imageLoader, file, options);
        }

        async Task<(string?, Stream?)> GetJsonStreamAsync()
        {
            var randomAccessStream = await _storageFile.OpenReadAsync();

            // Assume it's a JSON stream.
            return (_storageFile.Name, randomAccessStream.AsStreamForRead());
        }

        internal override ICompositionSurface? LoadImage(Uri imageUri) =>
            _imageLoader is null ? null : _imageLoader(imageUri);

        public override void Dispose()
        {
            // Nothing to dispose
        }
    }
}
