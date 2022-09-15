// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;

namespace CommunityToolkit.WinUI.Lottie.UIData.Tools
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
        private int CountNumberOfPathCommands(CanvasGeometry geometry)
        {
            if (geometry is CanvasGeometry.Path path)
            {
                return path.Commands.Length;
            }

            if (geometry is CanvasGeometry.Combination combination)
            {
                return CountNumberOfPathCommands(combination.A) + CountNumberOfPathCommands(combination.B);
            }

            if (geometry is CanvasGeometry.TransformedGeometry transformed)
            {
                return CountNumberOfPathCommands(transformed.SourceGeometry);
            }

            if (geometry is CanvasGeometry.Group group)
            {
                int res = 0;
                foreach (var g in group.Geometries)
                {
                    res += CountNumberOfPathCommands(g);
                }

                return res;
            }

            return 0;
        }

        private int CountNumberOfGeometries(CanvasGeometry geometry)
        {
            if (geometry is CanvasGeometry.Combination combination)
            {
                return CountNumberOfGeometries(combination.A) + CountNumberOfGeometries(combination.B);
            }

            if (geometry is CanvasGeometry.TransformedGeometry transformed)
            {
                return CountNumberOfGeometries(transformed.SourceGeometry);
            }

            if (geometry is CanvasGeometry.Group group)
            {
                int res = 0;
                foreach (var g in group.Geometries)
                {
                    res += CountNumberOfGeometries(g);
                }

                return res;
            }

            return 1;
        }

        public Stats(CompositionObject? root)
        {
            if (root is null)
            {
                return;
            }

            var objectGraph = Graph.FromCompositionObject(root, includeVertices: false);

            CompositionPathCount = objectGraph.CompositionPathNodes.Count();
            CanvasGeometryCount = objectGraph.CanvasGeometryNodes.Count();

            var v = new HashSet<CompositionObject>();

            foreach (var (_, obj) in objectGraph.CompositionObjectNodes)
            {
                AnimatorCount += obj.Animators.Count;

                var expressionAnimatorCount = obj.Animators.Where(a => a.Animation.Type == CompositionObjectType.ExpressionAnimation).Count();

                ExpressionAnimatorCount += expressionAnimatorCount;
                KeyframeAnimatorCount += obj.Animators.Count - expressionAnimatorCount;

                foreach (var animator in obj.Animators)
                {
                    // AnimatorCountByPropertyName[animator.AnimatedProperty] = AnimatorCountByPropertyName.GetValueOrDefault(animator.AnimatedProperty) + 1;
                    if (animator.Animation is KeyFrameAnimation_ animation)
                    {
                        KeyframeCount += animation.KeyFrameCount;
                    }
                }

                if (obj is CompositionSpriteShape spriteShape)
                {
                    if (spriteShape.Geometry is CompositionPathGeometry geometry)
                    {
                        if (geometry.Path?.Source is CanvasGeometry canvasGeometry)
                        {
                            PathCommandsCount += CountNumberOfPathCommands(canvasGeometry);
                            GeometriesCount += CountNumberOfGeometries(canvasGeometry);
                        }
                    }
                    else
                    {
                        GeometriesCount += 1;
                    }
                }

                if (obj is PathKeyFrameAnimation pathAnimation)
                {
                    foreach (var keyframe in pathAnimation.KeyFrames)
                    {
                        if (keyframe is PathKeyFrameAnimation.ValueKeyFrame valueKeyframe)
                        {
                            if (valueKeyframe.Value.Source is CanvasGeometry geometry)
                            {
                                PathCommandsCount += CountNumberOfPathCommands(geometry);
                                GeometriesCount += CountNumberOfGeometries(geometry);
                            }
                        }
                    }
                }

                // AnimatorCountByPropertyName[obj.Type.ToString()] = AnimatorCountByPropertyName.GetValueOrDefault(obj.Type.ToString()) + 1;
                CompositionObjectCount++;
                switch (obj.Type)
                {
                    case CompositionObjectType.AnimationController:
                        AnimationControllerCount++;
                        break;
                    case CompositionObjectType.BooleanKeyFrameAnimation:
                        BooleanKeyFrameAnimationCount++;
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
                    case CompositionObjectType.CompositionMaskBrush:
                        MaskBrushCount++;
                        break;
                    case CompositionObjectType.CompositionPathGeometry:
                        PathGeometryCount++;
                        break;
                    case CompositionObjectType.CompositionPropertySet:
                        {
                            var propertyCount = ((CompositionPropertySet)obj).Names.Count();
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
                    case CompositionObjectType.DropShadow:
                        DropShadowCount++;
                        break;
                    case CompositionObjectType.ExpressionAnimation:
                        ExpressionAnimationCount++;
                        break;
                    case CompositionObjectType.InsetClip:
                        InsetClipCount++;
                        break;
                    case CompositionObjectType.LayerVisual:
                        LayerVisualCount++;
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
                    case CompositionObjectType.CompositionEffectFactory:
                        EffectFactoryCount++;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public float CalculateApproximateAnimationsComplexity()
        {
            var complexity = 0.0f;

            var otherKeyframeAnimators = KeyframeAnimatorCount;

            complexity += ExpressionAnimatorCount * 15;

            otherKeyframeAnimators -= AnimatorCountByPropertyName.GetValueOrDefault("IsVisible");
            complexity += AnimatorCountByPropertyName.GetValueOrDefault("IsVisible") * 5;

            otherKeyframeAnimators -= AnimatorCountByPropertyName.GetValueOrDefault("Opacity");
            complexity += AnimatorCountByPropertyName.GetValueOrDefault("Opacity") * 30;

            otherKeyframeAnimators -= AnimatorCountByPropertyName.GetValueOrDefault("Color");
            complexity += AnimatorCountByPropertyName.GetValueOrDefault("Color") * 30;

            complexity += otherKeyframeAnimators * 10;

            complexity += KeyframeCount * 5;

            return complexity / 1000.0f;
        }

        public float CalculateApproximateGeometryComplexity()
        {
            var complexity = 0.0f;

            complexity += PathCommandsCount * 4;
            complexity += GeometriesCount * 20;

            return complexity / 1000.0f;
        }

        public float CalculateApproximateEffectsComplexity()
        {
            var complexity = 0.0f;

            complexity += EffectBrushCount * 500;
            complexity += EffectFactoryCount * 1000;

            return complexity / 1000.0f;
        }

        public float CalculateApproximateTreeComplexity()
        {
            var complexity = 0.0f;

            complexity += ContainerVisualCount * 5;
            complexity += LayerVisualCount * 5;
            complexity += ShapeVisualCount * 5;
            complexity += SpriteVisualCount * 20;
            complexity += ContainerShapeCount * 5;
            complexity += CompositionObjectCount;

            return complexity / 1000.0f;
        }

        public float CalculateApproximateComplexity()
        {
            var complexity = 0.0f;

            complexity += CalculateApproximateAnimationsComplexity();
            complexity += CalculateApproximateGeometryComplexity();
            complexity += CalculateApproximateEffectsComplexity();
            complexity += CalculateApproximateTreeComplexity();

            return complexity;
        }

        public int AnimationControllerCount { get; }

        public int AnimatorCount { get; }

        public int ExpressionAnimatorCount { get; }

        public int KeyframeAnimatorCount { get; }

        public int KeyframeCount { get; }

        public int PathCommandsCount { get; }

        public int GeometriesCount { get; }

        public Dictionary<string, int> AnimatorCountByPropertyName { get; } = new Dictionary<string, int>();

        public int BooleanKeyframeAnimatorCount { get; }

        public int BooleanKeyFrameAnimationCount { get; }

        public int CanvasGeometryCount { get; }

        public int ColorKeyFrameAnimationCount { get; }

        public int ColorBrushCount { get; }

        public int ColorGradientStopCount { get; }

        public int CompositionObjectCount { get; }

        public int CompositionPathCount { get; }

        public int ContainerShapeCount { get; }

        public int DropShadowCount { get; }

        public int EffectBrushCount { get; }

        public int EffectFactoryCount { get; }

        public int EllipseGeometryCount { get; }

        public int GeometricClipCount { get; }

        public int LinearGradientBrushCount { get; }

        public int MaskBrushCount { get; }

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

        public int LayerVisualCount { get; }

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
