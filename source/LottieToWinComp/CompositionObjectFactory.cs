// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;

using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.Expression;
using Sn = System.Numerics;

#if DEBUG
// For diagnosing issues, give nothing a clip.
//#define NoClipping
#endif

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    sealed class CompositionObjectFactory
    {
        readonly Compositor _compositor;

        // Holds CubicBezierEasingFunctions for reuse when they have the same parameters.
        readonly Dictionary<CubicBezierEasing, CubicBezierEasingFunction> _cubicBezierEasingFunctions = new Dictionary<CubicBezierEasing, CubicBezierEasingFunction>();

        // Holds ColorBrushes that are not animated and can therefore be reused.
        readonly Dictionary<Color, CompositionColorBrush> _nonAnimatedColorBrushes = new Dictionary<Color, CompositionColorBrush>();

        // Holds a LinearEasingFunction that can be reused in multiple animations.
        readonly LinearEasingFunction _linearEasingFunction;

        // Holds a StepEasingFunction that can be reused in multiple animations.
        readonly StepEasingFunction _holdStepEasingFunction;

        // Holds a StepEasingFunction that can be reused in multiple animations.
        readonly StepEasingFunction _jumpStepEasingFunction;

        internal CompositionObjectFactory(Compositor compositor)
        {
            _compositor = compositor;

            // Initialize singletons.
            _linearEasingFunction = _compositor.CreateLinearEasingFunction();
            _holdStepEasingFunction = _compositor.CreateStepEasingFunction(1);
            _holdStepEasingFunction.IsFinalStepSingleFrame = true;
            _jumpStepEasingFunction = _compositor.CreateStepEasingFunction(1);
            _jumpStepEasingFunction.IsInitialStepSingleFrame = true;
        }

        internal CompositionEllipseGeometry CreateEllipseGeometry() => _compositor.CreateEllipseGeometry();

        internal CompositionPathGeometry CreatePathGeometry() => _compositor.CreatePathGeometry();

        internal CompositionPathGeometry CreatePathGeometry(CompositionPath path) => _compositor.CreatePathGeometry(path);

        internal CompositionRectangleGeometry CreateRectangleGeometry() => _compositor.CreateRectangleGeometry();

        internal CompositionRoundedRectangleGeometry CreateRoundedRectangleGeometry() => _compositor.CreateRoundedRectangleGeometry();

        internal CompositionColorBrush CreateColorBrush(Color color) => _compositor.CreateColorBrush(WuiColor(color));

        internal CompositionColorBrush CreateNonAnimatedColorBrush(Color color)
        {
            if (color.A == 0)
            {
                // Transparent brushes that are never animated are all equivalent.
                color = LottieData.Color.TransparentBlack;
            }

            if (!_nonAnimatedColorBrushes.TryGetValue(color, out var result))
            {
                result = CreateColorBrush(color);
                _nonAnimatedColorBrushes.Add(color, result);
            }

            return result;
        }

        internal CompositionColorGradientStop CreateColorGradientStop(float offset, Color color) => _compositor.CreateColorGradientStop(offset, WuiColor(color));

        internal CompositionLinearGradientBrush CreateLinearGradientBrush() => _compositor.CreateLinearGradientBrush();

        internal CompositionRadialGradientBrush CreateRadialGradientBrush() => _compositor.CreateRadialGradientBrush();

        internal CompositionEasingFunction CreateCompositionEasingFunction(Easing easingFunction)
        {
            if (easingFunction == null)
            {
                return null;
            }

            switch (easingFunction.Type)
            {
                case Easing.EasingType.Linear:
                    return CreateLinearEasingFunction();
                case Easing.EasingType.CubicBezier:
                    return CreateCubicBezierEasingFunction((CubicBezierEasing)easingFunction);
                case Easing.EasingType.Hold:
                    return CreateHoldThenStepEasingFunction();
                default:
                    throw new InvalidOperationException();
            }
        }

        internal LinearEasingFunction CreateLinearEasingFunction() => _linearEasingFunction;

        internal CubicBezierEasingFunction CreateCubicBezierEasingFunction(CubicBezierEasing cubicBezierEasing)
        {
            if (!_cubicBezierEasingFunctions.TryGetValue(cubicBezierEasing, out var result))
            {
                // WinComp does not support control points with components > 1. Clamp the values to 1.
                var controlPoint1 = ClampedVector2(cubicBezierEasing.ControlPoint1);
                var controlPoint2 = ClampedVector2(cubicBezierEasing.ControlPoint2);

                result = _compositor.CreateCubicBezierEasingFunction(controlPoint1, controlPoint2);
                _cubicBezierEasingFunctions.Add(cubicBezierEasing, result);
            }

            return result;
        }

        // Returns an easing function that holds its initial value and steps to the final value at the end.
        internal StepEasingFunction CreateHoldThenStepEasingFunction() => _holdStepEasingFunction;

        // Returns an easing function that steps immediately to its final value.
        internal StepEasingFunction CreateStepThenHoldEasingFunction() => _jumpStepEasingFunction;

        internal ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation() => _compositor.CreateScalarKeyFrameAnimation();

        internal ColorKeyFrameAnimation CreateColorKeyFrameAnimation()
        {
            var result = _compositor.CreateColorKeyFrameAnimation();

            // BodyMovin always uses RGB interpolation. Composition defaults to
            // HSL. Override the default to be compatible with BodyMovin.
            result.InterpolationColorSpace = CompositionColorSpace.Rgb;
            return result;
        }

        internal PathKeyFrameAnimation CreatePathKeyFrameAnimation() => _compositor.CreatePathKeyFrameAnimation();

        internal Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation() => _compositor.CreateVector2KeyFrameAnimation();

        internal Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation() => _compositor.CreateVector3KeyFrameAnimation();

        internal InsetClip CreateInsetClip() => _compositor.CreateInsetClip();

        internal CompositionGeometricClip CreateGeometricClip() => _compositor.CreateGeometricClip();

        internal CompositionContainerShape CreateContainerShape() => _compositor.CreateContainerShape();

        internal ContainerVisual CreateContainerVisual() => _compositor.CreateContainerVisual();

        internal SpriteVisual CreateSpriteVisual() => _compositor.CreateSpriteVisual();

        internal ShapeVisual CreateShapeVisualWithChild(CompositionShape child, Sn.Vector2 size)
        {
            var result = _compositor.CreateShapeVisual();
            result.Shapes.Add(child);

            // ShapeVisual clips to its size
#if NoClipping
            result.Size = Vector2(float.MaxValue);
#else
            result.Size = size;
#endif
            return result;
        }

        internal CompositionSpriteShape CreateSpriteShape() => _compositor.CreateSpriteShape();

        internal ExpressionAnimation CreateExpressionAnimation(Expr expression) => _compositor.CreateExpressionAnimation(expression);

        internal CompositionVisualSurface CreateVisualSurface() => _compositor.CreateVisualSurface();

        internal CompositionSurfaceBrush CreateSurfaceBrush(ICompositionSurface surface) => _compositor.CreateSurfaceBrush(surface);

        internal CompositionEffectFactory CreateEffectFactory(GraphicsEffectBase effect) => _compositor.CreateEffectFactory(effect);

        static float Clamp(float value, float min, float max)
        {
            Debug.Assert(min <= max, "Precondition");
            return Math.Min(Math.Max(min, value), max);
        }

        static Sn.Vector2 ClampedVector2(LottieData.Vector3 vector3) => ClampedVector2((float)vector3.X, (float)vector3.Y);

        static Sn.Vector2 ClampedVector2(float x, float y) => Vector2(Clamp(x, 0, 1), Clamp(y, 0, 1));

        static WinCompData.Wui.Color WuiColor(Color color) =>
            WinCompData.Wui.Color.FromArgb((byte)(255 * color.A), (byte)(255 * color.R), (byte)(255 * color.G), (byte)(255 * color.B));

        static Sn.Vector2 Vector2(float x, float y) => new Sn.Vector2(x, y);
    }
}