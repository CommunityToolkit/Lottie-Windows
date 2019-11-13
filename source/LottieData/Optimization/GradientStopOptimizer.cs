// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// Takes a list of <see cref="GradientStop"/>s and produces an equivalent list of <see cref="ColorGradientStop"/>s
    /// with no <see cref="OpacityGradientStop"/>s. The opacity values from the <see cref="OpacityGradientStop"/>
    /// are converted to alpha values in <see cref="ColorGradientStop"/>s, and interpolation is done on each
    /// stop to ensure that the result is the same as if the <see cref="OpacityGradientStop"/>s were being
    /// used.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    static class GradientStopOptimizer
    {
        /// <summary>
        /// Convert a <see cref="KeyFrame{T}"/> of <see cref="GradientStop"/>s to an equivalent <see cref="KeyFrame{T}"/>
        /// of <see cref="ColorGradientStop"/>s.</summary>
        /// <returns>An equivalent <see cref="KeyFrame{T}"/> of <see cref="ColorGradientStop"/>s.</returns>
        public static KeyFrame<Sequence<ColorGradientStop>> Optimize(KeyFrame<Sequence<GradientStop>> keyFrame)
            => new KeyFrame<Sequence<ColorGradientStop>>(
                frame: keyFrame.Frame,
                value: new Sequence<ColorGradientStop>(Optimize(keyFrame.Value)),
                spatialControlPoint1: keyFrame.SpatialControlPoint1,
                spatialControlPoint2: keyFrame.SpatialControlPoint2,
                easing: keyFrame.Easing);

        /// <summary>
        /// Converts a list of <see cref="GradientStop"/>s into an equivalent list of
        /// <see cref="ColorGradientStop"/> of the same length, but with the opacity from any
        /// <see cref="OpacityGradientStop"/>s multiplied into the alpha channel of the
        /// <see cref="ColorGradientStop"/>s.
        /// The color and opacity values are calculated by interpolating from the surrounding stops.
        /// The result is ordered by offset.
        /// </summary>
        /// <returns>A list of <see cref="ColorGradientStop"/> ordered by offset.</returns>
        public static IEnumerable<ColorGradientStop> Optimize(IEnumerable<GradientStop> stops)
        {
            // Order the stops. This is necessary because we will be searching forward in offset to find
            // the next stop of a particular type to use for interpolating colors and opacities.
            // If a color stop is at the same offset as an opacity stop, put the color stop first.
            var orderedStops = stops.OrderBy(s => s.Offset).ThenBy(s => s.Kind == GradientStop.GradientStopKind.Color ? 0 : 1).ToArray();

            OpacityGradientStop previousOpacityStop = null;
            OpacityGradientStop nextOpacityStop = null;
            ColorGradientStop previousColorStop = null;
            ColorGradientStop nextColorStop = null;
            var foundLastColorStop = false;
            var foundLastOpacityStop = false;

            // Convert GradientStops into ColorGradientStops by interpolating the color or opacity values from
            // the surrounding GradientStops.
            for (var i = 0; i < orderedStops.Length; i++)
            {
                var currentStop = orderedStops[i];

                // The stop is either an OpacityGradientStop or a ColorGradientStop. Convert to a ColorGradientStop
                // by interpolating the color or opacity as necessary.
                Color color;
                double opacityPercent;

                if (currentStop.Kind == GradientStop.GradientStopKind.Color)
                {
                    // The stop is a ColorGradientStop. Get the color value directly from the stop,
                    // and interpolate the opacity value from the surrounding OpacityGradientStops.
                    var currentColorStop = previousColorStop = (ColorGradientStop)currentStop;
                    color = currentColorStop.Color;

                    // Invalidate nextColorStop to force a search for the next color stop.
                    nextColorStop = null;

                    // If there's an opacity stop at the same offset, it will be the next stop in the list.
                    if (i + 1 < orderedStops.Length && orderedStops[i + 1].Offset == currentStop.Offset)
                    {
                        opacityPercent = ((OpacityGradientStop)orderedStops[i + 1]).OpacityPercent;
                    }
                    else
                    {
                        // Find the next opacity stop, if there is one.
                        if (nextOpacityStop == null && !foundLastOpacityStop)
                        {
                            nextOpacityStop = (OpacityGradientStop)FindNextStopOfKind(orderedStops, i + 1, GradientStop.GradientStopKind.Opacity);
                            if (nextOpacityStop == null)
                            {
                                // Indicate that we should not search again.
                                foundLastOpacityStop = true;
                            }
                        }

                        // Interpolate the opacity value from the surrounding stops.
                        if (previousOpacityStop == null)
                        {
                            // There is no previous opacity stop. Use the next opacity
                            // stop if there is one, or 100% if there are no opacity stops.
                            opacityPercent = nextOpacityStop?.OpacityPercent ?? 100;
                        }
                        else
                        {
                            // If there's a following opacity stop, interpolate between previous
                            // and next, otherwise continue using the previous opacity.
                            opacityPercent = nextOpacityStop == null
                                ? previousOpacityStop.OpacityPercent
                                : InterpolateOpacityPercent(previousOpacityStop, nextOpacityStop, currentStop.Offset);
                        }
                    }
                }
                else
                {
                    // The stop is an OpacityGradientStop. Get the opacity value directly from the stop,
                    // and interpolate the color value from the surrounding ColorStops.
                    var currentOpacityStop = previousOpacityStop = (OpacityGradientStop)currentStop;
                    opacityPercent = previousOpacityStop.OpacityPercent;

                    // Invalidate nextOpacityStop to force a search for the next opacity stop.
                    nextOpacityStop = null;

                    // If there's a color stop at the same offset, it will be the previous stop in the list.
                    if (i > 0 && orderedStops[i - 1].Offset == currentStop.Offset)
                    {
                        color = ((ColorGradientStop)orderedStops[i - 1]).Color;
                    }
                    else
                    {
                        // Find the next color stop, if there is one.
                        if (nextColorStop == null && !foundLastColorStop)
                        {
                            nextColorStop = (ColorGradientStop)FindNextStopOfKind(orderedStops, i + 1, GradientStop.GradientStopKind.Color);
                            if (nextColorStop == null)
                            {
                                // Indicate that we should not search again.
                                foundLastColorStop = true;
                            }
                        }

                        // Interpolate the color value from the surrounding stops.
                        if (previousColorStop == null)
                        {
                            // There is no previous color. Use the next color, or black if there are no
                            // colors. There should always be at least one color, so black is arbitrary.
                            color = nextColorStop?.Color ?? Color.Black;
                        }
                        else
                        {
                            // If there's a following color stop, interpolate between previous
                            // and next, otherwise continue using the previous color.
                            color = nextColorStop == null
                                ? previousColorStop.Color
                                : InterpolateColor(previousColorStop, nextColorStop, currentStop.Offset);
                        }
                    }
                }

                yield return new ColorGradientStop(currentStop.Offset, color.MultipliedByOpacity(opacityPercent / 100.0));
            }
        }

        // Searches forward in the GradientStops array from startIndex and returns the first stop
        // of the given kind, or null if none found.
        static GradientStop FindNextStopOfKind(GradientStop[] stops, int startIndex, GradientStop.GradientStopKind kind)
        {
            for (var i = startIndex; i < stops.Length; i++)
            {
                if (stops[i].Kind == kind)
                {
                    return stops[i];
                }
            }

            return null;
        }

        // Returns the value of y at x along a line that passes through a and b.
        static double Lerp((double x, double y) a, (double x, double y) b, double x)
             => a.y + ((x - a.x) * ((b.y - a.y) / (b.x - a.x)));

        // Returns the opacity percent at the given offset between a and b.
        static double InterpolateOpacityPercent(OpacityGradientStop a, OpacityGradientStop b, double atOffset)
             => Lerp((a.Offset, a.OpacityPercent / 100.0), (b.Offset, b.OpacityPercent / 100.0), atOffset) * 100;

        // Returns the color at the given offset between a and b.
        static Color InterpolateColor(ColorGradientStop a, ColorGradientStop b, double atOffset)
            => Color.FromArgb(
                a: Lerp((a.Offset, a.Color.A), (b.Offset, b.Color.A), atOffset),
                r: Lerp((a.Offset, a.Color.R), (b.Offset, b.Color.R), atOffset),
                g: Lerp((a.Offset, a.Color.G), (b.Offset, b.Color.G), atOffset),
                b: Lerp((a.Offset, a.Color.B), (b.Offset, b.Color.B), atOffset));
    }
}
