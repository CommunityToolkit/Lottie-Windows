// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.WinUI.Lottie;
using Microsoft.UI.Composition;
using LottieIsland = CommunityToolkit.WinAppSDK.LottieIsland;
using MUXC = Microsoft.UI.Xaml.Controls;

namespace LottieWinRT
{
    public sealed class LottieVisualSourceWinRT : LottieIsland.IAnimatedVisualSourceFrameworkless
    {
        public event EventHandler<object?>? AnimatedVisualInvalidated;

        private LottieVisualSourceFrameworkless _lottieVisualSource;

        public LottieVisualSourceWinRT()
        {
            _lottieVisualSource = new LottieVisualSourceFrameworkless();
        }

        private LottieVisualSourceWinRT(LottieVisualSourceFrameworkless lottieVisualSource)
        {
            Debug.WriteLine("Hello from C#!!!");
            _lottieVisualSource = lottieVisualSource;
            _lottieVisualSource.AnimatedVisualInvalidated += (MUXC.IDynamicAnimatedVisualSource? sender, object? o) =>
            {
                AnimatedVisualInvalidated?.Invoke(this, o);
            };
        }

        public static LottieVisualSourceWinRT? CreateFromString(string uri)
        {
            LottieVisualSourceFrameworkless? lottieSource = LottieVisualSourceFrameworkless.CreateFromString(uri);
            if (lottieSource == null)
            {
                return null;
            }

            LottieVisualSourceWinRT winrtSource = new LottieVisualSourceWinRT(lottieSource);

            return winrtSource;
        }

        /// <summary>
        /// Gets or sets the Uniform Resource Identifier (URI) of the JSON source file for this <see cref="LottieVisualSource"/>.
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

        ///// <summary>
        ///// Implements <see cref="MUXC.IAnimatedVisualSource"/>.
        ///// </summary>
        ///// <param name="compositor">The <see cref="Compositor"/> that can be used as a factory for the resulting <see cref="MUXC.IAnimatedVisual"/>.</param>
        ///// <param name="diagnostics">An optional object that may provide extra information about the result.</param>
        ///// <returns>An <see cref="MUXC.IAnimatedVisual"/>.</returns>
        public LottieIsland.IAnimatedVisualFrameworkless? TryCreateAnimatedVisual(
            Compositor compositor,
            out object? diagnostics)
        {
            diagnostics = null;
            MUXC.IAnimatedVisual? visual = _lottieVisualSource?.TryCreateAnimatedVisual(compositor, out diagnostics);
            if (visual == null)
            {
                return null;
            }

            //return visual;
            return new AnimatedVisual(visual);
        }
    }
}
