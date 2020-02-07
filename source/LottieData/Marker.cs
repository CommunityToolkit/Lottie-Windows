// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Marker : LottieObject
    {
        public Marker(
            string name,
            double frame,
            double durationMilliseconds)
            : base(name)
        {
            Frame = frame;
            DurationInFrames = durationMilliseconds;
        }

        /// <summary>
        /// Gets the frame for the start of the marker.
        /// </summary>
        public double Frame { get; }

        /// <summary>
        /// Gets the duration in frames.
        /// </summary>
        public double DurationInFrames { get; }

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Marker;
    }
}
