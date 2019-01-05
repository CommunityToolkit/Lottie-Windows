// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

sealed class Reporter
{
    readonly TextWriter _infoStream;
    readonly TextWriter _errorStream;

    internal Reporter(TextWriter infoStream, TextWriter errorStream)
    {
        _infoStream = infoStream;
        _errorStream = errorStream;
    }

    internal TextWriter InfoStream => _infoStream;

    internal TextWriter ErrorStream => _errorStream;

    // Helper for writing errors to the error stream with a standard format.
    internal void WriteError(string errorMessage)
    {
        _errorStream.WriteLine($"Error: {errorMessage}");
    }

    // Helper for writing info lines to the info stream.
    internal void WriteInfo(string infoMessage)
    {
        _infoStream.WriteLine(infoMessage);
    }

    // Writes a new line to the info stream.
    internal void WriteInfoNewLine()
    {
        _infoStream.WriteLine();
    }
}