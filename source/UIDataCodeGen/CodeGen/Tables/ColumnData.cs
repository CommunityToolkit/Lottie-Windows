// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    readonly struct ColumnData
    {
        ColumnData(string text, TextAlignment alignment, int span)
            => (Text, Alignment, Span) = (text ?? string.Empty, alignment, span);

        internal static ColumnData Create(string text, TextAlignment alignment, int span)
            => new ColumnData(text, alignment, span);

        internal static ColumnData Create(string text, int span)
            => new ColumnData(text, TextAlignment.Center, span);

        internal static ColumnData Create(string text, TextAlignment alignment)
             => new ColumnData(text, alignment, 1);

        internal static ColumnData Create(string text)
             => new ColumnData(text, TextAlignment.Center, 1);

        internal static ColumnData Create(double value)
             => new ColumnData(value.ToString("0.0"), TextAlignment.Right, 1);

        internal static ColumnData Create(int value)
             => new ColumnData(value.ToString(), TextAlignment.Right, 1);

        internal static ColumnData Empty => Create(string.Empty);

        internal string Text { get; }

        internal TextAlignment Alignment { get; }

        internal int Span { get; }
    }
}