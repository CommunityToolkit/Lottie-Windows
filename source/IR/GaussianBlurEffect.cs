// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    /// <summary>
    /// A Gaussian blur effect.
    /// </summary>
#if PUBLIC_IR
    public
#endif
    sealed class GaussianBlurEffect : Effect
    {
        public GaussianBlurEffect(
            string name,
            bool isEnabled,
            Animatable<double> blurriness,
            Animatable<Enum<BlurDimension>> blurDimensions,
            Animatable<bool> repeatEdgePixels,
            bool? forceGpuRendering)
            : base(
                  name,
                  isEnabled)
        {
            Blurriness = blurriness;
            BlurDimensions = blurDimensions;
            RepeatEdgePixels = repeatEdgePixels;
            ForceGpuRendering = forceGpuRendering;
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
        /// Whether to force rendering onto the GPU.
        /// </summary>
        public bool? ForceGpuRendering { get; }

        /// <summary>
        /// Whether or not the blur repeats the pixels at the edge.
        /// </summary>
        public Animatable<bool> RepeatEdgePixels { get; }

        public override EffectType Type => EffectType.GaussianBlur;
    }
}
