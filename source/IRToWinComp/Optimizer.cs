// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR;
using LottieOptimizer = Microsoft.Toolkit.Uwp.UI.Lottie.IR.Optimization.Optimizer;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    static class Optimizer
    {
        public static Animatable<PathGeometry> GetOptimized(TranslationContext context, Animatable<PathGeometry> value)
        {
            var lottieOptimizer = context.GetStateCache<StateCache>().LottieOptimizer;
            return lottieOptimizer.GetOptimized(value);
        }

        public static Path OptimizePath(LayerContext context, Path path)
        {
            // Optimize the path data. This may result in a previously animated path
            // becoming non-animated.
            var optimizedPathData = TrimAnimatable(context, path.Data);

            return path.CloneWithNewGeometry(
                optimizedPathData.IsAnimated
                    ? new Animatable<PathGeometry>(optimizedPathData.KeyFrames, path.Data.PropertyIndex)
                    : new Animatable<PathGeometry>(optimizedPathData.InitialValue, path.Data.PropertyIndex));
        }

        public static TrimmedAnimatable<Vector3> TrimAnimatable(LayerContext context, IAnimatableVector3 animatable)
            => TrimAnimatable<Vector3>(context, (AnimatableVector3)animatable);

        public static TrimmedAnimatable<T> TrimAnimatable<T>(LayerContext context, Animatable<T> animatable)
            where T : IEquatable<T>
        {
            if (animatable.IsAnimated)
            {
                var trimmedKeyFrames = LottieOptimizer.RemoveRedundantKeyFrames(
                                            LottieOptimizer.TrimKeyFrames(
                                                animatable,
                                                context.CompositionContext.StartTime,
                                                context.CompositionContext.EndTime));

                return new TrimmedAnimatable<T>(
                    context,
                    trimmedKeyFrames.Count == 0
                        ? animatable.InitialValue
                        : trimmedKeyFrames[0].Value,
                    trimmedKeyFrames);
            }
            else
            {
                return new TrimmedAnimatable<T>(context, animatable.InitialValue, animatable.KeyFrames);
            }
        }

        sealed class StateCache
        {
            public LottieOptimizer LottieOptimizer { get; } = new LottieOptimizer();
        }
    }
}
