using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// Represents an arbitrary time range.
    /// </summary>
    struct Range
    {
        public double Start { get; }

        public double End { get; }

        public bool Intersect(Range other)
        {
            return Math.Max(Start, other.Start) < Math.Min(End, other.End);
        }

        public Range(double inPoint, double outPoint)
        {
            Start = inPoint;
            End = outPoint;
        }

        public static Range GetForLayer(Layer layer)
        {
            return new Range(layer.InPoint, layer.OutPoint);
        }

        public Range ShiftLeft(double value)
        {
            return new Range(Start - value, End - value);
        }
    }
}
