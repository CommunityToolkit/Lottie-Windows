// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Takes an <see cref="AnimatableVector3"/> and if the easings on the X, Y, or Z channels
    /// are different, returns an equivalent <see cref="AnimatableXYZ"/>.
    /// </summary>
    static class AnimatableVector3Rewriter
    {
        /// <summary>
        /// If the given <see cref="IAnimatableVector3"/> is an <see cref="AnimatableVector3"/> with multiple
        /// easings, returns an equivalent <see cref="AnimatableXYZ"/> with one easing per channel.
        /// </summary>
        /// <returns>An equivalent <see cref="IAnimatableVector3"/> with only one easing per
        /// channel.</returns>
        internal static IAnimatableVector3 EnsureOneEasingPerChannel(IAnimatableVector3 animatableVector3) =>
            animatableVector3 is AnimatableVector3 value ? EnsureOneEasingPerChannel(value) : animatableVector3;

        // If the given AnimatableVector3 has multiple easings, returns an equivalent
        // AnimatableXYZ with one easing per channel, otherwise returns the given AnimatableVector3.
        static IAnimatableVector3 EnsureOneEasingPerChannel(AnimatableVector3 animatableVector3)
        {
            // CubicBezierEasings can have multiple easings - one for each channel (X, Y, Z).
            // See if there are any CubicBezierEasings that have different easing for
            // each channel. If there aren't any, there's nothing to do.
            if (!HasMultipleEasings(animatableVector3))
            {
                return animatableVector3;
            }

            // Convert the AnimatableVector3 to an AnimatableVectorXYZ so that each channel
            // can have a separate easing.
            var xKeyFrames = ExtractKeyFrames(animatableVector3.KeyFrames, v => v.X, 0);
            var yKeyFrames = ExtractKeyFrames(animatableVector3.KeyFrames, v => v.Y, 1);
            var zKeyFrames = ExtractKeyFrames(animatableVector3.KeyFrames, v => v.Z, 2);

            return new AnimatableXYZ(
                new Animatable<double>(xKeyFrames),
                new Animatable<double>(yKeyFrames),
                new Animatable<double>(zKeyFrames));
        }

        // Extracts the key frames from a single channel of a Vector3.
        static KeyFrame<double>[] ExtractKeyFrames(
            IReadOnlyList<KeyFrame<Vector3>> keyFrames,
            Func<Vector3, double> channelValueSelector,
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
                    // use the easing from the X channel. Not all Vector3s have
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

        // Returns true iff the key frames in the given AnimatableVector3 do not use the
        // same easing for each of their channels (X, Y, Z).
        static bool HasMultipleEasings(AnimatableVector3 value)
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

                            // The X easing and the Y easing are the same.
                            // If there's a Z easing, check whether it's the same too.
                            if (cubicBezierEasing.Beziers.Count > 2 && xEasing != cubicBezierEasing.Beziers[2])
                            {
                                // The Z easing is different from the X and Y easings.
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
