// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Stores data as rows of columns. Columns are created automatically when adding data.
/// Column names are case insensitive.
/// </summary>
sealed class DataTable
{
    static readonly IEqualityComparer<string> s_columnNameComparer = StringComparer.OrdinalIgnoreCase;
    readonly List<(string, string)[]> _rows = new List<(string columnName, string value)[]>();

    internal void AddRow(IEnumerable<(string columnName, string value)> columns)
    {
        _rows.Add(columns.ToArray());
    }

    internal (string[] columnNames, string[][] rows) GetData()
    {
        // Get the column names. Each row may contain different columns. Find the distinct names.
        var columnNames =
            (from row in _rows
             from column in row
             select column.Item1).Distinct(s_columnNameComparer).ToArray();

        return (columnNames, GetRows(columnNames).ToArray());
    }

    IEnumerable<string[]> GetRows(string[] columnNames)
    {
        foreach (var row in _rows)
        {
            yield return GetRowData(columnNames, row).ToArray();
        }
    }

    static IEnumerable<string> GetRowData(string[] columnNames, (string, string)[] row)
    {
        // Create a dictionary for the row. This is used to match the column name to the column index.
        var rowDictionary = row.ToDictionary(c => c.Item1, s_columnNameComparer);
        foreach (var name in columnNames)
        {
            // Return the row in the correct column order.
            (string columnName, string columnValue) col;
            yield return rowDictionary.TryGetValue(name, out col) ? col.columnValue : null;
        }
    }
}