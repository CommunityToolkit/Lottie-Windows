// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class Compositor
    {
        public BooleanKeyFrameAnimation CreateBooleanKeyFrameAnimation() => new BooleanKeyFrameAnimation();

        public CompositionColorBrush CreateColorBrush() => new CompositionColorBrush();

        public CompositionColorBrush CreateColorBrush(Wui.Color color) => new CompositionColorBrush(color);

        public CompositionColorGradientStop CreateColorGradientStop() => new CompositionColorGradientStop();

        public CompositionColorGradientStop CreateColorGradientStop(float offset, Wui.Color color) => new CompositionColorGradientStop(offset, color);

        public ColorKeyFrameAnimation CreateColorKeyFrameAnimation() => new ColorKeyFrameAnimation();

        public CompositionContainerShape CreateContainerShape() => new CompositionContainerShape();

        public ContainerVisual CreateContainerVisual() => new ContainerVisual();

        public CubicBezierEasingFunction CreateCubicBezierEasingFunction(System.Numerics.Vector2 controlPoint1, System.Numerics.Vector2 controlPoint2) => new CubicBezierEasingFunction(controlPoint1, controlPoint2);

        public CompositionEffectFactory CreateEffectFactory(GraphicsEffectBase graphicsEffect) => new CompositionEffectFactory(graphicsEffect);

        public CompositionEllipseGeometry CreateEllipseGeometry() => new CompositionEllipseGeometry();

        public ExpressionAnimation CreateExpressionAnimation(Expression expression) => new ExpressionAnimation(expression);

        public CompositionGeometricClip CreateGeometricClip() => new CompositionGeometricClip();

        public InsetClip CreateInsetClip() => new InsetClip();

        public LinearEasingFunction CreateLinearEasingFunction() => new LinearEasingFunction();

        public CompositionLinearGradientBrush CreateLinearGradientBrush() => new CompositionLinearGradientBrush();

        public CompositionPathGeometry CreatePathGeometry() => new CompositionPathGeometry();

        public CompositionPathGeometry CreatePathGeometry(CompositionPath? path) => new CompositionPathGeometry(path);

        public PathKeyFrameAnimation CreatePathKeyFrameAnimation() => new PathKeyFrameAnimation();

        public CompositionPropertySet CreatePropertySet() => new CompositionPropertySet(null);

        public CompositionRadialGradientBrush CreateRadialGradientBrush() => new CompositionRadialGradientBrush();

        public CompositionRectangleGeometry CreateRectangleGeometry() => new CompositionRectangleGeometry();

        public CompositionRoundedRectangleGeometry CreateRoundedRectangleGeometry() => new CompositionRoundedRectangleGeometry();

        public ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation() => new ScalarKeyFrameAnimation();

        public ShapeVisual CreateShapeVisual() => new ShapeVisual();

        public CompositionSpriteShape CreateSpriteShape() => new CompositionSpriteShape();

        public SpriteVisual CreateSpriteVisual() => new SpriteVisual();

        public StepEasingFunction CreateStepEasingFunction() => new StepEasingFunction(1);

        public StepEasingFunction CreateStepEasingFunction(int stepCount) => new StepEasingFunction(stepCount);

        public CompositionSurfaceBrush CreateSurfaceBrush(ICompositionSurface surface) => new CompositionSurfaceBrush(surface);

        public Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation() => new Vector2KeyFrameAnimation();

        public Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation() => new Vector3KeyFrameAnimation();

        public Vector4KeyFrameAnimation CreateVector4KeyFrameAnimation() => new Vector4KeyFrameAnimation();

        public CompositionViewBox CreateViewBox() => new CompositionViewBox();

        public CompositionVisualSurface CreateVisualSurface() => new CompositionVisualSurface();
    }
}
