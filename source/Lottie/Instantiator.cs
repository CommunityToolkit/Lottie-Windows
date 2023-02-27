// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

#define ReuseExpressionAnimation

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Graphics.Effects;
using Expr = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using Mgc = Microsoft.Graphics.Canvas;
using Mgce = Microsoft.Graphics.Canvas.Effects;
#if WINAPPSDK
using Wc = Microsoft.UI.Composition;
using Wm = Microsoft.UI.Xaml.Media;
#else
using Wc = Windows.UI.Composition;
using Wm = Windows.UI.Xaml.Media;
#endif
using Wd = CommunityToolkit.WinUI.Lottie.WinCompData;
using Wmd = CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;
using Wui = Windows.UI;

namespace CommunityToolkit.WinUI.Lottie
{
    /// <summary>
    /// Creates instances of a <see cref="Wc.Visual"/> tree from a description
    /// of the tree.
    /// </summary>
    sealed class Instantiator
    {
        readonly Wc.Compositor _c;
        readonly Dictionary<object, object> _cache = new Dictionary<object, object>(new ReferenceEqualsComparer());
        readonly Func<Uri, Wc.ICompositionSurface?>? _surfaceResolver;
#if ReuseExpressionAnimation
        // The one and only ExpressionAnimation - reset and reparameterized for each time we need one.
        readonly Wc.ExpressionAnimation _expressionAnimation;
#endif

        public Instantiator(Wc.Compositor compositor, Func<Uri, Wc.ICompositionSurface?>? surfaceResolver)
        {
            _c = compositor;
            _surfaceResolver = surfaceResolver;
#if ReuseExpressionAnimation
            _expressionAnimation = _c.CreateExpressionAnimation();
#endif
        }

        public Wc.CompositionObject GetInstance(Wd.CompositionObject obj) => GetCompositionObject(obj);

        bool GetExisting<T>(object key, [MaybeNullWhen(false)] out T result)
            where T : class
        {
            if (_cache.TryGetValue(key, out object? cached))
            {
                result = (T)cached;
                return true;
            }
            else
            {
                result = default(T);
                return false;
            }
        }

        T CacheAndInitializeCompositionObject<T>(Wd.CompositionObject key, T obj)
            where T : Wc.CompositionObject
        {
            Cache(key, obj);
            InitializeCompositionObject(key, obj);
            return obj;
        }

        T CacheAndInitializeShape<T>(Wd.CompositionShape source, T target)
            where T : Wc.CompositionShape
        {
            CacheAndInitializeCompositionObject(source, target);
            if (source.CenterPoint.HasValue)
            {
                target.CenterPoint = source.CenterPoint.Value;
            }

            if (source.Offset.HasValue)
            {
                target.Offset = source.Offset.Value;
            }

            if (source.RotationAngleInDegrees.HasValue)
            {
                target.RotationAngleInDegrees = source.RotationAngleInDegrees.Value;
            }

            if (source.Scale.HasValue)
            {
                target.Scale = source.Scale.Value;
            }

            if (source.TransformMatrix.HasValue)
            {
                target.TransformMatrix = source.TransformMatrix.Value;
            }

            return target;
        }

        T CacheAndInitializeGradientBrush<T>(Wd.CompositionGradientBrush source, T target)
            where T : Wc.CompositionGradientBrush
        {
            CacheAndInitializeCompositionObject(source, target);

            if (source.AnchorPoint.HasValue)
            {
                target.AnchorPoint = source.AnchorPoint.Value;
            }

            if (source.CenterPoint.HasValue)
            {
                target.CenterPoint = source.CenterPoint.Value;
            }

            var stops = target.ColorStops;
            foreach (var stop in source.ColorStops)
            {
                stops.Add(GetCompositionColorGradientStop(stop));
            }

            if (source.ExtendMode.HasValue)
            {
                target.ExtendMode = ExtendMode(source.ExtendMode.Value);
            }

            if (source.InterpolationSpace.HasValue)
            {
                target.InterpolationSpace = ColorSpace(source.InterpolationSpace.Value);
            }

            if (source.MappingMode.HasValue)
            {
                target.MappingMode = MappingMode(source.MappingMode.Value);
            }

            if (source.Offset.HasValue)
            {
                target.Offset = source.Offset.Value;
            }

            if (source.RotationAngleInDegrees.HasValue)
            {
                target.RotationAngleInDegrees = source.RotationAngleInDegrees.Value;
            }

            if (source.Scale.HasValue)
            {
                target.Scale = source.Scale.Value;
            }

            if (source.TransformMatrix.HasValue)
            {
                target.TransformMatrix = source.TransformMatrix.Value;
            }

            return target;
        }

        T CacheAndInitializeVisual<T>(Wd.Visual source, T target)
            where T : Wc.Visual
        {
            CacheAndInitializeCompositionObject(source, target);

            if (source.BorderMode.HasValue)
            {
                target.BorderMode = BorderMode(source.BorderMode.Value);
            }

            if (source.CenterPoint.HasValue)
            {
                target.CenterPoint = source.CenterPoint.Value;
            }

            if (source.Clip is not null)
            {
                target.Clip = GetCompositionClip(source.Clip);
            }

            if (source.IsVisible.HasValue)
            {
                target.IsVisible = source.IsVisible.Value;
            }

            if (source.Offset.HasValue)
            {
                target.Offset = source.Offset.Value;
            }

            if (source.Opacity.HasValue)
            {
                target.Opacity = source.Opacity.Value;
            }

            if (source.RotationAngleInDegrees.HasValue)
            {
                target.RotationAngleInDegrees = source.RotationAngleInDegrees.Value;
            }

            if (source.RotationAxis.HasValue)
            {
                target.RotationAxis = source.RotationAxis.Value;
            }

            if (source.Scale.HasValue)
            {
                target.Scale = source.Scale.Value;
            }

            if (source.Size.HasValue)
            {
                target.Size = source.Size.Value;
            }

            if (source.TransformMatrix.HasValue)
            {
                target.TransformMatrix = source.TransformMatrix.Value;
            }

            return target;
        }

        T CacheAndInitializeAnimation<T>(Wd.CompositionAnimation source, T target)
            where T : Wc.CompositionAnimation
        {
            CacheAndInitializeCompositionObject(source, target);
            foreach (var parameter in source.ReferenceParameters)
            {
                target.SetReferenceParameter(parameter.Key, GetCompositionObject(parameter.Value));
            }

            if (!string.IsNullOrWhiteSpace(source.Target))
            {
                target.Target = source.Target;
            }

            return target;
        }

        T CacheAndInitializeKeyFrameAnimation<T>(Wd.KeyFrameAnimation_ source, T target)
            where T : Wc.KeyFrameAnimation
        {
            CacheAndInitializeAnimation(source, target);
            target.Duration = source.Duration;
            return target;
        }

        T CacheAndInitializeCompositionGeometry<T>(Wd.CompositionGeometry source, T target)
            where T : Wc.CompositionGeometry
        {
            CacheAndInitializeCompositionObject(source, target);
            if (source.TrimStart.HasValue)
            {
                target.TrimStart = source.TrimStart.Value;
            }

            if (source.TrimEnd.HasValue)
            {
                target.TrimEnd = source.TrimEnd.Value;
            }

            if (source.TrimOffset.HasValue)
            {
                target.TrimOffset = source.TrimOffset.Value;
            }

            return target;
        }

