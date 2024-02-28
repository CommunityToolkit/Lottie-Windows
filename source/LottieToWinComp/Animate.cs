// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.WinUI.Lottie.Animatables;
using CommunityToolkit.WinUI.Lottie.LottieData;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using Expr = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions.Expression;

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Static methods for animating Windows Composition objects.
    /// </summary>
    static class Animate
    {
        /// <summary>
        /// Animates a property on <paramref name="compObject"/> using an expression animation.
        /// </summary>
        public static void WithExpression(
            CompositionObject compObject,
            ExpressionAnimation animation,
            string target) =>
            compObject.StartAnimation(target, animation);

        /// <summary>
        /// Animates a property on <paramref name="compObject"/> using a key frame animation.
        /// </summary>
        public static void WithKeyFrame(
            TranslationContext context,
            CompositionObject compObject,
            string target,
            KeyFrameAnimation_ animation,
            double scale = 1,
            double offset = 0)
        {
            Debug.Assert(offset >= 0, "Precondition");
            Debug.Assert(scale <= 1, "Precondition");
            Debug.Assert(animation.KeyFrameCount > 0, "Precondition");

            var key = new ScaleAndOffset(scale, offset);
            var state = context.GetStateCache<StateCache>();

            if (context.ObjectFactory.IsUapApiAvailable(nameof(AnimationController)))
            {
                if (!state.ProgressControllers.TryGetValue(key, out var controllerCached))
                {
                    controllerCached = context.ObjectFactory.CreateAnimationControllerList();
                    controllerCached.Pause();

                    var rootProgressAnimation = context.ObjectFactory.CreateExpressionAnimation(scale == 1.0 && offset == 0.0 ? ExpressionFactory.RootProgress : ExpressionFactory.ScaledAndOffsetRootProgress(scale, offset));
                    rootProgressAnimation.SetReferenceParameter(ExpressionFactory.RootName, context.RootVisual!);
                    controllerCached.StartAnimation("Progress", rootProgressAnimation);

                    state.ProgressControllers.Add(key, controllerCached);
                }

                compObject.StartAnimation(target, animation, controllerCached);
                return;
            }

            // Start the animation ...
            compObject.StartAnimation(target, animation);

            // ... but pause it immediately so that it doesn't react to time. Instead, bind
            // its progress to the progress of the composition.
            var controller = compObject.TryGetAnimationController(target);
            controller!.Pause();

            // Bind it to the root visual's Progress property, scaling and offsetting if necessary.
            if (!state.ProgressBindingAnimations.TryGetValue(key, out var bindingAnimation))
            {
                bindingAnimation = context.ObjectFactory.CreateExpressionAnimation(ExpressionFactory.ScaledAndOffsetRootProgress(scale, offset));
                bindingAnimation.SetReferenceParameter(ExpressionFactory.RootName, context.RootVisual!);
                if (context.AddDescriptions)
                {
                    // Give the animation a nice readable name in codegen.
                    var name = key.Offset != 0 || key.Scale != 1
                        ? "RootProgressRemapped"
                        : "RootProgress";

                    bindingAnimation.SetName(name);
                }

                state.ProgressBindingAnimations.Add(key, bindingAnimation);
            }

            // Bind the controller's Progress with a single Progress property on the scene root.
            // The Progress property provides the time reference for the animation.
            controller.StartAnimation("Progress", bindingAnimation);
        }

        /// <summary>
        /// Adds and animates a <see cref="CompositionPropertySet"/> value on the target object.
        /// </summary>
        public static void ScalarPropertySetValue(
            LayerContext context,
            in TrimmedAnimatable<double> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            if (targetObject.Properties == targetObject)
            {
                throw new ArgumentException("targetObject must not be a CompositionPropertySet");
            }

            targetObject.Properties.InsertScalar(targetPropertyName, ConvertTo.Float(value.InitialValue));

            if (value.IsAnimated)
            {
                ScaledScalar(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);
            }
        }

        /// <summary>
        /// Adds and animates a <see cref="CompositionPropertySet"/> value on the target object.
        /// </summary>
        public static void TrimStartOrTrimEndPropertySetValue(
            LayerContext context,
            in TrimmedAnimatable<Trim> value,
            CompositionGeometry targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            targetObject.Properties.InsertScalar(targetPropertyName, ConvertTo.Float(value.InitialValue));

            if (value.IsAnimated)
            {
                TrimStartOrTrimEnd(context, value, targetObject, targetPropertyName, longDescription, shortDescription);
            }
        }

        /// <summary>
        /// Animates a rotation value.
        /// </summary>
        public static void Rotation(
            LayerContext context,
            in TrimmedAnimatable<Rotation> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
            => ScaledRotation(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        /// <summary>
        /// Animates a scalar value.
        /// </summary>
        public static void Scalar(
            LayerContext context,
            in TrimmedAnimatable<double> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
            => ScaledScalar(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        /// <summary>
        /// Animates a percent value.
        /// </summary>
        public static void Percent(
            LayerContext context,
            in TrimmedAnimatable<double> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
            => ScaledScalar(context, value, 0.01, targetObject, targetPropertyName, longDescription, shortDescription);

        /// <summary>
        /// Animates an opacity value.
        /// </summary>
        public static void Opacity(
            LayerContext context,
            in TrimmedAnimatable<Opacity> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
            => ScaledOpacity(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        /// <summary>
        /// Animates a trim start or trim end value.
        /// </summary>
        public static void TrimStartOrTrimEnd(
            LayerContext context,
            in TrimmedAnimatable<Trim> value,
            CompositionGeometry targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
            => ScaledTrimStartOrTrimEnd(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        /// <summary>
        /// Animates a rotation value.
        /// </summary>
        public static void ScaledRotation(
            LayerContext context,
            in TrimmedAnimatable<Rotation> value,
            double scale,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription,
            string? shortDescription)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateScalarKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, (float)(val.Degrees * scale), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates an opacity value.
        /// </summary>
        public static void ScaledOpacity(
            LayerContext context,
            in TrimmedAnimatable<Opacity> value,
            double scale,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription,
            string? shortDescription)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateScalarKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, (float)(val.Value * scale), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates a scalar value.
        /// </summary>
        public static void ScaledScalar(
            LayerContext context,
            in TrimmedAnimatable<double> value,
            double scale,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateScalarKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, (float)(val * scale), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates a boolean value.
        /// </summary>
        public static void Boolean(
            LayerContext context,
            in TrimmedAnimatable<bool> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateBooleanKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, val),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates a trim start or trim end value.
        /// </summary>
        static void ScaledTrimStartOrTrimEnd(
            LayerContext context,
            in TrimmedAnimatable<Trim> value,
            double scale,
            CompositionGeometry targetObject,
            string targetPropertyName,
            string? longDescription,
            string? shortDescription)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateScalarKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, (float)(val.Value * scale), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates a color using an expression animation.
        /// </summary>
        public static void ColorWithExpression(
            CompositionObject compObject,
            ExpressionAnimation animation,
            string target = "Color") =>
                WithExpression(compObject, animation, target);

        /// <summary>
        /// Animates a color value.
        /// </summary>
        public static void Color(
            LayerContext context,
            in TrimmedAnimatable<Color> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateColorKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, ConvertTo.Color(val), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        public static void ColorWithExpressionKeyFrameAnimation(
            LayerContext context,
            in TrimmedAnimatable<WinCompData.Expressions.Color> value,
            CompositionObject targetObject,
            string targetPropertyName,
            Action<ColorKeyFrameAnimation> beforeStartCallback,
            string? longDescription = null,
            string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateColorKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertExpressionKeyFrame(progress, val, easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription,
                beforeStartCallback);
        }

        /// <summary>
        /// Animates a color expressed as a Vector4 value.
        /// </summary>
        public static void ColorAsVector4(
            LayerContext context,
            in TrimmedAnimatable<Color> value,
            CompositionPropertySet targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateVector4KeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, ConvertTo.Vector4(ConvertTo.Color(val)), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates a path value.
        /// </summary>
        public static void Path(
                LayerContext context,
                in TrimmedAnimatable<PathGeometry> value,
                ShapeFill.PathFillType fillType,
                CompositionPathGeometry targetObject,
                string targetPropertyName,
                string? longDescription = null,
                string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");

            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreatePathKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(
                    progress,
                    Paths.CompositionPathFromPathGeometry(
                        context,
                        val,
                        fillType,

                        // Turn off the optimization that replaces cubic Beziers with
                        // segments because it may result in different numbers of
                        // control points in each path in the keyframes.
                        optimizeLines: false),
                    easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates a path group value.
        /// </summary>
        public static void PathGroup(
            LayerContext context,
            in TrimmedAnimatable<PathGeometryGroup> value,
            ShapeFill.PathFillType fillType,
            CompositionPathGeometry targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");

            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreatePathKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(
                    progress,
                    Paths.CompositionPathFromPathGeometryGroup(
                        context,
                        val.Data,
                        fillType,

                        // Turn off the optimization that replaces cubic Beziers with
                        // segments because it may result in different numbers of
                        // control points in each path in the keyframes.
                        optimizeLines: false),
                    easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates a Vector2 value.
        /// </summary>
        public static void Vector2(
            LayerContext context,
            in TrimmedAnimatable<Vector2> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
            => ScaledVector2(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        /// <summary>
        /// Animates a Vector2 value.
        /// </summary>
        public static void ScaledVector2(
            LayerContext context,
            in TrimmedAnimatable<Vector2> value,
            double scale,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateVector2KeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, ConvertTo.Vector2(val * scale), easing),
                (ca, progress, expr, easing) => ca.InsertExpressionKeyFrame(progress, scale * expr, easing),
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates a Vector2 value.
        /// </summary>
        public static void Vector2(
            LayerContext context,
            in TrimmedAnimatable<Vector3> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
            => ScaledVector2(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        /// <summary>
        /// Animates a Vector2 value.
        /// </summary>
        public static void ScaledVector2(
            LayerContext context,
            in TrimmedAnimatable<Vector3> value,
            double scale,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateVector2KeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, ConvertTo.Vector2(val * scale), easing),
                (ca, progress, expr, easing) => ca.InsertExpressionKeyFrame(progress, scale * expr, easing),
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        /// <summary>
        /// Animates a Vector3 value.
        /// </summary>
        public static void Vector3(
            LayerContext context,
            in TrimmedAnimatable<Vector3> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
            => ScaledVector3(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        /// <summary>
        /// Animates a Vector3 value.
        /// </summary>
        public static void ScaledVector3(
            LayerContext context,
            in TrimmedAnimatable<Vector3> value,
            double scale,
            CompositionObject targetObject,
            string targetPropertyName,
            string? longDescription = null,
            string? shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                context.ObjectFactory.CreateVector3KeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, ConvertTo.Vector3(val) * (float)scale, easing),
                (ca, progress, expr, easing) => ca.InsertExpressionKeyFrame(progress, scale * expr.AsVector3(), easing),
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        static void GenericCreateCompositionKeyFrameAnimation<TCA, T>(
                LayerContext context,
                in TrimmedAnimatable<T> value,
                Func<TCA> compositionAnimationFactory,
                Action<TCA, float, T, CompositionEasingFunction> keyFrameInserter,
                Action<TCA, float, CubicBezierFunction2, CompositionEasingFunction>? expressionKeyFrameInserter,
                CompositionObject targetObject,
                string targetPropertyName,
                string? longDescription,
                string? shortDescription,
                Action<TCA>? beforeStartCallback = null)
         where TCA : KeyFrameAnimation_
         where T : IEquatable<T>
        {
            Debug.Assert(value.IsAnimated, "Precondition");

            var translationContext = context.Translation;

            var compositionAnimation = compositionAnimationFactory();

            compositionAnimation.SetDescription(context, () => (longDescription ?? targetPropertyName, shortDescription ?? targetPropertyName));

            compositionAnimation.Duration = translationContext.LottieComposition.Duration;

            var trimmedKeyFrames = value.KeyFrames;

            var firstKeyFrame = trimmedKeyFrames[0];
            var lastKeyFrame = trimmedKeyFrames[trimmedKeyFrames.Count - 1];

            var animationStartTime = firstKeyFrame.Frame;
            var animationEndTime = lastKeyFrame.Frame;

            var highestProgressValueSoFar = Float32.PreviousSmallerThan(0);

            if (firstKeyFrame.Frame > context.CompositionContext.StartTime)
            {
                // The first key frame is after the start of the animation. Create an extra keyframe at 0 to
                // set and hold an initial value until the first specified keyframe.
                // Note that we could set an initial value for the property instead of using a key frame,
                // but seeing as we're creating key frames anyway, it will be fewer operations to
                // just use a first key frame and not set an initial value
                InsertKeyFrame(compositionAnimation, 0 /* progress */, firstKeyFrame.Value, context.ObjectFactory.CreateStepThenHoldEasingFunction() /*easing*/);

                animationStartTime = context.CompositionContext.StartTime;
            }

            if (lastKeyFrame.Frame < context.CompositionContext.EndTime)
            {
                // The last key frame is before the end of the animation.
                animationEndTime = context.CompositionContext.EndTime;
            }

            var animationDuration = animationEndTime - animationStartTime;

            // The Math.Min is to deal with rounding errors that cause the scale to be slightly more than 1.
            var scale = Math.Min(context.CompositionContext.DurationInFrames / animationDuration, 1.0);
            var offset = (context.CompositionContext.StartTime - animationStartTime) / animationDuration;

            // Insert the keyframes with the progress adjusted so the first keyframe is at 0 and the remaining
            // progress values are scaled appropriately.
            var previousValue = firstKeyFrame.Value;
            var previousProgress = Float32.PreviousSmallerThan(0);
            var rootReferenceRequired = false;
            var previousKeyFrameWasExpression = false;

            foreach (var keyFrame in trimmedKeyFrames)
            {
                // Convert the frame number to a progress value for the current key frame.
                var currentProgress = (float)((keyFrame.Frame - animationStartTime) / animationDuration);

                if (keyFrame.SpatialBezier?.IsLinear == false)
                {
                    // TODO - should only be on Vector3. In which case, should they be on Animatable, or on something else?
                    if (typeof(T) != typeof(Vector3))
                    {
                        Debug.WriteLine("Spatial control point on non-Vector3 type");
                    }

                    var spatialBezier = keyFrame.SpatialBezier.Value;

                    var cp0 = ConvertTo.Vector2((Vector3)(object)previousValue);
                    var cp1 = ConvertTo.Vector2(spatialBezier.ControlPoint1);
                    var cp2 = ConvertTo.Vector2(spatialBezier.ControlPoint2);
                    var cp3 = ConvertTo.Vector2((Vector3)(object)keyFrame.Value);
                    CubicBezierFunction2 cb;

                    switch (keyFrame.Easing.Type)
                    {
                        case Easing.EasingType.Linear:
                        case Easing.EasingType.CubicBezier:
                            cb = CubicBezierFunction2.Create(
                                cp0,
                                cp0 + cp1,
                                cp2 + cp3,
                                cp3,
                                Expr.Scalar("dummy"));
                            break;
                        case Easing.EasingType.Hold:
                            // Holds should never have interesting cubic Beziers, so replace with one that is definitely colinear.
                            cb = CubicBezierFunction2.ZeroBezier;
                            break;
                        default:
                            throw new InvalidOperationException();
                    }

                    if (cb.IsEquivalentToLinear || currentProgress == 0)
                    {
                        // The cubic Bezier function is equivalent to a line, or its value starts at the start of the animation, so no need
                        // for an expression to do spatial Beziers on it. Just use a regular key frame.
                        if (previousKeyFrameWasExpression)
                        {
                            // Ensure the previous expression doesn't continue being evaluated during the current keyframe.
                            // This is necessary because the expression is only defined from the previous progress to the current progress.
                            var nextLargerThanPrevious = Float32.NextLargerThan(previousProgress);
                            InsertKeyFrame(compositionAnimation, nextLargerThanPrevious, previousValue, context.ObjectFactory.CreateStepThenHoldEasingFunction());

                            if (currentProgress <= nextLargerThanPrevious)
                            {
                                // Prevent the next key frame from being inserted at the same progress value
                                // as the one we just inserted.
                                currentProgress = Float32.NextLargerThan(nextLargerThanPrevious);
                            }
                        }

                        // The easing for a keyframe at 0 is unimportant, so always use Hold.
                        var easing = currentProgress == 0 ? HoldEasing.Instance : keyFrame.Easing;

                        InsertKeyFrame(compositionAnimation, currentProgress, keyFrame.Value, context.ObjectFactory.CreateCompositionEasingFunction(easing));
                        previousKeyFrameWasExpression = false;
                    }
                    else
                    {
                        // Expression key frame needed for a spatial Bezier.

                        // Make the progress value just before the requested progress value
                        // so that there is room to add a key frame just after this to hold
                        // the final value. This is necessary so that the expression we're about
                        // to add won't get evaluated during the following segment.
                        if (currentProgress > 0)
                        {
                            currentProgress = Float32.PreviousSmallerThan(currentProgress);
                        }

                        if (previousProgress > 0)
                        {
                            previousProgress = Float32.NextLargerThan(previousProgress);
                        }

                        // Re-create the cubic Bezier using the real variable name (it was created previously just to
                        // see if it was linear).
                        cb = CubicBezierFunction2.Create(
                            cp0,
                            cp0 + cp1,
                            cp2 + cp3,
                            cp3,
                            ExpressionFactory.RootScalar(translationContext.ProgressMapFactory.GetVariableForProgressMapping(previousProgress, currentProgress, keyFrame.Easing, scale, offset)));

                        // Insert the cubic Bezier expression. The easing has to be a StepThenHold because otherwise
                        // the value will be interpolated between the result of the expression, and the previous
                        // key frame value. The StepThenHold will make it just evaluate the expression.
                        InsertExpressionKeyFrame(
                            compositionAnimation,
                            currentProgress,
                            cb,                                 // Expression.
                            context.ObjectFactory.CreateStepThenHoldEasingFunction());    // Jump to the final value so the expression is evaluated all the way through.

                        // Note that a reference to the root Visual is required by the animation because it
                        // is used in the expression.
                        rootReferenceRequired = true;
                        previousKeyFrameWasExpression = true;
                    }
                }
                else
                {
                    if (previousKeyFrameWasExpression)
                    {
                        // Ensure the previous expression doesn't continue being evaluated during the current keyframe.
                        var nextLargerThanPrevious = Float32.NextLargerThan(previousProgress);
                        InsertKeyFrame(compositionAnimation, nextLargerThanPrevious, previousValue, context.ObjectFactory.CreateStepThenHoldEasingFunction());

                        if (currentProgress <= nextLargerThanPrevious)
                        {
                            // Prevent the next key frame from being inserted at the same progress value
                            // as the one we just inserted.
                            currentProgress = Float32.NextLargerThan(nextLargerThanPrevious);
                        }
                    }

                    InsertKeyFrame(compositionAnimation, currentProgress, keyFrame.Value, context.ObjectFactory.CreateCompositionEasingFunction(keyFrame.Easing));
                    previousKeyFrameWasExpression = false;
                }

                previousValue = keyFrame.Value;
                previousProgress = currentProgress;
            }

            if (previousKeyFrameWasExpression && previousProgress < 1)
            {
                // Add a keyframe to hold the final value. Otherwise the expression on the last keyframe
                // will get evaluated outside the bounds of its keyframe.
                InsertKeyFrame(compositionAnimation, Float32.NextLargerThan(previousProgress), (T)(object)previousValue, context.ObjectFactory.CreateStepThenHoldEasingFunction());
            }

            // Add a reference to the root Visual if needed (i.e. if an expression keyframe was added).
            if (rootReferenceRequired)
            {
                compositionAnimation.SetReferenceParameter(ExpressionFactory.RootName, translationContext.RootVisual!);
            }

            beforeStartCallback?.Invoke(compositionAnimation);

            // Start the animation scaled and offset.
            Animate.WithKeyFrame(context, targetObject, targetPropertyName, compositionAnimation, scale, offset);

            // If the given progress value is equal to a progress value that was already
            // inserted into the animation, adjust it up to ensure we never try to
            // insert a key frame on top of an existing key frame. This relies on the
            // key frames being inserted in order.
            void AdjustProgress(ref float progress)
            {
                if (progress == highestProgressValueSoFar)
                {
                    progress = Float32.NextLargerThan(highestProgressValueSoFar);
                }

                highestProgressValueSoFar = progress;
            }

            // Local method to ensure we never insert more than 1 key frame with
            // the same progress value. This relies on the key frames being inserted
            // in order, so if we get a key frame with the same progress value as
            // the previous one we'll just adjust the progress value up slightly.
            void InsertKeyFrame(TCA animation, float progress, T value, CompositionEasingFunction easing)
            {
                AdjustProgress(ref progress);

                // If progress is > 1 then we have no more room to add key frames.
                // This can happen as a result of extra key frames being added for
                // various reasons. The dropped key frames shouldn't matter as they
                // would only affect a very small amount of time at the end of the
                // animation.
                if (progress <= 1)
                {
                    // Guard against progress values that are < 0.
                    if (progress >= 0)
                    {
                        keyFrameInserter(animation, progress, value, easing);
                    }
                }
            }

            // Local method to ensure we never insert more than 1 key frame with
            // the same progress value. This relies on the key frames being inserted
            // in order, so if we get a key frame with the same progress value as
            // the previous one we'll just adjust the progress value up slightly.
            void InsertExpressionKeyFrame(TCA animation, float progress, CubicBezierFunction2 expression, CompositionEasingFunction easing)
            {
                AdjustProgress(ref progress);

                // If progress is > 1 then we have no more room to add key frames.
                // This can happen as a result of extra key frames being added for
                // various reasons. The dropped key frames shouldn't matter as they
                // would only affect a very small amount of time at the end of the
                // animation.
                if (progress <= 1)
                {
                    if (expressionKeyFrameInserter is null)
                    {
                        throw new InvalidOperationException();
                    }

                    expressionKeyFrameInserter(animation, progress, expression, easing);
                }
            }
        }

        // A pair of doubles used as a key in a dictionary.
        public sealed class ScaleAndOffset
        {
            internal ScaleAndOffset(double scale, double offset)
            {
                Scale = scale;
                Offset = offset;
            }

            internal double Scale { get; }

            internal double Offset { get; }

            public override bool Equals(object? obj)
                => obj is ScaleAndOffset other &&
                   other.Scale == Scale &&
                   other.Offset == Offset;

            public override int GetHashCode() => Scale.GetHashCode() ^ Offset.GetHashCode();
        }

        sealed class StateCache
        {
            public Dictionary<ScaleAndOffset, ExpressionAnimation> ProgressBindingAnimations { get; }
                = new Dictionary<ScaleAndOffset, ExpressionAnimation>();

            public Dictionary<ScaleAndOffset, AnimationController> ProgressControllers { get; }
                = new Dictionary<ScaleAndOffset, AnimationController>();
        }
    }
}
