// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata
{
    /// <summary>
    /// A frame location in a Lottie composition.
    /// </summary>
#if PUBLIC_LottieMetadata
    public
#endif
    readonly struct Frame : IComparable<Frame>
    {
        readonly Duration _context;

        internal Frame(Duration context, double number)
        {
            Number = number;
            _context = context;
        }

        /// <summary>
        /// The frame number.
        /// </summary>
        public double Number { get; }

        /// <summary>
        /// The location as a proportion of the Lottie composition.
        /// </summary>
        public double Progress => Number / _context.Frames;

        /// <summary>
        /// Gets a nudged progress value. This gets the progress value that
        /// is the given <paramref name="frameProportion"/> greater than the
        /// actual progress value, unless this frame is frame 0.
        /// This is used to compensate for rounding of floating point values
        /// that may otherwise cause the progress value to refer to an
        /// animation value from the previous frame. Frame 0 is never nudged
        /// because it has no previous frame.
        /// </summary>
        /// <param name="frameProportion">The proportion of a frame
        /// time to nudge. Must be non-negative and less than 1.</param>
        /// <returns>The nudged progress.</returns>
        public double GetNudgedProgress(double frameProportion)
        {
            if (frameProportion < 0 || frameProportion >= 1)
            {
                throw new ArgumentException();
            }

            if (Number == 0)
            {
                // Do not nudge 0 values. There is no chance of
                // them referring to the next or previous frame.
                return 0;
            }

            return Math.Max(0, Math.Min(1, GetNudgedProgressUnsafe(frameProportion)));
        }

        /// <summary>
        /// The location as a time offset from the start of the Lottie composition.
        /// </summary>
        public TimeSpan Time => Progress * _context.Time;

        public static Frame operator +(Frame frame, Duration duration)
        {
            return new Frame(frame._context, frame.Number + duration.Frames);
        }

        public static Duration operator -(Frame frameA, Frame frameB)
        {
            // The frames must refer to the same Duration otherwise it makes no sense
            // to subtract them.
            AssertSameContext(frameA, frameB);

            return new Duration(frameA.Number - frameB.Number, frameA._context);
        }

        public override string ToString() => $"{Number}/{Progress}/{Time}";

        public int CompareTo(Frame other)
        {
            // The frames must refer to the same Duration otherwise it makes no sense
            // to compare them.
            AssertSameContext(this, other);

            return Number.CompareTo(other.Number);
        }

        /// <summary>
        /// Gets the progress value that is the given <paramref name="frameProportion"/> greater
        /// than the actual progress value. This is used to compensate for rounding of floating
        /// point values that may cause the progress value to refer to an animation value from
        /// the previous frame.
        /// </summary>
        /// <param name="frameProportion">The proportion of a frame time to nudge.</param>
        /// <returns>The nudged progress.</returns>
        double GetNudgedProgressUnsafe(double frameProportion)
             => (Number + frameProportion) / _context.Frames;

        static void AssertSameContext(Frame frameA, Frame frameB)
        {
            if (frameA._context != frameB._context)
            {
                throw new ArgumentException();
            }
        }
    }
}
