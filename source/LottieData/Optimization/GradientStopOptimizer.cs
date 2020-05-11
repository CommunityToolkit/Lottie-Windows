// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sn = System.Numerics;

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
        /// Returns an equivalent list of keyframes with any redundant color stops removed.
        /// </summary>
        /// <returns>An equivalent list of keyframes with any redundant color stops removed.</returns>
        public static IEnumerable<KeyFrame<Sequence<ColorGradientStop>>> RemoveRedundantStops(IEnumerable<KeyFrame<Sequence<ColorGradientStop>>> keyFrames)
        {
            var input = keyFrames.ToArray();

            // For each key frame, get a bool[] that has an entry set if the color stop
            // is redundant. Then aggregate all of the arrays to return a single array of bools
            // with an entry set if that stop is redundant in every key frame.
            // Any stops identified as redundant in the resulting bool[] can be removed
            // without significantly changing the appearance of the gradient.
            //
            // NOTE: we rely on each key frame having the same number of stops. This is a reasonable
            // expectation because it wouldn't make sense to animate between gradients with different
            // numbers of stops.
            var redundancies = input.Select(kf => FindRedundantColorStops(kf.Value.ToArray())).Aggregate((a, b) =>
            {
                Debug.Assert(a != null & b != null, "Invariant");

                for (var i = 0; i < a.Length; i++)
                {
                    // Set the entry in a iff it's set in both a and b.
                    a[i] &= b[i];
                }

                return a;
            });

            for (var i = 0; i < input.Length; i++)
            {
                var keyFrame = input[i];

                Debug.Assert(keyFrame.Value.Count() == redundancies.Length, "Invariant");

                // Get just the significant (i.e. non-redundant) stops.
                var significantStops = keyFrame.Value.Zip(redundancies, (stop, isRedundant) => (stop, isRedundant)).Where(item => !item.isRedundant).Select(item => item.stop);

                yield return new KeyFrame<Sequence<ColorGradientStop>>(
                    frame: keyFrame.Frame,
                    value: new Sequence<ColorGradientStop>(significantStops),
                    spatialControlPoint1: keyFrame.SpatialControlPoint1,
                    spatialControlPoint2: keyFrame.SpatialControlPoint2,
                    easing: keyFrame.Easing);
            }
        }

        /// <summary>
        /// Converts a list of <see cref="GradientStop"/>s into an equivalent list of
        /// <see cref="ColorGradientStop"/> of the same length, but with the opacity from any
        /// <see cref="OpacityGradientStop"/>s multiplied into the alpha channel of the
        /// <see cref="ColorGradientStop"/>s.
        /// The color and opacity values are calculated by interpolating from the surrounding stops.
        /// The result is ordered by offset.
        /// </summary>
        /// <returns>A list of <see cref="ColorGradientStop"/>s ordered by offset.</returns>
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
                Opacity opacity;

                if (currentStop.Kind == GradientStop.GradientStopKind.Color)
                {
                    // The stop is a ColorGradientStop. Get the color value directly from the stop,
                    // and interpolate the opacity value from the surrounding OpacityGradientStops.
                    var currentColorStop = previousColorStop = (ColorGradientStop)currentStop;
                    color = currentColorStop.Color;

                    // Invalidate nextColorStop to force a search for the next color stop.
                    nextColorStop = null;

                    // If there's an opacity stop at the same offset, it will be the next stop in the list.
                    if (i + 1 < orderedStops.Length &&
                        orderedStops[i + 1].Offset == currentStop.Offset &&
                        orderedStops[i + 1].Kind == GradientStop.GradientStopKind.Opacity)
                    {
                        opacity = ((OpacityGradientStop)orderedStops[i + 1]).Opacity;
                    }
                    else
                    {
                        // Find the next opacity stop, if there is one.
                        if (nextOpacityStop is null && !foundLastOpacityStop)
                        {
                            nextOpacityStop = (OpacityGradientStop)FindNextStopOfKind(
                                                                        orderedStops,
                                                                        i + 1,
                                                                        GradientStop.GradientStopKind.Opacity);
                            if (nextOpacityStop is null)
                            {
                                // Indicate that we should not search again.
                                foundLastOpacityStop = true;
                            }
                        }

                        // Interpolate the opacity value from the surrounding stops.
                        if (previousOpacityStop is null)
                        {
                            // There is no previous opacity stop. Use the next opacity
                            // stop if there is one, or Opaque if there are no opacity stops.
                            opacity = nextOpacityStop?.Opacity ?? Opacity.Opaque;
                        }
                        else
                        {
                            // If there's a following opacity stop, interpolate between previous
                            // and next, otherwise continue using the previous opacity.
                            opacity = nextOpacityStop is null
                                ? previousOpacityStop.Opacity
                                : InterpolateOpacity(previousOpacityStop, nextOpacityStop, currentStop.Offset);
                        }
                    }
                }
                else
                {
                    // The stop is an OpacityGradientStop. Get the opacity value directly from the stop,
                    // and interpolate the color value from the surrounding ColorStops.
                    var currentOpacityStop = previousOpacityStop = (OpacityGradientStop)currentStop;
                    opacity = previousOpacityStop.Opacity;

                    // Invalidate nextOpacityStop to force a search for the next opacity stop.
                    nextOpacityStop = null;

                    // If there's a color stop at the same offset, it will be the previous stop in the list.
                    if (i > 0 &&
                        orderedStops[i - 1].Offset == currentStop.Offset &&
                        orderedStops[i - 1].Kind == GradientStop.GradientStopKind.Color)
                    {
                        color = ((ColorGradientStop)orderedStops[i - 1]).Color;
                    }
                    else
                    {
                        // Find the next color stop, if there is one.
                        if (nextColorStop is null && !foundLastColorStop)
                        {
                            nextColorStop = (ColorGradientStop)FindNextStopOfKind(
                                                                    orderedStops,
                                                                    i + 1,
                                                                    GradientStop.GradientStopKind.Color);
                            if (nextColorStop is null)
                            {
                                // Indicate that we should not search again.
                                foundLastColorStop = true;
                            }
                        }

                        // Interpolate the color value from the surrounding stops.
                        if (previousColorStop is null)
                        {
                            // There is no previous color. Use the next color, or black if there are no
                            // colors. There should always be at least one color, so black is arbitrary.
                            color = nextColorStop?.Color ?? Color.Black;
                        }
                        else
                        {
                            // If there's a following color stop, interpolate between previous
                            // and next, otherwise continue using the previous color.
                            color = nextColorStop is null
                                ? previousColorStop.Color
                                : InterpolateColor(previousColorStop, nextColorStop, currentStop.Offset);
                        }
                    }
                }

                yield return new ColorGradientStop(currentStop.Offset, color * opacity);
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
        static Opacity InterpolateOpacity(OpacityGradientStop a, OpacityGradientStop b, double atOffset)
             => Opacity.FromFloat(Lerp((a.Offset, a.Opacity.Value), (b.Offset, b.Opacity.Value), atOffset));

        // Returns the color at the given offset between a and b.
        static Color InterpolateColor(ColorGradientStop a, ColorGradientStop b, double atOffset)
            => Color.FromArgb(
                a: Lerp((a.Offset, a.Color.A), (b.Offset, b.Color.A), atOffset),
                r: Lerp((a.Offset, a.Color.R), (b.Offset, b.Color.R), atOffset),
                g: Lerp((a.Offset, a.Color.G), (b.Offset, b.Color.G), atOffset),
                b: Lerp((a.Offset, a.Color.B), (b.Offset, b.Color.B), atOffset));

        /// <summary>
        /// Removes any redundant <see cref="ColorGradientStop"/>s.
        /// </summary>
        /// <returns>A list of <see cref="ColorGradientStop"/>s.</returns>
        public static IEnumerable<ColorGradientStop> OptimizeColorStops(IEnumerable<ColorGradientStop> stops)
        {
            var list = stops.ToArray();

            var redundantStops = FindRedundantColorStops(list);
            for (var i = 0; i < list.Length; i++)
            {
                if (!redundantStops[i])
                {
                    // Output the stop if it's not redundant.
                    yield return list[i];
                }
            }
        }

        // Returns a bool for each stop, set to true if the stop is redundant.
        // This is particularly useful for eliminating the default "midpoint" stop
        // that AfterEffects creates between any 2 stops that the user adds.
        static bool[] FindRedundantColorStops(ColorGradientStop[] stops)
        {
            var result = new bool[stops.Length];

            // We can only have redundant stops if there are 3 or more.
            if (stops.Length >= 3)
            {
                var left = 0;
                var middle = 1;

                for (var i = 2; i < stops.Length; i++)
                {
                    // See if the middle stop is redundant.
                    var right = i;

                    // Determine the angle between the line from middle to left and the line
                    // from middle to right. If the angle is small, the gradient stop does
                    // not contribute significantly and can be safely removed.
                    var angle = GetAngleBetweenStops(stops[left], stops[middle], stops[right]);

                    Debug.Assert(angle >= 0, "Invariant");

                    // This value can be tuned to be more or less sensitive to middle gradients
                    // that are more or less off the line formed by the outer gradients.
                    if (angle < 0.005)
                    {
                        // The middle stop is redundant.
                        result[middle] = true;
                    }
                    else
                    {
                        left = middle;
                    }

                    middle = right;
                }
            }

            return result;
        }

        // Returns a value that indicates how significant stop b is in the sequence of stops [a,b,c].
        // A significant stop is one that should not be removed because it would noticably change
        // the gradient.
        // The value ranges from 0 (not at all significant) to 2PI.
        static double GetAngleBetweenStops(ColorGradientStop a, ColorGradientStop b, ColorGradientStop c)
        {
            // Get the vectors from a to b and b to c that represent the RGB values in 4 dimensional space.
            var colorU = new Sn.Vector4((float)(b.Offset - a.Offset), (float)(b.Color.R - a.Color.R), (float)(b.Color.G - a.Color.G), (float)(b.Color.B - a.Color.B));
            var colorV = new Sn.Vector4((float)(c.Offset - b.Offset), (float)(c.Color.R - b.Color.R), (float)(c.Color.G - b.Color.G), (float)(c.Color.B - b.Color.B));

            colorU = Sn.Vector4.Normalize(colorU);
            colorV = Sn.Vector4.Normalize(colorV);

            // Get the angle between the vectors.
            // Returns a value between 0 and Pi.
            var colorAngle = Math.Acos(Sn.Vector4.Dot(colorU, colorV));

            // Get the vectors from a to b and b to c that represent the alpha values in 2 dimensional space.
            var alphaU = new Sn.Vector2((float)(b.Offset - a.Offset), (float)(b.Color.A - a.Color.A));
            var alphaV = new Sn.Vector2((float)(c.Offset - b.Offset), (float)(c.Color.A - b.Color.A));

            alphaU = Sn.Vector2.Normalize(alphaU);
            alphaV = Sn.Vector2.Normalize(alphaV);

            // Get the angle between the vectors.
            // Returns a value between 0 and Pi.
            var alphaAngle = Math.Acos(Sn.Vector2.Dot(alphaU, alphaV));

            if (double.IsNaN(colorAngle) || double.IsNaN(alphaAngle))
            {
                // There was an error calculating. This is caused by 0-length vectors.
                // Any 0-length vectors indicates that all the stops are significant
                // unless they're all the same.
                return a == b && b == c ? 0 : Math.PI * 2;
            }

            return colorAngle + alphaAngle;
        }
    }
}
