// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// Represents an arbitrary time range.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif

    struct TimeRange
    {
        public double Start { get; }

        public double End { get; }

        public bool Intersect(TimeRange other)
        {
            return Math.Max(Start, other.Start) < Math.Min(End, other.End);
        }

        public TimeRange(double inPoint, double outPoint)
        {
            Start = inPoint;
            End = outPoint;
        }

        public static TimeRange GetForLayer(Layer layer)
        {
            return new TimeRange(layer.InPoint, layer.OutPoint);
        }

        public TimeRange ShiftLeft(double value)
        {
            return new TimeRange(Start - value, End - value);
        }
    }
}
