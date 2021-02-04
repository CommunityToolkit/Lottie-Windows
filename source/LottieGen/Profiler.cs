// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieGen
{
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

        internal TimeSpan ParseTime => _parseTime;

        internal TimeSpan TranslateTime => _translateTime;

        internal TimeSpan OptimizationTime => _optimizationTime;

        internal TimeSpan CodegenTime => _codegenTime;

        internal TimeSpan SerializationTime => _serializationTime;

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
    }
}