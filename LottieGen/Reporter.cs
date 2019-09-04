// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

sealed class Reporter
{
    readonly Dictionary<string, DataTable> _dataTables =
        new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);

    internal Reporter(TextWriter infoStream, TextWriter errorStream)
    {
        InfoStream = infoStream;
        ErrorStream = errorStream;
    }

    internal TextWriter InfoStream { get; }

    internal TextWriter ErrorStream { get; }

    // Helper for writing errors to the error stream with a standard format.
    internal void WriteError(string errorMessage)
    {
        ErrorStream.WriteLine($"Error: {errorMessage}");
    }

    // Helper for writing info lines to the info stream.
    internal void WriteInfo(string infoMessage)
    {
        InfoStream.WriteLine(infoMessage);
    }

    // Writes a new line to the info stream.
    internal void WriteInfoNewLine()
    {
        InfoStream.WriteLine();
    }

    // Writes a row of data that can be retrieved later. Typically this is used to create CSV or TSV files
    // containing information about the result of processing some Lottie files.
    internal void WriteDataRow(string databaseName, IReadOnlyList<(string columnName, string value)> row)
    {
        lock (_dataTables)
        {
            if (!_dataTables.TryGetValue(databaseName, out var database))
            {
                database = new DataTable();
                _dataTables.Add(databaseName, database);
            }

            database.AddRow(row);
        }
    }

    // Returns the data for each dat table that was written.
    internal IEnumerable<(string dataTableName, string[] columnNames, string[][] rows)> GetDataTables()
    {
        foreach (var (dataTableName, dataTable) in _dataTables)
        {
            var (columNames, rows) = dataTable.GetData();
            yield return (dataTableName, columNames, rows);
        }
    }
}