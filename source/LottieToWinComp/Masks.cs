// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgc;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translation for Lottie masks and mattes.
    /// </summary>
    static class Masks
    {
        // Translate a mask into shapes for a shape visual. The mask is applied to the visual to be masked
        // using the VisualSurface. The VisualSurface can take the rendered contents of a visual tree and
        // use it as a brush. The final masked result is achieved by taking the visual to be masked, putting
        // it into a VisualSurface, then taking the mask and putting that in a VisualSurface and then combining
        // the result with a composite effect.
        public static Visual TranslateAndApplyMasksForLayer(
            LayerContext context,
            Visual visualToMask)
        {
            var result = visualToMask;
            var layer = context.Layer;

            if (layer.Masks.Count > 0)
            {
                if (layer.Masks.Count == 1)
                {
                    // Common case for masks: exactly one mask.
                    var masks = layer.Masks.Slice(0, 1);

                    switch (masks[0].Mode)
                    {
                        // If there's only 1 mask, Difference and Intersect act the same as Add.
                        case Mask.MaskMode.Add:
                        case Mask.MaskMode.Difference:
                        case Mask.MaskMode.Intersect:
                        case Mask.MaskMode.None:
                            // Composite using the mask.
                            result = TranslateAndApplyMasks(context, masks, result, CanvasComposite.DestinationIn);
                            break;

                        case Mask.MaskMode.Subtract:
                            // Composite using the mask.
                            result = TranslateAndApplyMasks(context, masks, result, CanvasComposite.DestinationOut);
                            break;

                        default:
                            context.Issues.MaskWithUnsupportedMode(masks[0].Mode.ToString());
                            break;
                    }
                }
                else
                {
                    // Uncommon case for masks: multiple masks.
                    // Get the contiguous segments of masks that have the same mode, create a shape tree for each
                    // segment, and composite the shape trees.
                    // The goal here is to use the smallest possible number of composites.
                    // 1) Get the masks that have the same mode and are next to each other in the list of masks.
                    // 2) Translate the masks to a ShapeVisual.
                    // 3) Composite each ShapeVisual with the previous ShapeVisual.
                    foreach (var (index, count) in EnumerateMaskListSegments(layer.Masks.ToArray()))
                    {
                        // Every mask in the segment has the same mode or None. The first mask is never None.
                        var masksWithSameMode = layer.Masks.Slice(index, count);
                        switch (masksWithSameMode[0].Mode)
                        {
                            case Mask.MaskMode.Add:
                                // Composite using the mask, and apply to what has been already masked.
                                result = TranslateAndApplyMasks(context, masksWithSameMode, result, CanvasComposite.DestinationIn);
                                break;
                            case Mask.MaskMode.Subtract:
                                // Composite using the mask, and apply to what has been already masked.
                                result = TranslateAndApplyMasks(context, masksWithSameMode, result, CanvasComposite.DestinationOut);
                                break;
                            default:
                                // Only Add, Subtract, and None modes are currently supported.
                                context.Issues.MaskWithUnsupportedMode(masksWithSameMode[0].Mode.ToString());
                                break;
                        }
                    }
                }
            }

            return result;
        }

        // Translate a matte layer and the layer to be matted into the composited resulting brush.
        // This brush will be used to paint a sprite visual. The brush is created by using a mask brush
        // which will use the matted layer as a source and the matte layer as an alpha mask.
        // A visual tree is turned into a brush by using the CompositionVisualSurface.
        public static LayerTranslator TranslateMatteLayer(
            CompositionContext context,
            Visual matteLayer,
            Visual mattedLayer,
            bool invert)
        {
            // Calculate the context size which we will use as the size of the images we want to use
            // for the matte content and the content to be matted.
            var contextSize = context.Size;
            var objectFactory = context.ObjectFactory;

            if (objectFactory.IsUapApiAvailable(nameof(CompositionVisualSurface), versionDependentFeatureDescription: "Matte"))
            {
                var matteLayerVisualSurface = objectFactory.CreateVisualSurface();
                matteLayerVisualSurface.SourceVisual = matteLayer;
                matteLayerVisualSurface.SourceSize = contextSize;
                var matteSurfaceBrush = objectFactory.CreateSurfaceBrush(matteLayerVisualSurface);

                var mattedLayerVisualSurface = objectFactory.CreateVisualSurface();
                mattedLayerVisualSurface.SourceVisual = mattedLayer;
                mattedLayerVisualSurface.SourceSize = contextSize;
                var mattedSurfaceBrush = objectFactory.CreateSurfaceBrush(mattedLayerVisualSurface);

                return new LayerTranslator.FromVisual(CompositeVisuals(
                            context,
                            matteLayer,
                            mattedLayer,
                            contextSize,
                            Sn.Vector2.Zero,
                            invert ? CanvasComposite.DestinationOut : CanvasComposite.DestinationIn));
            }
            else
            {
                // We can't translate the matteing. Just return the layer that needed to be matted as a compromise.
                return new LayerTranslator.FromVisual(mattedLayer);
            }
        }

        // Walk the collection of layer data and for each pair of matte layer and matted layer, compose them and return a visual
        // with the composed result. All other items are not touched.
        public static IEnumerable<LayerTranslator> ComposeMattedLayers(CompositionContext context, IEnumerable<(LayerTranslator translatedLayer, Layer layer)> items)
        {
            // Save off the visual for the layer to be matted when we encounter it. The very next
            // layer is the matte layer.
            Visual mattedVisual = null;
            Layer.MatteType matteType = Layer.MatteType.None;

            // NOTE: The items appear in reverse order from how they appear in the original Lottie file.
            // This means that the layer to be matted appears right before the layer that is the matte.
            foreach (var (translatedLayer, layer) in items)
            {
                var layerIsMattedLayer = false;
                layerIsMattedLayer = layer.LayerMatteType != Layer.MatteType.None;

                Visual visual = null;

                if (translatedLayer.IsShape)
                {
                    // If the layer is a shape then we need to wrap it
                    // in a shape visual so that it can be used for matte
                    // composition.
                    if (layerIsMattedLayer || mattedVisual != null)
                    {
                        visual = translatedLayer.GetVisualRoot(context);
                    }
                }
                else
                {
                    visual = translatedLayer.GetVisualRoot(context);
                }

                if (visual != null)
                {
                    // The layer to be matted comes first. The matte layer is the very next layer.
                    if (layerIsMattedLayer)
                    {
                        mattedVisual = visual;
                        matteType = layer.LayerMatteType;
                    }
                    else if (mattedVisual != null)
                    {
                        var compositedMatteVisual = Masks.TranslateMatteLayer(context, visual, mattedVisual, matteType == Layer.MatteType.Invert);
                        mattedVisual = null;
                        matteType = Layer.MatteType.None;
                        yield return compositedMatteVisual;
                    }
                    else
                    {
                        // Return the visual that was not a matte layer or a layer to be matted.
                        yield return new LayerTranslator.FromVisual(visual);
                    }
                }
                else
                {
                    // Return the shape which does not participate in mattes.
                    yield return translatedLayer;
                }
            }
        }

        // Takes the paths for the given masks and adds them as shapes on the maskContainerShape.
        // Requires at least one Mask.
        static void TranslateAndAddMaskPaths(
            LayerContext context,
            IReadOnlyList<Mask> masks,
            CompositionContainerShape resultContainer)
        {
            Debug.Assert(masks.Count > 0, "Precondition");

            var maskMode = masks[0].Mode;

            // Translate the mask paths
            foreach (var mask in masks)
            {
                if (mask.Inverted)
                {
                    context.Issues.MaskWithInvertIsNotSupported();

                    // Mask inverted is not yet supported. Skip this mask.
                    continue;
                }

                if (mask.Opacity.IsAnimated ||
                    !mask.Opacity.InitialValue.IsOpaque)
                {
                    context.Issues.MaskWithAlphaIsNotSupported();

                    // Opacity on masks is not supported. Skip this mask.
                    continue;
                }

                switch (mask.Mode)
                {
                    case Mask.MaskMode.None:
                        // Ignore None masks. They are just a way to disable a Mask in After Effects.
                        continue;
                    default:
                        if (mask.Mode != maskMode)
                        {
                            // Every mask must have the same mode.
                            throw new InvalidOperationException();
                        }

                        break;
                }

                var path = Optimizer.TrimAnimatable(context, Optimizer.GetOptimized(context, mask.Points));

                var maskSpriteShape = Paths.TranslatePath(context, path, ShapeFill.PathFillType.EvenOdd);

                // The mask geometry needs to be colored with something so that it can be used
                // as a mask.
                maskSpriteShape.FillBrush = Brushes.CreateNonAnimatedColorBrush(context, LottieData.Color.Black);

                resultContainer.Shapes.Add(maskSpriteShape);
            }
        }

        // Enumerates the segments of Masks with the same MaskMode.
        static IEnumerable<(int index, int count)> EnumerateMaskListSegments(Mask[] masks)
        {
            int i;

            // Find the first non-None mask.
            for (i = 0; i < masks.Length && masks[i].Mode == Mask.MaskMode.None; i++)
            {
                continue;
            }

            if (i == masks.Length)
            {
                // There were only None masks in the list.
                yield break;
            }

            var currentMode = masks[i].Mode;
            var segmentIndex = i;

            for (; i < masks.Length; i++)
            {
                var mode = masks[i].Mode;
                if (mode != currentMode && mode != Mask.MaskMode.None)
                {
                    // Switching to a new mask mode. Output the segment for the previous mode.
                    yield return (segmentIndex, i - segmentIndex);

                    currentMode = mode;
                    segmentIndex = i;
                }
            }

            // Output the last segment it's not empty.
            if (segmentIndex < i)
            {
                yield return (segmentIndex, i - segmentIndex);
            }
        }

        // Translates a list of masks to a Visual which can be used to mask another Visual.
        static Visual TranslateMasks(LayerContext context, IReadOnlyList<Mask> masks)
        {
            Debug.Assert(masks.Count > 0, "Precondition");

            // Duplicate the transform chain used on the Layer being masked so
            // that the mask correctly overlays the Layer.
            if (!Transforms.TryCreateContainerShapeTransformChain(
                context,
                out var containerShapeMaskRootNode,
                out var containerShapeMaskContentNode))
            {
                // The layer is never visible. This should have been discovered already.
                throw new InvalidOperationException();
            }

            // Create the mask tree from the masks.
            TranslateAndAddMaskPaths(context, masks, containerShapeMaskContentNode);

            var result = context.ObjectFactory.CreateShapeVisualWithChild(containerShapeMaskRootNode, context.CompositionContext.Size);
            result.SetDescription(context, () => "Masks");

            return result;
        }

        static Visual TranslateAndApplyMasks(LayerContext context, IReadOnlyList<Mask> masks, Visual visualToMask, CanvasComposite compositeMode)
        {
            Debug.Assert(masks.Count > 0, "Precondition");

            if (context.ObjectFactory.IsUapApiAvailable(nameof(CompositionVisualSurface), versionDependentFeatureDescription: "Mask"))
            {
                var maskShapeVisual = TranslateMasks(context, masks);

                return CompositeVisuals(
                                    context: context,
                                    source: maskShapeVisual,
                                    destination: visualToMask,
                                    size: context.CompositionContext.Size,
                                    offset: Sn.Vector2.Zero,
                                    compositeMode: compositeMode);
            }
            else
            {
                // We can't mask, so just return the unmasked visual as a compromise.
                return visualToMask;
            }
        }

        // Combines two visual trees using a CompositeEffect. This is used for Masks and Mattes.
        // The way that the trees are combined is determined by the composite mode. The composition works as follows:
        // +--------------+
        // | SpriteVisual | -- Has the final composited result.
        // +--------------+
        //     ^
        //     |
        // +--------------+
        // | EffectBrush  | -- Composition effect brush allows the composite effect result to be used as a brush.
        // +--------------+
        //     ^
        //     *
        //     *
        //     *
        // +-----------------+
        // | CompositeEffect | -- Composite effect does the work to combine the contents
        // +-----------------+    of the visual surfaces.
        //     |
        //     |  +---------+
        //     -> | Sources |
        //        +---------+
        //         ^   ^
        //         |   |
        //         |   |
        //         |   +----------------------+
        //         |   | Source Surface Brush | -- Surface brush that will paint with the output of the visual surface
        //         |   +----------------------+    that has the source visual assigned to it.
        //         |               |
        //         |               |  +-----------------------+
        //         |               -> | Source VisualSurface  | -- The visual surface captures the renderable contents of its source visual.
        //         |                  +-----------------------+
        //         |                               |
        //         |                               |  +------------------------+
        //         |                               -> | Source Contents Visual | -- The source visual.
        //         |                                  +------------------------+
        //         |
        //         |
        //         |
        //         +--------------------------+
        //         | Destination SurfaceBrush | -- Surface brush that will paint with the output of the visual surface
        //         +--------------------------+    that has the destination visual assigned to it.
        //                         |
        //                         |  +---------------------------+
        //                         -> | Destination VisualSurface | -- The visual surface captures the renderable contents of its source visual.
        //                            +---------------------------+
        //                                         |
        //                                         |  +-----------------------------+
        //                                         -> | Destination Contents Visual | -- The source visual.
        //                                            +-----------------------------+
        static SpriteVisual CompositeVisuals(
            TranslationContext context,
            Visual source,
            Visual destination,
            Sn.Vector2 size,
            Sn.Vector2 offset,
            CanvasComposite compositeMode)
        {
            var objectFactory = context.ObjectFactory;

            // The visual surface captures the contents of a visual and displays it in a brush.
            // If the visual has an offset, it will not be captured by the visual surface.
            // To capture any offsets we add an intermediate parent container visual so that
            // the visual we want captured by the visual surface has a parent to use as the
            // origin of its offsets.
            var sourceIntermediateParent = objectFactory.CreateContainerVisual();

            // Because this is the root of a tree, the inherited BorderMode is Hard.
            // We want it to be Soft in order to enable anti-aliasing.
            // Note that the border mode for trees that are attached to the desktop do not
            // need to have their BorderMode set as they inherit Soft from the desktop.
            sourceIntermediateParent.BorderMode = CompositionBorderMode.Soft;
            sourceIntermediateParent.Children.Add(source);

            var destinationIntermediateParent = objectFactory.CreateContainerVisual();

            // Because this is the root of a tree, the inherited BorderMode is Hard.
            // We want it to be Soft in order to enable anti-aliasing.
            // Note that the border mode for trees that are attached to the desktop do not
            // need to have their BorderMode set as they inherit Soft from the desktop.
            destinationIntermediateParent.BorderMode = CompositionBorderMode.Soft;
            destinationIntermediateParent.Children.Add(destination);

            var sourceVisualSurface = objectFactory.CreateVisualSurface();
            sourceVisualSurface.SourceVisual = sourceIntermediateParent;
            sourceVisualSurface.SourceSize = ConvertTo.Vector2DefaultIsZero(size);
            sourceVisualSurface.SourceOffset = ConvertTo.Vector2DefaultIsZero(offset);
            var sourceVisualSurfaceBrush = objectFactory.CreateSurfaceBrush(sourceVisualSurface);

            var destinationVisualSurface = objectFactory.CreateVisualSurface();
            destinationVisualSurface.SourceVisual = destinationIntermediateParent;
            destinationVisualSurface.SourceSize = ConvertTo.Vector2DefaultIsZero(size);
            destinationVisualSurface.SourceOffset = ConvertTo.Vector2DefaultIsZero(offset);
            var destinationVisualSurfaceBrush = objectFactory.CreateSurfaceBrush(destinationVisualSurface);

            var compositeEffect = new CompositeEffect();
            compositeEffect.Mode = compositeMode;

            compositeEffect.Sources.Add(new CompositionEffectSourceParameter("destination"));
            compositeEffect.Sources.Add(new CompositionEffectSourceParameter("source"));

            var compositionEffectFactory = objectFactory.CreateEffectFactory(compositeEffect);
            var effectBrush = compositionEffectFactory.CreateBrush();

            effectBrush.SetSourceParameter("destination", destinationVisualSurfaceBrush);
            effectBrush.SetSourceParameter("source", sourceVisualSurfaceBrush);

            var compositedVisual = objectFactory.CreateSpriteVisual();
            compositedVisual.Brush = effectBrush;
            compositedVisual.Size = size;
            compositedVisual.Offset = ConvertTo.Vector3(offset.X, offset.Y, 0);

            return compositedVisual;
        }
    }
}
