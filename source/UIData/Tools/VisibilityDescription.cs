// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.UIData.Tools
{
    readonly struct VisibilityDescription
    {
        internal VisibilityDescription(TimeSpan duration, VisibilityAtProgress[] sequence)
            => (Duration, Sequence) = (duration, sequence);

        /// <summary>
        /// The time over which the <see cref="VisibilityDescription"/> is valid.
        /// </summary>
        internal TimeSpan Duration { get; }

        /// <summary>
        /// The sequence of visibilites, ordered by progress. Initial visibility
        /// is "not visible". Each entry in the sequence represents a change to
        /// a different visibility.
        /// </summary>
        internal VisibilityAtProgress[] Sequence { get; }

        /// <summary>
        /// Composes the given <see cref="VisibilityDescription"/>s by ANDing them
        /// together.
        /// </summary>
        /// <returns>A composed <see cref="VisibilityDescription"/>.</returns>
        internal static VisibilityDescription Compose(
            in VisibilityDescription a,
            in VisibilityDescription b)
        {
            if (a.Sequence.Length == 0)
            {
                return b;
            }

            if (b.Sequence.Length == 0)
            {
                return a;
            }

            if (a.Duration != b.Duration)
            {
                throw new InvalidOperationException();
            }

            if (a.Sequence.SequenceEqual(b.Sequence))
            {
                // They're identical.
                return a;
            }

            // Combine the 2 sequences.
            var composedSequence = VisibilityAtProgress.Compose(a.Sequence, b.Sequence).ToArray();

            return new VisibilityDescription(a.Duration, composedSequence);
        }
    }
}
