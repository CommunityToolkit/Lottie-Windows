// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata
{
    /// <summary>
    /// A duration expressed as a frame count and time.
    /// </summary>
#if PUBLIC_LottieMetadata
    public
#endif
    readonly struct Duration
    {
        public Duration(double frames, double fps)
        {
            if (frames < 0 || fps < 0)
            {
                throw new ArgumentException();
            }

            Frames = frames;
            FPS = fps;
        }

        public Duration(double frames, Duration other)
        {
            Frames = frames;
            FPS = other.FPS;
        }

        public double Frames { get; }

        public TimeSpan Time => TimeSpan.FromSeconds(Frames / FPS);

        public double FPS { get; }

        /// <summary>
        /// Gets the <see cref="Frame"/> with the given <paramref name="frameNumber"/>.
        /// </summary>
        /// <param name="frameNumber">The frame number.</param>
        /// <returns>The frame in this <see cref="Duration"/>.</returns>
        public Frame GetFrame(double frameNumber)
            => new Frame(this, Math.Min(Frames, Math.Max(frameNumber, 0)));

        /// <summary>
        /// Gets the <see cref="Frame"/> that corresponds to the given progress.
        /// </summary>
        /// <param name="progress">The progress value.</param>
        /// <returns>The frame in this <see cref="Duration"/>.</returns>
        public Frame GetFrameFromProgress(double progress)
            => new Frame(this, Math.Min(Frames, Math.Max(progress * Frames, 0)));

        /// <summary>
        /// Gets the <see cref="Frame"/> that corresponds to the given nudged progress.
        /// For a description of nudged progress, see
        /// <seealso cref="Frame.GetNudgedProgress(double)"/>.</summary>
        /// <param name="progress">The nudged progress value.</param>
        /// <param name="frameProportion">The proportion of a frame by which the frame
        /// has been nudged.</param>
        /// <returns>The frame in this <see cref="Duration"/>.</returns>
        public Frame GetFrameFromNudgedProgress(double progress, double frameProportion)
        {
            var nudgedFrameNumber = progress * Frames;
            var unnudgedFrameNumber = nudgedFrameNumber - frameProportion;
            var safeUnnudgedFrameNumber = Math.Min(Frames, Math.Max(0, unnudgedFrameNumber));
            return GetFrame(safeUnnudgedFrameNumber);
        }

        public static Duration operator +(Duration a, Duration b)
        {
            if (a.FPS == b.FPS)
            {
                return new Duration(a.Frames + b.Frames, a.FPS);
            }
            else
            {
                // NOTE: we could convert, but there's currently no use case for it.
                throw new ArgumentException();
            }
        }

        public static Duration operator -(Duration a, Duration b)
        {
            if (a.FPS == b.FPS)
            {
                return new Duration(a.Frames - b.Frames, a.FPS);
            }
            else
            {
                // NOTE: we could convert, but there's currently no use case for it.
                throw new ArgumentException();
            }
        }

        public static bool operator ==(Duration a, Duration b)
            => a.FPS == b.FPS && a.Frames == b.Frames;

        public static bool operator !=(Duration a, Duration b) => !(a == b);

        public override bool Equals(object? obj) => obj is Duration other && this == other;

        public override int GetHashCode() => Frames.GetHashCode();

        public override string ToString() => $"{Frames}/{Time}";
    }
}
