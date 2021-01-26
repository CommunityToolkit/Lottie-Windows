// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    readonly struct TimeSegment
    {
        internal TimeSegment(double offset, double duration)
        {
            Offset = offset;
            Duration = duration;
        }

        public readonly double Offset;

        public readonly double Duration;

        public bool IsOverlapping(in TimeSegment other)
        {
            var myEnd = Offset + Duration;
            var otherOffset = other.Offset;
            var otherEnd = otherOffset + other.Duration;

            return Offset < otherEnd && myEnd > otherOffset;
        }

        public override string ToString() => $"{Offset}->{Offset + Duration}";
    }
}
