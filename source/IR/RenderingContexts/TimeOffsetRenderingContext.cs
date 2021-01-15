// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class TimeOffsetRenderingContext : RenderingContext
    {
        internal TimeOffsetRenderingContext(double startTime) => TimeOffset = startTime;

        public double TimeOffset { get; }

        public override bool IsAnimated => false;

        public override RenderingContext WithTimeOffset(double timeOffset)
            => new TimeOffsetRenderingContext(TimeOffset + timeOffset);

        public override string ToString() => $"Time offset: {TimeOffset}";
    }
}
