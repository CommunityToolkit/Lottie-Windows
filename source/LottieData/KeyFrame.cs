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
        public KeyFrame(double frame, T value, CubicBezier? spatialBezier, Easing easing)
        {
            Frame = frame;
            Value = value;
            SpatialBezier = spatialBezier;
            Easing = easing;
        }

        public KeyFrame(double frame, T value, Easing easing)
            : this(frame, value, spatialBezier: null, easing)
        {
        }

        /// <summary>
        /// Returns a <see cref="KeyFrame{T}"/> that is the same as this, but with a new value.
        /// </summary>
        /// <typeparam name="Tnew">The type of the new value.</typeparam>
        /// <returns>A new <see cref="KeyFrame{T}"/>.</returns>
        public KeyFrame<Tnew> CloneWithNewValue<Tnew>(Tnew newValue)
            where Tnew : IEquatable<Tnew> =>
            new KeyFrame<Tnew>(Frame, newValue, SpatialBezier, Easing);

        /// <summary>
        /// Returns a <see cref="KeyFrame{T}"/> that is the same as this, but with a new easing.
        /// </summary>
        /// <returns>A new <see cref="KeyFrame{T}"/>.</returns>
        public KeyFrame<T> CloneWithNewEasing(Easing newEasing) =>
            new KeyFrame<T>(Frame, Value, SpatialBezier, newEasing);

        /// <summary>
        /// Gets the value.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Gets the frame at which the animation should have the <see cref="Value"/>.
        /// </summary>
        public double Frame { get; }

        /// <summary>
        /// Describes the path that the animation follows. Only valid on Vector3 and Vector2 keyframes.
        /// </summary>
        public CubicBezier? SpatialBezier { get; }

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
