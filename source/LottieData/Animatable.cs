// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// A value that may be animated.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
#if PUBLIC_LottieData
    public
#endif
    class Animatable<T> : IAnimatableValue<T>
        where T : IEquatable<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Animatable{T}"/> class with
        /// a non-animated value.
        /// </summary>
        public Animatable(T value, int? propertyIndex)
        {
            Debug.Assert(value != null, "Precondition");
            KeyFrames = Array.Empty<KeyFrame<T>>();
            InitialValue = value;
            PropertyIndex = propertyIndex;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Animatable{T}"/> class with
        /// the given key frames.
        /// </summary>
        public Animatable(IEnumerable<KeyFrame<T>> keyFrames, int? propertyIndex)
        {
            KeyFrames = keyFrames.ToArray();

            // There must be a least one key frame otherwise this constructor should not have been called.
            InitialValue = KeyFrames[0].Value;

            if (KeyFrames.Count == 1)
            {
                // There's only one key frame so the value never changes. We have
                // saved the value in InitialValue. Might as well ditch the key frames.
                KeyFrames = Array.Empty<KeyFrame<T>>();
            }

            PropertyIndex = propertyIndex;

            Debug.Assert(KeyFrames.All(kf => kf != null), "Precondition");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Animatable{T}"/> class with
        /// the given key frames.
        /// </summary>
        public Animatable(T initialValue, IReadOnlyList<KeyFrame<T>> keyFrames, int? propertyIndex)
        {
            KeyFrames = keyFrames;

            InitialValue = initialValue;

            if (KeyFrames.Count == 1)
            {
                // There's only one key frame so the value never changes. We have
                // saved the value in InitialValue. Might as well ditch the key frames.
                KeyFrames = Array.Empty<KeyFrame<T>>();
            }

            PropertyIndex = propertyIndex;

            Debug.Assert(initialValue != null, "Precondition");
            Debug.Assert(KeyFrames.All(kf => kf != null), "Precondition");
        }

        /// <summary>
        /// Gets the initial value.
        /// </summary>
        public T InitialValue { get; }

        /// <summary>
        /// Gets the keyframes that describe how the value should be animated.
        /// </summary>
        public IReadOnlyList<KeyFrame<T>> KeyFrames { get; }

        /// <summary>
        /// Gets the property index used for expressions.
        /// </summary>
        public int? PropertyIndex { get; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="Animatable{T}"/> has any key frames.
        /// </summary>
        public bool IsAnimated => KeyFrames.Count > 1;

        /// <summary>
        /// Returns <c>true</c> if this value is always equal to the given value.
        /// </summary>
        /// <returns><c>true</c> if this value is always equal to the given value.</returns>
        public bool Always(T value) => !IsAnimated && value.Equals(InitialValue);

        /// <summary>
        /// Returns <c>true</c> if this value is ever equal to the given value.
        /// </summary>
        /// <returns><c>true</c> if this value is ever equal to the given value.</returns>
        public bool Ever(T value) => value.Equals(InitialValue) || KeyFrames.Any(kf => value.Equals(kf.Value));

        /// <summary>
        /// Returns <c>true</c> if this value is ever not equal to the given value.
        /// </summary>
        /// <returns><c>true</c> if this value is ever not equal to the given value.</returns>
        public bool EverNot(T value) => !Always(value);

        /// <inheritdoc/>
        // Not a great hash code because it ignore the KeyFrames, but quick.
        public override int GetHashCode() => InitialValue.GetHashCode();

        internal Animatable<T> CloneWithSelectedValue(Func<T, T> selector)
        {
            if (IsAnimated)
            {
                var keyframes =
                    from kf in KeyFrames.ToArray()
                    select new KeyFrame<T>(kf.Frame, selector(kf.Value), kf.Easing);
                return new Animatable<T>(keyframes, PropertyIndex);
            }
            else
            {
                return new Animatable<T>(selector(InitialValue), PropertyIndex);
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            IsAnimated
                ? string.Join(" -> ", KeyFrames.Select(kf => kf.Value.ToString()))
                : InitialValue.ToString();
    }
}
