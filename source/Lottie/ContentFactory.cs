// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Composition;

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    /// <summary>
    /// Information from which a composition's content can be instantiated. Contains the WinCompData
    /// translation of a composition and some metadata. This allows multiple instances of the translation
    /// to be instantiated without requiring repeated translations.
    /// </summary>
    sealed class ContentFactory : IAnimatedVisualSource
    {
        internal static readonly ContentFactory FailedContent = new ContentFactory(null);
        readonly LottieVisualDiagnostics _diagnostics;
        WinCompData.Visual _wincompDataRootVisual;
        double _width;
        double _height;
        TimeSpan _duration;

        internal ContentFactory(LottieVisualDiagnostics diagnostics)
        {
            _diagnostics = diagnostics;
        }

        internal void SetDimensions(double width, double height, TimeSpan duration)
        {
            _width = width;
            _height = height;
            _duration = duration;
        }

        internal void SetRootVisual(WinCompData.Visual rootVisual)
        {
            _wincompDataRootVisual = rootVisual;
        }

        internal bool CanInstantiate => _wincompDataRootVisual != null;

        public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)
        {
            var diags = _diagnostics?.Clone();
            diagnostics = diags;

            if (!CanInstantiate)
            {
                return null;
            }
            else
            {
                var sw = Stopwatch.StartNew();
                var result = new DisposableAnimatedVisual()
                {
                    RootVisual = Instantiator.CreateVisual(compositor, _wincompDataRootVisual),
                    Size = new System.Numerics.Vector2((float)_width, (float)_height),
                    Duration = _duration,
                };

                if (diags != null)
                {
                    diags.InstantiationTime = sw.Elapsed;
                }

                return result;
            }
        }
    }
}
