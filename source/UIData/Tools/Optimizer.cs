// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;
using CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;
using Expr = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using Wg = CommunityToolkit.WinUI.Lottie.WinCompData.Wg;
using Wui = CommunityToolkit.WinUI.Lottie.WinCompData.Wui;

namespace CommunityToolkit.WinUI.Lottie.UIData.Tools
{
    /// <summary>
    /// Transforms a WinCompData tree to an equivalent tree, optimizing the tree
    /// where possible.
    /// </summary>
#if PUBLIC_UIData
    public
#endif
    sealed class Optimizer
    {
        readonly Compositor _c = new Compositor();
        readonly ObjectGraph<ObjectData> _graph;

        public static Visual Optimize(Visual root, bool ignoreCommentProperties)
        {
            var result = CanonicalizeGraph(root, ignoreCommentProperties);

            AssertGraphsAreDisjoint(result, root);

            // Un-set properties that are set to their default values, and convert
            // non-default scalar properties to Matrix properties where possible.
            result = PropertyValueOptimizer.OptimizePropertyValues(result);

            // Try to optimize away redundant parts of the graph.
            result = GraphCompactor.Compact(result);

            // Re-run the property value optimizer because the compactor may have set new properties.
            result = PropertyValueOptimizer.OptimizePropertyValues(result);

            // The graph compactor may create new CompositionObjects (e.g. BooleanKeyFrameAnimations).
            // Re-run the canonicalizer so that the new objects are canonicalized.
            result = CanonicalizeGraph(result, ignoreCommentProperties);

            return result;
        }

        static Visual CanonicalizeGraph(Visual root, bool ignoreCommentProperties)
        {
            // Build the object graph.
            var graph = ObjectGraph<ObjectData>.FromCompositionObject(root, includeVertices: true);

            // Find the canonical objects in the graph.
            Canonicalizer.Canonicalize(graph, ignoreCommentProperties: ignoreCommentProperties);

            // Create a copy of the WinCompData objects from the canonical objects.
            // The copy is needed so that we can modify the tree without affecting the graph that
            // was given to us.
            return (Visual)new Optimizer(graph).GetCompositionObject(root);
        }

        // Asserts that the 2 graphs are disjoint. If this assert ever fires then
        // the graph copier is broken - it is reusing an object rather than making
        // a copy.
        [Conditional("DEBUG")]
        static void AssertGraphsAreDisjoint(CompositionObject root1, CompositionObject root2)
        {
            var graph1 = Graph.FromCompositionObject(root1, includeVertices: false);
            var graph2 = Graph.FromCompositionObject(root2, includeVertices: false);

            var graph1Objects = new HashSet<object>(graph1.Nodes.Select(n => n.Object));
            foreach (var obj in graph2.Nodes.Select(n => n.Object))
            {
                Debug.Assert(!graph1Objects.Contains(obj), "Graphs are not disjoint");
            }
        }

        sealed class ObjectData : CanonicalizedNode<ObjectData>
        {
            // The copied object.
            internal object? Copied { get; set; }
        }

        Optimizer(ObjectGraph<ObjectData> graph)
        {
            _graph = graph;
        }

        ObjectData NodeFor(CanvasGeometry obj)
        {
            return _graph[obj].Canonical;
        }

        ObjectData NodeFor(CompositionObject obj)
        {
            return _graph[obj].Canonical;
        }

        ObjectData NodeFor(CompositionPath obj)
        {
            return _graph[obj].Canonical;
        }

        ObjectData NodeFor(LoadedImageSurface obj)
        {
            return _graph[obj].Canonical;
        }

        bool GetExisting<T>(T key, [MaybeNullWhen(false)] out T result)
            where T : CompositionObject
        {
            var temp = NodeFor(key).Copied;
            if (temp is null)
            {
                result = null;
                return false;
            }
            else
            {
                result = (T)temp!;
                return true;
            }
        }

        bool GetExistingCanvasGeometry(CanvasGeometry key, [MaybeNullWhen(false)] out CanvasGeometry result)
        {
            result = (CanvasGeometry?)NodeFor(key).Copied;
            return result is not null;
        }

        bool GetExisting(CompositionPath key, [MaybeNullWhen(false)] out CompositionPath result)
        {
            result = (CompositionPath?)NodeFor(key).Copied;
            return result is not null;
        }

        bool GetExisting(LoadedImageSurface key, [MaybeNullWhen(false)] out LoadedImageSurface result)
        {
            result = (LoadedImageSurface?)NodeFor(key).Copied;
            return result is not null;
        }

        T CacheAndInitializeCompositionObject<T>(T key, T obj)
            where T : CompositionObject
        {
            Cache(key, obj);
            InitializeCompositionObject(key, obj);
            return obj;
        }

        T CacheAndInitializeShape<T>(T source, T target)
            where T : CompositionShape
        {
            CacheAndInitializeCompositionObject(source, target);
            target.CenterPoint = source.CenterPoint;
            target.Offset = source.Offset;
            target.RotationAngleInDegrees = source.RotationAngleInDegrees;
            target.Scale = source.Scale;
            target.TransformMatrix = source.TransformMatrix;

            return target;
        }