        T Cache<T>(object key, T obj)
            where T : class
        {
            _cache.Add(key, obj);
            return obj;
        }

        Wc.LayerVisual GetLayerVisual(Wd.LayerVisual obj)
        {
            if (GetExisting<Wc.LayerVisual>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeVisual(obj, _c.CreateLayerVisual());

            if (obj.Shadow is not null)
            {
                result.Shadow = GetCompositionShadow(obj.Shadow);
            }

            InitializeContainerVisual(obj, result);
            StartAnimations(obj, result);
            return result;
        }

        Wc.ShapeVisual GetShapeVisual(Wd.ShapeVisual obj)
        {
            if (GetExisting<Wc.ShapeVisual>(obj, out var result))
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
            StartAnimations(obj, result);
            return result;
        }

        Wc.SpriteVisual GetSpriteVisual(Wd.SpriteVisual obj)
        {
            if (GetExisting<Wc.SpriteVisual>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeVisual(obj, _c.CreateSpriteVisual());

            if (obj.Brush is not null)
            {
                result.Brush = GetCompositionBrush(obj.Brush);
            }

            InitializeContainerVisual(obj, result);
            StartAnimations(obj, result);
            return result;
        }

        Wc.ContainerVisual GetContainerVisual(Wd.ContainerVisual obj)
        {
            if (GetExisting<Wc.ContainerVisual>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeVisual(obj, _c.CreateContainerVisual());
            InitializeContainerVisual(obj, result);

            StartAnimations(obj, result);
            return result;
        }

        void InitializeContainerVisual(Wd.ContainerVisual source, Wc.ContainerVisual target)
        {
            var children = target.Children;
            foreach (var child in source.Children)
            {
                children.InsertAtTop(GetVisual(child));
            }
        }

        void InitializeCompositionObject(Wd.CompositionObject source, Wc.CompositionObject target)
        {
            // Get the CompositionPropertySet on this object. This has the side-effect of initializing
            // it and starting any animations.
            // Prevent infinite recursion - the Properties on a CompositionPropertySet is itself.
            if (source.Type != Wd.CompositionObjectType.CompositionPropertySet)
            {
                GetCompositionPropertySet(source.Properties);
            }

            if (source.Comment is not null)
            {
                target.Comment = source.Comment;
            }
        }

        /// <summary>
        /// Starts animations on <paramref name="target"/> that have been started on <paramref name="source"/>.
        /// </summary>
        /// <remarks>
        /// This is the last stage of initializing a CompositionObject. CompositionObjects are initialized
        /// in 3 stages: creation, setting of properties, starting of animations. Animations must be started
        /// after properties are set because setting a property will stop a running animation.
        /// </remarks>
        void StartAnimations(Wd.CompositionObject source, Wc.CompositionObject target)
        {
            foreach (var animator in source.Animators)
            {
                var animation = GetCompositionAnimation(animator.Animation);
                if (animator.Controller is null || !animator.Controller.IsCustom)
                {
                    target.StartAnimation(animator.AnimatedProperty, animation);
                    var controller = animator.Controller;
                    if (controller is not null)
                    {
                        var animationController = GetAnimationController(controller);
                        if (controller.IsPaused)
                        {
                            animationController.Pause();
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("LottieViewer and Instantiator does not support custom AnimationControllers yet");
                    /*
                    We should retarget to SDK 22621 to support this
                    target.StartAnimation(animator.AnimatedProperty, animation, GetAnimationController(animator.Controller));
                    */
                }
            }
        }

        Wc.AnimationController GetAnimationController(Wd.AnimationController obj)
        {
            if (GetExisting<Wc.AnimationController>(obj, out var result))
            {
                return result;
            }

            if (obj.IsCustom)
            {
                throw new InvalidOperationException("LottieViewer and Instantiator does not support custom AnimationControllers yet");
                /*
                We should retarget to SDK 22621 to support this
                result = CacheAndInitializeCompositionObject(obj, _c.CreateAnimationController());

                if (obj.IsPaused)
                {
                    result.Pause();
                }
                */
            }
            else
            {
                var targetObject = GetCompositionObject(obj.TargetObject!);
                result = CacheAndInitializeCompositionObject(obj, targetObject.TryGetAnimationController(obj.TargetProperty));
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionObject GetCompositionObject(Wd.CompositionObject obj) =>
            obj.Type switch
            {
                Wd.CompositionObjectType.AnimationController => GetAnimationController((Wd.AnimationController)obj),
                Wd.CompositionObjectType.BooleanKeyFrameAnimation => GetBooleanKeyFrameAnimation((Wd.BooleanKeyFrameAnimation)obj),
                Wd.CompositionObjectType.ColorKeyFrameAnimation => GetColorKeyFrameAnimation((Wd.ColorKeyFrameAnimation)obj),
                Wd.CompositionObjectType.CompositionColorBrush => GetCompositionColorBrush((Wd.CompositionColorBrush)obj),
                Wd.CompositionObjectType.CompositionColorGradientStop => GetCompositionColorGradientStop((Wd.CompositionColorGradientStop)obj),
                Wd.CompositionObjectType.CompositionContainerShape => GetCompositionContainerShape((Wd.CompositionContainerShape)obj),
                Wd.CompositionObjectType.CompositionEffectBrush => GetCompositionEffectBrush((Wd.CompositionEffectBrush)obj),
                Wd.CompositionObjectType.CompositionEllipseGeometry => GetCompositionEllipseGeometry((Wd.CompositionEllipseGeometry)obj),
                Wd.CompositionObjectType.CompositionGeometricClip => GetCompositionGeometricClip((Wd.CompositionGeometricClip)obj),
                Wd.CompositionObjectType.CompositionLinearGradientBrush => GetCompositionLinearGradientBrush((Wd.CompositionLinearGradientBrush)obj),
                Wd.CompositionObjectType.CompositionMaskBrush => GetCompositionMaskBrush((Wd.CompositionMaskBrush)obj),
                Wd.CompositionObjectType.CompositionPathGeometry => GetCompositionPathGeometry((Wd.CompositionPathGeometry)obj),
                Wd.CompositionObjectType.CompositionPropertySet => GetCompositionPropertySet((Wd.CompositionPropertySet)obj),
                Wd.CompositionObjectType.CompositionRadialGradientBrush => GetCompositionRadialGradientBrush((Wd.CompositionRadialGradientBrush)obj),
                Wd.CompositionObjectType.CompositionRectangleGeometry => GetCompositionRectangleGeometry((Wd.CompositionRectangleGeometry)obj),
                Wd.CompositionObjectType.CompositionRoundedRectangleGeometry => GetCompositionRoundedRectangleGeometry((Wd.CompositionRoundedRectangleGeometry)obj),
                Wd.CompositionObjectType.CompositionSpriteShape => GetCompositionSpriteShape((Wd.CompositionSpriteShape)obj),
                Wd.CompositionObjectType.CompositionViewBox => GetCompositionViewBox((Wd.CompositionViewBox)obj),
                Wd.CompositionObjectType.CompositionVisualSurface => GetCompositionVisualSurface((Wd.CompositionVisualSurface)obj),
                Wd.CompositionObjectType.ContainerVisual => GetContainerVisual((Wd.ContainerVisual)obj),
                Wd.CompositionObjectType.CubicBezierEasingFunction => GetCubicBezierEasingFunction((Wd.CubicBezierEasingFunction)obj),
                Wd.CompositionObjectType.CompositionSurfaceBrush => GetCompositionSurfaceBrush((Wd.CompositionSurfaceBrush)obj),
                Wd.CompositionObjectType.DropShadow => GetDropShadow((Wd.DropShadow)obj),
                Wd.CompositionObjectType.ExpressionAnimation => GetExpressionAnimation((Wd.ExpressionAnimation)obj),
                Wd.CompositionObjectType.InsetClip => GetInsetClip((Wd.InsetClip)obj),
                Wd.CompositionObjectType.LayerVisual => GetLayerVisual((Wd.LayerVisual)obj),
                Wd.CompositionObjectType.LinearEasingFunction => GetLinearEasingFunction((Wd.LinearEasingFunction)obj),
                Wd.CompositionObjectType.PathKeyFrameAnimation => GetPathKeyFrameAnimation((Wd.PathKeyFrameAnimation)obj),
                Wd.CompositionObjectType.ScalarKeyFrameAnimation => GetScalarKeyFrameAnimation((Wd.ScalarKeyFrameAnimation)obj),
                Wd.CompositionObjectType.ShapeVisual => GetShapeVisual((Wd.ShapeVisual)obj),
                Wd.CompositionObjectType.SpriteVisual => GetSpriteVisual((Wd.SpriteVisual)obj),
                Wd.CompositionObjectType.StepEasingFunction => GetStepEasingFunction((Wd.StepEasingFunction)obj),
                Wd.CompositionObjectType.Vector2KeyFrameAnimation => GetVector2KeyFrameAnimation((Wd.Vector2KeyFrameAnimation)obj),
                Wd.CompositionObjectType.Vector3KeyFrameAnimation => GetVector3KeyFrameAnimation((Wd.Vector3KeyFrameAnimation)obj),
                Wd.CompositionObjectType.Vector4KeyFrameAnimation => GetVector4KeyFrameAnimation((Wd.Vector4KeyFrameAnimation)obj),
                _ => throw new InvalidOperationException(),
            };

        Wc.CompositionPropertySet GetCompositionPropertySet(Wd.CompositionPropertySet obj)
        {
            if (GetExisting<Wc.CompositionPropertySet>(obj, out var result))
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
                    case Wd.MetaData.PropertySetValueType.Color:
                        {
                            obj.TryGetColor(name, out var value);
                            result.InsertColor(name, Color(value!.Value));
                            break;
                        }

                    case Wd.MetaData.PropertySetValueType.Scalar:
                        {
                            obj.TryGetScalar(name, out var value);
                            result.InsertScalar(name, value!.Value);
                            break;
                        }

                    case Wd.MetaData.PropertySetValueType.Vector2:
                        {
                            obj.TryGetVector2(name, out var value);
                            result.InsertVector2(name, value!.Value);
                            break;
                        }

                    case Wd.MetaData.PropertySetValueType.Vector3:
                        {
                            obj.TryGetVector3(name, out var value);
                            result.InsertVector3(name, value!.Value);
                            break;
                        }

                    case Wd.MetaData.PropertySetValueType.Vector4:
                        {
                            obj.TryGetVector4(name, out var value);
                            result.InsertVector4(name, value!.Value);
                            break;
                        }

                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.Visual GetVisual(Wd.Visual obj) =>
            obj.Type switch
            {
                Wd.CompositionObjectType.ContainerVisual => GetContainerVisual((Wd.ContainerVisual)obj),
                Wd.CompositionObjectType.LayerVisual => GetLayerVisual((Wd.LayerVisual)obj),
                Wd.CompositionObjectType.ShapeVisual => GetShapeVisual((Wd.ShapeVisual)obj),
                Wd.CompositionObjectType.SpriteVisual => GetSpriteVisual((Wd.SpriteVisual)obj),
                _ => throw new InvalidOperationException(),
            };

        Wc.CompositionAnimation GetCompositionAnimation(Wd.CompositionAnimation obj) =>
            obj.Type switch
            {
                Wd.CompositionObjectType.BooleanKeyFrameAnimation => GetBooleanKeyFrameAnimation((Wd.BooleanKeyFrameAnimation)obj),
                Wd.CompositionObjectType.ColorKeyFrameAnimation => GetColorKeyFrameAnimation((Wd.ColorKeyFrameAnimation)obj),
                Wd.CompositionObjectType.ExpressionAnimation => GetExpressionAnimation((Wd.ExpressionAnimation)obj),
                Wd.CompositionObjectType.PathKeyFrameAnimation => GetPathKeyFrameAnimation((Wd.PathKeyFrameAnimation)obj),
                Wd.CompositionObjectType.ScalarKeyFrameAnimation => GetScalarKeyFrameAnimation((Wd.ScalarKeyFrameAnimation)obj),
                Wd.CompositionObjectType.Vector2KeyFrameAnimation => GetVector2KeyFrameAnimation((Wd.Vector2KeyFrameAnimation)obj),
                Wd.CompositionObjectType.Vector3KeyFrameAnimation => GetVector3KeyFrameAnimation((Wd.Vector3KeyFrameAnimation)obj),
                Wd.CompositionObjectType.Vector4KeyFrameAnimation => GetVector4KeyFrameAnimation((Wd.Vector4KeyFrameAnimation)obj),
                _ => throw new InvalidOperationException(),
            };

        Wc.ExpressionAnimation GetExpressionAnimation(Wd.ExpressionAnimation obj)
        {
#if ReuseExpressionAnimation
            // Reset and reuse the same ExpressionAnimation each time.
            var result = _expressionAnimation;
            result.Comment = obj.Comment ?? string.Empty;

            // If there is a Target set it. Note however that the Target isn't used for anything
            // interesting in this scenario, and there is no way to reset the Target to an
            // empty string (the Target API disallows empty). In reality, for all our uses
            // the Target will not be set and it doesn't matter if it was set previously.
            if (!string.IsNullOrWhiteSpace(obj.Target))
            {
                result.Target = obj.Target;
            }

            result.Expression = obj.Expression.ToText();
            result.ClearAllParameters();
            foreach (var parameter in obj.ReferenceParameters)
            {
                result.SetReferenceParameter(parameter.Key, GetCompositionObject(parameter.Value));
            }
#else
            if (GetExisting(obj, out Wc.ExpressionAnimation result))
            {
                return result;
            }
            result = CacheAndInitializeAnimation(obj, _c.CreateExpressionAnimation(obj.Expression));
#endif
            StartAnimations(obj, result);
            return result;
        }

        Wc.BooleanKeyFrameAnimation GetBooleanKeyFrameAnimation(Wd.BooleanKeyFrameAnimation obj)
        {
            if (GetExisting<Wc.BooleanKeyFrameAnimation>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateBooleanKeyFrameAnimation());

            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case Wd.KeyFrameType.Expression:
                        var expressionKeyFrame = (Wd.KeyFrameAnimation<bool, Expr.Boolean>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression.ToText());
                        break;
                    case Wd.KeyFrameType.Value:
                        var valueKeyFrame = (Wd.KeyFrameAnimation<bool, Expr.Boolean>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.ColorKeyFrameAnimation GetColorKeyFrameAnimation(Wd.ColorKeyFrameAnimation obj)
        {
            if (GetExisting<Wc.ColorKeyFrameAnimation>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateColorKeyFrameAnimation());

            if (obj.InterpolationColorSpace != Wd.CompositionColorSpace.Auto)
            {
                result.InterpolationColorSpace = (Wc.CompositionColorSpace)obj.InterpolationColorSpace;
            }

            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case Wd.KeyFrameType.Expression:
                        var expressionKeyFrame = (Wd.KeyFrameAnimation<Wd.Wui.Color, Expr.Color>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression.ToText(), GetCompositionEasingFunction(kf.Easing));
                        break;
                    case Wd.KeyFrameType.Value:
                        var valueKeyFrame = (Wd.KeyFrameAnimation<Wd.Wui.Color, Expr.Color>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, Color(valueKeyFrame.Value), GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.ScalarKeyFrameAnimation GetScalarKeyFrameAnimation(Wd.ScalarKeyFrameAnimation obj)
        {
            if (GetExisting<Wc.ScalarKeyFrameAnimation>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateScalarKeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case Wd.KeyFrameType.Expression:
                        var expressionKeyFrame = (Wd.KeyFrameAnimation<float, Expr.Scalar>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression.ToText(), GetCompositionEasingFunction(kf.Easing));
                        break;
                    case Wd.KeyFrameType.Value:
                        var valueKeyFrame = (Wd.KeyFrameAnimation<float, Expr.Scalar>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value, GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.Vector2KeyFrameAnimation GetVector2KeyFrameAnimation(Wd.Vector2KeyFrameAnimation obj)
        {
            if (GetExisting<Wc.Vector2KeyFrameAnimation>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateVector2KeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case Wd.KeyFrameType.Expression:
                        var expressionKeyFrame = (Wd.KeyFrameAnimation<Vector2, Expr.Vector2>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression.ToText(), GetCompositionEasingFunction(kf.Easing));
                        break;
                    case Wd.KeyFrameType.Value:
                        var valueKeyFrame = (Wd.KeyFrameAnimation<Vector2, Expr.Vector2>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value, GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.Vector3KeyFrameAnimation GetVector3KeyFrameAnimation(Wd.Vector3KeyFrameAnimation obj)
        {
            if (GetExisting<Wc.Vector3KeyFrameAnimation>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateVector3KeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case Wd.KeyFrameType.Expression:
                        var expressionKeyFrame = (Wd.KeyFrameAnimation<Vector3, Expr.Vector3>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression.ToText(), GetCompositionEasingFunction(kf.Easing));
                        break;
                    case Wd.KeyFrameType.Value:
                        var valueKeyFrame = (Wd.KeyFrameAnimation<Vector3, Expr.Vector3>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value, GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidCastException();
                }
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.Vector4KeyFrameAnimation GetVector4KeyFrameAnimation(Wd.Vector4KeyFrameAnimation obj)
        {
            if (GetExisting<Wc.Vector4KeyFrameAnimation>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreateVector4KeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                switch (kf.Type)
                {
                    case Wd.KeyFrameType.Expression:
                        var expressionKeyFrame = (Wd.KeyFrameAnimation<Vector4, Expr.Vector4>.ExpressionKeyFrame)kf;
                        result.InsertExpressionKeyFrame(kf.Progress, expressionKeyFrame.Expression.ToText(), GetCompositionEasingFunction(kf.Easing));
                        break;
                    case Wd.KeyFrameType.Value:
                        var valueKeyFrame = (Wd.KeyFrameAnimation<Vector4, Expr.Vector4>.ValueKeyFrame)kf;
                        result.InsertKeyFrame(kf.Progress, valueKeyFrame.Value, GetCompositionEasingFunction(kf.Easing));
                        break;
                    default:
                        throw new InvalidCastException();
                }
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.PathKeyFrameAnimation GetPathKeyFrameAnimation(Wd.PathKeyFrameAnimation obj)
        {
            if (GetExisting<Wc.PathKeyFrameAnimation>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeKeyFrameAnimation(obj, _c.CreatePathKeyFrameAnimation());
            foreach (var kf in obj.KeyFrames)
            {
                result.InsertKeyFrame(kf.Progress, GetCompositionPath(((Wd.PathKeyFrameAnimation.ValueKeyFrame)kf).Value), GetCompositionEasingFunction(kf.Easing));
            }

            StartAnimations(obj, result);
            return result;
        }

        [return: NotNullIfNotNull("obj")]
        Wc.CompositionEasingFunction? GetCompositionEasingFunction(Wd.CompositionEasingFunction? obj)
        {
            if (obj is null)
            {
                return null;
            }

            return obj.Type switch
            {
                Wd.CompositionObjectType.LinearEasingFunction => GetLinearEasingFunction((Wd.LinearEasingFunction)obj),
                Wd.CompositionObjectType.StepEasingFunction => GetStepEasingFunction((Wd.StepEasingFunction)obj),
                Wd.CompositionObjectType.CubicBezierEasingFunction => GetCubicBezierEasingFunction((Wd.CubicBezierEasingFunction)obj),
                _ => throw new InvalidOperationException(),
            };
        }

        Wc.CompositionClip GetCompositionClip(Wd.CompositionClip obj) =>
            obj.Type switch
            {
                Wd.CompositionObjectType.InsetClip => GetInsetClip((Wd.InsetClip)obj),
                Wd.CompositionObjectType.CompositionGeometricClip => GetCompositionGeometricClip((Wd.CompositionGeometricClip)obj),
                _ => throw new InvalidOperationException(),
            };

        static bool IsNullOrZero(float? value) => value is null || value == 0;

        static bool IsNullOrOne(Vector2? value) => value is null || value == Vector2.One;

        static bool IsNullOrZero(Vector2? value) => value is null || value == Vector2.Zero;

        Wc.InsetClip GetInsetClip(Wd.InsetClip obj)
        {
            if (GetExisting<Wc.InsetClip>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateInsetClip());

            // CompositionClip properties
            if (!IsNullOrZero(obj.CenterPoint))
            {
                result.CenterPoint = obj.CenterPoint!.Value;
            }

            if (!IsNullOrOne(obj.Scale))
            {
                result.Scale = obj.Scale!.Value;
            }

            // InsetClip properties
            if (!IsNullOrZero(obj.LeftInset))
            {
                result.LeftInset = obj.LeftInset!.Value;
            }

            if (!IsNullOrZero(obj.RightInset))
            {
                result.RightInset = obj.RightInset!.Value;
            }

            if (!IsNullOrZero(obj.TopInset))
            {
                result.TopInset = obj.TopInset!.Value;
            }

            if (!IsNullOrZero(obj.BottomInset))
            {
                result.BottomInset = obj.BottomInset!.Value;
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionGeometricClip GetCompositionGeometricClip(Wd.CompositionGeometricClip obj)
        {
            if (GetExisting<Wc.CompositionGeometricClip>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateGeometricClip(GetCompositionGeometry(obj.Geometry)));
            StartAnimations(obj, result);
            return result;
        }

        Wc.LinearEasingFunction GetLinearEasingFunction(Wd.LinearEasingFunction obj)
        {
            if (GetExisting<Wc.LinearEasingFunction>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateLinearEasingFunction());
            StartAnimations(obj, result);
            return result;
        }

        Wc.StepEasingFunction GetStepEasingFunction(Wd.StepEasingFunction obj)
        {
            if (GetExisting<Wc.StepEasingFunction>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateStepEasingFunction());
            if (obj.FinalStep.HasValue)
            {
                result.FinalStep = obj.FinalStep.Value;
            }

            if (obj.InitialStep.HasValue)
            {
                result.InitialStep = obj.InitialStep.Value;
            }

            if (obj.IsFinalStepSingleFrame.HasValue)
            {
                result.IsFinalStepSingleFrame = obj.IsFinalStepSingleFrame.Value;
            }

            if (obj.IsInitialStepSingleFrame.HasValue)
            {
                result.IsInitialStepSingleFrame = obj.IsInitialStepSingleFrame.Value;
            }

            if (obj.StepCount.HasValue)
            {
                result.StepCount = obj.StepCount.Value;
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CubicBezierEasingFunction GetCubicBezierEasingFunction(Wd.CubicBezierEasingFunction obj)
        {
            if (GetExisting<Wc.CubicBezierEasingFunction>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateCubicBezierEasingFunction(obj.ControlPoint1, obj.ControlPoint2));
            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionShadow GetCompositionShadow(Wd.CompositionShadow obj) =>
            obj.Type switch
            {
                Wd.CompositionObjectType.DropShadow => GetDropShadow((Wd.DropShadow)obj),
                _ => throw new InvalidOperationException(),
            };

        Wc.DropShadow GetDropShadow(Wd.DropShadow obj)
        {
            if (GetExisting<Wc.DropShadow>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateDropShadow());

            if (obj.BlurRadius.HasValue)
            {
                result.BlurRadius = obj.BlurRadius.Value;
            }

            if (obj.Color.HasValue)
            {
                result.Color = Color(obj.Color.Value);
            }

            if (obj.Mask is not null)
            {
                result.Mask = GetCompositionBrush(obj.Mask);
            }

            if (obj.Offset.HasValue)
            {
                result.Offset = obj.Offset.Value;
            }

            if (obj.Opacity.HasValue)
            {
                result.Opacity = obj.Opacity.Value;
            }

            if (obj.SourcePolicy.HasValue)
            {
                result.SourcePolicy = DropShadowSourcePolicy(obj.SourcePolicy.Value);
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionViewBox GetCompositionViewBox(Wd.CompositionViewBox obj)
        {
            if (GetExisting<Wc.CompositionViewBox>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateViewBox());
            result.Size = obj.Size;
            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionVisualSurface GetCompositionVisualSurface(Wd.CompositionVisualSurface obj)
        {
            if (GetExisting<Wc.CompositionVisualSurface>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateVisualSurface());

            if (obj.SourceVisual is not null)
            {
                result.SourceVisual = GetVisual(obj.SourceVisual);
            }

            if (obj.SourceSize.HasValue)
            {
                result.SourceSize = obj.SourceSize.Value;
            }

            if (obj.SourceOffset.HasValue)
            {
                result.SourceOffset = obj.SourceOffset.Value;
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionShape GetCompositionShape(Wd.CompositionShape obj) =>
            obj.Type switch
            {
                Wd.CompositionObjectType.CompositionContainerShape => GetCompositionContainerShape((Wd.CompositionContainerShape)obj),
                Wd.CompositionObjectType.CompositionSpriteShape => GetCompositionSpriteShape((Wd.CompositionSpriteShape)obj),
                _ => throw new InvalidOperationException(),
            };

        Wc.CompositionContainerShape GetCompositionContainerShape(Wd.CompositionContainerShape obj)
        {
            if (GetExisting<Wc.CompositionContainerShape>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeShape(obj, _c.CreateContainerShape());
            var shapeCollection = result.Shapes;
            foreach (var child in obj.Shapes)
            {
                shapeCollection.Add(GetCompositionShape(child));
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionEffectBrush GetCompositionEffectBrush(Wd.CompositionEffectBrush obj)
        {
            if (GetExisting<Wc.CompositionEffectBrush>(obj, out var result))
            {
                return result;
            }

            // Create and initialize the effect brush.
            var effectBrush = GetCompositionEffectFactory(obj.GetEffectFactory()).CreateBrush();
            result = CacheAndInitializeCompositionObject(obj, effectBrush);

            // Set the sources.
            foreach (var source in obj.GetEffectFactory().Effect.Sources)
            {
                result.SetSourceParameter(source.Name, GetCompositionBrush(obj.GetSourceParameter(source.Name)));
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionEffectFactory GetCompositionEffectFactory(Wd.CompositionEffectFactory obj)
        {
            if (GetExisting<Wc.CompositionEffectFactory>(obj, out var result))
            {
                return result;
            }

            IGraphicsEffect graphicsEffect;

            switch (obj.Effect.Type)
            {
                case Wd.Mgce.GraphicsEffectType.CompositeEffect:
                    {
                        var effect = (Wd.Mgce.CompositeEffect)obj.Effect;

                        // Create the effect.
                        var resultEffect = new Mgce.CompositeEffect
                        {
                            Mode = CanvasComposite(effect.Mode),
                        };

                        foreach (var source in effect.Sources)
                        {
                            resultEffect.Sources.Add(new Wc.CompositionEffectSourceParameter(source.Name));
                        }

                        graphicsEffect = resultEffect;
                    }

                    break;
                case Wd.Mgce.GraphicsEffectType.GaussianBlurEffect:
                    {
                        var effect = (Wd.Mgce.GaussianBlurEffect)obj.Effect;

                        // Create the effect.
                        var resultEffect = new Mgce.GaussianBlurEffect();

                        resultEffect.BlurAmount = effect.BlurAmount;

                        resultEffect.Source = new Wc.CompositionEffectSourceParameter(effect.Sources.First().Name);

                        graphicsEffect = resultEffect;
                    }

                    break;
                default:
                    throw new InvalidOperationException();
            }

            var factory = _c.CreateEffectFactory(graphicsEffect);

            Cache(obj, factory);

            return factory;
        }

        Wc.CompositionSpriteShape GetCompositionSpriteShape(Wd.CompositionSpriteShape obj)
        {
            if (GetExisting<Wc.CompositionSpriteShape>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeShape(obj, _c.CreateSpriteShape());

            if (obj.StrokeBrush is not null)
            {
                result.StrokeBrush = GetCompositionBrush(obj.StrokeBrush);

                if (obj.StrokeDashCap.HasValue)
                {
                    result.StrokeDashCap = StrokeCap(obj.StrokeDashCap.Value);
                }

                if (obj.StrokeStartCap.HasValue)
                {
                    result.StrokeStartCap = StrokeCap(obj.StrokeStartCap.Value);
                }

                if (obj.StrokeEndCap.HasValue)
                {
                    result.StrokeEndCap = StrokeCap(obj.StrokeEndCap.Value);
                }

                if (obj.StrokeThickness.HasValue)
                {
                    result.StrokeThickness = obj.StrokeThickness.Value;
                }

                if (obj.StrokeMiterLimit.HasValue)
                {
                    result.StrokeMiterLimit = obj.StrokeMiterLimit.Value;
                }

                if (obj.StrokeLineJoin.HasValue)
                {
                    result.StrokeLineJoin = StrokeLineJoin(obj.StrokeLineJoin.Value);
                }

                if (obj.StrokeDashOffset.HasValue)
                {
                    result.StrokeDashOffset = obj.StrokeDashOffset.Value;
                }

                if (obj.IsStrokeNonScaling.HasValue)
                {
                    result.IsStrokeNonScaling = obj.IsStrokeNonScaling.Value;
                }

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

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionColorGradientStop GetCompositionColorGradientStop(Wd.CompositionColorGradientStop obj)
        {
            if (GetExisting<Wc.CompositionColorGradientStop>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateColorGradientStop(obj.Offset, Color(obj.Color)));

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionSurfaceBrush GetCompositionSurfaceBrush(Wd.CompositionSurfaceBrush obj)
        {
            if (GetExisting<Wc.CompositionSurfaceBrush>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateSurfaceBrush());

            if (obj.Surface is not null)
            {
                result.Surface = GetCompositionSurface(obj.Surface);
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionMaskBrush GetCompositionMaskBrush(Wd.CompositionMaskBrush obj)
        {
            if (GetExisting<Wc.CompositionMaskBrush>(obj, out var result))
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

            StartAnimations(obj, result);
            return result;
        }

        [return: NotNullIfNotNull("obj")]
        Wc.CompositionGeometry? GetCompositionGeometry(Wd.CompositionGeometry? obj)
        {
            if (obj is null)
            {
                return null;
            }

            return obj.Type switch
            {
                Wd.CompositionObjectType.CompositionPathGeometry => GetCompositionPathGeometry((Wd.CompositionPathGeometry)obj),
                Wd.CompositionObjectType.CompositionEllipseGeometry => GetCompositionEllipseGeometry((Wd.CompositionEllipseGeometry)obj),
                Wd.CompositionObjectType.CompositionRectangleGeometry => GetCompositionRectangleGeometry((Wd.CompositionRectangleGeometry)obj),
                Wd.CompositionObjectType.CompositionRoundedRectangleGeometry => GetCompositionRoundedRectangleGeometry((Wd.CompositionRoundedRectangleGeometry)obj),
                _ => throw new InvalidOperationException(),
            };
        }

        Wc.CompositionEllipseGeometry GetCompositionEllipseGeometry(Wd.CompositionEllipseGeometry obj)
        {
            if (GetExisting<Wc.CompositionEllipseGeometry>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionGeometry(obj, _c.CreateEllipseGeometry());
            if (obj.Center.X != 0 || obj.Center.Y != 0)
            {
                result.Center = obj.Center;
            }

            result.Radius = obj.Radius;
            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionRectangleGeometry GetCompositionRectangleGeometry(Wd.CompositionRectangleGeometry obj)
        {
            if (GetExisting<Wc.CompositionRectangleGeometry>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionGeometry(obj, _c.CreateRectangleGeometry());
            if (obj.Offset.HasValue)
            {
                result.Offset = obj.Offset.Value;
            }

            if (obj.Size.HasValue)
            {
                result.Size = obj.Size.Value;
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionRoundedRectangleGeometry GetCompositionRoundedRectangleGeometry(Wd.CompositionRoundedRectangleGeometry obj)
        {
            if (GetExisting<Wc.CompositionRoundedRectangleGeometry>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionGeometry(obj, _c.CreateRoundedRectangleGeometry());
            if (obj.Offset.HasValue)
            {
                result.Offset = obj.Offset.Value;
            }

            if (obj.Size.HasValue)
            {
                result.Size = obj.Size.Value;
            }

            result.CornerRadius = obj.CornerRadius;
            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionPathGeometry GetCompositionPathGeometry(Wd.CompositionPathGeometry obj)
        {
            if (GetExisting<Wc.CompositionPathGeometry>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionGeometry(obj, _c.CreatePathGeometry(obj.Path is null ? null : GetCompositionPath(obj.Path)));
            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionPath GetCompositionPath(Wd.CompositionPath obj)
        {
            if (GetExisting<Wc.CompositionPath>(obj, out var result))
            {
                return result;
            }

            result = Cache(obj, new Wc.CompositionPath(GetCanvasGeometry(obj.Source)));
            return result;
        }

        CanvasGeometry GetCanvasGeometry(Wd.Wg.IGeometrySource2D obj)
        {
            if (GetExisting<CanvasGeometry>(obj, out var result))
            {
                return result;
            }

            var canvasGeometry = (Wd.Mgcg.CanvasGeometry)obj;
            switch (canvasGeometry.Type)
            {
                case Wd.Mgcg.CanvasGeometry.GeometryType.Combination:
                    {
                        var combination = (Wd.Mgcg.CanvasGeometry.Combination)canvasGeometry;
                        return Cache(obj, GetCanvasGeometry(combination.A).CombineWith(
                            GetCanvasGeometry(combination.B),
                            combination.Matrix,
                            Combine(combination.CombineMode)));
                    }

                case Wd.Mgcg.CanvasGeometry.GeometryType.Ellipse:
                    var ellipse = (Wd.Mgcg.CanvasGeometry.Ellipse)canvasGeometry;
                    return CanvasGeometry.CreateEllipse(
                        null,
                        ellipse.X,
                        ellipse.Y,
                        ellipse.RadiusX,
                        ellipse.RadiusY);
                case Wd.Mgcg.CanvasGeometry.GeometryType.Group:
                    var group = (Wd.Mgcg.CanvasGeometry.Group)canvasGeometry;
                    return CanvasGeometry.CreateGroup(
                        null,
                        group.Geometries.Select(g => GetCanvasGeometry(g)).ToArray(),
                        FilledRegionDetermination(group.FilledRegionDetermination));
                case Wd.Mgcg.CanvasGeometry.GeometryType.Path:
                    using (var builder = new CanvasPathBuilder(null))
                    {
                        var path = (Wd.Mgcg.CanvasGeometry.Path)canvasGeometry;

                        if (path.FilledRegionDetermination != Wd.Mgcg.CanvasFilledRegionDetermination.Alternate)
                        {
                            builder.SetFilledRegionDetermination(FilledRegionDetermination(path.FilledRegionDetermination));
                        }

                        foreach (var command in path.Commands)
                        {
                            switch (command.Type)
                            {
                                case Wd.Mgcg.CanvasPathBuilder.CommandType.BeginFigure:
                                    builder.BeginFigure(((Wd.Mgcg.CanvasPathBuilder.Command.BeginFigure)command).StartPoint);
                                    break;
                                case Wd.Mgcg.CanvasPathBuilder.CommandType.EndFigure:
                                    builder.EndFigure(CanvasFigureLoop(((Wd.Mgcg.CanvasPathBuilder.Command.EndFigure)command).FigureLoop));
                                    break;
                                case Wd.Mgcg.CanvasPathBuilder.CommandType.AddLine:
                                    builder.AddLine(((Wd.Mgcg.CanvasPathBuilder.Command.AddLine)command).EndPoint);
                                    break;
                                case Wd.Mgcg.CanvasPathBuilder.CommandType.AddCubicBezier:
                                    var cb = (Wd.Mgcg.CanvasPathBuilder.Command.AddCubicBezier)command;
                                    builder.AddCubicBezier(cb.ControlPoint1, cb.ControlPoint2, cb.EndPoint);
                                    break;
                                default:
                                    throw new InvalidOperationException();
                            }
                        }

                        return Cache(obj, CanvasGeometry.CreatePath(builder));
                    }

                case Wd.Mgcg.CanvasGeometry.GeometryType.RoundedRectangle:
                    var roundedRectangle = (Wd.Mgcg.CanvasGeometry.RoundedRectangle)canvasGeometry;
                    return CanvasGeometry.CreateRoundedRectangle(
                        null,
                        roundedRectangle.X,
                        roundedRectangle.Y,
                        roundedRectangle.W,
                        roundedRectangle.H,
                        roundedRectangle.RadiusX,
                        roundedRectangle.RadiusY);
                case Wd.Mgcg.CanvasGeometry.GeometryType.TransformedGeometry:
                    var transformedGeometry = (Wd.Mgcg.CanvasGeometry.TransformedGeometry)canvasGeometry;
                    return GetCanvasGeometry(transformedGeometry.SourceGeometry).Transform(transformedGeometry.TransformMatrix);
                default:
                    throw new InvalidOperationException();
            }
        }

        Wc.CompositionBrush GetCompositionBrush(Wd.CompositionBrush obj)
        {
            switch (obj.Type)
            {
                case Wd.CompositionObjectType.CompositionColorBrush:
                    return GetCompositionColorBrush((Wd.CompositionColorBrush)obj);
                case Wd.CompositionObjectType.CompositionEffectBrush:
                    return GetCompositionEffectBrush((Wd.CompositionEffectBrush)obj);
                case Wd.CompositionObjectType.CompositionSurfaceBrush:
                    return GetCompositionSurfaceBrush((Wd.CompositionSurfaceBrush)obj);
                case Wd.CompositionObjectType.CompositionMaskBrush:
                    return GetCompositionMaskBrush((Wd.CompositionMaskBrush)obj);
                case Wd.CompositionObjectType.CompositionLinearGradientBrush:
                case Wd.CompositionObjectType.CompositionRadialGradientBrush:
                    return GetCompositionGradientBrush((Wd.CompositionGradientBrush)obj);
                default:
                    throw new InvalidOperationException();
            }
        }

        Wc.CompositionColorBrush GetCompositionColorBrush(Wd.CompositionColorBrush obj)
        {
            if (GetExisting<Wc.CompositionColorBrush>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeCompositionObject(obj, _c.CreateColorBrush());
            if (obj.Color.HasValue)
            {
                result.Color = Color(obj.Color.Value);
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionGradientBrush GetCompositionGradientBrush(Wd.CompositionGradientBrush obj) =>
            obj.Type switch
            {
                Wd.CompositionObjectType.CompositionLinearGradientBrush => GetCompositionLinearGradientBrush((Wd.CompositionLinearGradientBrush)obj),
                Wd.CompositionObjectType.CompositionRadialGradientBrush => GetCompositionRadialGradientBrush((Wd.CompositionRadialGradientBrush)obj),
                _ => throw new InvalidOperationException(),
            };

        Wc.CompositionLinearGradientBrush GetCompositionLinearGradientBrush(Wd.CompositionLinearGradientBrush obj)
        {
            if (GetExisting<Wc.CompositionLinearGradientBrush>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeGradientBrush(obj, _c.CreateLinearGradientBrush());

            if (obj.StartPoint.HasValue)
            {
                result.StartPoint = obj.StartPoint.Value;
            }

            if (obj.EndPoint.HasValue)
            {
                result.EndPoint = obj.EndPoint.Value;
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.CompositionRadialGradientBrush GetCompositionRadialGradientBrush(Wd.CompositionRadialGradientBrush obj)
        {
            if (GetExisting<Wc.CompositionRadialGradientBrush>(obj, out var result))
            {
                return result;
            }

            result = CacheAndInitializeGradientBrush(obj, _c.CreateRadialGradientBrush());

            if (obj.EllipseCenter.HasValue)
            {
                result.EllipseCenter = obj.EllipseCenter.Value;
            }

            if (obj.EllipseRadius.HasValue)
            {
                result.EllipseRadius = obj.EllipseRadius.Value;
            }

            if (obj.GradientOriginOffset.HasValue)
            {
                result.GradientOriginOffset = obj.GradientOriginOffset.Value;
            }

            StartAnimations(obj, result);
            return result;
        }

        Wc.ICompositionSurface? GetCompositionSurface(Wd.ICompositionSurface obj) =>
            obj switch
            {
                Wd.CompositionVisualSurface compositionVisualSurface => (Wc.ICompositionSurface)GetCompositionObject(compositionVisualSurface),
                Wmd.LoadedImageSurface loadedImageSurface => GetLoadedImageSurface(loadedImageSurface),
                _ => throw new InvalidOperationException(),
            };

        Wc.ICompositionSurface? GetLoadedImageSurface(Wmd.LoadedImageSurface obj)
        {
            if (GetExisting<Wc.ICompositionSurface>(obj, out var result))
            {
                return result;
            }

            switch (obj.Type)
            {
                case Wmd.LoadedImageSurface.LoadedImageSurfaceType.FromStream:
                    var bytes = ((Wmd.LoadedImageSurfaceFromStream)obj).Bytes;
                    result = Wm.LoadedImageSurface.StartLoadFromStream(bytes.AsBuffer().AsStream().AsRandomAccessStream());
                    break;
                case Wmd.LoadedImageSurface.LoadedImageSurfaceType.FromUri:
                    var uri = ((Wmd.LoadedImageSurfaceFromUri)obj).Uri;

                    // Ask the resolver to convert the URI to a surface. It can return null on failure.
                    result = _surfaceResolver?.Invoke(uri);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            if (result is not null)
            {
                Cache(obj, result);
            }

            return result;
        }

        static Wc.CompositionBorderMode BorderMode(Wd.CompositionBorderMode value) =>
            value switch
            {
                Wd.CompositionBorderMode.Hard => Wc.CompositionBorderMode.Hard,
                Wd.CompositionBorderMode.Inherit => Wc.CompositionBorderMode.Inherit,
                Wd.CompositionBorderMode.Soft => Wc.CompositionBorderMode.Soft,
                _ => throw new InvalidOperationException(),
            };

        static Wc.CompositionStrokeLineJoin StrokeLineJoin(Wd.CompositionStrokeLineJoin value) =>
            value switch
            {
                Wd.CompositionStrokeLineJoin.Miter => Wc.CompositionStrokeLineJoin.Miter,
                Wd.CompositionStrokeLineJoin.Bevel => Wc.CompositionStrokeLineJoin.Bevel,
                Wd.CompositionStrokeLineJoin.Round => Wc.CompositionStrokeLineJoin.Round,
                Wd.CompositionStrokeLineJoin.MiterOrBevel => Wc.CompositionStrokeLineJoin.MiterOrBevel,
                _ => throw new InvalidOperationException(),
            };

        static Wc.CompositionStrokeCap StrokeCap(Wd.CompositionStrokeCap value) =>
            value switch
            {
                Wd.CompositionStrokeCap.Flat => Wc.CompositionStrokeCap.Flat,
                Wd.CompositionStrokeCap.Square => Wc.CompositionStrokeCap.Square,
                Wd.CompositionStrokeCap.Round => Wc.CompositionStrokeCap.Round,
                Wd.CompositionStrokeCap.Triangle => Wc.CompositionStrokeCap.Triangle,
                _ => throw new InvalidOperationException(),
            };

        static Wui.Color Color(Wd.Wui.Color color) =>
            Wui.Color.FromArgb(color.A, color.R, color.G, color.B);

        static Wc.CompositionDropShadowSourcePolicy DropShadowSourcePolicy(Wd.CompositionDropShadowSourcePolicy value) =>
            value switch
            {
                Wd.CompositionDropShadowSourcePolicy.Default => Wc.CompositionDropShadowSourcePolicy.Default,
                Wd.CompositionDropShadowSourcePolicy.InheritFromVisualContent => Wc.CompositionDropShadowSourcePolicy.InheritFromVisualContent,
                _ => throw new InvalidOperationException(),
            };

        static CanvasFilledRegionDetermination FilledRegionDetermination(
            Wd.Mgcg.CanvasFilledRegionDetermination value) =>
            value switch
            {
                Wd.Mgcg.CanvasFilledRegionDetermination.Alternate => CanvasFilledRegionDetermination.Alternate,
                Wd.Mgcg.CanvasFilledRegionDetermination.Winding => CanvasFilledRegionDetermination.Winding,
                _ => throw new InvalidOperationException(),
            };

        static Mgc.CanvasComposite CanvasComposite(Wd.Mgc.CanvasComposite value) =>
            value switch
            {
                Wd.Mgc.CanvasComposite.SourceOver => Mgc.CanvasComposite.SourceOver,
                Wd.Mgc.CanvasComposite.DestinationOver => Mgc.CanvasComposite.DestinationOver,
                Wd.Mgc.CanvasComposite.SourceIn => Mgc.CanvasComposite.SourceIn,
                Wd.Mgc.CanvasComposite.DestinationIn => Mgc.CanvasComposite.DestinationIn,
                Wd.Mgc.CanvasComposite.SourceOut => Mgc.CanvasComposite.SourceOut,
                Wd.Mgc.CanvasComposite.DestinationOut => Mgc.CanvasComposite.DestinationOut,
                Wd.Mgc.CanvasComposite.SourceAtop => Mgc.CanvasComposite.SourceAtop,
                Wd.Mgc.CanvasComposite.DestinationAtop => Mgc.CanvasComposite.DestinationAtop,
                Wd.Mgc.CanvasComposite.Xor => Mgc.CanvasComposite.Xor,
                Wd.Mgc.CanvasComposite.Add => Mgc.CanvasComposite.Add,
                Wd.Mgc.CanvasComposite.Copy => Mgc.CanvasComposite.Copy,
                Wd.Mgc.CanvasComposite.BoundedCopy => Mgc.CanvasComposite.BoundedCopy,
                Wd.Mgc.CanvasComposite.MaskInvert => Mgc.CanvasComposite.MaskInvert,
                _ => throw new InvalidOperationException(),
            };

        static CanvasFigureLoop CanvasFigureLoop(Wd.Mgcg.CanvasFigureLoop value) =>
            value switch
            {
                Wd.Mgcg.CanvasFigureLoop.Open => Microsoft.Graphics.Canvas.Geometry.CanvasFigureLoop.Open,
                Wd.Mgcg.CanvasFigureLoop.Closed => Microsoft.Graphics.Canvas.Geometry.CanvasFigureLoop.Closed,
                _ => throw new InvalidOperationException(),
            };

        static CanvasGeometryCombine Combine(Wd.Mgcg.CanvasGeometryCombine value) =>
            value switch
            {
                Wd.Mgcg.CanvasGeometryCombine.Union => CanvasGeometryCombine.Union,
                Wd.Mgcg.CanvasGeometryCombine.Exclude => CanvasGeometryCombine.Exclude,
                Wd.Mgcg.CanvasGeometryCombine.Intersect => CanvasGeometryCombine.Intersect,
                Wd.Mgcg.CanvasGeometryCombine.Xor => CanvasGeometryCombine.Xor,
                _ => throw new InvalidOperationException(),
            };

        static Wc.CompositionGradientExtendMode ExtendMode(Wd.CompositionGradientExtendMode value) =>
            value switch
            {
                Wd.CompositionGradientExtendMode.Clamp => Wc.CompositionGradientExtendMode.Clamp,
                Wd.CompositionGradientExtendMode.Wrap => Wc.CompositionGradientExtendMode.Wrap,
                Wd.CompositionGradientExtendMode.Mirror => Wc.CompositionGradientExtendMode.Mirror,
                _ => throw new InvalidOperationException(),
            };

        static Wc.CompositionColorSpace ColorSpace(Wd.CompositionColorSpace value) =>
            value switch
            {
                Wd.CompositionColorSpace.Auto => Wc.CompositionColorSpace.Auto,
                Wd.CompositionColorSpace.Hsl => Wc.CompositionColorSpace.Hsl,
                Wd.CompositionColorSpace.Rgb => Wc.CompositionColorSpace.Rgb,
                Wd.CompositionColorSpace.HslLinear => Wc.CompositionColorSpace.HslLinear,
                Wd.CompositionColorSpace.RgbLinear => Wc.CompositionColorSpace.RgbLinear,
                _ => throw new InvalidOperationException(),
            };

        static Wc.CompositionMappingMode MappingMode(Wd.CompositionMappingMode value) =>
            value switch
            {
                Wd.CompositionMappingMode.Absolute => Wc.CompositionMappingMode.Absolute,
                Wd.CompositionMappingMode.Relative => Wc.CompositionMappingMode.Relative,
                _ => throw new InvalidOperationException(),
            };

        sealed class ReferenceEqualsComparer : IEqualityComparer<object>
        {
            bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj) => obj.GetHashCode();
        }
    }
}
