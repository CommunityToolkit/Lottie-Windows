// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    /// <summary>
    /// Takes an <see cref="AnimatableVector2"/> and if the easings on the X or Y channels
    /// are different, returns an equivalent <see cref="AnimatableXY"/>.
    /// </summary>
    static class AnimatableVector2Rewriter
    {
        /// <summary>
        /// If the given <see cref="IAnimatableVector2"/> is an <see cref="AnimatableVector2"/> with multiple
        /// easings, returns an equivalent <see cref="AnimatableXY"/> with one easing per channel.
        /// </summary>
        /// <returns>An equivalent <see cref="IAnimatableVector2"/> with only one easing per
        /// channel.</returns>
        internal static IAnimatableVector2 EnsureOneEasingPerChannel(IAnimatableVector2 animatableVector2) =>
            animatableVector2 is AnimatableVector2 value ? EnsureOneEasingPerChannel(value) : animatableVector2;

        // If the given AnimatableVector2 has multiple easings, returns an equivalent
        // AnimatableXY with one easing per channel, otherwise returns the given AnimatableVector2.
        static IAnimatableVector2 EnsureOneEasingPerChannel(AnimatableVector2 animatableVector2)
        {
            // CubicBezierEasings can have multiple easings - one for each channel (X, Y).
            // See if there are any CubicBezierEasings that have different easing for
            // each channel. If there aren't any, there's nothing to do.
            if (!HasMultipleEasings(animatableVector2))
            {
                return animatableVector2;
            }

            // Convert the AnimatableVector2 to an AnimatableVectorXY so that each channel
            // can have a separate easing.
            var xKeyFrames = ExtractKeyFrames(animatableVector2.KeyFrames, v => v.X, 0);
            var yKeyFrames = ExtractKeyFrames(animatableVector2.KeyFrames, v => v.Y, 1);

            return new AnimatableXY(
                new Animatable<double>(xKeyFrames),
                new Animatable<double>(yKeyFrames));
        }

        // Extracts the key frames from a single channel of a Vector2.
        static KeyFrame<double>[] ExtractKeyFrames(
            IReadOnlyList<KeyFrame<Vector2>> keyFrames,
            Func<Vector2, double> channelValueSelector,
            int channelIndex)
        {
            var result = new KeyFrame<double>[keyFrames.Count];

            for (var i = 0; i < keyFrames.Count; i++)
            {
                var kf = keyFrames[i];
                var value = channelValueSelector(kf.Value);
                var easing = kf.Easing;
                if (easing is CubicBezierEasing cubicBezierEasing)
                {
                    // Get the Bezier for the channel, if there is one, otherwise
                    // use the easing from the X channel. Not all Vector2s have
                    // multiple easings, and some are really representing Vector2s
                    // and might have easings for X and Y but probably not for Z,
                    // so for those cases the X easing will do well enough.
                    var bezier = cubicBezierEasing.Beziers.Count > channelIndex
                                        ? cubicBezierEasing.Beziers[channelIndex]
                                        : cubicBezierEasing.Beziers[0];

                    easing = new CubicBezierEasing(new[] { bezier });
                }

                result[i] = new KeyFrame<double>(kf.Frame, value, easing);
            }

            return result;
        }

        // Returns true iff the key frames in the given AnimatableVector2 do not use the
        // same easing for each of their channels (X, Y).
        static bool HasMultipleEasings(AnimatableVector2 value)
        {
            if (value.IsAnimated)
            {
                foreach (var kf in value.KeyFrames)
                {
                    if (kf.Easing is CubicBezierEasing cubicBezierEasing)
                    {
                        // Nothing to check if there is only one easing.
                        if (cubicBezierEasing.Beziers.Count > 1)
                        {
                            var xEasing = cubicBezierEasing.Beziers[0];

                            if (xEasing != cubicBezierEasing.Beziers[1])
                            {
                                // The X and Y easings are different from each other.
                                return true;
                            }
                        }

                        // The easings for each channel in that key frame are the same. Keep
                        // looking until a key frame is found where the easings are not the same.
                    }
                }
            }

            // The easings for each channel are the same in each key frame.
            return false;
        }
    }
}
