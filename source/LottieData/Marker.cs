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
            double progress,
            string name,
            double durationSeconds)
            : base(name)
        {
            Progress = progress;
            DurationSeconds = durationSeconds;
        }

        /// <summary>
        /// Gets the time value of the marker. This value must be multipled by the composition
        /// duration to get the actualy time.
        /// </summary>
        public double Progress { get; }

        public double DurationSeconds { get; }

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Marker;
    }
}
