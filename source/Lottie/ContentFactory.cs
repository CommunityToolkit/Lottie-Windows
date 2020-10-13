// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools;
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
        readonly LottieVisualDiagnostics? _diagnostics;
        WinCompData.Visual? _wincompDataRootVisual;
        WinCompData.CompositionPropertySet? _wincompDataThemingPropertySet;
        CompositionPropertySet? _themingPropertySet;
        double _width;
        double _height;
        TimeSpan _duration;

        internal ContentFactory(LottieVisualDiagnostics? diagnostics)
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
            // Save the root visual.
            _wincompDataRootVisual = rootVisual;

            // Find the theming property set, if any.
            var graph = ObjectGraph<Graph.Node>.FromCompositionObject(_wincompDataRootVisual, includeVertices: false);
            _wincompDataThemingPropertySet = graph.
                                                CompositionObjectNodes.
                                                Where(n => n.Object is WinCompData.CompositionPropertySet cps && cps.Owner is null).
                                                Select(n => (WinCompData.CompositionPropertySet)n.Object).FirstOrDefault();
        }

        internal bool CanInstantiate => _wincompDataRootVisual != null;

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

                var instantiator = new Instantiator(compositor);

                // _wincompDataRootVisual != null is implied by CanInstantiate.
                var result = new DisposableAnimatedVisual((Visual)instantiator.GetInstance(_wincompDataRootVisual!))
                {
                    Size = new System.Numerics.Vector2((float)_width, (float)_height),
                    Duration = _duration,
                };

                if (diags != null)
                {
                    if (_wincompDataThemingPropertySet != null && _themingPropertySet is null)
                    {
                        // Instantiate the theming property set. This is shared by all of the instantiations.
                        _themingPropertySet = (CompositionPropertySet)instantiator.GetInstance(_wincompDataThemingPropertySet);

                        // _diagnostics != null is implied by diags != null;
                        diags.ThemingPropertySet = _diagnostics!.ThemingPropertySet = _themingPropertySet;
                    }

                    diags.InstantiationTime = sw.Elapsed;
                }

                return result;
            }
        }
    }
}
