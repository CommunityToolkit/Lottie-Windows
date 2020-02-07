// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    sealed class LottieMarkersMonospaceTableFormatter : MonospaceTableFormatter
    {
        internal static IEnumerable<string> GetMarkersDescriptionLines(
            Stringifier stringifier,
            IEnumerable<(Marker marker, string startConstant, string endConstant)> markers)
        {
            var ms = markers.ToArray();
            var hasNonZeroDurations = ms.Any(m => m.marker.Duration.Frames > 0);

            return hasNonZeroDurations
                ? GetMarkersWithDurationDescriptionLines(stringifier, markers)
                : GetMarkersWithNoDurationsDescriptionLines(stringifier, markers);
        }

        static IEnumerable<string> GetMarkersWithNoDurationsDescriptionLines(
            Stringifier stringifier,
            IEnumerable<(Marker marker, string startConstant, string endConstant)> markers)
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
                let marker = m.marker
                select (Row)new Row.ColumnData(
                    ColumnData.Create(marker.Name, TextAlignment.Left),
                    ColumnData.Create(m.startConstant, TextAlignment.Left),
                    ColumnData.Create(marker.Frame.Number),
                    ColumnData.Create(marker.Frame.Time.TotalMilliseconds),
                    ColumnData.Create(stringifier.Float(marker.Frame.Progress), TextAlignment.Left)
                );

            records = records.Append(Row.BodyBottom);

            return GetTableLines(header.Concat(records));
        }

        static IEnumerable<string> GetMarkersWithDurationDescriptionLines(
            Stringifier stringifier,
            IEnumerable<(Marker marker, string startConstant, string endConstant)> markers)
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
                let marker = m.marker
                select (Row)new Row.ColumnData(
                    ColumnData.Create(marker.Name, TextAlignment.Left),
                    ColumnData.Create(m.startConstant, TextAlignment.Left),
                    ColumnData.Create(m.endConstant, TextAlignment.Left),
                    ColumnData.Create(marker.Frame.Number),
                    ColumnData.Create(marker.Frame.Time.TotalMilliseconds),
                    marker.Duration.Frames > 0 ? ColumnData.Create(marker.Duration.Time.TotalMilliseconds) : ColumnData.Empty,
                    ColumnData.Create(stringifier.Float(marker.Frame.Progress), TextAlignment.Left),
                    marker.Duration.Frames > 0 ? ColumnData.Create(stringifier.Float((marker.Frame + marker.Duration).Progress), TextAlignment.Left) : ColumnData.Empty
                );

            records = records.Append(Row.BodyBottom);

            return GetTableLines(header.Concat(records));
        }
    }
}
