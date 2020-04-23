// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Information about a marker. Markers refer to points in time or segments of time in
    /// an animation.
    /// </summary>
    sealed class MarkerInfo
    {
        readonly Marker _marker;

        MarkerInfo(Marker marker, string name, string startConstant, string endConstant)
        {
            _marker = marker;
            Name = name;
            StartConstant = startConstant;
            EndConstant = endConstant;
        }

        public TimeSpan Duration => _marker.Duration.Time;

        public double DurationInFrames => _marker.Duration.Frames;

        public string StartConstant { get; }

        public double StartFrame => _marker.Frame.Number;

        public TimeSpan StartTime => _marker.Frame.Time;

        public double StartProgress => _marker.Frame.Progress;

        public string EndConstant { get; }

        public double EndProgress => (_marker.Frame + _marker.Duration).Progress;

        public string Name { get; }

        internal static IEnumerable<MarkerInfo> GetMarkerInfos(IEnumerable<Marker> markers)
        {
            // Ensure the names are valid and distinct.
            var nameMap = markers.ToDictionary(m => m, m => SanitizeMarkerName(m.Name));
            EnsureNamesAreDistinct(nameMap);

            foreach (var m in markers.OrderBy(m => m.Frame.Number))
            {
                var name = nameMap[m];
                var constantBaseName = ConstantName(name);
                var isZeroDuration = m.Duration.Frames <= 0;
                var baseName = $"M_{constantBaseName}";
                var startConstant = isZeroDuration ? baseName : $"{baseName}_start";
                var endConstant = isZeroDuration ? null : $"{baseName}_end";
                yield return new MarkerInfo(m, name, startConstant, endConstant);
            }
        }

        // Returns the given name with non-printing and newline characters removed.
        // If the result is empty returns "anonymous".
        static string SanitizeMarkerName(string markerName)
        {
            IEnumerable<char> Sanitize()
            {
                foreach (var ch in markerName)
                {
                    switch (ch)
                    {
                        case '\r':
                        case '\n':
                            break;
                        case '\t':
                            yield return ' ';
                            break;
                        default:
                            if (char.IsControl(ch))
                            {
                                continue;
                            }

                            yield return ch;
                            break;
                    }
                }
            }

            var sanitizedName = new string(Sanitize().ToArray());

            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                sanitizedName = "anonymous";
            }

            return sanitizedName;
        }

        // Returns a name that can be used as the name of a class constant.
        // The returned name is expected to be prefixed with string.
        static string ConstantName(string baseName)
        {
            // Replace any disallowed character with underscores.
            var constantName =
                new string((from ch in baseName
                            select char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

            // Remove any duplicated underscores.
            return constantName.Replace("__", "_");
        }

        // Ensures that every name in the given map is unique, adjusting the names if necessary.
        static void EnsureNamesAreDistinct(Dictionary<Marker, string> markerNameMap)
        {
            var markersWithNonUniqueNames = markerNameMap.GroupBy(m => m.Value).Where(g => g.Count() > 1);

            if (markersWithNonUniqueNames.Any())
            {
                // Add a suffix to the names that are not unique.
                foreach (var group in markersWithNonUniqueNames)
                {
                    var differentiatingSuffx = 0;
                    foreach (var item in group.OrderBy(g => g.Key.Frame))
                    {
                        markerNameMap[item.Key] = $"{item.Value}_{differentiatingSuffx}";
                        differentiatingSuffx++;
                    }
                }

                // We changed some names so we may have created a new collision. Run the algorithm
                // again to ensure that there are no collisions.
                EnsureNamesAreDistinct(markerNameMap);
            }
        }
    }
}