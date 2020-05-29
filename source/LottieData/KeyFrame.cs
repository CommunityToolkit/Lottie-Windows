// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// Describes a value at a particular point in time, and an optional easing function to
    /// interpolate from the previous value.
    /// </summary>
    /// <typeparam name="T">The type of the key frame's value.</typeparam>
#if PUBLIC_LottieData
    public
#endif
    sealed class KeyFrame<T> : IEquatable<KeyFrame<T>>
        where T : IEquatable<T>
    {
        public KeyFrame(double frame, T value, Vector3 spatialControlPoint1, Vector3 spatialControlPoint2, Easing easing)
        {
            Frame = frame;
            Value = value;
            SpatialControlPoint1 = spatialControlPoint1;
            SpatialControlPoint2 = spatialControlPoint2;
            Easing = easing;
        }

        public KeyFrame(double frame, T value, Easing easing)
            : this(frame, value, Vector3.Zero, Vector3.Zero, easing)
        {
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Gets the frame at which the animation should have the <see cref="Value"/>.
        /// </summary>
        public double Frame { get; }

        /// <summary>
        /// Gets the path that the animation follows. Only valid on Vector3 keyframes.
        /// </summary>
        public Vector3 SpatialControlPoint1 { get; }

        /// <summary>
        /// Gets the path that the animation follows. Only valid on Vector3 keyframes.
        /// </summary>
        public Vector3 SpatialControlPoint2 { get; }

        /// <summary>
        /// Gets the easing function used to interpolate from the previous value.
        /// </summary>
        public Easing Easing { get; }

        /// <inheritdoc/>
        public bool Equals(KeyFrame<T> other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other is null)
            {
                return false;
            }

            if (!Value.Equals(other.Value))
            {
                return false;
            }

            if (Frame != other.Frame)
            {
                return false;
            }

            if (!Equals(Easing, other.Easing))
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode() ^ Frame.GetHashCode() ^ Easing.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => Easing is null ? $"{Value} @{Frame}" : $"{Value} @{Frame} using {Easing}";
    }
}
