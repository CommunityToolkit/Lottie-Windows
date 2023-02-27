// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.UIData.Tools;
using Microsoft.UI.Xaml.Controls;

#if WINAPPSDK
using Microsoft.UI.Composition;
#else
using Windows.UI.Composition;
#endif

namespace CommunityToolkit.WinUI.Lottie
{
    /// <summary>
    /// Information from which a composition's content can be instantiated. Contains the WinCompData
    /// translation of a composition and some metadata. This allows multiple instances of the translation
    /// to be instantiated without requiring repeated translations.
    /// </summary>
    sealed class AnimatedVisualFactory
        : IAnimatedVisualSource
    {
        readonly Dictionary<Uri, ICompositionSurface?> _imageCache = new Dictionary<Uri, ICompositionSurface?>();
        readonly LottieVisualDiagnostics? _diagnostics;
        Loader? _loader;
        WinCompData.Visual? _wincompDataRootVisual;
        WinCompData.CompositionPropertySet? _wincompDataThemingPropertySet;
        IEnumerable<WinCompData.AnimationController>? _wincompDataAnimationControllers;
        CompositionPropertySet? _themingPropertySet;
        double _width;
        double _height;
        TimeSpan _duration;

        internal AnimatedVisualFactory(Loader loader, LottieVisualDiagnostics? diagnostics)
        {
            _loader = loader;
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
            // Save the root visual.
            _wincompDataRootVisual = rootVisual;

            // Find the theming property set, if any.
            var graph = ObjectGraph<Graph.Node>.FromCompositionObject(_wincompDataRootVisual, includeVertices: false);
            _wincompDataThemingPropertySet = graph.
                                                CompositionObjectNodes.
                                                Where(n => n.Object is WinCompData.CompositionPropertySet cps && cps.Owner is null).
                                                Select(n => (WinCompData.CompositionPropertySet)n.Object).FirstOrDefault();
            _wincompDataAnimationControllers = graph.CompositionObjectNodes.Where(n => (n.Object is WinCompData.AnimationController) && ((WinCompData.AnimationController)n.Object).IsCustom).Select(n => (WinCompData.AnimationController)n.Object);
        }

        internal bool CanInstantiate => _wincompDataRootVisual is not null;

        public IAnimatedVisual? TryCreateAnimatedVisual(Compositor compositor, [MaybeNull] out object diagnostics)
        {
            // Clone the Diagnostics object so that the data from the translation is captured, then we
            // will update the clone with information about this particular instantiation.
            var diags = _diagnostics?.Clone();
            diagnostics = diags;

            if (!CanInstantiate)
            {
                return null;
            }
            else
            {
                var sw = Stopwatch.StartNew();

                var instantiator = new Instantiator(compositor, surfaceResolver: LoadImageFromUri);

                // _wincompDataRootVisual is not null is implied by CanInstantiate.
                Visual rootVisual = (Visual)instantiator.GetInstance(_wincompDataRootVisual!);
                IEnumerable<AnimationController> animationControllers = _wincompDataAnimationControllers!.Select(o => (AnimationController)instantiator.GetInstance(o));

                var result = new DisposableAnimatedVisual(rootVisual, animationControllers)
                {
                    Size = new System.Numerics.Vector2((float)_width, (float)_height),
                    Duration = _duration,
                };

                if (diags is not null)
                {
                    if (_wincompDataThemingPropertySet is not null && _themingPropertySet is null)
                    {
                        // Instantiate the theming property set. This is shared by all of the instantiations.
                        _themingPropertySet = (CompositionPropertySet)instantiator.GetInstance(_wincompDataThemingPropertySet);

                        // _diagnostics is not null is implied by diags is not null;
                        diags.ThemingPropertySet = _diagnostics!.ThemingPropertySet = _themingPropertySet;
                    }

                    diags.InstantiationTime = sw.Elapsed;
                }

                // After the first instantiation, all the images are cached so the
                // loader is no longer needed.
                _loader?.Dispose();
                _loader = null;

                return result;
            }
        }

        ICompositionSurface? LoadImageFromUri(Uri uri)
        {
            if (!_imageCache.TryGetValue(uri, out var result))
            {
                // The loader will not be null, because either this is the
                // first instantiation of the animated visual in which case the
                // image loader hasn't been set to null, or it's a second instantiation
                // so the images are already cached.
                result = _loader!.LoadImage(uri);

                // Cache the result so we can share the surfaces.
                _imageCache.Add(uri, result);
            }

            return result;
        }
    }
}
