// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

sealed class Reporter
{
    // Lock to protect method and object that do not support multi-threaded access. Note that the
    // TextWriter objects do not need locking (they are assumed to be threadsafe).
    readonly object _lock = new object();

    readonly Dictionary<string, DataTable> _dataTables =
        new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);

    internal Reporter(TextWriter infoStream, TextWriter errorStream)
    {
        InfoStream = new Writer(infoStream);
        ErrorStream = new Writer(errorStream);
    }

    internal Writer InfoStream { get; }

    internal Writer ErrorStream { get; }

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
    internal void WriteDataTableRow(string databaseName, IReadOnlyList<(string columnName, string value)> row)
    {
        lock (_lock)
        {
            if (!_dataTables.TryGetValue(databaseName, out var database))
            {
                database = new DataTable();
                _dataTables.Add(databaseName, database);
            }

            database.AddRow(row);
        }
    }

    // Returns the data for each data table that was written.
    internal IEnumerable<(string dataTableName, string[] columnNames, string[][] rows)> GetDataTables()
    {
        lock (_lock)
        {
            foreach (var (dataTableName, dataTable) in _dataTables.OrderBy(dt => dt.Key))
            {
                var (columNames, rows) = dataTable.GetData();
                yield return (dataTableName, columNames, rows);
            }
        }
    }

    internal sealed class Writer
    {
        readonly TextWriter _wrapped;

        internal Writer(TextWriter wrapped)
        {
            _wrapped = wrapped;
        }

        public void WriteLine() => _wrapped.WriteLine();

        public void WriteLine(string value) => _wrapped.WriteLine(value);
   }
}