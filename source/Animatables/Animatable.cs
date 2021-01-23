// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
    /// <summary>
    /// A value that may be animated.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
#if PUBLIC_Animatables
    public
#endif
    class Animatable<T> : IAnimatableValue<T>
        where T : IEquatable<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Animatable{T}"/> class with
        /// a non-animated value.
        /// </summary>
        public Animatable(T value)
        {
            KeyFrames = Array.Empty<KeyFrame<T>>();
            InitialValue = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Animatable{T}"/> class with
        /// the given key frames.
        /// </summary>
        public Animatable(IEnumerable<KeyFrame<T>> keyFrames)
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
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Animatable{T}"/> class with
        /// the given key frames.
        /// </summary>
        public Animatable(T initialValue, IReadOnlyList<KeyFrame<T>> keyFrames)
        {
            KeyFrames = keyFrames;

            InitialValue = initialValue;

            if (KeyFrames.Count == 1)
            {
                // There's only one key frame so the value never changes. We have
                // saved the value in InitialValue. Might as well ditch the key frames.
                KeyFrames = Array.Empty<KeyFrame<T>>();
            }
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
        /// Gets a value indicating whether the <see cref="Animatable{T}"/> has any key frames.
        /// </summary>
        public bool IsAnimated => KeyFrames.Count > 1;

        /// <summary>
        /// Returns <c>true</c> if this value is always equal to the given value.
        /// </summary>
        /// <returns><c>true</c> if this value is always equal to the given value.</returns>
        public bool IsAlways(T value) => !IsAnimated && value.Equals(InitialValue);

        /// <summary>
        /// Returns <c>true</c> if this value is ever equal to the given value.
        /// </summary>
        /// <returns><c>true</c> if this value is ever equal to the given value.</returns>
        public bool IsEver(T value) => value.Equals(InitialValue) || KeyFrames.Any(kf => value.Equals(kf.Value));

        /// <summary>
        /// Returns <c>true</c> if this value is ever not equal to the given value.
        /// </summary>
        /// <returns><c>true</c> if this value is ever not equal to the given value.</returns>
        public bool IsEverNot(T value) => !IsAlways(value);

        public Animatable<T> WithTimeOffset(double timeOffset)
            => timeOffset == 0
                ? this
                : new Animatable<T>(KeyFrames.Select(kf => kf.WithTimeOffset(timeOffset)));

        IAnimatableValue<T> IAnimatableValue<T>.WithTimeOffset(double timeOffset)
            => WithTimeOffset(timeOffset);

        public Animatable<T> Select(Func<T, T> selector)
            => new Animatable<T>(KeyFrames.Select(kf => new KeyFrame<T>(kf.Frame, selector(kf.Value), kf.Easing)));

        public Animatable<Tnew> Select<Tnew>(Func<T, Tnew> selector)
            where Tnew : IEquatable<Tnew>
            => new Animatable<Tnew>(KeyFrames.Select(kf => new KeyFrame<Tnew>(kf.Frame, selector(kf.Value), kf.Easing)));

        /// <inheritdoc/>
        // Not a great hash code because it ignore the KeyFrames, but quick.
        public override int GetHashCode() => InitialValue.GetHashCode();

        /// <inheritdoc/>
        public override string? ToString() =>
            IsAnimated
                ? string.Join(" -> ", KeyFrames.Select(kf => kf.Value.ToString()))
                : InitialValue.ToString();
    }
}
