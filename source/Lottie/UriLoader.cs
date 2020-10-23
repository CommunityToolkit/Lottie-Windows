﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Windows.UI.Composition;

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    /// <summary>
    /// Loads files from a Uri.
    /// </summary>
    sealed class UriLoader : Loader
    {
        readonly ImageAssetHandler? _imageLoader;

        UriLoader(ImageAssetHandler? imageLoader)
        {
            _imageLoader = imageLoader;
        }

        [return: NotNullIfNotNull("uri")]
        internal static async Task<AnimatedVisualFactory?> LoadAsync(
            ImageAssetHandler? imageLoader,
            Uri uri,
            LottieVisualOptions options)
        {
            if (uri is null)
            {
                return null;
            }

            var absoluteUri = Uris.GetAbsoluteUri(uri);

            if (absoluteUri.Scheme.StartsWith("ms-"))
            {
                // The URI is an application URI. Defer to the StorageFileLoader.
                return await StorageFileLoader.LoadAsync(imageLoader, absoluteUri, options);
            }
            else
            {
                var loader = new UriLoader(imageLoader);

                return await Loader.LoadAsync(
                    () => GetJsonStreamAsync(uri),
                    loader,
                    options);
            }
        }

        static async Task<(string?, Stream?)> GetJsonStreamAsync(Uri uri)
        {
            var absoluteUri = Uris.GetAbsoluteUri(uri);
            if (absoluteUri != null)
            {
                var winrtClient = new Windows.Web.Http.HttpClient();
                var response = await winrtClient.GetAsync(absoluteUri);

                var result = await response.Content.ReadAsInputStreamAsync();
                return (absoluteUri.LocalPath, result.AsStreamForRead());
            }

            return (null, null);
        }

        internal override ICompositionSurface? LoadImage(Uri imageUri) =>
            _imageLoader is null ? null : _imageLoader(imageUri);

        public override void Dispose()
        {
            // Nothing to dispose.
        }
    }
}
