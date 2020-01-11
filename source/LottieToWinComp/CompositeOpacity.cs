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
        readonly Animatable<Opacity> _current;

        CompositeOpacity(CompositeOpacity previous, Animatable<Opacity> opacity)
        {
            Debug.Assert(previous is null || opacity.IsAnimated, "Precondition");
            _previous = previous;
            _current = opacity;
        }

        internal static CompositeOpacity Opaque { get; } = new CompositeOpacity(null, new Animatable<Opacity>(Opacity.Opaque, null));

        /// <summary>
        /// Returns a <see cref="CompositeOpacity"/> that multiplies the given opacity by
        /// this <see cref="CompositeOpacity"/>.
        /// </summary>
        /// <returns>A <see cref="CompositeOpacity"/> that multiplies the given opacity by
        /// this <see cref="CompositeOpacity"/>.</returns>
        internal CompositeOpacity ComposedWith(Animatable<Opacity> opacity)
        {
            if (opacity.AlwaysEquals(Opacity.Opaque))
            {
                // Nothing to do.
                return this;
            }

            // Try to compose the current opacity with the new one.
            var composedOpacity = TryComposeOpacities(_current, opacity);

            return composedOpacity is null
                ? new CompositeOpacity(this, opacity)
                : new CompositeOpacity(_previous, composedOpacity);
        }

        // Checking for whether the current opacity is animated is sufficient because if it
        // wasn't animated it would have been multiplied into the previous animations.
        internal bool IsAnimated => _current.IsAnimated;

        internal IEnumerable<Animatable<Opacity>> GetAnimatables()
        {
            var cur = this;
            do
            {
                yield return cur._current;
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

                return _current.InitialValue;
            }
        }

        public override string ToString() => IsAnimated ? "Animated opacity" : "Non-animated opacity";

        static Animatable<Opacity> TryComposeOpacities(Animatable<Opacity> a, Animatable<Opacity> b)
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
                            return ComposeNonOverlappingAnimatedOpacities(b, a);
                        }
                        else
                        {
                            return ComposeNonOverlappingAnimatedOpacities(a, b);
                        }
                    }
                    else
                    {
                        // The animations overlap.
                        // Return null to indicate that they can't be composed into a single result.
                        return null;
                    }
                }
                else
                {
                    return ComposeAnimatedAndNonAnimated(a, b.InitialValue);
                }
            }
            else
            {
                return ComposeAnimatedAndNonAnimated(b, a.InitialValue);
            }
        }

        static Animatable<Opacity> ComposeAnimatedAndNonAnimated(Animatable<Opacity> animatable, Opacity opacity)
        {
            return opacity.IsOpaque
                ? animatable
                : new Animatable<Opacity>(
                    initialValue: animatable.InitialValue * opacity,
                    keyFrames: animatable.KeyFrames.SelectToSpan(kf => new KeyFrame<Opacity>(
                                kf.Frame,
                                kf.Value * opacity,
                                kf.SpatialControlPoint1,
                                kf.SpatialControlPoint2,
                                kf.Easing)),
                    propertyIndex: null);
        }

        // Composes 2 animated opacity values where the frames in first come before second.
        static Animatable<Opacity> ComposeNonOverlappingAnimatedOpacities(Animatable<Opacity> first, Animatable<Opacity> second)
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

            return new Animatable<Opacity>(
                first.InitialValue,
                new ReadOnlySpan<KeyFrame<Opacity>>(resultFrames, 0, resultCount),
                null);
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