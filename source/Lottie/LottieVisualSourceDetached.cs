// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Lottie;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Streams;

#if WINAPPSDK
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
#else
using Windows.UI.Composition;
using Windows.UI.Xaml;
#endif

#if WINAPPSDK
namespace MicrosoftToolkit.WinUI.Lottie
#else
namespace Microsoft.Toolkit.Uwp.UI.Lottie
#endif
{
    /// <summary>
    /// An <see cref="IAnimatedVisualSource"/> for a Lottie composition.
    /// Does not inherit DependencyObject so you can use it in console applications.
    /// </summary>
    public sealed class LottieVisualSourceDetached : IAnimatedVisualSource
    {
        int _loadVersion;
        Uri? _uriSource;
        AnimatedVisualFactory? _animatedVisualFactory;
        ImageAssetHandler? _imageAssetHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="LottieVisualSourceDetached"/> class.
        /// </summary>
        public LottieVisualSourceDetached()
        {
        }

        /// <summary>
        /// Gets or sets options for how the Lottie is loaded.
        /// </summary>
        public LottieVisualOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the Uniform Resource Identifier (URI) of the JSON source file for this <see cref="LottieVisualSourceDetached"/>.
        /// </summary>
        public Uri? UriSource
        {
            get => _uriSource;
            set { _uriSource = value; }
        }

        /// <summary>
        /// Called by XAML to convert a string to an <see cref="IAnimatedVisualSource"/>.
        /// </summary>
        /// <returns>The <see cref="LottieVisualSourceDetached"/> for the given url.</returns>
        public static LottieVisualSourceDetached? CreateFromString(string uri)
        {
            var uriUri = Uris.StringToUri(uri);
            if (uriUri is null)
            {
                return null;
            }

            return new LottieVisualSourceDetached { UriSource = uriUri };
        }

        /// <summary>
        /// Sets the source for the <see cref="LottieVisualSourceDetached"/>.
        /// </summary>
        /// <param name="stream">A stream containing the text of a JSON Lottie file encoded as UTF-8.</param>
        /// <returns>An <see cref="IAsyncAction"/> that completes when the load completes or fails.</returns>
        [Overload("SetSourceStreamAsync")]
        public IAsyncAction SetSourceAsync(IInputStream stream)
        {
            _uriSource = null;
            return LoadAsync(InputStreamLoader.LoadAsync(_imageAssetHandler, stream, Options)).AsAsyncAction();
        }

        /// <summary>
        /// Sets the source for the <see cref="LottieVisualSourceDetached"/>.
        /// </summary>
        /// <param name="file">A file that is a JSON Lottie file.</param>
        /// <returns>An <see cref="IAsyncAction"/> that completes when the load completes or fails.</returns>
        [Overload("SetSourceFileAsync")]
        public IAsyncAction SetSourceAsync(StorageFile file)
        {
            _uriSource = null;
            return LoadAsync(StorageFileLoader.LoadAsync(_imageAssetHandler, file, Options)).AsAsyncAction();
        }

        /// <summary>
        /// Sets the source for the <see cref="LottieVisualSourceDetached"/>.
        /// </summary>
        /// <param name="sourceUri">A URI that refers to a JSON Lottie file.</param>
        /// <returns>An <see cref="IAsyncAction"/> that completes when the load completes or fails.</returns>
        [DefaultOverload]
        [Overload("SetSourceUriAsync")]
        public IAsyncAction SetSourceAsync(Uri sourceUri)
        {
            _uriSource = sourceUri;

            // Update the dependency property to keep it in sync with _uriSource.
            // This will not trigger loading because it will be seen as no change
            // from the current (just set) _uriSource value.
            UriSource = sourceUri;

            return LoadAsync(UriLoader.LoadAsync(_imageAssetHandler, sourceUri, Options)).AsAsyncAction();
        }

        /// <summary>
        /// Sets a delegate that returns an <see cref="ICompositionSurface"/> for the given image uri.
        /// If this is null, no images will be loaded from references to external images.
        /// </summary>
        /// <remarks>Most Lottie files do not reference external images, but those that do
        /// will refer to the files via a uri. It is up to the user of <see cref="LottieVisualSourceDetached"/>
        /// to manage the loading of the image, and return an <see cref="ICompositionSurface"/> for
        /// that image. Alternatively the delegate may return null, and the image will not be
        /// displayed.</remarks>
        public void SetImageAssetHandler(ImageAssetHandler? imageAssetHandler)
        {
            _imageAssetHandler = imageAssetHandler;
        }

        /// <summary>
        /// Implements <see cref="IAnimatedVisualSource"/>.
        /// </summary>
        /// <param name="compositor">The <see cref="Compositor"/> that can be used as a factory for the resulting <see cref="IAnimatedVisual"/>.</param>
        /// <param name="diagnostics">An optional object that may provide extra information about the result.</param>
        /// <returns>An <see cref="IAnimatedVisual"/>.</returns>
        // TODO: currently explicitly implemented interfaces are causing a problem with .NET Native. Make them implicit for now.
        //bool IAnimatedVisualSource.TryCreateAnimatedVisual(
        public IAnimatedVisual? TryCreateAnimatedVisual(
            Compositor compositor,
            out object? diagnostics)
        {
            if (_animatedVisualFactory is null)
            {
                // No content has been loaded yet.
                // Return an IAnimatedVisual that produces nothing.
                diagnostics = null;
                return null;
            }
            else
            {
                // Some content was loaded. Ask the factory to produce an
                // IAnimatedVisual. If it returns null, the player will treat it
                // as an error.
                return _animatedVisualFactory.TryCreateAnimatedVisual(compositor, out diagnostics);
            }
        }

        // Starts loading. Completes the returned task when the load completes or is replaced by another load.
        async Task LoadAsync(Task<AnimatedVisualFactory?> loader)
        {
            var loadVersion = ++_loadVersion;

            var oldFactory = _animatedVisualFactory;
            _animatedVisualFactory = null;

            // Disable the warning about the task possibly having being started in
            // another context. There is no other context here.
#pragma warning disable VSTHRD003

            // Wait for the loader to finish.
            var factory = await loader;
#pragma warning restore VSTHRD003

            if (loadVersion != _loadVersion)
            {
                // Another load request came in before this one completed.
                return;
            }

            if (factory is null)
            {
                // Load didn't produce anything.
                return;
            }

            // We are the the most recent load. Save the result.
            _animatedVisualFactory = factory;

            if (!factory.CanInstantiate)
            {
                // The load did not produce any content. Throw an exception so the caller knows.
                throw new ArgumentException("Failed to load animated visual.");
            }
        }
    }
}