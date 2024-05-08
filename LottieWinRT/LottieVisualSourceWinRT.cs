// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.WinAppSDK.LottieIsland;
using CommunityToolkit.WinUI.Lottie;
using Microsoft.UI.Composition;
using LottieIsland = CommunityToolkit.WinAppSDK.LottieIsland;

namespace CommunityToolkit.WinAppSDK.LottieWinRT
{
    public sealed class LottieVisualSourceWinRT
    {
        public event EventHandler<object?>? AnimatedVisualInvalidated;

        private LottieVisualSource _lottieVisualSource;

        public LottieVisualSourceWinRT()
        {
            _lottieVisualSource = new LottieVisualSource();
        }

        private LottieVisualSourceWinRT(LottieVisualSource lottieVisualSource)
        {
            _lottieVisualSource = lottieVisualSource;
            _lottieVisualSource.AnimatedVisualInvalidated += (LottieVisualSource? sender, object? o) =>
            {
                AnimatedVisualInvalidated?.Invoke(this, o);
            };
        }

        public static LottieVisualSourceWinRT? CreateFromString(string uri)
        {
            LottieVisualSource? lottieSource = LottieVisualSource.CreateFromString(uri);
            if (lottieSource == null)
            {
                return null;
            }

            LottieVisualSourceWinRT winrtSource = new LottieVisualSourceWinRT(lottieSource);

            return winrtSource;
        }

        /// <summary>
        /// Gets or sets the Uniform Resource Identifier (URI) of the JSON source file for this <see cref="LottieVisualSourceWinRT"/>.
        /// </summary>
        public Uri? UriSource
        {
            get => _lottieVisualSource?.UriSource;
            set
            {
                if (_lottieVisualSource != null)
                {
                    _lottieVisualSource.UriSource = value;
                }
            }
        }

        /// <summary>
        /// Implements <see cref="LottieIsland.IAnimatedVisualFrameworkless"/>.
        /// WinRT Wrapper around <see cref="LottieIsland.IAnimatedVisualFrameworkless"/> for use by C++ or non-WinUI applications.
        /// </summary>
        /// <param name="compositor">The <see cref="Compositor"/> that can be used as a factory for the resulting <see cref="LottieIsland.IAnimatedVisualFrameworkless"/>.</param>
        /// <param name="diagnostics">An optional object that may provide extra information about the result.</param>
        /// <returns>An <see cref="LottieIsland.IAnimatedVisualFrameworkless"/>.</returns>
        public LottieIsland.IAnimatedVisualFrameworkless? TryCreateAnimatedVisual(
            Compositor compositor,
            out object? diagnostics)
        {
            diagnostics = null;
            return _lottieVisualSource?.TryCreateAnimatedVisual(compositor, out diagnostics);
        }
    }
}
