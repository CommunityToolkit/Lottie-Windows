// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
#if PUBLIC_IR
    public
#endif
    sealed class Marker : IRObject
    {
        public Marker(
            string name,
            double frame,
            double durationInFrames)
            : base(name)
        {
            Frame = frame;
            DurationInFrames = durationInFrames;
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
        public override IRObjectType ObjectType => IRObjectType.Marker;
    }
}
