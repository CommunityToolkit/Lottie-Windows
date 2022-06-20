// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.WinUI.Lottie.Animatables;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using static CommunityToolkit.WinUI.Lottie.LottieToWinComp.ConvertTo;
using Expr = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions.Expression;
using Sn = System.Numerics;

#if DEBUG
// For diagnosing issues, give nothing a clip.
//#define NoClipping
#endif

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
{
    sealed class CompositionObjectFactory
    {
        readonly TranslationContext _context;
        readonly Compositor _compositor;

        // The UAP version of the Compositor.
        // This is used to determine whether a class that is only available in
        // some UAP version is available from the factory.
        readonly uint _targetUapVersion;

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

        internal CompositionObjectFactory(TranslationContext context, Compositor compositor, uint targetUapVersion)
        {
            _context = context;
            _compositor = compositor;
            _targetUapVersion = targetUapVersion;

            // Initialize singletons.
            _linearEasingFunction = _compositor.CreateLinearEasingFunction();
            _holdStepEasingFunction = _compositor.CreateStepEasingFunction(1);
            _holdStepEasingFunction.IsFinalStepSingleFrame = true;
            _jumpStepEasingFunction = _compositor.CreateStepEasingFunction(1);
            _jumpStepEasingFunction.IsInitialStepSingleFrame = true;
        }

        // The UAP version required for the objects that have been produced by the factory so far.
        // Defaults to 7 because that is the first version in which Shapes became usable.
        internal uint HighestUapVersionUsed { get; private set; } = 7;

        // Checks whether the given API is available for the UAP version of the compositor.
        // Only responds to features above version 7.
        internal bool IsUapApiAvailable(string apiName) => GetUapVersionForApi(apiName) <= _targetUapVersion;

        // Returns the UAP version required for the given API.
        // Only responds to features above version 7.
        internal uint GetUapVersionForApi(string apiName)
        {
            switch (apiName)
            {
                // Classes introduced in version 8.
                case nameof(CompositionRadialGradientBrush):
                case nameof(CompositionVisualSurface):
                    return 8;

                // Classes introduced in version 11.
                // PathKeyFrameAnimation was introduced in 6, but was unreliable
                // until 11.
                case nameof(PathKeyFrameAnimation):
                    return 11;

                // AnimationController class was introduced in version 6, but
                // it became possible to create it explicitly only after verstion 15
                // with compositor.CreateAnimationController() method
                case nameof(AnimationController):
                    return 15;

                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Checks whether the given API is available for the current translation.
        /// </summary>
        /// <returns><c>true</c> if the given api is available in this translation.</returns>
        internal bool IsUapApiAvailable(string apiName, string versionDependentFeatureDescription)
        {
            if (!IsUapApiAvailable(apiName))
            {
                _context.Issues.UapVersionNotSupported(versionDependentFeatureDescription, GetUapVersionForApi(apiName).ToString());
                return false;
            }

            return true;
        }

        internal CompositionEllipseGeometry CreateEllipseGeometry() => _compositor.CreateEllipseGeometry();

        internal CompositionPathGeometry CreatePathGeometry() => _compositor.CreatePathGeometry();

        internal CompositionPathGeometry CreatePathGeometry(CompositionPath path) => _compositor.CreatePathGeometry(path);

        internal CompositionPropertySet CreatePropertySet() => _compositor.CreatePropertySet();

        // Returns either a CompositionRectangleGeometry or a CompositionRoundedRectangleGeometry.
        internal RectangleOrRoundedRectangleGeometry CreateRectangleGeometry()
        {
            const int c_rectangleGeometryIsUnreliableUntil = 12;

            RectangleOrRoundedRectangleGeometry result;

            if (_targetUapVersion < c_rectangleGeometryIsUnreliableUntil)
            {
                // <= V7 did not reliably draw non-rounded rectangles.
                // <= V11 draws non-rounded rectangles with aliased edges.
                // Work around the problem by using a rounded rectangle with a tiny corner radius.
                var roundedRectangleGeometry = _compositor.CreateRoundedRectangleGeometry();

                // NOTE: magic tiny corner radius number - do not change!
                roundedRectangleGeometry.CornerRadius = new Sn.Vector2(0.000001F);

                result = roundedRectangleGeometry;
            }
            else
            {
                // Later versions do not need the rounded rectangle workaround.
                ConsumeVersionFeature(c_rectangleGeometryIsUnreliableUntil);

                result = _compositor.CreateRectangleGeometry();
            }

            return result;
        }

        internal CompositionRoundedRectangleGeometry CreateRoundedRectangleGeometry() => _compositor.CreateRoundedRectangleGeometry();

        internal CompositionColorBrush CreateColorBrush() => _compositor.CreateColorBrush();

        internal CompositionColorBrush CreateColorBrush(Color color) => _compositor.CreateColorBrush(Color(color));

        internal CompositionColorBrush CreateNonAnimatedColorBrush(Color color)
        {
            if (color.A == 0)
            {
                // Transparent brushes that are never animated are all equivalent.
                color = Animatables.Color.TransparentBlack;
            }

            if (!_nonAnimatedColorBrushes.TryGetValue(color, out var result))
            {
                result = CreateColorBrush(color);
                _nonAnimatedColorBrushes.Add(color, result);
            }

            return result;
        }

        internal CompositionMaskBrush CreateMaskBrush() => _compositor.CreateMaskBrush();

        internal CompositionColorGradientStop CreateColorGradientStop() => _compositor.CreateColorGradientStop();

        internal CompositionColorGradientStop CreateColorGradientStop(float offset, Color color) => _compositor.CreateColorGradientStop(offset, Color(color));

        internal CompositionLinearGradientBrush CreateLinearGradientBrush() => _compositor.CreateLinearGradientBrush();

        internal CompositionRadialGradientBrush CreateRadialGradientBrush()
        {
            ConsumeVersionFeature(8);
            return _compositor.CreateRadialGradientBrush();
        }

        [return: NotNullIfNotNull("easingFunction")]
        internal CompositionEasingFunction? CreateCompositionEasingFunction(Easing? easingFunction)
        {
            if (easingFunction is null)
            {
                return null;
            }

            return easingFunction.Type switch
            {
                Easing.EasingType.Linear => CreateLinearEasingFunction(),
                Easing.EasingType.CubicBezier => CreateCubicBezierEasingFunction((CubicBezierEasing)easingFunction),
                Easing.EasingType.Hold => CreateHoldThenStepEasingFunction(),
                _ => throw new InvalidOperationException(),
            };
        }

        internal LinearEasingFunction CreateLinearEasingFunction() => _linearEasingFunction;

        internal CubicBezierEasingFunction CreateCubicBezierEasingFunction(CubicBezierEasing cubicBezierEasing)
        {
            if (!_cubicBezierEasingFunctions.TryGetValue(cubicBezierEasing, out var result))
            {
                // WinComp does not support control points with components > 1. Clamp the values to 1.
                var controlPoint1 = ClampedVector2(cubicBezierEasing.Beziers[0].ControlPoint1);
                var controlPoint2 = ClampedVector2(cubicBezierEasing.Beziers[0].ControlPoint2);

                result = _compositor.CreateCubicBezierEasingFunction(controlPoint1, controlPoint2);
                _cubicBezierEasingFunctions.Add(cubicBezierEasing, result);
            }

            return result;
        }

        // Returns an easing function that holds its initial value and steps to the final value at the end.
        internal StepEasingFunction CreateHoldThenStepEasingFunction() => _holdStepEasingFunction;

        // Returns an easing function that steps immediately to its final value.
        internal StepEasingFunction CreateStepThenHoldEasingFunction() => _jumpStepEasingFunction;

        internal BooleanKeyFrameAnimation CreateBooleanKeyFrameAnimation() => _compositor.CreateBooleanKeyFrameAnimation();

        internal ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation() => _compositor.CreateScalarKeyFrameAnimation();

        internal ColorKeyFrameAnimation CreateColorKeyFrameAnimation()
        {
            var result = _compositor.CreateColorKeyFrameAnimation();

            // BodyMovin always uses RGB interpolation. Composition defaults to
            // HSL. Override the default to be compatible with BodyMovin.
            result.InterpolationColorSpace = CompositionColorSpace.Rgb;
            return result;
        }

        internal PathKeyFrameAnimation CreatePathKeyFrameAnimation()
        {
            // PathKeyFrameAnimation was added in 6 but was unreliable until 11.
            ConsumeVersionFeature(11);
            return _compositor.CreatePathKeyFrameAnimation();
        }

        internal Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation() => _compositor.CreateVector2KeyFrameAnimation();

        internal Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation() => _compositor.CreateVector3KeyFrameAnimation();

        internal Vector4KeyFrameAnimation CreateVector4KeyFrameAnimation() => _compositor.CreateVector4KeyFrameAnimation();

        internal DropShadow CreateDropShadow() => _compositor.CreateDropShadow();

        internal InsetClip CreateInsetClip() => _compositor.CreateInsetClip();

        internal CompositionGeometricClip CreateGeometricClip() => _compositor.CreateGeometricClip();

        internal CompositionContainerShape CreateContainerShape() => _compositor.CreateContainerShape();

        internal ContainerVisual CreateContainerVisual() => _compositor.CreateContainerVisual();

        internal LayerVisual CreateLayerVisual() => _compositor.CreateLayerVisual();

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

        internal CompositionVisualSurface CreateVisualSurface()
        {
            ConsumeVersionFeature(8);
            return _compositor.CreateVisualSurface();
        }

        internal CompositionSurfaceBrush CreateSurfaceBrush(ICompositionSurface surface) => _compositor.CreateSurfaceBrush(surface);

        // Get cached factory instead of creating from compositor.
        internal CompositionEffectFactory CreateEffectFactory(GraphicsEffectBase effect) => CompositionEffectFactory.GetFactoryCached(effect);

        // Call this when consuming a feature that is only available in UAP versions > 7.
        void ConsumeVersionFeature(uint uapVersion)
        {
            // If this assert fires, it indicates that the caller didn't check that the
            // feature was available before using some Composition feature.
            // Call IsUapApiAvailable to prevent calling here with _targetUapVersion
            // being lower than the uapVersion of the feature being consumed.
            Debug.Assert(
                _targetUapVersion >= uapVersion,
                $"UAP version {uapVersion} features are not available.");

            HighestUapVersionUsed = Math.Max(HighestUapVersionUsed, uapVersion);
        }

        internal AnimationController CreateAnimationControllerList()
        {
            ConsumeVersionFeature(GetUapVersionForApi(nameof(AnimationController)));
            return _compositor.CreateAnimationController();
        }
    }
}