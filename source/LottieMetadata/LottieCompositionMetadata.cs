// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata
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
            IEnumerable<(string name, double frame, double durationMilliseconds)> markers)
        {
            CompositionName = compositionName;
            FramesPerSecond = framesPerSecond;
            InPoint = inPoint;
            OutPoint = outPoint;
            Markers = markers.Select(m => new Marker(m.name, m.frame, m.durationMilliseconds)).ToArray();
        }

        public static LottieCompositionMetadata Empty => new LottieCompositionMetadata(string.Empty, 0, 0, 0, Array.Empty<(string, double, double)>());

        public string CompositionName { get; }

        public double FramesPerSecond { get; }

        public double InPoint { get; }

        public double OutPoint { get; }

        public IReadOnlyList<Marker> Markers { get; }

        public sealed class Marker
        {
            internal Marker(string name, double frame, double durationMilliseconds)
            {
                Name = name;
                Frame = frame;
                DurationMilliseconds = durationMilliseconds;
            }

            public string Name { get; }

            public double Frame { get; }

            public double DurationMilliseconds { get; }
        }
    }
}
