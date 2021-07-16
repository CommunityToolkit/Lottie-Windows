using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    struct Range
    {
        public double InPoint { get; }

        public double OutPoint { get; }

        public bool Intersect(Range other)
        {
            return Math.Max(InPoint, other.InPoint) < Math.Min(OutPoint, other.OutPoint);
        }

        public Range(double inPoint, double outPoint)
        {
            InPoint = inPoint;
            OutPoint = outPoint;
        }

        public static Range ForLayer(Layer layer)
        {
            return new Range(layer.InPoint, layer.OutPoint);
        }

        public Range ShiftLeft(double value)
        {
            return new Range(InPoint - value, OutPoint - value);
        }
    }
}
