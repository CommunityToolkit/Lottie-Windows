// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
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

            var dropShadowEffect = context.Effects.DropShadowEffect;

            if (dropShadowEffect is not null)
            {
                result = ApplyDropShadow(context, result, dropShadowEffect);
            }

            var gaussianBlurEffect = context.Effects.GaussianBlurEffect;

            if (gaussianBlurEffect is not null)
            {
                result = ApplyGaussianBlur(context, result, gaussianBlurEffect);
            }

            return new LayerTranslator.FromVisual(result);
        }

        /// <summary>
        /// Applies the given <see cref="DropShadowEffect"/>.
        /// </summary>
        static ContainerVisual ApplyDropShadow(PreCompLayerContext context, ContainerVisual source, DropShadowEffect dropShadowEffect)
        {
            Debug.Assert(dropShadowEffect.IsEnabled, "Precondition");

            // Shadow:
            // +------------------+
            // | Container Visual | -- Has the final composited result.
            // +------------------+ <------
            //     ^ Child                | Child
            //     |                      |
            // +---------------------+   +-----------------+
            // | ApplyGaussianBlur() |   | ContainerVisual |
            // +---------------------+   +-----------------+
            //     ^                        |
            //     |                        |
            // +----------------+           |
            // | SpriteVisual   |           |
            // +----------------+           |
            //     ^ Source                 |
            //     |                        |
            // +--------------+             |
            // | MaskBrush    |             |
            // +--------------+             |
            //     ^ Source   ^ Mask        |
            //     |           \            V
            // +----------+   +---------------+
            // |ColorBrush|   | VisualSurface |
            // +----------+   +---------------+
            GaussianBlurEffect gaussianBlurEffect = new GaussianBlurEffect(
                name: dropShadowEffect.Name + "_blur",
                isEnabled: true,
                blurriness: dropShadowEffect.Softness,
                blurDimensions: new Animatable<Enum<BlurDimension>>(BlurDimension.HorizontalAndVertical),
                repeatEdgePixels: new Animatable<bool>(true),
                forceGpuRendering: true
                );

            var factory = context.ObjectFactory;
            var size = ConvertTo.Vector2(context.Layer.Width, context.Layer.Height);

            var visualSurface = factory.CreateVisualSurface();
            visualSurface.SourceSize = size;
            visualSurface.SourceVisual = source;

            var maskBrush = factory.CreateMaskBrush();

            var colorBrush = factory.CreateColorBrush(dropShadowEffect.Color.InitialValue);

            var color = Optimizer.TrimAnimatable(context, dropShadowEffect.Color);
            if (color.IsAnimated)
            {
                Animate.Color(context, color, colorBrush, nameof(colorBrush.Color));
            }
            else
            {
                colorBrush.Color = ConvertTo.Color(color.InitialValue);
            }

            maskBrush.Source = colorBrush;
            maskBrush.Mask = factory.CreateSurfaceBrush(visualSurface);

            var shadowSpriteVisual = factory.CreateSpriteVisual();
            shadowSpriteVisual.Size = size;
            shadowSpriteVisual.Brush = maskBrush;

            var blurResult = ApplyGaussianBlur(context, shadowSpriteVisual, gaussianBlurEffect);

            var opacity = Optimizer.TrimAnimatable(context, dropShadowEffect.Opacity);
            if (opacity.IsAnimated)
            {
                Animate.Opacity(context, opacity, blurResult, nameof(blurResult.Opacity));
            }
            else
            {
                blurResult.Opacity = (float)opacity.InitialValue.Value;
            }

            // Convert direction and distance to a Vector3.
            var direction = Optimizer.TrimAnimatable(context, dropShadowEffect.Direction);
            var distance = Optimizer.TrimAnimatable(context, dropShadowEffect.Distance);

            if (direction.IsAnimated)
            {
                if (distance.IsAnimated)
                {
                    // Direction and distance are animated.
                    // NOTE: we could support this in some cases. The worst cases are
                    //       where the keyframes don't line up, and/or the easings are different
                    //       between distance and direction.
                    context.Issues.AnimatedLayerEffectParameters("drop shadow");
                }
                else
                {
                    // Only direction is animated.
                    var distanceValue = distance.InitialValue;
                    var keyFrames = direction.KeyFrames.Select(
                        kf => new KeyFrame<Vector3>(kf.Frame, VectorFromRotationAndDistance(kf.Value, distanceValue), kf.Easing)).ToArray();
                    var directionAnimation = new TrimmedAnimatable<Vector3>(context, keyFrames[0].Value, keyFrames);
                    Animate.Vector3(context, directionAnimation, blurResult, nameof(blurResult.Offset));
                }
            }
            else if (distance.IsAnimated)
            {
                // Only distance is animated.
                var directionRadians = direction.InitialValue.Radians;
                var keyFrames = distance.KeyFrames.Select(
                    kf => new KeyFrame<Vector3>(kf.Frame, VectorFromRotationAndDistance(directionRadians, kf.Value), kf.Easing)).ToArray();
                var distanceAnimation = new TrimmedAnimatable<Vector3>(context, keyFrames[0].Value, keyFrames);
                Animate.Vector3(context, distanceAnimation, blurResult, nameof(blurResult.Offset));
            }
            else
            {
                // Direction and distance are both not animated.
                var directionRadians = direction.InitialValue.Radians;
                var distanceValue = distance.InitialValue;

                blurResult.Offset = ConvertTo.Vector3(VectorFromRotationAndDistance(direction.InitialValue, distance.InitialValue));
            }

            var result = factory.CreateContainerVisual();
            result.Size = size;
            result.Children.Add(blurResult);
            if (dropShadowEffect.IsShadowOnly.IsAlways(false))
            {
                result.Children.Add(source);
            }

            return result;
        }

        static Vector3 VectorFromRotationAndDistance(Rotation direction, double distance) =>
            VectorFromRotationAndDistance(direction.Radians, distance);

        static Vector3 VectorFromRotationAndDistance(double directionRadians, double distance) =>
            new Vector3(
                x: Math.Sin(directionRadians) * distance,
                y: -Math.Cos(directionRadians) * distance,
                z: 1);

        /// <summary>
        /// Applies a Gaussian blur effect to the given <paramref name="source"/> and
        /// returns a new root. This is only designed to work on a <see cref="PreCompLayer"/>
        /// because the bounds of the <paramref name="source"/> tree must be known.
        /// </summary>
        /// <returns>A new subtree that contains <paramref name="source"/>.</returns>
        internal static ContainerVisual ApplyGaussianBlur(
            PreCompLayerContext context,
            ContainerVisual source,
            GaussianBlurEffect gaussianBlurEffect)
        {
            Debug.Assert(gaussianBlurEffect.IsEnabled, "Precondition");

            var factory = context.ObjectFactory;

            if (!factory.IsUapApiAvailable(nameof(CompositionVisualSurface), versionDependentFeatureDescription: "Gaussian blur"))
            {
                // The effect can't be displayed on the targeted version.
                return source;
            }

            // Gaussian blur:
            // +--------------+
            // | SpriteVisual | -- Has the final composited result.
            // +--------------+
            //     ^
            //     |
            // +--------------+
            // | EffectBrush  | -- Composition effect brush allows the composite effect result to be used as a brush.
            // +--------------+
            //     ^
            //     |
            // +--------------------+
            // | GaussianBlurEffect |
            // +--------------------+
            //     ^ Source
            //     |
            // +--------------+
            // | SurfaceBrush | -- Surface brush that will paint with the output of the VisualSurface
            // +--------------+    that has the source visual assigned to it.
            //      ^ CompositionEffectSourceParameter("source")
            //      |
            // +---------------+
            // | VisualSurface | -- The visual surface captures the renderable contents of its source visual.
            // +---------------+
            //      ^
            //      |
            //  +--------+
            //  | Visual | -- The layer translated to a Visual.
            //  +--------+
            var size = ConvertTo.Vector2(context.Layer.Width, context.Layer.Height);

            // Build from the bottom up.
            var visualSurface = factory.CreateVisualSurface();
            visualSurface.SourceVisual = source;
            visualSurface.SourceSize = size;

            var surfaceBrush = factory.CreateSurfaceBrush(visualSurface);

            var effect = new WinCompData.Mgce.GaussianBlurEffect();

            var blurriness = Optimizer.TrimAnimatable(context, gaussianBlurEffect.Blurriness);
            if (blurriness.IsAnimated)
            {
                context.Issues.AnimatedLayerEffectParameters("Gaussian blur");
            }

            effect.BlurAmount = ConvertTo.Float(blurriness.InitialValue / 10.0);

            // We only support HorizontalAndVertical blur dimension.
            var blurDimensions = Optimizer.TrimAnimatable(context, gaussianBlurEffect.BlurDimensions);
            var unsupportedBlurDimensions = blurDimensions
                .KeyFrames
                .Select(kf => kf.Value)
                .Distinct()
                .Where(v => v.Value != BlurDimension.HorizontalAndVertical).ToArray();

            foreach (var value in unsupportedBlurDimensions)
            {
                context.Issues.UnsupportedLayerEffectParameter("gaussian blur", "blur dimension", value.Value.ToString());
            }

            effect.Source = new CompositionEffectSourceParameter("source");

            var effectBrush = factory.CreateEffectFactory(effect).CreateBrush();
            effectBrush.SetSourceParameter("source", surfaceBrush);

            var result = factory.CreateSpriteVisual();
            result.Brush = effectBrush;
            result.Size = size;

            return result;
        }
    }
}