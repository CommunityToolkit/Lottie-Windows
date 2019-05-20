// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// The context in which to translate a composition. This is used to ensure that
    /// layers in a PreComp are translated in the context of the PreComp, and to carry
    /// around other context-specific state.
    /// </summary>
    sealed class TranslationContext
    {
        internal Layer Layer { get; }

        internal TranslationContext ContainingContext { get; }

        // A set of layers that can be referenced by id.
        internal LayerCollection Layers { get; }

        internal double Width { get; }

        internal double Height { get; }

        // The start time of the current layer, in composition time.
        internal double StartTime { get; }

        internal double EndTime => StartTime + DurationInFrames;

        internal double DurationInFrames { get; }

        // Constructs the root context.
        internal TranslationContext(LottieComposition lottieComposition)
        {
            Layers = lottieComposition.Layers;
            StartTime = lottieComposition.InPoint;
            DurationInFrames = lottieComposition.OutPoint - lottieComposition.InPoint;
            Width = lottieComposition.Width;
            Height = lottieComposition.Height;
        }

        // Constructs a context for the given layer.
        internal TranslationContext(TranslationContext context, PreCompLayer layer, LayerCollection layers)
        {
            Layer = layer;

            // Precomps define a new temporal and spatial space.
            Width = layer.Width;
            Height = layer.Height;
            StartTime = context.StartTime - layer.StartTime;

            ContainingContext = context;
            Layers = layers;
            DurationInFrames = context.DurationInFrames;
        }

        internal TrimmedAnimatable<Vector3> TrimAnimatable(IAnimatableVector3 animatable)
        {
            return TrimAnimatable<Vector3>((AnimatableVector3)animatable);
        }

        internal TrimmedAnimatable<T> TrimAnimatable<T>(Animatable<T> animatable)
            where T : IEquatable<T>
        {
            if (animatable.IsAnimated)
            {
                var trimmedKeyFrames = Optimizer.RemoveRedundantKeyFrames(Optimizer.TrimKeyFrames(animatable, StartTime, EndTime));
                return new TrimmedAnimatable<T>(
                    this,
                    trimmedKeyFrames.Length == 0
                        ? animatable.InitialValue
                        : trimmedKeyFrames[0].Value,
                    trimmedKeyFrames);
            }
            else
            {
                return new TrimmedAnimatable<T>(this, animatable.InitialValue, animatable.KeyFrames);
            }
        }
    }
}
