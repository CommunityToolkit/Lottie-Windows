// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

#if DEBUG
// For diagnosing issues, give nothing a clip.
//#define NoClipping
#endif

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
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

#if DropShadows // Drop shadows are not yet supported.
            var dropShadowEffects =
                context.Layer.Effects.Where(eff => eff.Type == LottieData.Effect.EffectType.DropShadow).ToArray();

            if (dropShadowEffects.Length > 0 && dropShadowEffects.All(eff => eff.Type == LottieData.Effect.EffectType.DropShadow))
            {
                // TODO - if they're not all drop shadows, ISSUE.
                // TODO - ignore if not enabled.

                // Create a LayerVisual so we can add a drop shadow.
                var layerVisual = context.ObjectFactory.CreateLayerVisual();
                var shadow = context.ObjectFactory.CreateDropShadow();
                layerVisual.Shadow = shadow;

                // TODO - use the correct value.
                shadow.Offset = new System.Numerics.Vector3(1);
                layerVisual.Children.Add(result);
                result = layerVisual;
        }
#endif // DropShadows

            return new LayerTranslator.FromVisual(result);
        }
    }
}