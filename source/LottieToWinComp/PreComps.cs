// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// DropShadows are currently disabled because of LayerVisual bugs.
//#define EnableDropShadow
#nullable enable

using System;
using System.Diagnostics;
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

#if EnableDropShadow
            var dropShadowEffect = context.Effects.DropShadowEffect;

            if (dropShadowEffect != null)
            {
                result = ApplyDropShadow(context, result, dropShadowEffect);
            }
#else
            context.Effects.EmitIssueIfDropShadow();
#endif

            var gaussianBlurEffect = context.Effects.GaussianBlurEffect;

            if (gaussianBlurEffect != null)
            {
                result = ApplyGaussianBlur(context, result, gaussianBlurEffect);
            }

            return new LayerTranslator.FromVisual(result);
        }

        /// <summary>
        /// Applies the given <see cref="DropShadowEffect"/>.
        /// </summary>
        static LayerVisual ApplyDropShadow(PreCompLayerContext context, Visual visual, DropShadowEffect dropShadowEffect)
        {
            Debug.Assert(dropShadowEffect.IsEnabled, "Precondition");

            // Create a LayerVisual so we can add a drop shadow.
            var result = context.ObjectFactory.CreateLayerVisual();
            result.Children.Add(visual);

            // TODO: Due to a Composition bug, LayerVisual currently must be given a size for the drop
            // shadow to show up correctly. And even then it is not reliable.
            result.Size = context.CompositionContext.Size;

            var shadow = context.ObjectFactory.CreateDropShadow();

            result.Shadow = shadow;
            shadow.SourcePolicy = CompositionDropShadowSourcePolicy.InheritFromVisualContent;

            var isShadowOnly = Optimizer.TrimAnimatable(context, dropShadowEffect.IsShadowOnly);
            if (!isShadowOnly.IsAlways(true))
            {
                context.Issues.ShadowOnlyShadowEffect();
            }

            // TODO - it's not clear whether BlurRadius and Softness are equivalent. We may
            //        need to scale Softness to convert it to BlurRadius.
            var blurRadius = Optimizer.TrimAnimatable(context, dropShadowEffect.Softness);
            if (blurRadius.IsAnimated)
            {
                Animate.Scalar(context, blurRadius, shadow, nameof(shadow.BlurRadius));
            }
            else
            {
                shadow.BlurRadius = (float)blurRadius.InitialValue;
            }

            var color = Optimizer.TrimAnimatable(context, dropShadowEffect.Color);
            if (color.IsAnimated)
            {
                Animate.Color(context, color, shadow, nameof(shadow.Color));
            }
            else
            {
                shadow.Color = ConvertTo.Color(color.InitialValue);
            }

            var opacity = Optimizer.TrimAnimatable(context, dropShadowEffect.Opacity);
            if (opacity.IsAnimated)
            {
                Animate.Opacity(context, opacity, shadow, nameof(shadow.Opacity));
            }
            else
            {
                shadow.Opacity = (float)opacity.InitialValue.Value;
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
                    Animate.Vector3(context, directionAnimation, shadow, nameof(shadow.Offset));
                }
            }
            else if (distance.IsAnimated)
            {
                // Only distance is animated.
                var directionRadians = direction.InitialValue.Radians;
                var keyFrames = distance.KeyFrames.Select(
                    kf => new KeyFrame<Vector3>(kf.Frame, VectorFromRotationAndDistance(directionRadians, kf.Value), kf.Easing)).ToArray();
                var distanceAnimation = new TrimmedAnimatable<Vector3>(context, keyFrames[0].Value, keyFrames);
                Animate.Vector3(context, distanceAnimation, shadow, nameof(shadow.Offset));
            }
            else
            {
                // Direction and distance are both not animated.
                var directionRadians = direction.InitialValue.Radians;
                var distanceValue = distance.InitialValue;

                shadow.Offset = ConvertTo.Vector3(VectorFromRotationAndDistance(direction.InitialValue, distance.InitialValue));
            }

            return result;
        }

        static Vector3 VectorFromRotationAndDistance(Rotation direction, double distance) =>
            VectorFromRotationAndDistance(direction.Radians, distance);

        static Vector3 VectorFromRotationAndDistance(double directionRadians, double distance) =>
            new Vector3(
                x: Math.Sin(directionRadians) * distance,
                y: Math.Cos(directionRadians) * distance,
                z: 1);

        /// <summary>
        /// Applies a Gaussian blur effect to the given <paramref name="source"/> and
        /// returns a new root. This is only designed to work on a <see cref="PreCompLayer"/>
        /// because the bounds of the <paramref name="source"/> tree must be known.
        /// </summary>
        /// <returns>A new subtree that contains <paramref name="source"/>.</returns>
        internal static SpriteVisual ApplyGaussianBlur(
            PreCompLayerContext context,
            Visual source,
            GaussianBlurEffect gaussianBlurEffect)
        {
            Debug.Assert(gaussianBlurEffect.IsEnabled, "Precondition");

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
            var factory = context.ObjectFactory;
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
                context.Issues.UnsupportedLayerEffectParameter("guassian blur", "blur dimension", value.Value.ToString());
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