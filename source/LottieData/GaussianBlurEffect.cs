﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// A gaussian blur effect.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class GaussianBlurEffect : Effect
    {
        public GaussianBlurEffect(
            string name,
            bool isEnabled,
            Animatable<double> blurriness,
            Animatable<Enum<BlurDimension>> blurDimensions,
            Animatable<bool> repeatEdgePixels)
            : base(
                  name,
                  isEnabled)
        {
            Blurriness = blurriness;
            BlurDimensions = blurDimensions;
            RepeatEdgePixels = repeatEdgePixels;
        }

        /// <summary>
        /// The intensity of the blur.
        /// </summary>
        public Animatable<double> Blurriness { get; }

        /// <summary>
        /// Whether the blur is horizontal, vertical, or both.
        /// </summary>
        public Animatable<Enum<BlurDimension>> BlurDimensions { get; }

        /// <summary>
        /// Whether or not the blur repeats the pixels at the edge.
        /// </summary>
        public Animatable<bool> RepeatEdgePixels { get; }

        public override EffectType Type => EffectType.GaussianBlur;
    }
}
