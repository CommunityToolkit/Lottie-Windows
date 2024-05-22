// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using CommunityToolkit.WinAppSDK.LottieIsland;
using CommunityToolkit.WinUI.Lottie;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CommunityToolkit.WinUI.Lottie.Controls
{
    /// <summary>
    /// An <see cref="IAnimatedVisualSource"/> for a Lottie composition. This allows
    /// a Lottie to be specified as the source for a <see cref="AnimatedVisualPlayer"/>.
    /// </summary>
    public sealed class LottieVisualSourceWinUI : DependencyObject, IDynamicAnimatedVisualSource
    {
        LottieVisualSource _lottieVisualSource;

        HashSet<TypedEventHandler<IDynamicAnimatedVisualSource?, object?>> _compositionInvalidatedEventTokenTable = new HashSet<TypedEventHandler<IDynamicAnimatedVisualSource?, object?>>();

        /// <summary>
        /// Gets the options for the <see cref="LottieVisualSourceWinUI"/>.
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
            (owner, oldValue, newValue) => owner._lottieVisualSource.UriSource = newValue);

        static DependencyProperty RegisterDp<T>(string propertyName, T defaultValue) =>
            DependencyProperty.Register(propertyName, typeof(T), typeof(LottieVisualSourceWinUI), new PropertyMetadata(defaultValue));

        static DependencyProperty RegisterDp<T>(string propertyName, T? defaultValue, Action<LottieVisualSourceWinUI, T, T> callback)
            where T : class
            =>
            DependencyProperty.Register(propertyName, typeof(T), typeof(LottieVisualSourceWinUI),
                new PropertyMetadata(defaultValue, (d, e) => callback((LottieVisualSourceWinUI)d, (T)e.OldValue, (T)e.NewValue)));

        /// <summary>
        /// Initializes a new instance of the <see cref="LottieVisualSourceWinUI"/> class.
        /// </summary>
        public LottieVisualSourceWinUI()
        {
            _lottieVisualSource = new LottieVisualSource();
            _lottieVisualSource.AnimatedVisualInvalidated += OnAnimatedVisualInvalidated;
        }

        public LottieVisualSourceWinUI(LottieVisualSource lottieVisualSource)
        {
            _lottieVisualSource = lottieVisualSource;
            _lottieVisualSource.AnimatedVisualInvalidated += OnAnimatedVisualInvalidated;
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
        /// Gets or sets the Uniform Resource Identifier (URI) of the JSON source file for this <see cref="LottieVisualSourceWinUI"/>.
        /// </summary>
        public Uri UriSource
        {
            get => (Uri)GetValue(UriSourceProperty);
            set => SetValue(UriSourceProperty, value);
        }

        /// <summary>
        /// Called by XAML to convert a string to an <see cref="IAnimatedVisualSource"/>.
        /// </summary>
        /// <returns>The <see cref="LottieVisualSourceWinUI"/> for the given url.</returns>
        public static LottieVisualSourceWinUI? CreateFromString(string uri)
        {
            LottieVisualSource? lottieVisualSource = LottieVisualSource.CreateFromString(uri);
            if (lottieVisualSource != null)
            {
                return new LottieVisualSourceWinUI(lottieVisualSource);
            }
            else
            {
                return new LottieVisualSourceWinUI();
            }
        }

        /// <summary>
        /// Sets the source for the <see cref="LottieVisualSourceWinUI"/>.
        /// </summary>
        /// <param name="stream">A stream containing the text of a JSON Lottie file encoded as UTF-8.</param>
        /// <returns>An <see cref="IAsyncAction"/> that completes when the load completes or fails.</returns>
        [Overload("SetSourceStreamAsync")]
        public IAsyncAction SetSourceAsync(IInputStream stream)
        {
            return _lottieVisualSource.SetSourceAsync(stream);
        }

        /// <summary>
        /// Sets the source for the <see cref="LottieVisualSourceWinUI"/>.
        /// </summary>
        /// <param name="file">A file that is a JSON Lottie file.</param>
        /// <returns>An <see cref="IAsyncAction"/> that completes when the load completes or fails.</returns>
        [Overload("SetSourceFileAsync")]
        public IAsyncAction SetSourceAsync(StorageFile file)
        {
            return _lottieVisualSource.SetSourceAsync(file);
        }

        /// <summary>
        /// Sets the source for the <see cref="LottieVisualSourceWinUI"/>.
        /// </summary>
        /// <param name="sourceUri">A URI that refers to a JSON Lottie file.</param>
        /// <returns>An <see cref="IAsyncAction"/> that completes when the load completes or fails.</returns>
        [DefaultOverload]
        [Overload("SetSourceUriAsync")]
        public IAsyncAction SetSourceAsync(Uri sourceUri)
        {
            return _lottieVisualSource.SetSourceAsync(sourceUri);
        }

        /// <summary>
        /// Implements <see cref="IDynamicAnimatedVisualSource"/>.
        /// </summary>
        // TODO: currently explicitly implemented interfaces are causing a problem with .NET Native. Make them implicit for now.
        public event TypedEventHandler<IDynamicAnimatedVisualSource?, object?> AnimatedVisualInvalidated
        {
            add
            {
                _compositionInvalidatedEventTokenTable.Add(value);
            }

            remove
            {
                _compositionInvalidatedEventTokenTable.Remove(value);
            }
        }

        /// <summary>
        /// Sets a delegate that returns an <see cref="ICompositionSurface"/> for the given image uri.
        /// If this is null, no images will be loaded from references to external images.
        /// </summary>
        /// <remarks>Most Lottie files do not reference external images, but those that do
        /// will refer to the files via a uri. It is up to the user of <see cref="LottieVisualSourceWinUI"/>
        /// to manage the loading of the image, and return an <see cref="ICompositionSurface"/> for
        /// that image. Alternatively the delegate may return null, and the image will not be
        /// displayed.</remarks>
        public void SetImageAssetHandler(ImageAssetHandler? imageAssetHandler)
        {
            _lottieVisualSource.SetImageAssetHandler(imageAssetHandler);
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
            if (_lottieVisualSource is null)
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
                IAnimatedVisualFrameworkless? animatedVisual = _lottieVisualSource.TryCreateAnimatedVisual(compositor, out diagnostics);
                if (animatedVisual != null)
                {
                    return new LottieVisualWinUI(animatedVisual);
                }
                else
                {
                    return null;
                }
            }
        }

        private void OnAnimatedVisualInvalidated(LottieVisualSource? sender, object? args)
        {
            foreach (var v in _compositionInvalidatedEventTokenTable)
            {
                v.Invoke(this, null);
            }
        }
    }
}