        T CacheAndInitializeGradientBrush<T>(CompositionGradientBrush source, T target)
            where T : CompositionGradientBrush
        {
            CacheAndInitializeCompositionObject(source, target);

            target.AnchorPoint = source.AnchorPoint;

            target.CenterPoint = source.CenterPoint;

            var stops = target.ColorStops;
            foreach (var stop in source.ColorStops)
            {
                stops.Add(GetCompositionColorGradientStop(stop));
            }

            target.ExtendMode = source.ExtendMode;
            target.InterpolationSpace = source.InterpolationSpace;

            target.MappingMode = source.MappingMode;
            target.Offset = source.Offset;
            target.RotationAngleInDegrees = source.RotationAngleInDegrees;
            target.Scale = source.Scale;
            target.TransformMatrix = source.TransformMatrix;
            return target;
        }

        T CacheAndInitializeVisual<T>(Visual source, T target)
            where T : Visual
        {
            CacheAndInitializeCompositionObject(source, target);

            if (source.Clip is not null)
            {
                target.Clip = GetCompositionClip(source.Clip);
            }

            target.BorderMode = source.BorderMode;
            target.CenterPoint = source.CenterPoint;
            target.IsVisible = source.IsVisible;
            target.Offset = source.Offset;
            target.Opacity = source.Opacity;
            target.RotationAngleInDegrees = source.RotationAngleInDegrees;
            target.RotationAxis = source.RotationAxis;
            target.Scale = source.Scale;
            target.Size = source.Size;
            target.TransformMatrix = source.TransformMatrix;

            return target;
        }

        T CacheAndInitializeAnimation<T>(T source, T target)
            where T : CompositionAnimation
        {
            CacheAndInitializeCompositionObject(source, target);
            foreach (var parameter in source.ReferenceParameters)
            {
                var referenceObject = GetCompositionObject(parameter.Value);
                target.SetReferenceParameter(parameter.Key, referenceObject);
            }

            if (!string.IsNullOrWhiteSpace(source.Target))
            {
                target.Target = source.Target;
            }

            return target;
        }

        T CacheAndInitializeKeyFrameAnimation<T>(KeyFrameAnimation_ source, T target)
            where T : KeyFrameAnimation_
        {
            CacheAndInitializeAnimation(source, target);
            target.Duration = source.Duration;
            return target;
        }

        T CacheAndInitializeCompositionGeometry<T>(CompositionGeometry source, T target)
            where T : CompositionGeometry
        {
            CacheAndInitializeCompositionObject(source, target);
            if (source.TrimStart != 0)
            {
                target.TrimStart = source.TrimStart;
            }

            if (source.TrimEnd != 1)
            {
                target.TrimEnd = source.TrimEnd;
            }

            if (source.TrimOffset != 0)
            {
                target.TrimOffset = source.TrimOffset;
            }

            return target;
        }

        CanvasGeometry CacheCanvasGeometry(CanvasGeometry key, CanvasGeometry obj)
        {
            var node = NodeFor(key);
            Debug.Assert(node.Copied is null, "Precondition");
            Debug.Assert(!ReferenceEquals(key, obj), "Precondition");
            node.Copied = obj;
            return obj;
        }

        T Cache<T>(T key, T obj)
            where T : CompositionObject
        {
            var node = NodeFor(key);
            Debug.Assert(node.Copied is null, "Precondition");
            Debug.Assert(!ReferenceEquals(key, obj), "Precondition");
            node.Copied = obj;
            return obj;
        }

        CompositionPath Cache(CompositionPath key, CompositionPath obj)
        {
            var node = NodeFor(key);
            Debug.Assert(node.Copied is null, "Precondition");
            Debug.Assert(!ReferenceEquals(key, obj), "Precondition");
            node.Copied = obj;
            return obj;
        }

        LoadedImageSurface Cache(LoadedImageSurface key, LoadedImageSurface obj)
        {
            var node = NodeFor(key);
            Debug.Assert(node.Copied is null, "Precondition");
            Debug.Assert(!ReferenceEquals(key, obj), "Precondition");
            node.Copied = obj;
            return obj;
        }

        DropShadow GetDropShadow(DropShadow obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateDropShadow());

