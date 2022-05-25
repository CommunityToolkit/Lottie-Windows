// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.LottieMetadata
{
    /// <summary>
    /// Data about the contents of a Lottie file.
    /// </summary>
#if PUBLIC_LottieMetadata
    public
#endif
    sealed class LottieCompositionMetadata
    {
        public LottieCompositionMetadata(
            string compositionName,
            double framesPerSecond,
            double inPoint,
            double outPoint,
            IEnumerable<(string name, double frame, double durationInFrames)> markers)
        {
            CompositionName = compositionName;
            Duration = new Duration(outPoint - inPoint, framesPerSecond);
            Markers = markers.Select(
                m => new Marker(
                    m.name,
                    Duration.GetFrameFromFrameNumber(m.frame - inPoint),
                    new Duration(m.durationInFrames, Duration))).ToArray();
        }

        public static LottieCompositionMetadata Empty { get; } =
            new LottieCompositionMetadata(string.Empty, 0, 0, 0, Array.Empty<(string, double, double)>());

        public string CompositionName { get; }

        public Duration Duration { get; }

        public IReadOnlyList<Marker> Markers { get; }

        /// <summary>
        /// Gets the <see cref="Marker"/>s filtered to remove any that do not refer
        /// to parts of the composition, and with markers that cross the start or
        /// end of the composition adjusted so that they are contained by the composition.
        /// </summary>
        public IReadOnlyList<Marker> FilteredMarkers => FilterMarkers(Markers, Duration).ToArray();

        // Takes a markers list and returns another list where any markers that are
        // partially outside of the range are adjusted to be inside range, and any markers
        // that are completely outside of the range are discarded.
        static IEnumerable<Marker> FilterMarkers(IEnumerable<Marker> markers, Duration range)
        {
            foreach (var marker in markers)
            {
                var result = marker;
                if (result.Frame.Number < 0)
                {
                    // The marker starts before the start of the range.
                    if ((result.Frame + result.Duration).Number < 0)
                    {
                        // It is completely before the start of the range.
                        continue;
                    }

                    // Adjust the start and duration so that it starts at 0.
                    result = new Marker(result.Name, new Frame(range, 0), result.Duration - new Duration(-result.Frame.Number, range));
                }
                else if (result.Frame.Number > range.Frames)
                {
                    // It is completely after the end of the range.
                    continue;
                }

                if ((result.Frame + result.Duration).Number > range.Frames)
                {
                    // The marker ends after the end of the range. Adjust the duration so that it ends at the end of the range.
                    result = new Marker(result.Name, result.Frame, new Duration(range.Frames - result.Frame.Number, range));
                }

                yield return result;
            }
        }
    }
}
