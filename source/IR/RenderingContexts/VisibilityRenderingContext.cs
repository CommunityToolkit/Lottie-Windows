// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class VisibilityRenderingContext : RenderingContext
    {
        internal VisibilityRenderingContext(IReadOnlyList<double> stateChangeTimes)
        {
            Debug.Assert(stateChangeTimes.Count > 0, "Precondition");
            StateChangeTimes = stateChangeTimes;
        }

        /// <summary>
        /// Frame times when the visibility state flips. Initial state is
        /// non-visible. Even indices describe when the content becomes
        /// visible; odd inidices describe when the content becomes invisible.
        /// </summary>
        public IReadOnlyList<double> StateChangeTimes { get; }

        public override bool IsAnimated => false;

        public override sealed RenderingContext WithOffset(Vector2 offset) => this;

        public override RenderingContext WithTimeOffset(double timeOffset)
             => timeOffset == 0
                ? this
                : new VisibilityRenderingContext(
                    StateChangeTimes.Select(t => t + timeOffset).ToArray());

        public static RenderingContext WithoutRedundants(RenderingContext context)
        {
            if (context.SubContexts.Count == 0)
            {
                return context;
            }

            // Collect all of the VisibilitiesRenderingContexts, adjusted for time offset.
            var adjustedVisibilities = new List<VisibilityRenderingContext>(context.SubContexts.Count);
            var timeAdjustment = 0.0;
            foreach (var subContext in context.SubContexts)
            {
                switch (subContext)
                {
                    case TimeOffsetRenderingContext timeOffset:
                        timeAdjustment += timeOffset.TimeOffset;
                        break;

                    case VisibilityRenderingContext visibilityContext:
                        adjustedVisibilities.Add((VisibilityRenderingContext)visibilityContext.WithTimeOffset(timeAdjustment));
                        break;
                }
            }

            if (adjustedVisibilities.Count == 0)
            {
                return context;
            }

            // Combine the VisiblityReneringContexts into a single context.
            var combinedContext = Combine(adjustedVisibilities);

            // Return a new composite with the combined visibility at the front and all
            // others removed.
            var filtered = context.Filter((VisibilityRenderingContext c) => false);

            return combinedContext + filtered;
        }

        static VisibilityRenderingContext Combine(IReadOnlyList<VisibilityRenderingContext> contexts)
        {
            var ios = contexts.SelectMany(c => InOrOutFrame.ConvertToInOrOutFrame(c.StateChangeTimes)).OrderBy(inOrOut => inOrOut.Offset).ToArray();

            var states = InOrOutFrame.ConvertToStateChange(ios, contexts.Count).ToArray();

            return new VisibilityRenderingContext(states);
        }

        readonly struct InOrOutFrame
        {
            InOrOutFrame(double offset, bool isIn)
            {
                Offset = offset;
                IsIn = isIn;
            }

            internal double Offset { get; }

            internal bool IsIn { get; }

            internal static IEnumerable<InOrOutFrame> ConvertToInOrOutFrame(IEnumerable<double> stateChanges)
            {
                var isIn = true;
                foreach (var item in stateChanges)
                {
                    yield return new InOrOutFrame(item, isIn);
                    isIn = !isIn;
                }
            }

            internal static IEnumerable<double> ConvertToStateChange(IEnumerable<InOrOutFrame> inOrOutFrames, int threshold)
            {
                var counter = 0;
                foreach (var io in inOrOutFrames)
                {
                    if (io.IsIn)
                    {
                        counter++;
                        if (counter == threshold)
                        {
                            yield return io.Offset;
                        }
                    }
                    else
                    {
                        if (counter == threshold)
                        {
                            yield return io.Offset;
                        }

                        counter--;
                    }
                }
            }
        }

        public override string ToString()
        {
            var visibilities = EnumerateVisibilities().Select(
                                        pair => pair.inVisibleAt == double.PositiveInfinity
                                            ? $"{pair.visibleAt}->..."
                                            : $"{pair.visibleAt}->{pair.inVisibleAt}");

            return "Visibility: " + string.Join(", ", visibilities);
        }

        IEnumerable<(double visibleAt, double inVisibleAt)> EnumerateVisibilities()
        {
            for (var i = 0; i < StateChangeTimes.Count; i += 2)
            {
                yield return
                    (StateChangeTimes[i],
                     i < StateChangeTimes.Count - 1
                        ? StateChangeTimes[i + 1]
                        : double.PositiveInfinity);
            }
        }
    }
}
