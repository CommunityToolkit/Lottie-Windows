// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// The context in which to translate a composition. This is used to ensure that
    /// layers are translated in the context of the composition or their containing
    /// PreComp, and to carry around other context-specific state.
    /// </summary>
    abstract class TranslationContext
    {
        FrameNumberEqualityComparer _frameNumberEqualityComparer;

        TranslationContext(TranslationContext containingContext, Layer layer)
        {
            ContainingContext = containingContext;
            Layer = layer;
        }

        // Constructs the root context.
        TranslationContext(LottieComposition lottieComposition)
        {
            Layers = lottieComposition.Layers;
            StartTime = lottieComposition.InPoint;
            DurationInFrames = lottieComposition.OutPoint - lottieComposition.InPoint;
            Size = new Sn.Vector2((float)lottieComposition.Width, (float)lottieComposition.Height);
        }

        internal TranslationContext ContainingContext { get; }

        internal Layer Layer { get; }

        // A set of layers that can be referenced by id.
        internal LayerCollection Layers { get; private set; }

        internal Sn.Vector2 Size { get; private set; }

        // The start time of the current layer, in composition time.
        internal double StartTime { get; private set; }

        internal double EndTime => StartTime + DurationInFrames;

        internal double DurationInFrames { get; private set; }

        public override string ToString() => Layer.Name ?? Layer.Type.ToString();

        /// <summary>
        /// The <see cref="Layer"/>'s in point as a progress value.
        /// </summary>
        internal float InPointAsProgress =>
            (float)((Layer.InPoint - StartTime) / DurationInFrames);

        /// <summary>
        /// The <see cref="Layer"/>'s out point as a progress value.
        /// </summary>
        internal float OutPointAsProgress =>
            (float)((Layer.OutPoint - StartTime) / DurationInFrames);

        internal IEqualityComparer<double> FrameNumberComparer =>
            _frameNumberEqualityComparer ??= new FrameNumberEqualityComparer(this);

        // Constructs a context for the given layer that is a child of this context.
        internal For<T> SubContext<T>(T layer)
            where T : Layer
        {
            var result = new For<T>(this, layer)
            {
                Size = Size,
                StartTime = StartTime,
                Layers = Layers,
                DurationInFrames = DurationInFrames,
            };

            return result;
        }

        // Constructs a context for the given PreCompLayer that is a child of this context.
        internal For<PreCompLayer> PreCompSubContext(LayerCollection layers)
        {
            var layer = (PreCompLayer)Layer;
            var result = new For<PreCompLayer>(this, layer)
            {
                // Precomps define a new temporal and spatial space.
                Size = new Sn.Vector2((float)layer.Width, (float)layer.Height),
                StartTime = StartTime - layer.StartTime,
                Layers = layers,
                DurationInFrames = DurationInFrames,
            };

            return result;
        }

        internal Path OptimizePath(Path path)
        {
            // Optimize the path data. This may result in a previously animated path
            // becoming non-animated.
            var optimizedPathData = TrimAnimatable(path.Data);

            return path.CloneWithNewGeometry(
                optimizedPathData.IsAnimated
                    ? new Animatable<PathGeometry>(optimizedPathData.KeyFrames.ToArray(), path.Data.PropertyIndex)
                    : new Animatable<PathGeometry>(optimizedPathData.InitialValue, path.Data.PropertyIndex));
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

        // The context for a Layer.
        internal sealed class For<T> : TranslationContext
            where T : Layer
        {
            internal For(TranslationContext containingContext, T layer)
                : base(containingContext, layer)
            {
                Layer = layer;
            }

            internal new T Layer { get; }
        }

        // The root context.
        internal sealed class Root : TranslationContext
        {
            internal Root(LottieComposition lottieComposition)
                : base(lottieComposition)
            {
            }
        }

        /// <summary>
        /// Compares frame numbers for equality. This takes into account the lossiness of the conversion
        /// that is done from <see cref="double"/> frame numbers to <see cref="float"/> progress values.
        /// </summary>
        sealed class FrameNumberEqualityComparer : IEqualityComparer<double>
        {
            readonly TranslationContext _context;

            internal FrameNumberEqualityComparer(TranslationContext context)
            {
                _context = context;
            }

            public bool Equals(double x, double y) => ProgressOf(x) == ProgressOf(y);

            public int GetHashCode(double obj) => ProgressOf(obj).GetHashCode();

            // Converts a frame number into a progress value.
            float ProgressOf(double value) =>
                (float)((value - _context.StartTime) / _context.DurationInFrames);
        }
    }
}
