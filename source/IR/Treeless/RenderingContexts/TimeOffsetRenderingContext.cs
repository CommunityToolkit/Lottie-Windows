// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless.RenderingContexts
{
    sealed class TimeOffsetRenderingContext : RenderingContext
    {
        internal TimeOffsetRenderingContext(double startTime) => StartTime = startTime;

        public double StartTime { get; }

        public override string ToString() => $"Start time: {StartTime}";
    }
}
