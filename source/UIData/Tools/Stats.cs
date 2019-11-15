// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    /// <summary>
    /// Calculates stats for a WinCompData tree. Used to report the size of data
    /// and the effectiveness of optimization.
    /// </summary>
#if PUBLIC_UIData
    public
#endif
    sealed class Stats
    {
        public Stats(CompositionObject root)
        {
            var objectGraph = Graph.FromCompositionObject(root, includeVertices: false);

            CompositionPathCount = objectGraph.CompositionPathNodes.Count();
            CanvasGeometryCount = objectGraph.CanvasGeometryNodes.Count();

            foreach (var n in objectGraph.CompositionObjectNodes)
            {
                CompositionObjectCount++;
                switch (n.Object.Type)
                {
                    case CompositionObjectType.AnimationController:
                        AnimationControllerCount++;
                        break;
                    case CompositionObjectType.ColorKeyFrameAnimation:
                        ColorKeyFrameAnimationCount++;
                        break;
                    case CompositionObjectType.CompositionColorBrush:
                        ColorBrushCount++;
                        break;
                    case CompositionObjectType.CompositionColorGradientStop:
                        ColorGradientStopCount++;
                        break;
                    case CompositionObjectType.CompositionContainerShape:
                        ContainerShapeCount++;
                        break;
                    case CompositionObjectType.CompositionEffectBrush:
                        EffectBrushCount++;
                        break;
                    case CompositionObjectType.CompositionEllipseGeometry:
                        EllipseGeometryCount++;
                        break;
                    case CompositionObjectType.CompositionGeometricClip:
                        GeometricClipCount++;
                        break;
                    case CompositionObjectType.CompositionLinearGradientBrush:
                        LinearGradientBrushCount++;
                        break;
                    case CompositionObjectType.CompositionPathGeometry:
                        PathGeometryCount++;
                        break;
                    case CompositionObjectType.CompositionPropertySet:
                        {
                            var propertyCount = ((CompositionPropertySet)n.Object).PropertyNames.Count();
                            if (propertyCount > 0)
                            {
                                PropertySetCount++;
                                PropertySetPropertyCount += propertyCount;
                            }
                        }

                        break;
                    case CompositionObjectType.CompositionRadialGradientBrush:
                        RadialGradientBrushCount++;
                        break;
                    case CompositionObjectType.CompositionRectangleGeometry:
                        RectangleGeometryCount++;
                        break;
                    case CompositionObjectType.CompositionRoundedRectangleGeometry:
                        RoundedRectangleGeometryCount++;
                        break;
                    case CompositionObjectType.CompositionSpriteShape:
                        SpriteShapeCount++;
                        break;
                    case CompositionObjectType.CompositionSurfaceBrush:
                        SurfaceBrushCount++;
                        break;
                    case CompositionObjectType.CompositionViewBox:
                        ViewBoxCount++;
                        break;
                    case CompositionObjectType.CompositionVisualSurface:
                        VisualSurfaceCount++;
                        break;
                    case CompositionObjectType.ContainerVisual:
                        ContainerVisualCount++;
                        break;
                    case CompositionObjectType.CubicBezierEasingFunction:
                        CubicBezierEasingFunctionCount++;
                        break;
                    case CompositionObjectType.ExpressionAnimation:
                        ExpressionAnimationCount++;
                        break;
                    case CompositionObjectType.InsetClip:
                        InsetClipCount++;
                        break;
                    case CompositionObjectType.LinearEasingFunction:
                        LinearEasingFunctionCount++;
                        break;
                    case CompositionObjectType.PathKeyFrameAnimation:
                        PathKeyFrameAnimationCount++;
                        break;
                    case CompositionObjectType.ScalarKeyFrameAnimation:
                        ScalarKeyFrameAnimationCount++;
                        break;
                    case CompositionObjectType.ShapeVisual:
                        ShapeVisualCount++;
                        break;
                    case CompositionObjectType.SpriteVisual:
                        SpriteVisualCount++;
                        break;
                    case CompositionObjectType.StepEasingFunction:
                        StepEasingFunctionCount++;
                        break;
                    case CompositionObjectType.Vector2KeyFrameAnimation:
                        Vector2KeyFrameAnimationCount++;
                        break;
                    case CompositionObjectType.Vector3KeyFrameAnimation:
                        Vector3KeyFrameAnimationCount++;
                        break;
                    case CompositionObjectType.Vector4KeyFrameAnimation:
                        Vector4KeyFrameAnimationCount++;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public int CompositionObjectCount { get; }

        public int CompositionPathCount { get; }

        public int CanvasGeometryCount { get; }

        public int AnimationControllerCount { get; }

        public int ColorKeyFrameAnimationCount { get; }

        public int ColorBrushCount { get; }

        public int ColorGradientStopCount { get; }

        public int ContainerShapeCount { get; }

        public int EffectBrushCount { get; }

        public int EllipseGeometryCount { get; }

        public int GeometricClipCount { get; }

        public int LinearGradientBrushCount { get; }

        public int PathGeometryCount { get; }

        public int PropertySetPropertyCount { get; }

        public int PropertySetCount { get; }

        public int RadialGradientBrushCount { get; }

        public int RectangleGeometryCount { get; }

        public int RoundedRectangleGeometryCount { get; }

        public int SpriteShapeCount { get; }

        public int SurfaceBrushCount { get; }

        public int ViewBoxCount { get; }

        public int VisualSurfaceCount { get; }

        public int ContainerVisualCount { get; }

        public int CubicBezierEasingFunctionCount { get; }

        public int ExpressionAnimationCount { get; }

        public int InsetClipCount { get; }

        public int LinearEasingFunctionCount { get; }

        public int PathKeyFrameAnimationCount { get; }

        public int ScalarKeyFrameAnimationCount { get; }

        public int ShapeVisualCount { get; }

        public int SpriteVisualCount { get; }

        public int StepEasingFunctionCount { get; }

        public int Vector2KeyFrameAnimationCount { get; }

        public int Vector3KeyFrameAnimationCount { get; }

        public int Vector4KeyFrameAnimationCount { get; }
    }
}
