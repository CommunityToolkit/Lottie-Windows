// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Takes an <see cref="AnimatableVector3"/> and if the easings on the X, Y, or Z channels
    /// are different, returns an equivalent <see cref="AnimatableXYZ"/>.
    /// </summary>
    static class AnimatableVector3ToAnimatableXYZ
    {
        // Asserts that the given AnimatableVector3 has the same easing for each channel.
        internal static void AssertSingleEasing(IAnimatableVector3 value)
        {
            if (value is AnimatableVector3 animatableVector3 && HasMultipleEasings(animatableVector3))
            {
                Debug.Fail("Multiple easings!");
            }
        }

        internal static IAnimatableVector3 SeparateEasings(IAnimatableVector3 animatableVector3) =>
            animatableVector3 is AnimatableVector3 value ? SeparateEasings(value) : animatableVector3;

        internal static IAnimatableVector3 SeparateEasings(AnimatableVector3 animatableVector3)
        {
            // CubicBezierEasings can have multiple easings - one for each channel (X, Y, Z).
            // See if there are any CubicBezierEasings that have different easing for
            // each channel. It there aren't any, there's nothing to do.
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
                new Animatable<double>(xKeyFrames, animatableVector3.PropertyIndex),
                new Animatable<double>(yKeyFrames, animatableVector3.PropertyIndex),
                new Animatable<double>(zKeyFrames, animatableVector3.PropertyIndex));
        }

        // Extracts the key frames from a single channel of a Vector3.
        static KeyFrame<double>[] ExtractKeyFrames(
            ReadOnlySpan<KeyFrame<Vector3>> keyFrames,
            Func<Vector3, double> valueExtractor,
            int valueIndex)
        {
            var result = new KeyFrame<double>[keyFrames.Length];

            for (var i = 0; i < keyFrames.Length; i++)
            {
                var kf = keyFrames[i];
                var value = valueExtractor(kf.Value);
                var easing = kf.Easing;
                if (easing is CubicBezierEasing cubicBezierEasing)
                {
                    var bezier = cubicBezierEasing.Beziers.Count > valueIndex ? cubicBezierEasing.Beziers[valueIndex] : cubicBezierEasing.Beziers[0];
                    easing = new CubicBezierEasing(new[] { bezier });
                }

                result[i] = new KeyFrame<double>(kf.Frame, value, easing);
            }

            return result;
        }

        // Returns true iff the given AnimatableVector3 does not use the same easing for each
        // of its channels (X, Y, Z).
        static bool HasMultipleEasings(AnimatableVector3 value)
        {
            if (value.IsAnimated)
            {
                foreach (var kf in value.KeyFrames)
                {
                    if (kf.Easing is CubicBezierEasing cubicBezierEasing)
                    {
                        if (cubicBezierEasing.Beziers.Count > 1)
                        {
                            var xEasing = cubicBezierEasing.Beziers[0];

                            if (xEasing != cubicBezierEasing.Beziers[1])
                            {
                                return true;
                            }

                            if (cubicBezierEasing.Beziers.Count > 2 && xEasing != cubicBezierEasing.Beziers[2])
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
