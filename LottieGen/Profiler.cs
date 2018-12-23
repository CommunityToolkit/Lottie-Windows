// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

// Measures time spent in each phase.
sealed class Profiler
{
    readonly Stopwatch _sw = Stopwatch.StartNew();

    // Bucket of time to dump time we don't want to measure. Never reported.
    TimeSpan _unmeasuredTime;
    TimeSpan _parseTime;
    TimeSpan _translateTime;
    TimeSpan _optimizationTime;
    TimeSpan _codegenTime;
    TimeSpan _serializationTime;

    internal void OnUnmeasuredFinished() => OnPhaseFinished(ref _unmeasuredTime);

    internal void OnParseFinished() => OnPhaseFinished(ref _parseTime);

    internal void OnTranslateFinished() => OnPhaseFinished(ref _translateTime);

    internal void OnOptimizationFinished() => OnPhaseFinished(ref _optimizationTime);

    internal void OnCodeGenFinished() => OnPhaseFinished(ref _codegenTime);

    internal void OnSerializationFinished() => OnPhaseFinished(ref _serializationTime);

    void OnPhaseFinished(ref TimeSpan counter)
    {
        counter = _sw.Elapsed;
        _sw.Restart();
    }

    // True if there is at least one time value.
    internal bool HasAnyResults
        => new[]
        {
                _parseTime,
                _translateTime,
                _optimizationTime,
                _codegenTime,
                _serializationTime,
        }.Any(ts => ts > TimeSpan.Zero);

    internal void WriteReport(TextWriter writer)
    {
        WriteReportForPhase(writer, "parse", _parseTime);
        WriteReportForPhase(writer, "translate", _translateTime);
        WriteReportForPhase(writer, "optimization", _optimizationTime);
        WriteReportForPhase(writer, "codegen", _codegenTime);
        WriteReportForPhase(writer, "serialization", _serializationTime);
    }

    void WriteReportForPhase(TextWriter writer, string phaseName, TimeSpan value)
    {
        // Ignore phases that didn't occur.
        if (value > TimeSpan.Zero)
        {
            writer.WriteLine($"{value} spent in {phaseName}.");
        }
    }
}