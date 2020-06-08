// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Uncomment these for debugging
//#define DisableKeyFrameTrimming
//#define DisableKeyFrameOptimization
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// Creates and caches optimized versions of Lottie data. The optimized data is functionally
    /// equivalent to unoptimized data, but may be represented more efficiently.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class Optimizer
    {
        static readonly AnimatableComparer<PathGeometry> PathGeometryComparer = new AnimatableComparer<PathGeometry>();
        readonly Dictionary<Animatable<PathGeometry>, Animatable<PathGeometry>> _animatablePathGeometriesCache;

        public Optimizer()
        {
            _animatablePathGeometriesCache = new Dictionary<Animatable<PathGeometry>, Animatable<PathGeometry>>(PathGeometryComparer);
        }

        public Animatable<PathGeometry> GetOptimized(Animatable<PathGeometry> value)
        {
            var optimized = GetOptimized(value, _animatablePathGeometriesCache);

            // If the geometries have different numbers of segments they can't be animated. However
            // in one specific case we can fix that.
            var geometries = value.KeyFrames.SelectToArray(kf => kf.Value);
            var distinctSegmentCounts = geometries.Select(g => g.BezierSegments.Items.Length).Distinct().Count();

            if (distinctSegmentCounts != 2)
            {
                return optimized;
            }

            // The geometries have different numbers of segments. See if this is the fixable case.
            // Requires:
            //  * Every segment is a line.
            //  * Geometries have only 1 or 2 segments.
            //  * If there are 2 segments, the second segment draws back over the first.
            foreach (var g in geometries)
            {
                var segments = g.BezierSegments;

                foreach (var segment in segments)
                {
                    if (!segment.IsALine)
                    {
                        return optimized;
                    }
                }

                switch (segments.Items.Length)
                {
                    default:
                        return optimized;
                    case 1:
                        if (!segments.Items[0].IsALine)
                        {
                            return optimized;
                        }

                        break;
                    case 2:
                        if (!segments.Items[0].IsALine || !segments.Items[1].IsALine)
                        {
                            return optimized;
                        }

                        // Start of line 0
                        var a = segments.Items[0].ControlPoint0;

                        // End of line 0
                        var b = segments.Items[0].ControlPoint3;

                        // End of line 1
                        var c = segments.Items[1].ControlPoint3;

                        if (!BezierSegment.ArePointsColinear(0, a, b, c))
                        {
                            return optimized;
                        }

                        if (!IsBetween(a, c, b))
                        {
                            return optimized;
                        }

                        // We can handle this case - the second segment draws back over the first.
                        break;
                }
            }

            // Create a new Animatable<PathGeometry> which has only one segment in each keyframe.
            var hacked = optimized.KeyFrames.SelectToSpan(pg => HackPathGeometry(pg));
            return new Animatable<PathGeometry>(hacked[0].Value, hacked, optimized.PropertyIndex);
        }

        // Returns a KeyFrame<PathGeometry> that contains only the first Bezier segment of the given
        // KeyFrame<PathGeometry>.
        static KeyFrame<PathGeometry> HackPathGeometry(KeyFrame<PathGeometry> value) =>
            value.CloneWithNewValue(new PathGeometry(new Sequence<BezierSegment>(new[] { value.Value.BezierSegments.Items[0] }), isClosed: false));

        static bool HasNonLinearCubicBezierEasing<T>(KeyFrame<T> keyFrame)
            where T : IEquatable<T>
        {
            var easing = keyFrame.Easing;

            return easing.Type == Easing.EasingType.CubicBezier && !((CubicBezierEasing)easing).Beziers[0].IsLinear;
        }

        // True iff b is between and c.
        static bool IsBetween(Vector2 a, Vector2 b, Vector2 c)
        {
            return
                IsBetween(a.X, b.X, c.X) &&
                IsBetween(a.Y, b.Y, c.Y);
        }

        // True iff b is between a and c.
        static bool IsBetween(double a, double b, double c)
        {
            var deltaAC = Math.Abs(a - c);

            if (Math.Abs(a - b) > deltaAC)
            {
                return false;
            }

            if (Math.Abs(c - b) > deltaAC)
            {
                return false;
            }

            return true;
        }

        static Animatable<T> GetOptimized<T>(Animatable<T> value, Dictionary<Animatable<T>, Animatable<T>> cache)
            where T : IEquatable<T>
        {
            if (!cache.TryGetValue(value, out Animatable<T> result))
            {
                // Nothing in the cache yet.
                if (!value.IsAnimated)
                {
                    // The value isn't animated, so the keyframe optimization doesn't apply.
                    result = value;
                }
                else
                {
                    var keyFrames = RemoveRedundantKeyFrames(value.KeyFrames);

                    if (keyFrames.Length == value.KeyFrames.Length)
                    {
                        // Optimization didn't achieve anything.
                        result = value;
                    }
                    else
                    {
                        var optimized = new Animatable<T>(value.InitialValue, keyFrames, null);
                        result = optimized;
                    }
                }

                cache.Add(value, result);
            }

            return result;
        }

        /// <summary>
        /// Returns an equivalent list of <see cref="KeyFrame{T}"/>s but with any redundant
        /// <see cref="KeyFrame{T}"/>s removed.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="KeyFrame{T}"/>.</typeparam>
        /// <param name="keyFrames">The <see cref="KeyFrame{T}"/>s to filter.</param>
        /// <returns>An equivalent list of <see cref="KeyFrame{T}"/>s but with any redundant
        /// <see cref="KeyFrame{T}"/>s removed.
        /// </returns>
        public static ReadOnlySpan<KeyFrame<T>> RemoveRedundantKeyFrames<T>(in ReadOnlySpan<KeyFrame<T>> keyFrames)
            where T : IEquatable<T>
        {
#if DisableKeyFrameOptimization
            return keyFrames;
#else
            if (keyFrames.Length <= 1)
            {
                // None of the key frames is redundant.
                return keyFrames;
            }

            KeyFrame<T>[] optimizedFrames = null;
            var optimizedCount = 0;

            // There's at least 2 key frames.
            var keyFrame0 = keyFrames[0];

            for (var i = 1; i < keyFrames.Length; i++)
            {
                var keyFrame1 = keyFrames[i];
                var redundantCount = 0;

                // Is there at least one more key frame to look at?
                if (i < keyFrames.Length - 1)
                {
                    // Do the first 2 key frames have the same value?
                    if (keyFrame0.Value.Equals(keyFrame1.Value))
                    {
                        // First 2 key frames have the same value. If the next has the same
                        // value then the one in between is redundant unless there is a
                        // non-linear cubic Bezier easing between them.
                        while (true)
                        {
                            var keyFrame2 = keyFrames[i + 1];

                            if (!keyFrame0.Value.Equals(keyFrame2.Value))
                            {
                                // Not redundant.
                                break;
                            }

                            // Check for a non-linear cubic Bezier easing. A non-linear cubic Bezier
                            // easing between frames will result in the value changing even though
                            // the frames have the same values.
                            if (HasNonLinearCubicBezierEasing(keyFrame1) ||
                                HasNonLinearCubicBezierEasing(keyFrame2))
                            {
                                // Not redundant.
                                break;
                            }

                            // keyFrame1 is redundant. Count it and skip it.
                            redundantCount++;
                            keyFrame1 = keyFrame2;

                            i++;
                            if (i == keyFrames.Length - 1)
                            {
                                // No more to look at.
                                break;
                            }
                        }

                        // No more redundant key frames.
                        if (redundantCount > 0)
                        {
                            if (optimizedFrames is null)
                            {
                                // Lazily Create an array to hold the new set of key frames.
                                optimizedFrames = new KeyFrame<T>[keyFrames.Length - redundantCount];

                                // Fill the destination with the key frames so far.
                                for (optimizedCount = 0; optimizedCount < i - redundantCount; optimizedCount++)
                                {
                                    optimizedFrames[optimizedCount] = keyFrames[optimizedCount];
                                }
                            }
                        }
                    }
                }

                if (optimizedFrames != null)
                {
                    if (redundantCount > 0)
                    {
                        Debug.Assert(keyFrame1.Value.Equals(keyFrame0.Value), "Invariant");

                        // keyFrame1 has the same value as keyFrame0, so there's no need for
                        // anything more complicated than a Hold easing.
                        keyFrame1 = GetKeyFrameWithHoldEasing(keyFrame1);
                    }

                    optimizedFrames[optimizedCount] = keyFrame1;
                    optimizedCount++;
                }

                keyFrame0 = keyFrame1;
            }

            // All triples of frames have been checked for redundancy.
            if (optimizedFrames is null)
            {
                // No redundant key frames found yet.
                // If the final 2 key frames have the same value, the final key frame is redundant,
                // unless it has a non-linear cubic Bezier easing.
                if (keyFrames[keyFrames.Length - 1].Value.Equals(keyFrames[keyFrames.Length - 2].Value) &&
                    !HasNonLinearCubicBezierEasing(keyFrames[keyFrames.Length - 1]))
                {
                    // Final keyframe is redundant.
                    return keyFrames.Slice(0, keyFrames.Length - 1);
                }
                else
                {
                    return keyFrames;
                }
            }
            else
            {
                // Some redundant key frames found.
                // If the final 2 key frames have the same value, the final key frame is redundant,
                // unless it has a non-linear cubic Bezier easing.
                if (optimizedFrames[optimizedCount - 1].Value.Equals(optimizedFrames[optimizedCount - 2].Value) &&
                    !HasNonLinearCubicBezierEasing(optimizedFrames[optimizedCount - 1]))
                {
                    optimizedCount--;
                }

                var result = new ReadOnlySpan<KeyFrame<T>>(optimizedFrames, 0, optimizedCount);
                return result;
            }
#endif // DisableKeyFrameOptimization
        }

        /// <summary>
        /// Returns a keyframe that is identical, except the easing function is Hold easing.
        /// </summary>
        /// <typeparam name="T">The type of the key frame's value.</typeparam>
        static KeyFrame<T> GetKeyFrameWithHoldEasing<T>(KeyFrame<T> keyFrame)
            where T : IEquatable<T>
        {
            return keyFrame.Easing.Type == Easing.EasingType.Hold
                ? keyFrame
                : keyFrame.CloneWithNewEasing(HoldEasing.Instance);
        }

        /// <summary>
        /// Returns only the key frames that are visible between <paramref name="startTime"/>
        /// and <paramref name="endTime"/>, with other key frames removed.
        /// </summary>
        /// <typeparam name="T">The type of key frame.</typeparam>
        /// <param name="animatable">An <see cref="Animatable{T}"/>.</param>
        /// <param name="startTime">The frame time at which rendering starts.</param>
        /// <param name="endTime">The frame time at which rendering ends.</param>
        /// <returns>
        /// The key frames that are visible for rendering between <paramref name="startTime"/>
        /// and <paramref name="endTime"/>, with other key frames removed.
        /// </returns>
        public static ReadOnlySpan<KeyFrame<T>> TrimKeyFrames<T>(Animatable<T> animatable, double startTime, double endTime)
            where T : IEquatable<T>
        {
#if DisableKeyFrameTrimming
            return animatale.KeyFrames;
#else
            if (!animatable.IsAnimated)
            {
                return default(ReadOnlySpan<KeyFrame<T>>);
            }

            var keyFrames = animatable.KeyFrames;

            // Find the key frame preceding the first frame > startTime.
            var inFrame = 0;
            for (var i = inFrame; i < keyFrames.Length; i++)
            {
                if (keyFrames[i].Frame > startTime)
                {
                    break;
                }

                inFrame = i;
            }

            // Find the key frame following the last frame < endTime.
            var outFrame = keyFrames.Length - 1;
            for (var i = outFrame; i >= 0; i--)
            {
                if (keyFrames[i].Frame < endTime)
                {
                    break;
                }

                outFrame = i;
            }

            var trimmedLength = 1 + outFrame - inFrame;

            // Check for any key frames with 0 length.
            for (var i = inFrame; i < inFrame + trimmedLength - 1; i++)
            {
                if (keyFrames[i].Frame == keyFrames[i + 1].Frame)
                {
                    // Rare case - found a 0 length key frame. Create a new list of key frames
                    // with the 0 length key frames removed.
                    return RemoveRedundantKeyFrames<T>(keyFrames.Slice(inFrame, trimmedLength).ToArray()).ToArray();
                }
            }

            return keyFrames.Slice(inFrame, trimmedLength);
#endif // DisableKeyFrameTrimming
        }

        // Returns the given key frames with any 0-length key frames removed.
        static IEnumerable<KeyFrame<T>> RemoveRedundantKeyFrames<T>(KeyFrame<T>[] keyFrames)
            where T : IEquatable<T>
        {
            for (var i = 0; i < keyFrames.Length - 1; i++)
            {
                // Only include the key frame if it has a frame value that is different
                // from the next key frame's frame or if it has a non-linear cubic Bezier easing
                // function.
                if (keyFrames[i].Frame != keyFrames[i + 1].Frame ||
                    HasNonLinearCubicBezierEasing(keyFrames[i + 1]))
                {
                    yield return keyFrames[i];
                }
            }

            yield return keyFrames[keyFrames.Length - 1];
        }

        sealed class AnimatableComparer<T>
            : IEqualityComparer<IEnumerable<KeyFrame<T>>>,
              IEqualityComparer<KeyFrame<T>>,
              IEqualityComparer<Easing>,
              IEqualityComparer<Animatable<T>>
              where T : IEquatable<T>
        {
            public bool Equals(KeyFrame<T> x, KeyFrame<T> y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.Equals(y);
            }

            public bool Equals(IEnumerable<KeyFrame<T>> x, IEnumerable<KeyFrame<T>> y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.SequenceEqual(y);
            }

            public bool Equals(Easing x, Easing y) => Equates(x, y);

            public bool Equals(Animatable<T> x, Animatable<T> y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.InitialValue.Equals(y.InitialValue) && x.KeyFrames.SequenceEqual(y.KeyFrames);
            }

            public int GetHashCode(KeyFrame<T> obj) => obj.GetHashCode();

            public int GetHashCode(IEnumerable<KeyFrame<T>> obj) => obj.Select(kf => kf.GetHashCode()).Aggregate((a, b) => a ^ b);

            public int GetHashCode(Easing obj) => obj.GetHashCode();

            public int GetHashCode(Animatable<T> obj) => obj.GetHashCode();

            // Compares 2 IEquatable<V> for equality.
            static bool Equates<TV>(TV x, TV y)
                where TV : class, IEquatable<TV> => x is null ? y is null : x.Equals(y);
        }
    }
}
