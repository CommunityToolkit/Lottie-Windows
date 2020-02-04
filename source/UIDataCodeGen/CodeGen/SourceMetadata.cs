// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;
using LCM = Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata.LottieCompositionMetadata;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class SourceMetadata
    {
        // Identifies the bound property names in SourceMetadata.
        static readonly Guid s_propertyBindingNamesKey = new Guid("A115C46A-254C-43E6-A3C7-9DE516C3C3C8");

        // Identifies the Lottie metadata in SourceMetadata.
        static readonly Guid s_lottieMetadataKey = new Guid("EA3D6538-361A-4B1C-960D-50A6C35563A5");

        readonly IReadOnlyDictionary<Guid, object> _sourceMetadata;
        IReadOnlyList<(string bindingName, PropertySetValueType actualType, PropertySetValueType exposedType)> _propertyBindings;
        Lottie _lottieMetadata;

        internal SourceMetadata(IReadOnlyDictionary<Guid, object> sourceMetadata)
        {
            _sourceMetadata = sourceMetadata;
        }

        internal Lottie LottieMetadata => _lottieMetadata ?? (_lottieMetadata = new Lottie(this));

        internal IReadOnlyList<(string bindingName, PropertySetValueType actualType, PropertySetValueType exposedType)> ProperytBindings
            => _propertyBindings ?? (_propertyBindings = _sourceMetadata.TryGetValue(s_propertyBindingNamesKey, out var propertyBindingNames)
                ? (IReadOnlyList<(string bindingName, PropertySetValueType actualType, PropertySetValueType exposedType)>)propertyBindingNames
                : Array.Empty<(string bindingName, PropertySetValueType actualType, PropertySetValueType exposedType)>());

        internal sealed class Lottie
        {
            readonly LCM _metadata;

            internal Lottie(SourceMetadata owner)
            {
                _metadata =
                    owner._sourceMetadata.TryGetValue(s_lottieMetadataKey, out var result)
                        ? (LCM)result
                        : LCM.Empty;
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
        }

        static double Clamp01(double value)
             => Math.Min(Math.Max(0, value), 1);
    }
}
