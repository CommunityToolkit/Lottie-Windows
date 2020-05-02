// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    abstract class Row
    {
        Row()
        {
        }

        internal static Row HeaderTop { get; } = new NoData(RowType.HeaderTop);

        internal static Row HeaderBottom { get; } = new NoData(RowType.HeaderBottom);

        internal static Row BodyBottom { get; } = new NoData(RowType.BodyBottom);

        internal static Row Separator { get; } = new NoData(RowType.Separator);

        internal abstract RowType Type { get; }

        internal sealed class ColumnData : Row
        {
            internal ColumnData(params Tables.ColumnData[] columns) => Columns = columns;

            internal IReadOnlyList<Tables.ColumnData> Columns { get;  }

            internal override RowType Type => RowType.ColumnData;
        }

        sealed class NoData : Row
        {
            internal NoData(RowType type) => Type = type;

            internal override RowType Type { get; }
        }

        internal enum RowType
        {
            None = 0,
            BodyBottom,
            ColumnData,
            HeaderBottom,
            HeaderTop,
            Separator,
        }
    }
}