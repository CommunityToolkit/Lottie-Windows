// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    sealed class LottieMarkersMonospaceTableFormatter : MonospaceTableFormatter
    {
        internal static IEnumerable<string> GetMarkersDescriptionLines(
            Stringifier stringifier,
            IEnumerable<MarkerInfo> markers)
        {
            var ms = markers.ToArray();
            var hasNonZeroDurations = ms.Any(m => m.DurationInFrames > 0);

            return hasNonZeroDurations
                ? GetMarkersWithDurationDescriptionLines(stringifier, markers)
                : GetMarkersWithNoDurationsDescriptionLines(stringifier, markers);
        }

        static IEnumerable<string> GetMarkersWithNoDurationsDescriptionLines(
            Stringifier stringifier,
            IEnumerable<MarkerInfo> markers)
        {
            var header = new[] {
                Row.HeaderTop,
                new Row.ColumnData(
                    ColumnData.Create("Marker"),
                    ColumnData.Create("Constant"),
                    ColumnData.Create("Frame"),
                    ColumnData.Create("mS"),
                    ColumnData.Create("Progress")
                ),
                Row.HeaderBottom,
            };

            var records =
                from m in markers
                select (Row)new Row.ColumnData(
                    ColumnData.Create(m.Name, TextAlignment.Left),
                    ColumnData.Create(m.StartConstant, TextAlignment.Left),
                    ColumnData.Create(m.StartFrame),
                    ColumnData.Create(m.StartTime.TotalMilliseconds),
                    ColumnData.Create(stringifier.Float(m.StartProgress), TextAlignment.Left)
                );

            records = records.Append(Row.BodyBottom);

            return GetTableLines(header.Concat(records));
        }

        static IEnumerable<string> GetMarkersWithDurationDescriptionLines(
            Stringifier stringifier,
            IEnumerable<MarkerInfo> markers)
        {
            var header = new[] {
                Row.HeaderTop,
                new Row.ColumnData(
                    ColumnData.Create("Marker"),
                    ColumnData.Create("Constant", 2),
                    ColumnData.Empty,
                    ColumnData.Create("Start", 2),
                    ColumnData.Empty,
                    ColumnData.Create("Duration"),
                    ColumnData.Create("Progress", 2),
                    ColumnData.Empty
                ),
                new Row.ColumnData(
                    ColumnData.Empty,
                    ColumnData.Create("start", TextAlignment.Right),
                    ColumnData.Create("end", TextAlignment.Left),
                    ColumnData.Create("Frame", TextAlignment.Right),
                    ColumnData.Create("mS", TextAlignment.Right),
                    ColumnData.Create("mS", TextAlignment.Right),
                    ColumnData.Create("start", TextAlignment.Right),
                    ColumnData.Create("end", TextAlignment.Left)
                ),
                Row.HeaderBottom,
            };

            var records =
                from m in markers
                select (Row)new Row.ColumnData(
                    ColumnData.Create(m.Name, TextAlignment.Left),
                    ColumnData.Create(m.StartConstant, TextAlignment.Left),
                    ColumnData.Create(m.EndConstant ?? string.Empty, TextAlignment.Left),
                    ColumnData.Create(m.StartFrame),
                    ColumnData.Create(m.StartTime.TotalMilliseconds),
                    m.DurationInFrames > 0 ? ColumnData.Create(m.Duration.TotalMilliseconds) : ColumnData.Empty,
                    ColumnData.Create(stringifier.Float(m.StartProgress), TextAlignment.Left),
                    m.DurationInFrames > 0 ? ColumnData.Create(stringifier.Float(m.EndProgress), TextAlignment.Left) : ColumnData.Empty
                );

            records = records.Append(Row.BodyBottom);

            return GetTableLines(header.Concat(records));
        }
    }
}