            result.BlurRadius = obj.BlurRadius;
            result.Color = obj.Color;
            result.Mask = obj.Mask;
            result.Offset = obj.Offset;
            result.Opacity = obj.Opacity;
            result.SourcePolicy = obj.SourcePolicy;

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        LayerVisual GetLayerVisual(LayerVisual obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeVisual(obj, _c.CreateLayerVisual());

            if (obj.Shadow is not null)
            {
                result.Shadow = GetCompositionShadow(obj.Shadow);
            }

            InitializeContainerVisual(obj, result);
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        ShapeVisual GetShapeVisual(ShapeVisual obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeVisual(obj, _c.CreateShapeVisual());

            if (obj.ViewBox is not null)
            {
                result.ViewBox = GetCompositionViewBox(obj.ViewBox);
            }

            var shapesCollection = result.Shapes;
            foreach (var child in obj.Shapes)
            {
                shapesCollection.Add(GetCompositionShape(child));
            }

            InitializeContainerVisual(obj, result);
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        SpriteVisual GetSpriteVisual(SpriteVisual obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeVisual(obj, _c.CreateSpriteVisual());

            if (obj.Brush is not null)
            {
                result.Brush = GetCompositionBrush(obj.Brush);
            }

            InitializeContainerVisual(obj, result);
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        ContainerVisual GetContainerVisual(ContainerVisual obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeVisual(obj, _c.CreateContainerVisual());
            InitializeContainerVisual(obj, result);
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        void InitializeContainerVisual<T>(T source, T target)
            where T : ContainerVisual
        {
            var children = target.Children;
            foreach (var child in source.Children)
            {
                children.Add(GetVisual(child));
            }
        }

        void InitializeIDescribable<T>(T source, T target)
            where T : IDescribable
        {
            target.LongDescription = source.LongDescription;
            target.ShortDescription = source.ShortDescription;
            target.Name = source.Name;
        }

        void InitializeCompositionObject<T>(T source, T target)
            where T : CompositionObject
        {
            // Get the CompositionPropertySet on this object. This has the side-effect of initializing
            // it and starting any animations.
            // Prevent infinite recursion - the Properties on a CompositionPropertySet is itself.
            if (source.Type != CompositionObjectType.CompositionPropertySet)
            {
                GetCompositionPropertySet(source.Properties);
            }

            target.Comment = source.Comment;
            InitializeIDescribable(source, target);
        }

        void StartAnimationsAndFreeze(CompositionObject source, CompositionObject target)
        {
            foreach (var animator in source.Animators)
            {
                var animation = GetCompositionAnimation(animator.Animation);
                var controller = animator.Controller;

                // Freeze the animation to indicate that it will not be mutated further. This
                // will ensure that it does not need to be copied when target.StartAnimation is called.
                animation.Freeze();
                if (controller is null || !controller.IsCustom)
                {
                    target.StartAnimation(animator.AnimatedProperty, animation);
                }
                else
                {
                   target.StartAnimation(animator.AnimatedProperty, animation, GetAnimationController(controller));
                }

                if (controller is not null && !controller.IsCustom)
                {
                    var animationController = GetAnimationController(controller);
                    if (controller.IsPaused)
                    {
                        animationController.Pause();
                    }
                }
            }
        }

        AnimationController GetAnimationController(AnimationController obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            if (obj.IsCustom)
            {
                result = CacheAndInitializeCompositionObject(obj, _c.CreateAnimationController());

                if (obj.IsPaused)
                {
                    result.Pause();
                }
            }
            else
            {
                var targetObject = GetCompositionObject(obj.TargetObject!);

                result = CacheAndInitializeCompositionObject(obj, targetObject.TryGetAnimationController(obj.TargetProperty!)!);
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        /// <summary>
        /// Returns a copy of the graph of composition objects starting at the given object.
        /// </summary>
        CompositionObject GetCompositionObject(CompositionObject obj) =>
            obj.Type switch
            {
                CompositionObjectType.AnimationController => GetAnimationController((AnimationController)obj),
                CompositionObjectType.BooleanKeyFrameAnimation => GetBooleanKeyFrameAnimation((BooleanKeyFrameAnimation)obj),
                CompositionObjectType.ColorKeyFrameAnimation => GetColorKeyFrameAnimation((ColorKeyFrameAnimation)obj),
                CompositionObjectType.CompositionColorBrush => GetCompositionColorBrush((CompositionColorBrush)obj),
                CompositionObjectType.CompositionColorGradientStop => GetCompositionColorGradientStop((CompositionColorGradientStop)obj),
                CompositionObjectType.CompositionContainerShape => GetCompositionContainerShape((CompositionContainerShape)obj),
                CompositionObjectType.CompositionEffectBrush => GetCompositionEffectBrush((CompositionEffectBrush)obj),
                CompositionObjectType.CompositionEllipseGeometry => GetCompositionEllipseGeometry((CompositionEllipseGeometry)obj),
                CompositionObjectType.CompositionGeometricClip => GetCompositionGeometricClip((CompositionGeometricClip)obj),
                CompositionObjectType.CompositionLinearGradientBrush => GetCompositionLinearGradientBrush((CompositionLinearGradientBrush)obj),
                CompositionObjectType.CompositionMaskBrush => GetCompositionMaskBrush((CompositionMaskBrush)obj),
                CompositionObjectType.CompositionPathGeometry => GetCompositionPathGeometry((CompositionPathGeometry)obj),
                CompositionObjectType.CompositionPropertySet => GetCompositionPropertySet((CompositionPropertySet)obj),
                CompositionObjectType.CompositionRadialGradientBrush => GetCompositionRadialGradientBrush((CompositionRadialGradientBrush)obj),
                CompositionObjectType.CompositionRectangleGeometry => GetCompositionRectangleGeometry((CompositionRectangleGeometry)obj),
                CompositionObjectType.CompositionRoundedRectangleGeometry => GetCompositionRoundedRectangleGeometry((CompositionRoundedRectangleGeometry)obj),
                CompositionObjectType.CompositionSpriteShape => GetCompositionSpriteShape((CompositionSpriteShape)obj),
                CompositionObjectType.CompositionSurfaceBrush => GetCompositionSurfaceBrush((CompositionSurfaceBrush)obj),
                CompositionObjectType.CompositionViewBox => GetCompositionViewBox((CompositionViewBox)obj),
                CompositionObjectType.CompositionVisualSurface => GetCompositionVisualSurface((CompositionVisualSurface)obj),
                CompositionObjectType.ContainerVisual => GetContainerVisual((ContainerVisual)obj),
                CompositionObjectType.CubicBezierEasingFunction => GetCubicBezierEasingFunction((CubicBezierEasingFunction)obj),
                CompositionObjectType.DropShadow => GetDropShadow((DropShadow)obj),
                CompositionObjectType.ExpressionAnimation => GetExpressionAnimation((ExpressionAnimation)obj),
                CompositionObjectType.InsetClip => GetInsetClip((InsetClip)obj),
                CompositionObjectType.LayerVisual => GetLayerVisual((LayerVisual)obj),
                CompositionObjectType.LinearEasingFunction => GetLinearEasingFunction((LinearEasingFunction)obj),
                CompositionObjectType.PathKeyFrameAnimation => GetPathKeyFrameAnimation((PathKeyFrameAnimation)obj),
                CompositionObjectType.ScalarKeyFrameAnimation => GetScalarKeyFrameAnimation((ScalarKeyFrameAnimation)obj),
                CompositionObjectType.ShapeVisual => GetShapeVisual((ShapeVisual)obj),
                CompositionObjectType.SpriteVisual => GetSpriteVisual((SpriteVisual)obj),
                CompositionObjectType.StepEasingFunction => GetStepEasingFunction((StepEasingFunction)obj),
                CompositionObjectType.Vector2KeyFrameAnimation => GetVector2KeyFrameAnimation((Vector2KeyFrameAnimation)obj),
                CompositionObjectType.Vector3KeyFrameAnimation => GetVector3KeyFrameAnimation((Vector3KeyFrameAnimation)obj),
                CompositionObjectType.Vector4KeyFrameAnimation => GetVector4KeyFrameAnimation((Vector4KeyFrameAnimation)obj),
                CompositionObjectType.CompositionEffectFactory => GetCompositionEffectFactory((CompositionEffectFactory)obj),
                _ => throw new InvalidOperationException(),
            };

        CompositionPropertySet GetCompositionPropertySet(CompositionPropertySet obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            // CompositionPropertySets are usually created implicitly by CompositionObjects that own them.
            // If the CompositionPropertySet is not owned, then create it now.
            if (obj.Owner is null)
            {
                result = _c.CreatePropertySet();
            }
            else
            {
                result = GetCompositionObject(obj.Owner).Properties;
            }

            result = CacheAndInitializeCompositionObject(obj, result);

            foreach (var (name, type) in obj.Names)
            {
                switch (type)
                {
                    case WinCompData.MetaData.PropertySetValueType.Color:
                        {
                            obj.TryGetColor(name, out var value);
                            result.InsertColor(name, value!.Value);
                            break;
                        }

                    case WinCompData.MetaData.PropertySetValueType.Scalar:
                        {
                            obj.TryGetScalar(name, out var value);
                            result.InsertScalar(name, value!.Value);
                            break;
                        }

                    case WinCompData.MetaData.PropertySetValueType.Vector2:
                        {
                            obj.TryGetVector2(name, out var value);
                            result.InsertVector2(name, value!.Value);
                            break;
                        }

                    case WinCompData.MetaData.PropertySetValueType.Vector3:
                        {
                            obj.TryGetVector3(name, out var value);
                            result.InsertVector3(name, value!.Value);
                            break;
                        }

                    case WinCompData.MetaData.PropertySetValueType.Vector4:
                        {
                            obj.TryGetVector4(name, out var value);
                            result.InsertVector4(name, value!.Value);
                            break;
                        }

                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionShadow GetCompositionShadow(CompositionShadow obj) =>
            obj.Type switch
            {
                CompositionObjectType.DropShadow => GetDropShadow((DropShadow)obj),
                _ => throw new InvalidOperationException(),
            };

        Visual GetVisual(Visual obj) =>
            obj.Type switch
            {
                CompositionObjectType.ContainerVisual => GetContainerVisual((ContainerVisual)obj),
                CompositionObjectType.LayerVisual => GetLayerVisual((LayerVisual)obj),
                CompositionObjectType.ShapeVisual => GetShapeVisual((ShapeVisual)obj),
                CompositionObjectType.SpriteVisual => GetSpriteVisual((SpriteVisual)obj),
                _ => throw new InvalidOperationException(),
            };

        CompositionAnimation GetCompositionAnimation(CompositionAnimation obj) =>
            obj.Type switch
            {
                CompositionObjectType.ExpressionAnimation => GetExpressionAnimation((ExpressionAnimation)obj),
                CompositionObjectType.BooleanKeyFrameAnimation => GetBooleanKeyFrameAnimation((BooleanKeyFrameAnimation)obj),
                CompositionObjectType.ColorKeyFrameAnimation => GetColorKeyFrameAnimation((ColorKeyFrameAnimation)obj),
                CompositionObjectType.PathKeyFrameAnimation => GetPathKeyFrameAnimation((PathKeyFrameAnimation)obj),
                CompositionObjectType.ScalarKeyFrameAnimation => GetScalarKeyFrameAnimation((ScalarKeyFrameAnimation)obj),
                CompositionObjectType.Vector2KeyFrameAnimation => GetVector2KeyFrameAnimation((Vector2KeyFrameAnimation)obj),
                CompositionObjectType.Vector3KeyFrameAnimation => GetVector3KeyFrameAnimation((Vector3KeyFrameAnimation)obj),
                CompositionObjectType.Vector4KeyFrameAnimation => GetVector4KeyFrameAnimation((Vector4KeyFrameAnimation)obj),
                _ => throw new InvalidOperationException(),
            };

        ExpressionAnimation GetExpressionAnimation(ExpressionAnimation obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeAnimation(obj, _c.CreateExpressionAnimation(obj.Expression));
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        BooleanKeyFrameAnimation GetBooleanKeyFrameAnimation(BooleanKeyFrameAnimation obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateBooleanKeyFrameAnimation());

            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case KeyFrameType.Expression:
                        var expressionKeyFrame = (KeyFrameAnimation<bool, Expr.Boolean>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression, GetCompositionEasingFunction(kf.Easing));
                        break;
                    case KeyFrameType.Value:
                        var valueKeyFrame = (KeyFrameAnimation<bool, Expr.Boolean>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        ColorKeyFrameAnimation GetColorKeyFrameAnimation(ColorKeyFrameAnimation obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateColorKeyFrameAnimation());
            result.InterpolationColorSpace = obj.InterpolationColorSpace;

            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case KeyFrameType.Expression:
                        var expressionKeyFrame = (KeyFrameAnimation<Wui.Color, Expr.Color>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression, GetCompositionEasingFunction(kf.Easing));
                        break;
                    case KeyFrameType.Value:
                        var valueKeyFrame = (KeyFrameAnimation<Wui.Color, Expr.Color>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value, GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        ScalarKeyFrameAnimation GetScalarKeyFrameAnimation(ScalarKeyFrameAnimation obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateScalarKeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case KeyFrameType.Expression:
                        var expressionKeyFrame = (KeyFrameAnimation<float, Expr.Scalar>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression, GetCompositionEasingFunction(kf.Easing));
                        break;
                    case KeyFrameType.Value:
                        var valueKeyFrame = (KeyFrameAnimation<float, Expr.Scalar>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value, GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        Vector2KeyFrameAnimation GetVector2KeyFrameAnimation(Vector2KeyFrameAnimation obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateVector2KeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case KeyFrameType.Expression:
                        var expressionKeyFrame = (KeyFrameAnimation<Vector2, Expr.Vector2>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression, GetCompositionEasingFunction(kf.Easing));
                        break;
                    case KeyFrameType.Value:
                        var valueKeyFrame = (KeyFrameAnimation<Vector2, Expr.Vector2>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value, GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        Vector3KeyFrameAnimation GetVector3KeyFrameAnimation(Vector3KeyFrameAnimation obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateVector3KeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case KeyFrameType.Expression:
                        var expressionKeyFrame = (KeyFrameAnimation<Vector3, Expr.Vector3>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression, GetCompositionEasingFunction(kf.Easing));
                        break;
                    case KeyFrameType.Value:
                        var valueKeyFrame = (KeyFrameAnimation<Vector3, Expr.Vector3>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value, GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidCastException();
                }
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        Vector4KeyFrameAnimation GetVector4KeyFrameAnimation(Vector4KeyFrameAnimation obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateVector4KeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case KeyFrameType.Expression:
                        var expressionKeyFrame = (KeyFrameAnimation<Vector4, Expr.Vector4>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression, GetCompositionEasingFunction(kf.Easing));
                        break;
                    case KeyFrameType.Value:
                        var valueKeyFrame = (KeyFrameAnimation<Vector4, Expr.Vector4>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value, GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidCastException();
                }
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        PathKeyFrameAnimation GetPathKeyFrameAnimation(PathKeyFrameAnimation obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreatePathKeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                result.InsertKeyFrame(kf.Progress, GetCompositionPath(((PathKeyFrameAnimation.ValueKeyFrame)kf).Value), GetCompositionEasingFunction(kf.Easing));
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        [return: NotNullIfNotNull("obj")]
        CompositionEasingFunction? GetCompositionEasingFunction(CompositionEasingFunction? obj)
        {
            if (obj is null)
            {
                return null;
            }

            return obj.Type switch
            {
                CompositionObjectType.LinearEasingFunction => GetLinearEasingFunction((LinearEasingFunction)obj),
                CompositionObjectType.StepEasingFunction => GetStepEasingFunction((StepEasingFunction)obj),
                CompositionObjectType.CubicBezierEasingFunction => GetCubicBezierEasingFunction((CubicBezierEasingFunction)obj),
                _ => throw new InvalidOperationException(),
            };
        }

        CompositionClip GetCompositionClip(CompositionClip obj) =>
            obj.Type switch
            {
                CompositionObjectType.InsetClip => GetInsetClip((InsetClip)obj),
                CompositionObjectType.CompositionGeometricClip => GetCompositionGeometricClip((CompositionGeometricClip)obj),
                _ => throw new InvalidOperationException(),
            };

        InsetClip GetInsetClip(InsetClip obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateInsetClip());

            // CompositionClip properties
            result.CenterPoint = obj.CenterPoint;
            result.Scale = obj.Scale;

            // InsetClip properties
            result.LeftInset = obj.LeftInset;
            result.RightInset = obj.RightInset;
            result.TopInset = obj.TopInset;
            result.BottomInset = obj.BottomInset;

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionGeometricClip GetCompositionGeometricClip(CompositionGeometricClip obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateGeometricClip());
            result.Geometry = GetCompositionGeometry(obj.Geometry);

            return result;
        }

        LinearEasingFunction GetLinearEasingFunction(LinearEasingFunction obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateLinearEasingFunction());
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        StepEasingFunction GetStepEasingFunction(StepEasingFunction obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateStepEasingFunction());
            result.FinalStep = obj.FinalStep;
            result.InitialStep = obj.InitialStep;
            result.IsFinalStepSingleFrame = obj.IsFinalStepSingleFrame;
            result.IsInitialStepSingleFrame = obj.IsInitialStepSingleFrame;
            result.StepCount = obj.StepCount;

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CubicBezierEasingFunction GetCubicBezierEasingFunction(CubicBezierEasingFunction obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateCubicBezierEasingFunction(obj.ControlPoint1, obj.ControlPoint2));
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionViewBox GetCompositionViewBox(CompositionViewBox obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateViewBox());
            result.Size = obj.Size;
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionVisualSurface GetCompositionVisualSurface(CompositionVisualSurface obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateVisualSurface());

            if (obj.SourceVisual is not null)
            {
                result.SourceVisual = GetVisual(obj.SourceVisual);
            }

            result.SourceSize = obj.SourceSize;

            result.SourceOffset = obj.SourceOffset;

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        LoadedImageSurface GetLoadedImageSurface(LoadedImageSurface obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            switch (obj.Type)
            {
                case LoadedImageSurface.LoadedImageSurfaceType.FromStream:
                    var bytes = ((LoadedImageSurfaceFromStream)obj).Bytes;
                    result = LoadedImageSurfaceFromStream.StartLoadFromStream(bytes);
                    break;
                case LoadedImageSurface.LoadedImageSurfaceType.FromUri:
                    var uri = ((LoadedImageSurfaceFromUri)obj).Uri;
                    result = LoadedImageSurfaceFromUri.StartLoadFromUri(uri);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            InitializeIDescribable(obj, result);
            Cache(obj, result);
            return result;
        }

        CompositionEffectFactory GetCompositionEffectFactory(CompositionEffectFactory obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateEffectFactory(obj.Effect));

            return result;
        }

        CompositionEffectBrush GetCompositionEffectBrush(CompositionEffectBrush obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            var effectFactory = GetCompositionEffectFactory(obj.GetEffectFactory());
            var effectBrush = effectFactory.CreateBrush();

            result = CacheAndInitializeCompositionObject(obj, effectBrush);

            // Set the sources.
            foreach (var source in effectFactory.Effect.Sources)
            {
                result.SetSourceParameter(source.Name, GetCompositionBrush(obj.GetSourceParameter(source.Name)));
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionLinearGradientBrush GetCompositionLinearGradientBrush(CompositionLinearGradientBrush obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeGradientBrush(obj, _c.CreateLinearGradientBrush());
            result.StartPoint = obj.StartPoint;
            result.EndPoint = obj.EndPoint;

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionRadialGradientBrush GetCompositionRadialGradientBrush(CompositionRadialGradientBrush obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeGradientBrush(obj, _c.CreateRadialGradientBrush());
            result.EllipseCenter = obj.EllipseCenter;
            result.EllipseRadius = obj.EllipseRadius;
            result.GradientOriginOffset = obj.GradientOriginOffset;

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionShape GetCompositionShape(CompositionShape obj) =>
            obj.Type switch
            {
                CompositionObjectType.CompositionContainerShape => GetCompositionContainerShape((CompositionContainerShape)obj),
                CompositionObjectType.CompositionSpriteShape => GetCompositionSpriteShape((CompositionSpriteShape)obj),
                _ => throw new InvalidOperationException(),
            };

        CompositionContainerShape GetCompositionContainerShape(CompositionContainerShape obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            // If this container has only 1 child, it might be coalescable with its child.
            if (obj.Shapes.Count == 1)
            {
                var child = obj.Shapes[0];
                if (!obj.Animators.Any())
                {
                    // The container has no animations. It can be replaced with its child as
                    // long as the child doesn't animate any of the non-default properties and
                    // the container isn't referenced by an animation.
                }
                else if (!child.Animators.Any() && child.Type == CompositionObjectType.CompositionContainerShape)
                {
                    // The child has no animations. It can be replaced with its parent as long
                    // as the parent doesn't animate any of the child's non-default properties
                    // and the child isn't referenced by an animation.
                }
            }

            result = CacheAndInitializeShape(obj, _c.CreateContainerShape());
            var shapeCollection = result.Shapes;
            foreach (var child in obj.Shapes)
            {
                shapeCollection.Add(GetCompositionShape(child));
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionSpriteShape GetCompositionSpriteShape(CompositionSpriteShape obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeShape(obj, _c.CreateSpriteShape());

            if (obj.StrokeBrush is not null)
            {
                result.IsStrokeNonScaling = obj.IsStrokeNonScaling;
                result.StrokeBrush = GetCompositionBrush(obj.StrokeBrush);
                result.StrokeDashCap = obj.StrokeDashCap;
                result.StrokeStartCap = obj.StrokeStartCap;
                result.StrokeEndCap = obj.StrokeEndCap;
                result.StrokeThickness = obj.StrokeThickness;
                result.StrokeMiterLimit = obj.StrokeMiterLimit;
                result.StrokeLineJoin = obj.StrokeLineJoin;
                result.StrokeDashOffset = obj.StrokeDashOffset;

                var strokeDashArray = result.StrokeDashArray;
                foreach (var strokeDash in obj.StrokeDashArray)
                {
                    strokeDashArray.Add(strokeDash);
                }
            }

            result.Geometry = GetCompositionGeometry(obj.Geometry);
            if (obj.FillBrush is not null)
            {
                result.FillBrush = GetCompositionBrush(obj.FillBrush);
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionSurfaceBrush GetCompositionSurfaceBrush(CompositionSurfaceBrush obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateSurfaceBrush(obj.Surface));

            if (obj.Surface is not null)
            {
                result.Surface = GetCompositionSurface(obj.Surface);
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionMaskBrush GetCompositionMaskBrush(CompositionMaskBrush obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateMaskBrush());

            if (obj.Mask is not null)
            {
                result.Mask = GetCompositionBrush(obj.Mask);
            }

            if (obj.Source is not null)
            {
                result.Source = GetCompositionBrush(obj.Source);
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        [return: NotNullIfNotNull("obj")]
        CompositionGeometry? GetCompositionGeometry(CompositionGeometry? obj)
        {
            if (obj is null)
            {
                return null;
            }

            return obj.Type switch
            {
                CompositionObjectType.CompositionPathGeometry => GetCompositionPathGeometry((CompositionPathGeometry)obj),
                CompositionObjectType.CompositionEllipseGeometry => GetCompositionEllipseGeometry((CompositionEllipseGeometry)obj),
                CompositionObjectType.CompositionRectangleGeometry => GetCompositionRectangleGeometry((CompositionRectangleGeometry)obj),
                CompositionObjectType.CompositionRoundedRectangleGeometry => GetCompositionRoundedRectangleGeometry((CompositionRoundedRectangleGeometry)obj),
                _ => throw new InvalidOperationException(),
            };
        }

        CompositionEllipseGeometry GetCompositionEllipseGeometry(CompositionEllipseGeometry obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionGeometry(obj, _c.CreateEllipseGeometry());
            if (obj.Center.X != 0 || obj.Center.Y != 0)
            {
                result.Center = obj.Center;
            }

            result.Radius = obj.Radius;
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionRectangleGeometry GetCompositionRectangleGeometry(CompositionRectangleGeometry obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionGeometry(obj, _c.CreateRectangleGeometry());
            if (obj.Offset is not null)
            {
                result.Offset = obj.Offset;
            }

            result.Size = obj.Size;
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionRoundedRectangleGeometry GetCompositionRoundedRectangleGeometry(CompositionRoundedRectangleGeometry obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionGeometry(obj, _c.CreateRoundedRectangleGeometry());
            if (obj.Offset is not null)
            {
                result.Offset = obj.Offset;
            }

            result.Size = obj.Size;
            result.CornerRadius = obj.CornerRadius;
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionPathGeometry GetCompositionPathGeometry(CompositionPathGeometry obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionGeometry(obj, _c.CreatePathGeometry(obj.Path is null ? null : GetCompositionPath(obj.Path)));
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionPath GetCompositionPath(CompositionPath obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = Cache(obj, new CompositionPath(GetCanvasGeometry(obj.Source)));
            InitializeIDescribable(obj, result);
            return result;
        }

        CanvasGeometry GetCanvasGeometry(Wg.IGeometrySource2D obj)
        {
            if (GetExistingCanvasGeometry((CanvasGeometry)obj, out var result))
            {
                return result;
            }

            var canvasGeometry = (CanvasGeometry)obj;
            switch (canvasGeometry.Type)
            {
                case CanvasGeometry.GeometryType.Combination:
                    {
                        var combination = (CanvasGeometry.Combination)canvasGeometry;
                        result = GetCanvasGeometry(combination.A).CombineWith(
                            GetCanvasGeometry(combination.B),
                            combination.Matrix,
                            combination.CombineMode);
                        break;
                    }

                case CanvasGeometry.GeometryType.Ellipse:
                    var ellipse = (CanvasGeometry.Ellipse)canvasGeometry;
                    result = CanvasGeometry.CreateEllipse(
                        null,
                        ellipse.X,
                        ellipse.Y,
                        ellipse.RadiusX,
                        ellipse.RadiusY);
                    break;

                case CanvasGeometry.GeometryType.Group:
                    var group = (CanvasGeometry.Group)canvasGeometry;
                    var geometries = group.Geometries.Select(g => GetCanvasGeometry(g)).ToArray();
                    result = CanvasGeometry.CreateGroup(null, geometries, group.FilledRegionDetermination);
                    break;

                case CanvasGeometry.GeometryType.Path:
                    using (var builder = new CanvasPathBuilder(null))
                    {
                        var path = (CanvasGeometry.Path)canvasGeometry;

                        if (path.FilledRegionDetermination != CanvasFilledRegionDetermination.Alternate)
                        {
                            builder.SetFilledRegionDetermination(path.FilledRegionDetermination);
                        }

                        foreach (var command in path.Commands)
                        {
                            switch (command.Type)
                            {
                                case CanvasPathBuilder.CommandType.BeginFigure:
                                    builder.BeginFigure(((CanvasPathBuilder.Command.BeginFigure)command).StartPoint);
                                    break;
                                case CanvasPathBuilder.CommandType.EndFigure:
                                    builder.EndFigure(((CanvasPathBuilder.Command.EndFigure)command).FigureLoop);
                                    break;
                                case CanvasPathBuilder.CommandType.AddLine:
                                    builder.AddLine(((CanvasPathBuilder.Command.AddLine)command).EndPoint);
                                    break;
                                case CanvasPathBuilder.CommandType.AddCubicBezier:
                                    var cb = (CanvasPathBuilder.Command.AddCubicBezier)command;
                                    builder.AddCubicBezier(cb.ControlPoint1, cb.ControlPoint2, cb.EndPoint);
                                    break;
                                default:
                                    throw new InvalidOperationException();
                            }
                        }

                        result = CanvasGeometry.CreatePath(builder);
                    }

                    break;
                case CanvasGeometry.GeometryType.RoundedRectangle:
                    var roundedRectangle = (CanvasGeometry.RoundedRectangle)canvasGeometry;
                    result = CanvasGeometry.CreateRoundedRectangle(
                        null,
                        roundedRectangle.X,
                        roundedRectangle.Y,
                        roundedRectangle.W,
                        roundedRectangle.H,
                        roundedRectangle.RadiusX,
                        roundedRectangle.RadiusY);
                    break;
                case CanvasGeometry.GeometryType.TransformedGeometry:
                    var transformedGeometry = (CanvasGeometry.TransformedGeometry)canvasGeometry;
                    result = GetCanvasGeometry(transformedGeometry.SourceGeometry).Transform(transformedGeometry.TransformMatrix);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            InitializeIDescribable(canvasGeometry, result);
            CacheCanvasGeometry(canvasGeometry, result);

            return result;
        }

        CompositionBrush GetCompositionBrush(CompositionBrush obj)
        {
            switch (obj.Type)
            {
                case CompositionObjectType.CompositionColorBrush:
                    return GetCompositionColorBrush((CompositionColorBrush)obj);
                case CompositionObjectType.CompositionEffectBrush:
                    return GetCompositionEffectBrush((CompositionEffectBrush)obj);
                case CompositionObjectType.CompositionLinearGradientBrush:
                case CompositionObjectType.CompositionRadialGradientBrush:
                    return GetCompositionGradientBrush((CompositionGradientBrush)obj);
                case CompositionObjectType.CompositionSurfaceBrush:
                    return GetCompositionSurfaceBrush((CompositionSurfaceBrush)obj);
                case CompositionObjectType.CompositionMaskBrush:
                    return GetCompositionMaskBrush((CompositionMaskBrush)obj);
                default:
                    throw new InvalidOperationException();
            }
        }

        CompositionGradientBrush GetCompositionGradientBrush(CompositionGradientBrush obj) =>
            obj.Type switch
            {
                CompositionObjectType.CompositionLinearGradientBrush => GetCompositionLinearGradientBrush((CompositionLinearGradientBrush)obj),
                CompositionObjectType.CompositionRadialGradientBrush => GetCompositionRadialGradientBrush((CompositionRadialGradientBrush)obj),
                _ => throw new InvalidOperationException(),
            };

        CompositionColorBrush GetCompositionColorBrush(CompositionColorBrush obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateColorBrush());
            if (obj.Color is not null)
            {
                result.Color = obj.Color;
            }

            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        CompositionColorGradientStop GetCompositionColorGradientStop(CompositionColorGradientStop obj)
        {
            if (GetExisting(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateColorGradientStop(obj.Offset, obj.Color));
            StartAnimationsAndFreeze(obj, result);
            return result;
        }

        ICompositionSurface GetCompositionSurface(ICompositionSurface obj) =>
            obj switch
            {
                CompositionVisualSurface compositionVisualSurface => GetCompositionVisualSurface(compositionVisualSurface),
                LoadedImageSurface loadedImageSurface => GetLoadedImageSurface(loadedImageSurface),
                _ => throw new InvalidOperationException(),
            };
    }
}
