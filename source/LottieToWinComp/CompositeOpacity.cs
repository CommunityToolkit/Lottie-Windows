// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Composes animatable opacities.
    /// </summary>
    sealed class CompositeOpacity
    {
        readonly CompositeOpacity _previous;
        readonly Opacity _initialValue;
        readonly KeyFrame<Opacity>[] _keyFrames;

        CompositeOpacity(CompositeOpacity previous, Opacity initialValue, in ReadOnlySpan<KeyFrame<Opacity>> keyFrames)
        {
            Debug.Assert(previous is null || keyFrames.Length > 1, "Precondition");

            _previous = previous;

            _keyFrames = keyFrames.Length > 1 ? keyFrames.ToArray() : Array.Empty<KeyFrame<Opacity>>();
            _initialValue = initialValue;
        }

        internal static CompositeOpacity Opaque { get; } = new CompositeOpacity(null, Opacity.Opaque, Array.Empty<KeyFrame<Opacity>>());

        /// <summary>
        /// Returns a <see cref="CompositeOpacity"/> that multiplies the given opacity by
        /// this <see cref="CompositeOpacity"/>.
        /// </summary>
        /// <returns>A <see cref="CompositeOpacity"/> that multiplies the given opacity by
        /// this <see cref="CompositeOpacity"/>.</returns>
        internal CompositeOpacity ComposedWith(in TrimmedAnimatable<Opacity> opacity)
        {
            if (opacity.AlwaysEquals(Opacity.Opaque))
            {
                // Nothing to do.
                return this;
            }

            var myOpacity = new TrimmedAnimatable<Opacity>(opacity.Context, _initialValue, _keyFrames);

            // Try to compose the current opacity with the new one.
            if (TryComposeOpacities(in myOpacity, in opacity, out var composedOpacity))
            {
                return new CompositeOpacity(_previous, composedOpacity.InitialValue, composedOpacity.KeyFrames);
            }
            else
            {
                // Couldn't compose into the TrimmedAnimatable.
                return new CompositeOpacity(this, opacity.InitialValue, opacity.KeyFrames);
            }
        }

        // Checking for whether the current opacity is animated is sufficient because if it
        // wasn't animated it would have been multiplied into the previous animations.
        internal bool IsAnimated => _keyFrames.Length > 1;

        internal IEnumerable<Animatable<Opacity>> GetAnimatables()
        {
            var cur = this;
            do
            {
                yield return new Animatable<Opacity>(initialValue: cur._initialValue, keyFrames: cur._keyFrames, propertyIndex: null);
                cur = cur._previous;
            } while (cur != null);
        }

        // Gets the value of the opacity. Only valid to call if the opacity is not animated.
        internal Opacity NonAnimatedValue
        {
            get
            {
                if (IsAnimated)
                {
                    throw new InvalidOperationException();
                }

                return _initialValue;
            }
        }

        public override string ToString() => IsAnimated ? "Animated opacity" : "Non-animated opacity";

        static bool TryComposeOpacities(in TrimmedAnimatable<Opacity> a, in TrimmedAnimatable<Opacity> b, out TrimmedAnimatable<Opacity> result)
        {
            var isAAnimated = a.IsAnimated;
            var isBAnimated = b.IsAnimated;

            if (isAAnimated)
            {
                if (isBAnimated)
                {
                    // Both are animated.
                    if (a.KeyFrames[0].Frame >= b.KeyFrames[b.KeyFrames.Length - 1].Frame ||
                        b.KeyFrames[0].Frame >= a.KeyFrames[a.KeyFrames.Length - 1].Frame)
                    {
                        // The animations are non-overlapping.
                        if (a.KeyFrames[0].Frame >= b.KeyFrames[b.KeyFrames.Length - 1].Frame)
                        {
                            result = ComposeNonOverlappingAnimatedOpacities(in b, in a);
                            return true;
                        }
                        else
                        {
                            result = ComposeNonOverlappingAnimatedOpacities(in a, in b);
                            return true;
                        }
                    }
                    else
                    {
                        // The animations overlap.
                        // Return false to indicate that they can't be composed into a single result.
                        result = default(TrimmedAnimatable<Opacity>);
                        return false;
                    }
                }
                else
                {
                    result = ComposeAnimatedAndNonAnimated(in a, b.InitialValue);
                    return true;
                }
            }
            else
            {
                result = ComposeAnimatedAndNonAnimated(in b, a.InitialValue);
                return true;
            }
        }

        static TrimmedAnimatable<Opacity> ComposeAnimatedAndNonAnimated(in TrimmedAnimatable<Opacity> animatable, Opacity opacity)
        {
            return opacity.IsOpaque
                ? animatable
                : new TrimmedAnimatable<Opacity>(
                    context: animatable.Context,
                    initialValue: animatable.InitialValue * opacity,
                    keyFrames: animatable.KeyFrames.SelectToSpan(kf => new KeyFrame<Opacity>(
                                kf.Frame,
                                kf.Value * opacity,
                                kf.SpatialControlPoint1,
                                kf.SpatialControlPoint2,
                                kf.Easing)));
        }

        // Composes 2 animated opacity values where the frames in first come before second.
        static TrimmedAnimatable<Opacity> ComposeNonOverlappingAnimatedOpacities(in TrimmedAnimatable<Opacity> first, in TrimmedAnimatable<Opacity> second)
        {
            Debug.Assert(first.IsAnimated, "Precondition");
            Debug.Assert(second.IsAnimated, "Precondition");
            Debug.Assert(first.KeyFrames[first.KeyFrames.Length - 1].Frame <= second.KeyFrames[0].Frame, "Precondition");

            var resultFrames = new KeyFrame<Opacity>[first.KeyFrames.Length + second.KeyFrames.Length];
            var resultCount = 0;
            var secondInitialScale = second.InitialValue;
            var firstFinalScale = first.KeyFrames[first.KeyFrames.Length - 1].Value;

            foreach (var kf in first.KeyFrames)
            {
                resultFrames[resultCount] = ScaleKeyFrame(kf, secondInitialScale);
                resultCount++;
            }

            foreach (var kf in second.KeyFrames)
            {
                resultFrames[resultCount] = ScaleKeyFrame(kf, firstFinalScale);
                resultCount++;
            }

            return new TrimmedAnimatable<Opacity>(
                first.Context,
                first.InitialValue,
                new ReadOnlySpan<KeyFrame<Opacity>>(resultFrames, 0, resultCount));
        }

        static KeyFrame<Opacity> ScaleKeyFrame(KeyFrame<Opacity> keyFrame, Opacity scale)
        {
            return new KeyFrame<Opacity>(
                            keyFrame.Frame,
                            keyFrame.Value * scale,
                            keyFrame.SpatialControlPoint1,
                            keyFrame.SpatialControlPoint2,
                            keyFrame.Easing);
        }
    }
}