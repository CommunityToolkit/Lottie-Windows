// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class Compositor
    {
        public CompositionGeometricClip CreateCompositionGeometricClip() => new CompositionGeometricClip();

        public ContainerVisual CreateContainerVisual() => new ContainerVisual();

        public ShapeVisual CreateShapeVisual() => new ShapeVisual();

        public CompositionViewBox CreateViewBox() => new CompositionViewBox();

        public ExpressionAnimation CreateExpressionAnimation(WinCompData.Expressions.Expression expression) => new ExpressionAnimation(expression);

        public InsetClip CreateInsetClip() => new InsetClip();

        public CompositionSpriteShape CreateSpriteShape() => new CompositionSpriteShape();

        public CompositionContainerShape CreateContainerShape() => new CompositionContainerShape();

        public CompositionColorBrush CreateColorBrush(Wui.Color color) => new CompositionColorBrush(color);

        public CompositionPathGeometry CreatePathGeometry() => new CompositionPathGeometry();

        public CompositionPathGeometry CreatePathGeometry(CompositionPath path) => new CompositionPathGeometry(path);

        public CompositionPropertySet CreatePropertySet() => new CompositionPropertySet(null);

        public CompositionEllipseGeometry CreateEllipseGeometry() => new CompositionEllipseGeometry();

        public CubicBezierEasingFunction CreateCubicBezierEasingFunction(Vector2 controlPoint1, Vector2 controlPoint2) => new CubicBezierEasingFunction(controlPoint1, controlPoint2);

        public StepEasingFunction CreateStepEasingFunction(int steps) => new StepEasingFunction(steps);

        public StepEasingFunction CreateStepEasingFunction() => new StepEasingFunction(1);

        public ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation() => new ScalarKeyFrameAnimation();

        public ColorKeyFrameAnimation CreateColorKeyFrameAnimation() => new ColorKeyFrameAnimation();

        public PathKeyFrameAnimation CreatePathKeyFrameAnimation() => new PathKeyFrameAnimation();

        public Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation() => new Vector2KeyFrameAnimation();

        public Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation() => new Vector3KeyFrameAnimation();

        public LinearEasingFunction CreateLinearEasingFunction() => new LinearEasingFunction();

        public CompositionRectangleGeometry CreateRectangleGeometry() => new CompositionRectangleGeometry();

        public CompositionRoundedRectangleGeometry CreateRoundedRectangleGeometry() => new CompositionRoundedRectangleGeometry();

        public SpriteVisual CreateSpriteVisual() => new SpriteVisual();

        public CompositionMaskBrush CreateMaskBrush() => new CompositionMaskBrush();

        public CompositionVisualSurface CreateVisualSurface() => new CompositionVisualSurface();

        public CompositionSurfaceBrush CreateSurfaceBrush(ICompositionSurface surface) => new CompositionSurfaceBrush(surface);

        public CompositionEffectBrush CreateEffectBrush(Mgce.CompositeEffect effect) => new CompositionEffectBrush(effect);
    }
}
