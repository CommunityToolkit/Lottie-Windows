// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    static class Layers
    {
        /// <summary>
        /// Translates each of the layers in the given <see cref="CompositionContext"/>
        /// to a <see cref="Visual"/>.
        /// </summary>
        /// <returns>The translated layers.</returns>
        public static Visual[] TranslateLayersToVisuals(CompositionContext context)
        {
            var layerTranslators =
                (from layer in context.Layers.GetLayersBottomToTop()
                 let layerTranslator = CreateTranslatorForLayer(context, layer)
                 where layerTranslator != null
                 select (layerTranslator: layerTranslator, layer: layer)).ToArray();

            // Set descriptions on each translate layer so that it's clear where the layer starts.
            if (context.Translation.AddDescriptions)
            {
                foreach (var (layerTranslator, layer) in layerTranslators)
                {
                    // Add a description if not added already.
                    if (layerTranslator.ShortDescription is null)
                    {
                        layerTranslator.SetDescription(context, $"{layer.Type} layer: {layer.Name}");
                    }
                }
            }

            // Go through the layers and compose matte layer and layer to be matted into
            // the resulting visuals. Any layer that is not a matte or matted layer is
            // simply returned unmodified.
            var compositionGraphs = Masks.ComposeMattedLayers(context, layerTranslators).ToArray();

            // Layers are translated into either a visual tree or a shape tree. Convert the list of Visual and
            // Shape roots to a list of Visual roots by wrapping the shape trees in ShapeVisuals.
            return VisualsAndShapesToVisuals(context, compositionGraphs).ToArray();
        }

        // Combines 1 or more LayerTranslators as CompositionShape subgraphs under a ShapeVisual.
        static Visual GetVisualForLayerTranslators(CompositionContext context, IReadOnlyList<LayerTranslator> shapes)
        {
            Debug.Assert(shapes.All(s => s.IsShape), "Precondition");

            var compositionShapes = shapes.Select(s => (shape: s.GetShapeRoot(context), subgraph: s)).Where(s => s.shape != null).ToArray();

            switch (compositionShapes.Length)
            {
                case 0:
                    return null;
                case 1:
                    // There's only 1 shape. Get it to translate directly to a Visual.
                    return compositionShapes[0].subgraph.GetVisualRoot(context);
                default:
                    // There are multiple contiguous shapes. Group them under a ShapeVisual.
                    // The ShapeVisual has to have a size (it clips to its size).
                    // TODO - if the shape graphs share the same opacity and/or visiblity, get them
                    //        to translate without opacity/visiblity and we'll pull those
                    //        into the Visual.
                    var shapeVisual = context.ObjectFactory.CreateShapeVisualWithChild(compositionShapes[0].shape, context.Size);

                    shapeVisual.SetDescription(context, () => "Layer aggregator");

                    for (var i = 1; i < compositionShapes.Length; i++)
                    {
                        shapeVisual.Shapes.Add(compositionShapes[i].shape);
                    }

                    return shapeVisual;
            }
        }

        // Takes a list of Visuals and Shapes and returns a list of Visuals by combining all direct
        // sibling shapes together into a ShapeVisual.
        static IEnumerable<Visual> VisualsAndShapesToVisuals(CompositionContext context, IEnumerable<LayerTranslator> items)
        {
            var shapeSubGraphs = new List<LayerTranslator>();

            foreach (var item in items)
            {
                if (item.IsShape)
                {
                    shapeSubGraphs.Add(item);
                }
                else
                {
                    if (shapeSubGraphs.Count > 0)
                    {
                        var visual = GetVisualForLayerTranslators(context, shapeSubGraphs);

                        if (visual != null)
                        {
                            yield return visual;
                        }

                        shapeSubGraphs.Clear();
                    }

                    var visualRoot = item.GetVisualRoot(context);
                    if (visualRoot != null)
                    {
                        yield return visualRoot;
                    }
                }
            }

            if (shapeSubGraphs.Count > 0)
            {
                var visual = GetVisualForLayerTranslators(context, shapeSubGraphs);
                if (visual != null)
                {
                    yield return visual;
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="LayerTranslator"/> for the given Lottie layer.
        /// </summary>
        /// <returns>The <see cref="LayerTranslator"/> that will translate the
        /// given Lottie layer to a Shape or a Visual.</returns>
        static LayerTranslator CreateTranslatorForLayer(CompositionContext context, Layer layer)
        {
            if (layer.Is3d)
            {
                context.Issues.ThreeDLayerIsNotSupported();
            }

            if (layer.BlendMode != BlendMode.Normal)
            {
                context.Issues.BlendModeNotNormal(layer.Name, layer.BlendMode.ToString());
            }

            if (layer.TimeStretch != 1)
            {
                context.Issues.TimeStretchIsNotSupported();
            }

            if (layer.IsHidden)
            {
                return null;
            }

            switch (layer.Type)
            {
                case Layer.LayerType.Image:
                    return Images.CreateImageLayerTranslator(context.CreateLayerContext((ImageLayer)layer));
                case Layer.LayerType.Null:
                    // Null layers only exist to hold transforms when declared as parents of other layers.
                    return null;
                case Layer.LayerType.PreComp:
                    return PreComps.CreatePreCompLayerTranslator(context.CreateLayerContext((PreCompLayer)layer));
                case Layer.LayerType.Shape:
                    return Shapes.CreateShapeLayerTranslator(context.CreateLayerContext((ShapeLayer)layer));
                case Layer.LayerType.Solid:
                    return SolidLayers.CreateSolidLayerTranslator(context.CreateLayerContext((SolidLayer)layer));
                case Layer.LayerType.Text:
                    return TextLayers.CreateTextLayerTranslator(context.CreateLayerContext((TextLayer)layer));
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}