// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Lottie.DotLottie;
using Windows.Storage;

#if WINAPPSDK
using Microsoft.UI.Composition;

#else
using Windows.UI.Composition;
using Windows.UI.Xaml.Media;
#endif

namespace CommunityToolkit.WinUI.Lottie
{
    /// <summary>
    /// Loads files that conform to the .lottie spec. See: https://dotlottie.io/.
    /// </summary>
    sealed class DotLottieLoader : Loader
    {
        DotLottieFile? _dotLottieFile;

        DotLottieLoader()
        {
        }

        internal static async Task<AnimatedVisualFactory?> LoadAsync(
            StorageFile file,
            LottieVisualOptions options)
        {
            var stream = (await file.OpenReadAsync()).AsStreamForRead();
            return await LoadAsync(file.Name, stream, options);
        }

        static async Task<AnimatedVisualFactory?> LoadAsync(
            string fileName,
            Stream stream,
            LottieVisualOptions options)
        {
            ZipArchive zipArchive;
            try
            {
                zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
            }
            catch (InvalidDataException)
            {
                // Not a valid zip file.
                return null;
            }

            var loader = new DotLottieLoader();

            return await Loader.LoadAsync(
                () => loader.GetJsonStreamAsync(zipArchive, fileName),
                loader,
                options);
        }

        async Task<(string?, Stream?)> GetJsonStreamAsync(ZipArchive zipArchive, string fileName)
        {
            _dotLottieFile = await DotLottieFile.FromZipArchiveAsync(zipArchive);

            if (_dotLottieFile is null)
            {
                return (null, null);
            }

            var firstAnimation = _dotLottieFile.Animations[0];

            return (fileName, firstAnimation.Open());
        }

        internal override ICompositionSurface? LoadImage(Uri imageUri)
        {
            if (!imageUri.IsAbsoluteUri || imageUri.Authority != "localhost")
            {
                return null;
            }

            // Load the image from the .lottie file. This is loaded into a MemoryStream
            // because the streams that come from ZipArchive cannot be randomly accessed
            // as required by LoadedImageSurface. This also has the benefit that it is
            // safe to Dispose the DotLottieFile as soon as the last image has started
            // decoding, becuase we already have all the bytes in the MemoryStream.
            var imageStream = _dotLottieFile!.OpenFileAsMemoryStream(imageUri.AbsolutePath);
            if (imageStream is null)
            {
                return null;
            }

            // TODO - Load this image some other way
            return null;

            //return LoadedImageSurface.StartLoadFromStream(imageStream.AsRandomAccessStream());
        }

        public override void Dispose()
        {
            _dotLottieFile?.Dispose();
        }
    }
}