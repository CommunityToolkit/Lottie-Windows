// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    // Formats table data for display with a monospaced font.
    // Normal usage is to subclass this formatter to create a formatter
    // that is specific to a particular data set.
    abstract class MonospaceTableFormatter
    {
        protected static IEnumerable<string> GetTableLines(IEnumerable<Row> rows)
        {
            // Get the width of each column in each row and find the maximum width
            // required by each column.
            var columnWidths =
                (from row in rows.Select(r => GetRequiredMinimumWidths(r))
                 where row != null
                 select row).Aggregate((w1, w2) => w1.Select((w, i) => Math.Max(w2[i], w)).ToArray()).ToArray();

            // The total width includes space for the column separators.
            var totalWidth = columnWidths.Sum() + columnWidths.Length - 1;

            foreach (var r in rows)
            {
                switch (r.Type)
                {
                    case Row.RowType.ColumnData:
                        yield return FormatRow(((Row.ColumnData)r).Columns.Select((x, i) => (x, columnWidths[i])));
                        break;
                    case Row.RowType.HeaderTop:
                        yield return new string('_', totalWidth + 2);
                        break;
                    case Row.RowType.HeaderBottom:
                        yield return $"|{string.Join("|", columnWidths.Select(w => new string('_', w)))}|";
                        break;
                    case Row.RowType.BodyBottom:
                        yield return new string('-', totalWidth + 2);
                        break;
                    case Row.RowType.Separator:
                        yield return $"|{string.Join('+', columnWidths.Select(w => new string('-', w)))}|";
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        static int[] GetRequiredMinimumWidths(Row row)
        {
            switch (row.Type)
            {
                case Row.RowType.ColumnData:
                    var columnDataRow = (Row.ColumnData)row;

                    // Columns that span more than one column get an extra space that
                    // would otherwise be taken up by each column separator.
                    return columnDataRow.Columns.Select(c => c.Text.Length + c.Span + 1).ToArray();
                case Row.RowType.HeaderTop:
                case Row.RowType.HeaderBottom:
                case Row.RowType.BodyBottom:
                case Row.RowType.Separator:
                    // Null indicates "don't care".
                    return null;
                default:
                    throw new InvalidOperationException();
            }
        }

        static string Align(string str, int width, TextAlignment alignment)
        {
            var padding = width - str.Length;
            switch (alignment)
            {
                case TextAlignment.Center:
                    {
                        if (padding == 0)
                        {
                            return str;
                        }

                        var leftPadding = padding / 2;
                        if (leftPadding == 0)
                        {
                            leftPadding = 1;
                        }

                        return new string(' ', leftPadding) + str + new string(' ', padding - leftPadding);
                    }

                case TextAlignment.Left:
                    return padding == 0
                        ? str
                        : ' ' + str + new string(' ', padding - 1);

                case TextAlignment.Right:
                    return padding == 0
                        ? str
                        : new string(' ', padding - 1) + str + " ";

                default:
                    throw new InvalidOperationException();
            }
        }

        static string FormatRow(IEnumerable<(ColumnData data, int requiredWidth)> rowData)
        {
            var sb = new StringBuilder();

            var spanWidth = 0;
            string spanText = null;
            TextAlignment spanAlignment = default(TextAlignment);
            int spanCountdown = -1;

            foreach (var (column, requiredWidth) in rowData)
            {
                if (spanCountdown == 0)
                {
                    // Output the spanning column.
                    sb.Append("|");
                    sb.Append(Align(spanText, spanWidth, spanAlignment));
                    spanCountdown = -1;
                }
                else if (spanCountdown > 0)
                {
                    // Accumulate the width and otherwise ignore this column.
                    spanWidth += requiredWidth + 1;
                    spanCountdown--;
                    continue;
                }

                if (column.Span == 1)
                {
                    // Output the column.
                    sb.Append("|");
                    sb.Append(Align(column.Text, requiredWidth, column.Alignment));
                }
                else
                {
                    // Span is > 1.
                    // Save the column information until the column has been spanned.
                    spanCountdown = column.Span - 1;
                    spanText = column.Text;
                    spanAlignment = column.Alignment;
                    spanWidth = requiredWidth;
                }
            }

            // Output the final column.
            if (spanCountdown == 0)
            {
                // Output the previous column.
                sb.Append("|");
                sb.Append(Align(spanText, spanWidth, spanAlignment));
            }

            sb.Append("|");

            return sb.ToString();
        }
    }
}