// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata
{
    /// <summary>
    /// A named segment in a Lottie composition.
    /// </summary>
#if PUBLIC_LottieMetadata
    public
#endif
    readonly struct Marker
    {
        public Marker(string name, Frame frame, Duration duration)
        {
            Name = name;
            Frame = frame;
            Duration = duration;
        }

        /// <summary>
        /// The name of the marker.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The frame that the marker refers to.
        /// </summary>
        public Frame Frame { get; }

        /// <summary>
        /// The duration of the marker.
        /// </summary>
        public Duration Duration { get; }

        public override string ToString() => $"{Name}:{Frame}..{Frame + Duration}";
    }
}
