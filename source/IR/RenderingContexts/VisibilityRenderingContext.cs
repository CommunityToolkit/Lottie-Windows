// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

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
        /// visible; odd indices describe when the content becomes invisible.
        /// </summary>
        public IReadOnlyList<double> StateChangeTimes { get; }

        public IEnumerable<TimeSegment> GetVisibleSegments()
        {
            var isVisible = false;
            var visibleAt = 0.0;

            foreach (var offset in StateChangeTimes)
            {
                if (isVisible)
                {
                    yield return new TimeSegment(visibleAt, offset - visibleAt);
                    isVisible = false;
                }
                else
                {
                    visibleAt = offset;
                    isVisible = true;
                }
            }

            if (isVisible)
            {
                yield return new TimeSegment(visibleAt, double.PositiveInfinity);
            }
        }

        protected override sealed bool DependsOn(RenderingContext other)
            => other switch
            {
                TimeOffsetRenderingContext _ => true,
                _ => false,
            };

        public override bool IsAnimated => false;

        public bool IsVisibleDuring(in TimeSegment segment)
        {
            foreach (var mySegment in GetVisibleSegments())
            {
                if (segment.IsOverlapping(mySegment))
                {
                    return true;
                }
            }

            return false;
        }

        public override sealed RenderingContext WithOffset(Vector2 offset) => this;

        public override RenderingContext WithTimeOffset(double timeOffset)
             => timeOffset == 0
                ? this
                : new VisibilityRenderingContext(
                    StateChangeTimes.Select(t => t + timeOffset).ToArray());

        /// <summary>
        /// Combines the given contexts such that the resulting context is visible only when
        /// all of the contexts are visible.
        /// </summary>
        /// <returns>A context that ANDs the given contexts.</returns>
        public static VisibilityRenderingContext CombineAnd(IEnumerable<VisibilityRenderingContext> contexts)
            => CombineAndOr(contexts, isAnd: true);

        /// <summary>
        /// Combines the given contexts such that the resulting context is visible when
        /// any of the contexts are visible.
        /// </summary>
        /// <returns>A context that ORs the given contexts.</returns>
        public static VisibilityRenderingContext CombineOr(IEnumerable<VisibilityRenderingContext> contexts)
            => CombineAndOr(contexts, isAnd: false);

        static VisibilityRenderingContext CombineAndOr(IEnumerable<VisibilityRenderingContext> contexts, bool isAnd)
        {
            var ios = contexts.SelectMany(c => InOrOutFrame.ConvertToInOrOutFrame(c.StateChangeTimes)).ToArray();

            var states = InOrOutFrame.ConvertToStateChange(ios, isAnd ? contexts.Count() : 1).ToArray();

            return new VisibilityRenderingContext(states);
        }

        // Gets the list of time segments described by the given contexts. A time segment
        // is a segment where at least one of the contexts describes a change from invisible
        // to visible, and ends where the next time segment begins or where the current
        // segment becomes invisible.
        internal static IEnumerable<TimeSegment>
            GetVisibilitySegments(IEnumerable<VisibilityRenderingContext> contexts)
        {
            // Convert to an ordered list of InOrOutFrames.
            var ios = contexts.SelectMany(c => InOrOutFrame.ConvertToInOrOutFrame(c.StateChangeTimes)).
                            OrderBy(inOrOut => inOrOut.Offset).
                            ToArray();

            var inFrame = 0.0;

            // Invisible when this is 0.
            var visibilityCounter = 0;

            foreach (var io in ios)
            {
                // How long since the last transition?
                var duration = io.Offset - inFrame;

                visibilityCounter += io.IsIn ? 1 : -1;

                if (visibilityCounter != 1 && duration > 0)
                {
                    yield return new TimeSegment(inFrame, duration);
                }

                inFrame = io.Offset;
            }

            if (visibilityCounter > 0)
            {
                // Still visible at the end of the sequence. Output an
                // infinite duration segment.
                yield return new TimeSegment(inFrame, double.PositiveInfinity);
            }
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

            internal static IEnumerable<double> ConvertToStateChange(
                                                    IEnumerable<InOrOutFrame> inOrOutFrames,
                                                    int threshold)
            {
                var frames = inOrOutFrames.GroupBy(f => f.Offset).OrderBy(g => g.Key).ToArray();

                var counter = 0;
                var wasIn = false;
                foreach (var io in frames)
                {
                    var netValue = io.Sum(ioFrame => ioFrame.IsIn ? 1 : -1);
                    if (netValue != 0)
                    {
                        counter += netValue;
                        if ((wasIn && counter < threshold) || (!wasIn && counter >= threshold))
                        {
                            wasIn = !wasIn;
                            yield return io.Key;
                        }
                    }
                }
            }

            public override string ToString() => $"{(IsIn ? "In" : "Out")}@{Offset}";
        }

        public override string ToString()
        {
            var visibilities = GetVisibleSegments().Select(
                                        pair => pair.Duration == double.PositiveInfinity
                                            ? $"{pair.Offset}->..."
                                            : $"{pair.Offset}->{pair.Offset + pair.Duration}");

            return "Visibility: " + string.Join(", ", visibilities);
        }
    }
}
