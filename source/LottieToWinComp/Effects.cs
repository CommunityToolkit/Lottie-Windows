// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Provides access to the effects for a <see cref="Layer"/>.
    /// Reports issues for effects that are not supported, and ignores
    /// effects that are disabled.
    /// </summary>
    sealed class Effects
    {
        readonly LayerContext _context;

        internal Effects(LayerContext context)
        {
            _context = context;

            // Validate the effects, and save the valid ones for use by the layer translator.
            foreach (var effect in context.Layer.Effects.Where(e => e.IsEnabled))
            {
                switch (effect.Type)
                {
                    case Effect.EffectType.DropShadow:
                        if (DropShadowEffect is not null)
                        {
                            // Emit an issue about there being more than one.
                            context.Issues.RepeatedLayerEffect("Drop shadow");
                        }

                        DropShadowEffect = (DropShadowEffect)effect;
                        break;
                    case Effect.EffectType.GaussianBlur:
                        var gaussianBlurEffect = (GaussianBlurEffect)effect;

                        // Ignore if the effect has no blurriness. It is effectively disabled.
                        var trimmedBlurriness = Optimizer.TrimAnimatable(context, gaussianBlurEffect.Blurriness);
                        if (!trimmedBlurriness.IsAlways(0))
                        {
                            if (GaussianBlurEffect is not null)
                            {
                                // Emit an issue about there being more than one.
                                context.Issues.RepeatedLayerEffect("Gaussian blur");
                            }

                            GaussianBlurEffect = gaussianBlurEffect;
                        }

                        break;
                    default:
                        EmitIssueAboutUnsupportedEffect(effect.Type.ToString());
                        break;
                }
            }
        }

        public DropShadowEffect? DropShadowEffect { get; }

        public GaussianBlurEffect? GaussianBlurEffect { get; }

        /// <summary>
        /// If there is a drop shadow effect, emit an issue about it not being
        /// supported.
        /// </summary>
        internal void EmitIssueIfDropShadow() =>
            EmitIssueAboutUnsupportedEffect(DropShadowEffect, "drop shadow");

        /// <summary>
        /// If there is a Gaussian blur effect, emit an issue about it not being
        /// supported.
        /// </summary>
        internal void EmitIssueIfGaussianBlur() =>
            EmitIssueAboutUnsupportedEffect(GaussianBlurEffect, "Gaussian blur");

        // If the given effect is not null, emit an issue about the effect not
        // being supported on this layer.
        void EmitIssueAboutUnsupportedEffect(Effect? effect, string effectName)
        {
            if (effect is not null)
            {
                EmitIssueAboutUnsupportedEffect(effectName);
            }
        }

        // Emit an issue about the effect not being supported on this layer.
        void EmitIssueAboutUnsupportedEffect(string effectName) =>
                _context.Issues.LayerEffectNotSupportedOnLayer(effectName, _context.Layer.Type.ToString());

        /// <summary>
        /// Applies the given <see cref="DropShadowEffect"/>.
        /// This is only designed to work on a <see cref="PreCompLayer"/> and <see cref="ShapeLayer"/>
        /// because the bounds of the <paramref name="source"/> tree must be known.
        /// In case of <see cref="ShapeLayer"/> we are using parent context size instead.
        /// </summary>
        /// <returns>Visual node with shadow.</returns>
        public static Visual ApplyDropShadow(
            LayerContext context,
            Visual source,
            DropShadowEffect dropShadowEffect)
        {
            if (!context.ObjectFactory.IsUapApiAvailable(nameof(CompositionVisualSurface), versionDependentFeatureDescription: "Drop Shadow"))
            {
                // The effect can't be displayed on the targeted version.
                return source;
            }

            Debug.Assert(dropShadowEffect.IsEnabled, "Precondition");
            Debug.Assert(context is PreCompLayerContext || context is ShapeLayerContext, "Precondition");

            // Shadow:
            // +------------------+
            // | Container Visual | -- Has the final composited result.
            // +------------------+ <
            //     ^ Child #1        \ Child #2 (original layer)
            //     | (shadow layer)   \
            //     |                   \
            // +---------------------+  \
            // | ApplyGaussianBlur() |   \
            // +---------------------+   +-----------------+
            //     ^                     | ContainerVisual | - Original Visual node.
            //     |                     +-----------------+
            // +----------------+           .
            // | SpriteVisual   |           .
            // +----------------+           .
            //     ^ Source                 .
            //     |                        .
            // +--------------+             .
            // | MaskBrush    |             .
            // +--------------+             .
            //     ^ Source   ^ Mask        . Source
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
                forceGpuRendering: true);

            var factory = context.ObjectFactory;
            var size = context.CompositionContext.Size;

            if (context is PreCompLayerContext)
            {
                size = ConvertTo.Vector2(((PreCompLayerContext)context).Layer.Width, ((PreCompLayerContext)context).Layer.Height);
            }

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

            // We need to transfer "IsVisible" property from source to result
            // because result is now the root visual for both shadow and source.
            // Otherwise "IsVisible" property will be applied only to source.
            TransferIsVisibleProperty(context, source, result);

            // Check if ShadowOnly can be false
            if (!dropShadowEffect.IsShadowOnly.IsAlways(true))
            {
                // Check if ShadowOnly can be true
                if (!dropShadowEffect.IsShadowOnly.IsAlways(false))
                {
                    var isVisible = FlipBoolAnimatable(dropShadowEffect.IsShadowOnly); // isVisible = !isShadowOnly

                    source.IsVisible = isVisible.InitialValue;
                    if (isVisible.IsAnimated)
                    {
                        Animate.Boolean(
                            context,
                            Optimizer.TrimAnimatable(context, isVisible),
                            source,
                            nameof(blurResult.IsVisible));
                    }
                }

                result.Children.Add(source);
            }

            return result;
        }

        static void TransferIsVisibleProperty(TranslationContext context, Visual source, Visual target)
        {
            // Here we are transfering default value of "IsVisible" to target
            target.IsVisible = source.IsVisible;
            source.IsVisible = null;

            // Here we are transfering "isVisible" animation to target
            foreach (var animator in source.Animators.ToList())
            {
                if (animator.AnimatedProperty != nameof(source.IsVisible))
                {
                    continue;
                }

                if (animator.Animation is KeyFrameAnimation_)
                {
                    Animate.WithKeyFrame(context, target, animator.AnimatedProperty, (KeyFrameAnimation_)animator.Animation);
                }
                else if (animator.Animation is ExpressionAnimation)
                {
                    Animate.WithExpression(target, (ExpressionAnimation)animator.Animation, animator.AnimatedProperty);
                }
                else
                {
                    throw new ArgumentException("IsVisible animation should be KeyFrameAnimation_ or ExpressionAnimation");
                }

                source.StopAnimation(animator.AnimatedProperty);
            }
        }

        static Vector3 VectorFromRotationAndDistance(Rotation direction, double distance) =>
            VectorFromRotationAndDistance(direction.Radians, distance);

        /// <summary>
        /// Construct a 2D vector with a given rotation and length.
        /// Note: In After Effects 0 degrees angle corresponds to UP direction
        /// and 90 degrees angle corresponds to RIGHT direction.
        /// </summary>
        /// <param name="directionRadians">Rotation in radians.</param>
        /// <param name="distance">Vector length.</param>
        /// <returns>Vector with given parameters.</returns>
        static Vector3 VectorFromRotationAndDistance(double directionRadians, double distance) =>
            new Vector3(
                x: Math.Sin(directionRadians) * distance,
                y: -Math.Cos(directionRadians) * distance,
                z: 1);

        static Animatable<bool> FlipBoolAnimatable(Animatable<bool> animatable)
        {
            if (!animatable.IsAnimated)
            {
                return new Animatable<bool>(!animatable.InitialValue);
            }

            var keyFrames = new List<KeyFrame<bool>>();

            foreach (var keyFrame in animatable.KeyFrames)
            {
                keyFrames.Add(new KeyFrame<bool>(keyFrame.Frame, !keyFrame.Value, keyFrame.Easing));
            }

            return new Animatable<bool>(!animatable.InitialValue, keyFrames);
        }

        /// <summary>
        /// Applies a Gaussian blur effect to the given <paramref name="source"/> and
        /// returns a new root. This is only designed to work on a <see cref="PreCompLayer"/> and <see cref="ShapeLayer"/>
        /// because the bounds of the <paramref name="source"/> tree must be known.
        /// In case of <see cref="ShapeLayer"/> we are using parent context size instead.
        /// </summary>
        /// <returns>A new subtree that contains <paramref name="source"/>.</returns>
        public static Visual ApplyGaussianBlur(
            LayerContext context,
            Visual source,
            GaussianBlurEffect gaussianBlurEffect)
        {
            Debug.Assert(gaussianBlurEffect.IsEnabled, "Precondition");
            Debug.Assert(context is PreCompLayerContext || context is ShapeLayerContext, "Precondition");

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
            var size = context.CompositionContext.Size;

            if (context is PreCompLayerContext)
            {
                size = ConvertTo.Vector2(((PreCompLayerContext)context).Layer.Width, ((PreCompLayerContext)context).Layer.Height);
            }

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
