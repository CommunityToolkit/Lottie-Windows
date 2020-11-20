// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Composition;

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    /// <summary>
    /// A loader that loads from an <see cref="IInputStream"/>.
    /// </summary>
    sealed class InputStreamLoader : Loader
    {
        readonly ImageAssetHandler? _imageLoader;
        readonly IInputStream _inputStream;

        InputStreamLoader(ImageAssetHandler? imageLoader, IInputStream inputStream)
        {
            _imageLoader = imageLoader;
            _inputStream = inputStream;
        }

        internal static async Task<AnimatedVisualFactory?> LoadAsync(
            ImageAssetHandler? imageLoader,
            IInputStream inputStream,
            LottieVisualOptions options)
        {
            if (inputStream is null)
            {
                return null;
            }

            var loader = new InputStreamLoader(imageLoader, inputStream);
            return await Loader.LoadAsync(
                loader.GetJsonStreamAsync,
                loader,
                options);
        }

        Task<(string?, Stream?)> GetJsonStreamAsync()
        {
            return Task.FromResult(((string?)string.Empty, (Stream?)_inputStream.AsStreamForRead()));
        }

        internal override ICompositionSurface? LoadImage(Uri imageUri) =>
            _imageLoader is null ? null : _imageLoader(imageUri);

        public override void Dispose()
        {
            // Nothing to dispose.
        }
    }
}
