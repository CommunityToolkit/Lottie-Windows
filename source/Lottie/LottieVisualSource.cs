// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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

namespace CommunityToolkit.WinUI.Lottie
{
    /// <summary>
    /// An <see cref="IAnimatedVisualSource"/> for a Lottie composition. This allows
    /// a Lottie to be specified as the source for a <see cref="AnimatedVisualPlayer"/>.
    /// </summary>
    public sealed class LottieVisualSource : DependencyObject, IDynamicAnimatedVisualSource
    {
#if WINAPPSDK
        HashSet<TypedEventHandler<IDynamicAnimatedVisualSource?, object?>> _compositionInvalidatedEventTokenTable = new HashSet<TypedEventHandler<IDynamicAnimatedVisualSource?, object?>>();
#else
        EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource?, object?>>? _compositionInvalidatedEventTokenTable;
#endif

        int _loadVersion;
        Uri? _uriSource;
        AnimatedVisualFactory? _animatedVisualFactory;
        ImageAssetHandler? _imageAssetHandler;

        /// <summary>
        /// Gets the options for the <see cref="LottieVisualSource"/>.
        /// </summary>
        // Optimize Lotties by default. Optimization takes a little longer but usually produces much
        // more efficient translations. The only reason someone would turn optimization off is if
        // the time to translate is too high, but in that case the Lottie is probably going to perform
        // so badly on the machine that it won't really be usable with our without optimization.
        public static DependencyProperty OptionsProperty { get; } =
            RegisterDp(nameof(Options), LottieVisualOptions.Optimize);

        /// <summary>
        /// Gets the URI from which to load a JSON Lottie file.
        /// </summary>
        public static DependencyProperty UriSourceProperty { get; } =
            RegisterDp<Uri>(nameof(UriSource), null,
            (owner, oldValue, newValue) => owner.HandleUriSourcePropertyChanged(oldValue, newValue));

        static DependencyProperty RegisterDp<T>(string propertyName, T defaultValue) =>
            DependencyProperty.Register(propertyName, typeof(T), typeof(LottieVisualSource), new PropertyMetadata(defaultValue));

        static DependencyProperty RegisterDp<T>(string propertyName, T? defaultValue, Action<LottieVisualSource, T, T> callback)
            where T : class
            =>
            DependencyProperty.Register(propertyName, typeof(T), typeof(LottieVisualSource),
                new PropertyMetadata(defaultValue, (d, e) => callback((LottieVisualSource)d, (T)e.OldValue, (T)e.NewValue)));

        /// <summary>
        /// Initializes a new instance of the <see cref="LottieVisualSource"/> class.
        /// </summary>
        public LottieVisualSource()
        {
        }

        /// <summary>
        /// Gets or sets options for how the Lottie is loaded.
        /// </summary>
        public LottieVisualOptions Options
        {
            get => (LottieVisualOptions)GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        /// <summary>
        /// Gets or sets the Uniform Resource Identifier (URI) of the JSON source file for this <see cref="LottieVisualSource"/>.
        /// </summary>
        public Uri UriSource
        {
            get => (Uri)GetValue(UriSourceProperty);
            set => SetValue(UriSourceProperty, value);
        }

        /// <summary>
        /// Called by XAML to convert a string to an <see cref="IAnimatedVisualSource"/>.
        /// </summary>
        /// <returns>The <see cref="LottieVisualSource"/> for the given url.</returns>
        public static LottieVisualSource? CreateFromString(string uri)
        {
            var uriUri = Uris.StringToUri(uri);
            if (uriUri is null)
            {
                return null;
            }

            return new LottieVisualSource { UriSource = uriUri };
        }

        /// <summary>
        /// Sets the source for the <see cref="LottieVisualSource"/>.
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
        /// Sets the source for the <see cref="LottieVisualSource"/>.
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
        /// Sets the source for the <see cref="LottieVisualSource"/>.
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
        /// Implements <see cref="IDynamicAnimatedVisualSource"/>.
        /// </summary>
        // TODO: currently explicitly implemented interfaces are causing a problem with .NET Native. Make them implicit for now.
        public event TypedEventHandler<IDynamicAnimatedVisualSource?, object?> AnimatedVisualInvalidated
        {
            add
            {
#if WINAPPSDK
                _compositionInvalidatedEventTokenTable.Add(value);
#else
                return EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource?, object?>>
                   .GetOrCreateEventRegistrationTokenTable(ref _compositionInvalidatedEventTokenTable)
                   .AddEventHandler(value);
#endif
            }

            remove
            {
#if WINAPPSDK
                _compositionInvalidatedEventTokenTable.Remove(value);
#else
                EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource?, object?>>
                   .GetOrCreateEventRegistrationTokenTable(ref _compositionInvalidatedEventTokenTable)
                    .RemoveEventHandler(value);
#endif
            }
        }

        /// <summary>
        /// Sets a delegate that returns an <see cref="ICompositionSurface"/> for the given image uri.
        /// If this is null, no images will be loaded from references to external images.
        /// </summary>
        /// <remarks>Most Lottie files do not reference external images, but those that do
        /// will refer to the files via a uri. It is up to the user of <see cref="LottieVisualSource"/>
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

        void NotifyListenersThatCompositionChanged()
        {
#if WINAPPSDK
            foreach (var v in _compositionInvalidatedEventTokenTable)
            {
                v.Invoke(this, null);
            }
#else
            EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource?, object?>>
                .GetOrCreateEventRegistrationTokenTable(ref _compositionInvalidatedEventTokenTable)
                .InvocationList?.Invoke(this, null);
#endif
        }

        // Called when the UriSource property is updated.
        void HandleUriSourcePropertyChanged(Uri oldValue, Uri newValue)
        {
            if (newValue == _uriSource)
            {
                // Ignore if setting to the current value. This can't happen if the value
                // is being set via the DependencyProperty, but it will happen if the value
                // is set via SetSourceAsync, as _uriSource will have been set before this
                // is called.
                return;
            }

            _uriSource = newValue;

            var ignoredTask = StartLoadingAndIgnoreErrorsAsync();

            async Task StartLoadingAndIgnoreErrorsAsync()
            {
                try
                {
                    await LoadAsync(UriLoader.LoadAsync(_imageAssetHandler, UriSource, Options));
                }
                catch
                {
                    // Swallow any errors - nobody is listening.
                }
            }
        }

        // Starts loading. Completes the returned task when the load completes or is replaced by another load.
        async Task LoadAsync(Task<AnimatedVisualFactory?> loader)
        {
            var loadVersion = ++_loadVersion;

            var oldFactory = _animatedVisualFactory;
            _animatedVisualFactory = null;

            if (oldFactory is not null)
            {
                // Notify all listeners that their existing content is no longer valid.
                // They should stop showing the content. We will notify them again when the
                // content changes.
                NotifyListenersThatCompositionChanged();
            }

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

            // Notify all listeners that they should try to create their instance of the content again.
            NotifyListenersThatCompositionChanged();

            if (!factory.CanInstantiate)
            {
                // The load did not produce any content. Throw an exception so the caller knows.
                throw new ArgumentException("Failed to load animated visual.");
            }
        }

#if !WINAPPSDK
        /// <summary>
        /// Returns a string representation of the <see cref="LottieVisualSource"/> for debugging purposes.
        /// </summary>
        /// <returns>A string representation of the <see cref="LottieVisualSource"/> for debugging purposes.</returns>
        public override string ToString()
        {
            var identity = _uriSource?.ToString() ?? string.Empty;
            return $"LottieVisualSource({identity})";
        }
#endif
    }
}