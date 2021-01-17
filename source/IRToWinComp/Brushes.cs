// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Optimization;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.Expression;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    /// <summary>
    /// Translates strokes and fills to Windows Composition brushes.
    /// </summary>
    static class Brushes
    {
        // Generates a sequence of ints from 0..int.MaxValue. Used to attach indexes to sequences using Zip.
        static readonly IEnumerable<int> PositiveInts = Enumerable.Range(0, int.MaxValue);

        public static CompositionColorBrush CreateNonAnimatedColorBrush(TranslationContext context, Color color)
        {
            var nonAnimatedColorBrushes = context.GetStateCache<StateCache>().NonAnimatedColorBrushes;

            if (!nonAnimatedColorBrushes.TryGetValue(color, out var result))
            {
                result = context.ObjectFactory.CreateNonAnimatedColorBrush(color);
                nonAnimatedColorBrushes.Add(color, result);
            }

            return result;
        }

        public static CompositionColorBrush CreateAnimatedColorBrush(LayerContext context, Color color, in TrimmedAnimatable<Opacity> opacity)
        {
            var multipliedColor = MultiplyColorByAnimatableOpacity(color, in opacity);
            return CreateAnimatedColorBrush(context, multipliedColor);
        }

        public static CompositionColorBrush CreateAnimatedColorBrush(
            LayerContext context,
            in TrimmedAnimatable<Color> color,
            CompositeOpacity opacity)
        {
            // Opacity is pushed to the alpha channel of the brush. Translate this in the simplest
            // way depending on whether the color or the opacities are animated.
            if (!opacity.IsAnimated)
            {
                // The opacity isn't animated, so it can be simply multiplied into the color.
                var nonAnimatedOpacity = opacity.NonAnimatedValue;
                return color.IsAnimated
                    ? CreateAnimatedColorBrush(context, MultiplyAnimatableColorByOpacity(color, nonAnimatedOpacity))
                    : CreateNonAnimatedColorBrush(context, color.InitialValue * nonAnimatedOpacity);
            }

            // The opacity has animation. If it's a simple animation (i.e. not composed) and the color
            // is not animated then the color can simply be multiplied by the animation. Otherwise we
            // need to create an expression to relate the opacity value to the color value.
            var animatableOpacities =
                (from a in opacity.GetAnimatables().Zip(PositiveInts, (first, second) => (First: first, Second: second))
                 select (animatable: a.First, name: $"Opacity{a.Second}")).ToArray();

            if (animatableOpacities.Length == 1 && !color.IsAnimated)
            {
                // The color is not animated, so the opacity can be multiplied into the alpha channel.
                return CreateAnimatedColorBrush(
                    context,
                    MultiplyColorByAnimatableOpacity(color.InitialValue, Optimizer.TrimAnimatable(context, animatableOpacities[0].animatable)));
            }

            // We can't simply multiply the opacity into the alpha channel because the opacity animation is not simple
            // or the color is animated. Create properties for the opacities and color and multiply them into a
            // color expression.
            var result = context.ObjectFactory.CreateColorBrush();

            // Add a property for each opacity.
            foreach (var (animatable, name) in animatableOpacities)
            {
                var trimmed = Optimizer.TrimAnimatable(context, animatable);
                var propertyName = name;
                result.Properties.InsertScalar(propertyName, ConvertTo.Opacity(trimmed.InitialValue));

                // The opacity is animated, but it might be non-animated after trimming.
                if (trimmed.IsAnimated)
                {
                    Animate.Opacity(context, trimmed, result.Properties, propertyName, propertyName, null);
                }
            }

            result.Properties.InsertVector4("Color", ConvertTo.Vector4(ConvertTo.Color(color.InitialValue)));
            if (color.IsAnimated)
            {
                Animate.ColorAsVector4(context, color, result.Properties, "Color", "Color", null);
            }

            var opacityScalarExpressions = animatableOpacities.Select(a => Expr.Scalar($"my.{a.name}")).ToArray();
            var anim = context.ObjectFactory.CreateExpressionAnimation(ExpressionFactory.MyColorAsVector4MultipliedByOpacity(opacityScalarExpressions));
            anim.SetReferenceParameter("my", result.Properties);
            Animate.ColorWithExpression(result, anim);
            return result;
        }

        static CompositionColorBrush CreateAnimatedColorBrush(LayerContext context, in TrimmedAnimatable<Color> color)
        {
            if (color.IsAnimated)
            {
                var result = context.ObjectFactory.CreateColorBrush();

                Animate.Color(
                    context,
                    color,
                    result,
                    targetPropertyName: nameof(result.Color),
                    longDescription: "Color",
                    shortDescription: null);
                return result;
            }
            else
            {
                return CreateNonAnimatedColorBrush(context, color.InitialValue);
            }
        }

        public static void TranslateAndApplyStroke(
                             LayerContext context,
                             ShapeStroke? shapeStroke,
                             CompositionSpriteShape sprite,
                             CompositeOpacity contextOpacity)
        {
            if (shapeStroke is null)
            {
                return;
            }

            if (shapeStroke.StrokeWidth.IsAlways(0))
            {
                return;
            }

            switch (shapeStroke.StrokeKind)
            {
                case ShapeStroke.ShapeStrokeKind.SolidColor:
                    TranslateAndApplySolidColorStroke(context, (SolidColorStroke)shapeStroke, sprite, contextOpacity);
                    break;
                case ShapeStroke.ShapeStrokeKind.LinearGradient:
                    TranslateAndApplyLinearGradientStroke(context, (LinearGradientStroke)shapeStroke, sprite, contextOpacity);
                    break;
                case ShapeStroke.ShapeStrokeKind.RadialGradient:
                    TranslateAndApplyRadialGradientStroke(context, (RadialGradientStroke)shapeStroke, sprite, contextOpacity);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        static void TranslateAndApplyLinearGradientStroke(
            LayerContext context,
            LinearGradientStroke shapeStroke,
            CompositionSpriteShape sprite,
            CompositeOpacity contextOpacity)
        {
            ApplyCommonStrokeProperties(
                context,
                shapeStroke,
                TranslateLinearGradient(context, shapeStroke, contextOpacity),
                sprite);
        }

        static void TranslateAndApplyRadialGradientStroke(
            LayerContext context,
            RadialGradientStroke shapeStroke,
            CompositionSpriteShape sprite,
            CompositeOpacity contextOpacity)
        {
            ApplyCommonStrokeProperties(
                context,
                shapeStroke,
                TranslateRadialGradient(context, shapeStroke, contextOpacity),
                sprite);
        }

        static void TranslateAndApplySolidColorStroke(
            LayerContext context,
            SolidColorStroke shapeStroke,
            CompositionSpriteShape sprite,
            CompositeOpacity contextOpacity)
        {
            ApplyCommonStrokeProperties(
                context,
                shapeStroke,
                TranslateSolidColorStrokeColor(context, shapeStroke, contextOpacity),
                sprite);

            // NOTE: DashPattern animation (animating dash sizes) are not supported on CompositionSpriteShape.
            foreach (var dash in shapeStroke.DashPattern)
            {
                sprite.StrokeDashArray.Add((float)dash);
            }

            // Set DashOffset
            var strokeDashOffset = Optimizer.TrimAnimatable(context, shapeStroke.DashOffset);
            if (strokeDashOffset.IsAnimated)
            {
                Animate.Scalar(context, strokeDashOffset, sprite, nameof(sprite.StrokeDashOffset));
            }
            else
            {
                sprite.StrokeDashOffset = (float)strokeDashOffset.InitialValue;
            }
        }

        // Applies the properties that are common to all Lottie ShapeStrokes to a CompositionSpriteShape.
        static void ApplyCommonStrokeProperties(
            LayerContext context,
            ShapeStroke shapeStroke,
            CompositionBrush? brush,
            CompositionSpriteShape sprite)
        {
            var strokeThickness = Optimizer.TrimAnimatable(context, shapeStroke.StrokeWidth);

            if (!ThemePropertyBindings.TryBindScalarPropertyToTheme(
                    context: context,
                    target: sprite,
                    bindingSpec: shapeStroke.Name,
                    lottiePropertyName: nameof(shapeStroke.StrokeWidth),
                    compositionPropertyName: nameof(sprite.StrokeThickness),
                    defaultValue: strokeThickness.InitialValue))
            {
                if (strokeThickness.IsAnimated)
                {
                    Animate.Scalar(context, strokeThickness, sprite, nameof(sprite.StrokeThickness));
                }
                else
                {
                    sprite.StrokeThickness = ConvertTo.Float(strokeThickness.InitialValue);
                }
            }

            sprite.StrokeStartCap = sprite.StrokeEndCap = sprite.StrokeDashCap = ConvertTo.StrokeCapDefaultIsFlat(shapeStroke.CapType);

            sprite.StrokeLineJoin = ConvertTo.StrokeLineJoinDefaultIsMiter(shapeStroke.JoinType);

            // Lottie (and SVG/CSS) defines miter limit as (miter_length / stroke_thickness).
            // WUC defines miter limit as (miter_length / (2*stroke_thickness).
            // WUC requires the value not be < 1.
            sprite.StrokeMiterLimit = ConvertTo.Float(Math.Max(shapeStroke.MiterLimit / 2, 1));

            sprite.StrokeBrush = brush;
        }

        public static CompositionBrush? TranslateShapeFill(LayerContext context, ShapeFill? shapeFill, CompositeOpacity opacity)
        {
            if (shapeFill is null)
            {
                return null;
            }

            return shapeFill.FillKind switch
            {
                ShapeFill.ShapeFillKind.SolidColor => TranslateSolidColorFill(context, (SolidColorFill)shapeFill, opacity),
                ShapeFill.ShapeFillKind.LinearGradient => TranslateLinearGradient(context, (LinearGradientFill)shapeFill, opacity),
                ShapeFill.ShapeFillKind.RadialGradient => TranslateRadialGradient(context, (RadialGradientFill)shapeFill, opacity),
                _ => throw new InvalidOperationException(),
            };
        }

        static CompositionColorBrush TranslateSolidColorStrokeColor(
            LayerContext context,
            SolidColorStroke shapeStroke,
            CompositeOpacity inheritedOpacity)
            => TranslateSolidColorWithBindings(
                context,
                shapeStroke.Color,
                inheritedOpacity.ComposedWith(Optimizer.TrimAnimatable(context, shapeStroke.Opacity)),
                bindingSpec: shapeStroke.Name);

        static CompositionColorBrush TranslateSolidColorFill(
            LayerContext context,
            SolidColorFill shapeFill,
            CompositeOpacity inheritedOpacity)
            => TranslateSolidColorWithBindings(
                context,
                shapeFill.Color,
                inheritedOpacity.ComposedWith(Optimizer.TrimAnimatable(context, shapeFill.Opacity)),
                bindingSpec: shapeFill.Name);

        // Returns a single color that can be used to represent the given animatable color.
        // This is used as the default color for property bindings. If the animatable color is
        // not animated then we return its value. If it's animated we return the value of the
        // keyframe with the highest alpha, so that it's likely to be visible.
        // The actual color we return here isn't all that important since it is expected to be set
        // to some other value at runtime via property binding, but it is handy to have a visible
        // color when testing, and even better if the color looks like what the designer saw.
        static Color DefaultValueOf(Animatable<Color> animatableColor)
            => animatableColor.IsAnimated
                ? animatableColor.KeyFrames.ToArray().OrderByDescending(kf => kf.Value.A).First().Value
                : animatableColor.InitialValue;

        static CompositionColorBrush TranslateSolidColorWithBindings(
            LayerContext context,
            Animatable<Color> color,
            CompositeOpacity opacity,
            string bindingSpec)
        {
            // Look for a color binding embedded into the name of the fill or stroke.
            var bindingName = ThemePropertyBindings.GetThemeBindingNameForLottieProperty(context, bindingSpec, "Color");

            if (bindingName != null)
            {
                // A color binding string was found. Bind the color to a property with the
                // name described by the binding string.
                return TranslateBoundSolidColor(context, opacity, bindingName, displayName: bindingName, DefaultValueOf(color));
            }

            if (context.Translation.ColorPalette != null && !color.IsAnimated)
            {
                // Color palette binding is enabled. Bind the color to a property with
                // the name of the color in the palette.
                var paletteColor = color.InitialValue;

                var paletteColorAsWinUIColor = ConvertTo.Color(paletteColor);

                if (!context.Translation.ColorPalette.TryGetValue(paletteColor, out bindingName))
                {
                    bindingName = $"Color_{paletteColorAsWinUIColor.HexWithoutAlpha}";
                    context.Translation.ColorPalette.Add(paletteColor, bindingName);
                }

                return TranslateBoundSolidColor(
                    context,
                    opacity,
                    bindingName,
                    displayName: $"#{paletteColorAsWinUIColor.R:X2}{paletteColorAsWinUIColor.G:X2}{paletteColorAsWinUIColor.B:X2}",
                    paletteColor);
            }

            // Do not generate a binding for this color.
            return Brushes.CreateAnimatedColorBrush(context, Optimizer.TrimAnimatable(context, color), opacity);
        }

        // Translates a SolidColorFill that gets its color value from a property set value with the given name.
        static CompositionColorBrush TranslateBoundSolidColor(
                LayerContext context,
                CompositeOpacity opacity,
                string bindingName,
                string displayName,
                Color defaultColor)
        {
            // Ensure there is a property added to the theme property set.
            ThemePropertyBindings.EnsureColorThemePropertyExists(context, bindingName, displayName, defaultColor);

            var result = context.ObjectFactory.CreateColorBrush();

            if (context.Translation.AddDescriptions)
            {
                result.SetDescription(context, $"Color bound to theme property value: {bindingName}", bindingName);

                // Name the brush with a name that includes the binding name. This will allow the code generator to
                // give its factory a more meaningful name.
                result.SetName($"ThemeColor_{bindingName}");
            }

            if (opacity.IsAnimated)
            {
                // The opacity has animation. Create an expression to relate the opacity value to the color value.
                var animatableOpacities =
                    (from a in opacity.GetAnimatables().Zip(PositiveInts, (first, second) => (First: first, Second: second))
                     select (animatable: a.First, name: $"Opacity{a.Second}")).ToArray();

                // Add a property for each opacity.
                foreach (var (animatable, name) in animatableOpacities)
                {
                    var trimmed = Optimizer.TrimAnimatable(context, animatable);
                    var propertyName = name;
                    result.Properties.InsertScalar(propertyName, ConvertTo.Opacity(trimmed.InitialValue));

                    // The opacity is animated, but it might be non-animated after trimming.
                    if (trimmed.IsAnimated)
                    {
                        Animate.Opacity(context, trimmed, result.Properties, propertyName, propertyName, null);
                    }
                }

                var opacityScalarExpressions = animatableOpacities.Select(a => Expr.Scalar($"my.{a.name}")).ToArray();
                var anim = context.ObjectFactory.CreateExpressionAnimation(ExpressionFactory.ThemedColorAsVector4MultipliedByOpacities(bindingName, opacityScalarExpressions));
                anim.SetReferenceParameter("my", result.Properties);
                anim.SetReferenceParameter(ThemePropertyBindings.ThemePropertiesName, ThemePropertyBindings.GetThemePropertySet(context));

                Animate.ColorWithExpression(result, anim);
            }
            else
            {
                // Opacity isn't animated.
                // Create an expression that multiplies the alpha channel of the color by the opacity value.
                var anim = context.ObjectFactory.CreateExpressionAnimation(ExpressionFactory.ThemedColorMultipliedByOpacity(bindingName, opacity.NonAnimatedValue));
                anim.SetReferenceParameter(ThemePropertyBindings.ThemePropertiesName, ThemePropertyBindings.GetThemePropertySet(context));
                Animate.ColorWithExpression(result, anim);
            }

            return result;
        }

        static CompositionLinearGradientBrush? TranslateLinearGradient(
            LayerContext context,
            IGradient linearGradient,
            CompositeOpacity opacity)
        {
            var result = context.ObjectFactory.CreateLinearGradientBrush();

            // BodyMovin specifies start and end points in absolute values.
            result.MappingMode = CompositionMappingMode.Absolute;

            var startPoint = Optimizer.TrimAnimatable(context, linearGradient.StartPoint);
            var endPoint = Optimizer.TrimAnimatable(context, linearGradient.EndPoint);

            if (startPoint.IsAnimated)
            {
                Animate.Vector2(context, startPoint, result, nameof(result.StartPoint));
            }
            else
            {
                result.StartPoint = ConvertTo.Vector2(startPoint.InitialValue);
            }

            if (endPoint.IsAnimated)
            {
                Animate.Vector2(context, endPoint, result, nameof(result.EndPoint));
            }
            else
            {
                result.EndPoint = ConvertTo.Vector2(endPoint.InitialValue);
            }

            var gradientStops = Optimizer.TrimAnimatable(context, linearGradient.GradientStops);

            if (gradientStops.InitialValue.IsEmpty)
            {
                // If there are no gradient stops then we can't create a brush.
                return null;
            }

            TranslateAndApplyGradientStops(context, result, in gradientStops, opacity);

            return result;
        }

        static CompositionGradientBrush? TranslateRadialGradient(
            LayerContext context,
            IRadialGradient gradient,
            CompositeOpacity opacity)
        {
            if (!context.ObjectFactory.IsUapApiAvailable(nameof(CompositionRadialGradientBrush), versionDependentFeatureDescription: "Radial gradient fill"))
            {
                // CompositionRadialGradientBrush didn't exist until UAP v8. If the target OS doesn't support
                // UAP v8 then fall back to linear gradients as a compromise.
                return TranslateLinearGradient(context, gradient, opacity);
            }

            var result = context.ObjectFactory.CreateRadialGradientBrush();

            // BodyMovin specifies start and end points in absolute values.
            result.MappingMode = CompositionMappingMode.Absolute;

            var startPoint = Optimizer.TrimAnimatable(context, gradient.StartPoint);
            var endPoint = Optimizer.TrimAnimatable(context, gradient.EndPoint);

            if (startPoint.IsAnimated)
            {
                Animate.Vector2(context, startPoint, result, nameof(result.EllipseCenter));
            }
            else
            {
                result.EllipseCenter = ConvertTo.Vector2(startPoint.InitialValue);
            }

            if (endPoint.IsAnimated)
            {
                // We don't yet support animated EndPoint.
                context.Issues.GradientFillIsNotSupported("Radial", "animated end point");
            }

            result.EllipseRadius = new Sn.Vector2(Sn.Vector2.Distance(ConvertTo.Vector2(startPoint.InitialValue), ConvertTo.Vector2(endPoint.InitialValue)));

            if (gradient.HighlightLength != null &&
                (gradient.HighlightLength.InitialValue != 0 || gradient.HighlightLength.IsAnimated))
            {
                // We don't yet support animated HighlightLength.
                context.Issues.GradientFillIsNotSupported("Radial", "animated highlight length");
            }

            var gradientStops = Optimizer.TrimAnimatable(context, gradient.GradientStops);

            if (gradientStops.InitialValue.IsEmpty)
            {
                // If there are no gradient stops then we can't create a brush.
                return null;
            }

            TranslateAndApplyGradientStops(context, result, in gradientStops, opacity);

            return result;
        }

        static void TranslateAndApplyGradientStops(
            LayerContext context,
            CompositionGradientBrush brush,
            in TrimmedAnimatable<Sequence<GradientStop>> gradientStops,
            CompositeOpacity opacity)
        {
            if (gradientStops.IsAnimated)
            {
                TranslateAndApplyAnimatedGradientStops(context, brush, gradientStops, opacity);
            }
            else
            {
                TranslateAndApplyNonAnimatedGradientStops(context, brush, gradientStops.InitialValue, opacity);
            }
        }

        static void TranslateAndApplyAnimatedGradientStops(
            LayerContext context,
            CompositionGradientBrush brush,
            in TrimmedAnimatable<Sequence<GradientStop>> gradientStops,
            CompositeOpacity opacity)
        {
            if (opacity.IsAnimated)
            {
                TranslateAndApplyAnimatedGradientStopsWithAnimatedOpacity(context, brush, in gradientStops, opacity);
            }
            else
            {
                TranslateAndApplyAnimatedColorGradientStopsWithStaticOpacity(context, brush, in gradientStops, opacity.NonAnimatedValue);
            }
        }

        static void TranslateAndApplyAnimatedGradientStopsWithAnimatedOpacity(
            LayerContext context,
            CompositionGradientBrush brush,
            in TrimmedAnimatable<Sequence<GradientStop>> gradientStops,
            CompositeOpacity opacity)
        {
            // Lottie represents animation of stops as a sequence of lists of stops.
            // WinComp uses a single list of stops where each stop is animated.

            // Lottie represents stops as either color or opacity stops. Convert them all to color stops.
            var colorStopKeyFrames = gradientStops.KeyFrames.SelectToArray(kf => GradientStopOptimizer.Optimize(kf));
            colorStopKeyFrames = GradientStopOptimizer.RemoveRedundantStops(colorStopKeyFrames).ToArray();
            var stopsCount = colorStopKeyFrames[0].Value.Count();
            var keyframesCount = colorStopKeyFrames.Length;

            // The opacity has animation. Create an expression to relate the opacity value to the color value.
            var animatableOpacities =
                (from a in opacity.GetAnimatables().Zip(PositiveInts, (first, second) => (First: first, Second: second))
                 select (animatable: a.First, name: $"Opacity{a.Second}")).ToArray();

            // Add a property for each opacity.
            foreach (var (animatable, name) in animatableOpacities)
            {
                var trimmedOpacity = Optimizer.TrimAnimatable(context, animatable);
                var propertyName = name;
                brush.Properties.InsertScalar(propertyName, ConvertTo.Opacity(trimmedOpacity.InitialValue * 255));

                // Pre-multiply the opacities by 255 so we can use the simpler
                // expression for multiplying color by opacity.
                Animate.ScaledOpacity(context, trimmedOpacity, 255, brush.Properties, propertyName, propertyName, null);
            }

            var opacityExpressions = animatableOpacities.Select(ao => Expr.Scalar($"my.{ao.name}")).ToArray();

            // Create the Composition stops and animate them.
            for (var i = 0; i < stopsCount; i++)
            {
                var gradientStop = context.ObjectFactory.CreateColorGradientStop();

                gradientStop.SetDescription(context, () => $"Stop {i}");

                brush.ColorStops.Add(gradientStop);

                // Extract the color key frames for this stop.
                var colorKeyFrames = ExtractKeyFramesFromColorStopKeyFrames(
                    colorStopKeyFrames,
                    i,
                    gs => ExpressionFactory.ColorMultipliedByPreMultipliedOpacities(ConvertTo.Color(gs.Color), opacityExpressions)).ToArray();

                // Bind the color to the opacities multiplied by the colors.
                Animate.ColorWithExpressionKeyFrameAnimation(
                    context,
                    new TrimmedAnimatable<WinCompData.Expressions.Color>(context, colorKeyFrames[0].Value, colorKeyFrames),
                    gradientStop,
                    nameof(gradientStop.Color),
                    anim => anim.SetReferenceParameter("my", brush.Properties));

                // Extract the offset key frames for this stop.
                var offsetKeyFrames = ExtractKeyFramesFromColorStopKeyFrames(colorStopKeyFrames, i, gs => gs.Offset).ToArray();
                Animate.Scalar(
                    context,
                    new TrimmedAnimatable<double>(context, offsetKeyFrames[0].Value, offsetKeyFrames),
                    gradientStop,
                    nameof(gradientStop.Offset));
            }
        }

        static void TranslateAndApplyAnimatedColorGradientStopsWithStaticOpacity(
            LayerContext context,
            CompositionGradientBrush brush,
            in TrimmedAnimatable<Sequence<GradientStop>> gradientStops,
            Opacity opacity)
        {
            // Lottie represents animation of stops as a sequence of lists of stops.
            // WinComp uses a single list of stops where each stop is animated.

            // Lottie represents stops as either color or opacity stops. Convert them all to color stops.
            var colorStopKeyFrames = gradientStops.KeyFrames.SelectToArray(kf => GradientStopOptimizer.Optimize(kf));
            colorStopKeyFrames = GradientStopOptimizer.RemoveRedundantStops(colorStopKeyFrames).ToArray();
            var stopsCount = colorStopKeyFrames[0].Value.Count();
            var keyframesCount = colorStopKeyFrames.Length;

            // Create the Composition stops and animate them.
            for (var i = 0; i < stopsCount; i++)
            {
                var gradientStop = context.ObjectFactory.CreateColorGradientStop();

                gradientStop.SetDescription(context, () => $"Stop {i}");

                brush.ColorStops.Add(gradientStop);

                // Extract the color key frames for this stop.
                var colorKeyFrames = ExtractKeyFramesFromColorStopKeyFrames(
                    colorStopKeyFrames,
                    i,
                    gs => gs.Color * opacity).ToArray();

                Animate.Color(
                    context,
                    new TrimmedAnimatable<Color>(context, colorKeyFrames[0].Value, colorKeyFrames),
                    gradientStop,
                    nameof(gradientStop.Color));

                // Extract the offset key frames for this stop.
                var offsetKeyFrames = ExtractKeyFramesFromColorStopKeyFrames(colorStopKeyFrames, i, gs => gs.Offset).ToArray();
                Animate.Scalar(
                    context,
                    new TrimmedAnimatable<double>(context, offsetKeyFrames[0].Value, offsetKeyFrames),
                    gradientStop,
                    nameof(gradientStop.Offset));
            }
        }

        static void TranslateAndApplyNonAnimatedGradientStops(
            LayerContext context,
            CompositionGradientBrush brush,
            Sequence<GradientStop> gradientStops,
            CompositeOpacity opacity)
        {
            var optimizedGradientStops = GradientStopOptimizer.OptimizeColorStops(GradientStopOptimizer.Optimize(gradientStops));

            if (opacity.IsAnimated)
            {
                TranslateAndApplyNonAnimatedColorGradientStopsWithAnimatedOpacity(context, brush, optimizedGradientStops, opacity);
            }
            else
            {
                TranslateAndApplyNonAnimatedColorGradientStopsWithStaticOpacity(context, brush, optimizedGradientStops, opacity.NonAnimatedValue);
            }
        }

        static void TranslateAndApplyNonAnimatedColorGradientStopsWithStaticOpacity(
            LayerContext context,
            CompositionGradientBrush brush,
            IEnumerable<ColorGradientStop> gradientStops,
            Opacity opacity)
        {
            var i = 0;
            foreach (var stop in gradientStops)
            {
                var color = stop.Color * opacity;

                var gradientStop = context.ObjectFactory.CreateColorGradientStop(ConvertTo.Float(stop.Offset), color);

                gradientStop.SetDescription(context, () => $"Stop {i}");

                brush.ColorStops.Add(gradientStop);
                i++;
            }
        }

        static void TranslateAndApplyNonAnimatedColorGradientStopsWithAnimatedOpacity(
            LayerContext context,
            CompositionGradientBrush brush,
            IEnumerable<ColorGradientStop> gradientStops,
            CompositeOpacity opacity)
        {
            // The opacity has animation. Create an expression to relate the opacity value to the color value.
            var animatableOpacities =
                (from a in opacity.GetAnimatables().Zip(PositiveInts, (first, second) => (First: first, Second: second))
                 select (animatable: a.First, name: $"Opacity{a.Second}")).ToArray();

            // Add a property for each opacity.
            foreach (var (animatable, name) in animatableOpacities)
            {
                var trimmedOpacity = Optimizer.TrimAnimatable(context, animatable);
                var propertyName = name;
                brush.Properties.InsertScalar(propertyName, ConvertTo.Opacity(trimmedOpacity.InitialValue * 255));

                // The opacity is animated, but it might be non-animated after trimming.
                if (trimmedOpacity.IsAnimated)
                {
                    // Pre-multiply the opacities by 255 so we can use the simpler
                    // expression for multiplying color by opacity.
                    Animate.ScaledOpacity(context, trimmedOpacity, 255, brush.Properties, propertyName, propertyName, null);
                }
            }

            var opacityExpressions = animatableOpacities.Select(ao => Expr.Scalar($"my.{ao.name}")).ToArray();

            var i = 0;
            foreach (var stop in gradientStops)
            {
                var gradientStop = context.ObjectFactory.CreateColorGradientStop();

                gradientStop.SetDescription(context, () => $"Stop {i}");

                gradientStop.Offset = ConvertTo.Float(stop.Offset);

                if (stop.Color.A == 0)
                {
                    // The stop has 0 alpha, so no point multiplying it by opacity.
                    gradientStop.Color = ConvertTo.Color(stop.Color);
                }
                else
                {
                    // Bind the color to the opacity multiplied by the color.
                    var anim = context.ObjectFactory.CreateExpressionAnimation(ExpressionFactory.ColorMultipliedByPreMultipliedOpacities(ConvertTo.Color(stop.Color), opacityExpressions));
                    anim.SetReferenceParameter("my", brush.Properties);
                    Animate.ColorWithExpression(gradientStop, anim);
                }

                brush.ColorStops.Add(gradientStop);
                i++;
            }
        }

        static IEnumerable<KeyFrame<TKeyFrame>> ExtractKeyFramesFromColorStopKeyFrames<TKeyFrame>(
            KeyFrame<Sequence<ColorGradientStop>>[] stops,
            int stopIndex,
            Func<ColorGradientStop, TKeyFrame> selector)
            where TKeyFrame : IEquatable<TKeyFrame>
        {
            for (var i = 0; i < stops.Length; i++)
            {
                var kf = stops[i];
                var value = kf.Value[stopIndex];
                var selected = selector(value);

                yield return kf.CloneWithNewValue(selected);
            }
        }

        static TrimmedAnimatable<Color> MultiplyColorByAnimatableOpacity(
            Color color,
            in TrimmedAnimatable<Opacity> opacity)
        {
            if (!opacity.IsAnimated)
            {
                return new TrimmedAnimatable<Color>(opacity.Context, color * opacity.InitialValue);
            }
            else
            {
                // Multiply the single color value by the opacity animation.
                return new TrimmedAnimatable<Color>(
                    opacity.Context,
                    initialValue: color * opacity.InitialValue,
                    keyFrames: opacity.KeyFrames.SelectToArray(kf => kf.CloneWithNewValue(color * kf.Value)));
            }
        }

        static TrimmedAnimatable<Color> MultiplyAnimatableColorByOpacity(
            in TrimmedAnimatable<Color> color,
            Opacity opacity)
        {
            var initialColorValue = color.InitialValue * opacity;

            if (color.IsAnimated)
            {
                // Multiply the color animation by the opacity.
                return new TrimmedAnimatable<Color>(
                    color.Context,
                    initialValue: initialColorValue,
                    keyFrames: color.KeyFrames.SelectToArray(kf => kf.CloneWithNewValue(kf.Value * opacity)));
            }
            else
            {
                return new TrimmedAnimatable<Color>(color.Context, initialColorValue);
            }
        }

        sealed class StateCache
        {
            /// <summary>
            /// A cache of color brushes that are not animated and can therefore be reused.
            /// </summary>
            public Dictionary<Color, CompositionColorBrush> NonAnimatedColorBrushes { get; } = new Dictionary<Color, CompositionColorBrush>();
        }
    }
}
