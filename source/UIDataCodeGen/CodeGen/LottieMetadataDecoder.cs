// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    sealed class LottieMetadataDecoder
    {
        // Identifies the Lottie metadata in SourceMetadata.
        static readonly Guid s_lottieMetadataKey = new Guid("EA3D6538-361A-4B1C-960D-50A6C35563A5");
        readonly LottieMetadata.LottieCompositionMetadata _metadata;

        LottieMetadataDecoder(LottieMetadata.LottieCompositionMetadata metadata)
        {
            _metadata = metadata;
        }

        internal static LottieMetadataDecoder GetMetadata(IReadOnlyDictionary<Guid, object> sourceMetadata)
        {
            if (sourceMetadata.TryGetValue(s_lottieMetadataKey, out var result))
            {
                return new LottieMetadataDecoder((LottieMetadata.LottieCompositionMetadata)result);
            }
            else
            {
                return null;
            }
        }

        internal string CompositionName => _metadata.CompositionName;

        internal double FramesPerSecond => _metadata.FramesPerSecond;

        internal TimeSpan Duration => TimeSpan.FromSeconds(DurationInFrames / FramesPerSecond);

        internal double DurationInFrames => _metadata.OutPoint - _metadata.InPoint;

        internal IEnumerable<(string name, (TimeSpan time, double progress) start, (TimeSpan time, double progress) end)> Markers
        {
            get
            {
                foreach (var m in _metadata.Markers)
                {
                    var startFrame = m.Frame;
                    var startTime = TimeSpan.FromSeconds(startFrame / FramesPerSecond);
                    var startProgress = Clamp01(startFrame / DurationInFrames);
                    var duration = TimeSpan.FromMilliseconds(m.DurationMilliseconds);
                    var durationInFrames = duration.TotalSeconds * FramesPerSecond;
                    var endFrame = startFrame + durationInFrames;
                    var endProgress = Clamp01(endFrame / DurationInFrames);
                    var endTime = TimeSpan.FromSeconds(endFrame / FramesPerSecond);
                    yield return (m.Name, (startTime, startProgress), (endTime, endProgress));
                }
            }
        }

        static double Clamp01(double value)
            => Math.Min(Math.Max(0, value), 1);
    }
}