// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.Animatables;
using CommunityToolkit.WinUI.Lottie.LottieData;
using CommunityToolkit.WinUI.Lottie.WinCompData;

#if DEBUG
// For diagnosing issues, give nothing a clip.
//#define NoClipping
#endif

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates Lottie PreComp layers.
    /// </summary>
    static class PreComps
    {
        public static LayerTranslator? CreatePreCompLayerTranslator(PreCompLayerContext context)
        {
            // TODO - the animations produced inside a PreComp need to be time-mapped.

            // Create the transform chain.
            if (!Transforms.TryCreateContainerVisualTransformChain(context, out var rootNode, out var contentsNode))
            {
                // The layer is never visible.
                return null;
            }

#if !NoClipping
            // PreComps must clip to their size.
            // Create another ContainerVisual to apply clipping to.
            var clippingNode = context.ObjectFactory.CreateContainerVisual();
            contentsNode.Children.Add(clippingNode);
            contentsNode = clippingNode;
            contentsNode.Clip = context.ObjectFactory.CreateInsetClip();
            contentsNode.Size = context.ChildrenCompositionContext.Size;
#endif

            // Add the translations of each layer to the clipping node. This will recursively
            // add the tranlation of the layers in nested precomps.
            var contentsChildren = contentsNode.Children;
            foreach (var visual in Layers.TranslateLayersToVisuals(context.ChildrenCompositionContext))
            {
                contentsChildren.Add(visual);
            }

            // Add mask if the layer has masks.
            // This must be done after all children are added to the content node.
            bool layerHasMasks = false;
#if !NoClipping
            layerHasMasks = context.Layer.Masks.Any();
#endif
            var result = context.ObjectFactory.CreateContainerVisual();

            if (layerHasMasks)
            {
                var compositedVisual = Masks.TranslateAndApplyMasksForLayer(context, rootNode);

                result.Children.Add(compositedVisual);
            }
            else
            {
                result.Children.Add(rootNode);
            }

            Visual resultVisual = result;

            var dropShadowEffect = context.Effects.DropShadowEffect;

            if (dropShadowEffect is not null)
            {
                resultVisual = Effects.ApplyDropShadow(context, resultVisual, dropShadowEffect);
            }

            var gaussianBlurEffect = context.Effects.GaussianBlurEffect;

            if (gaussianBlurEffect is not null)
            {
                resultVisual = Effects.ApplyGaussianBlur(context, resultVisual, gaussianBlurEffect);
            }

            return new LayerTranslator.FromVisual(resultVisual);
        }
    }
}