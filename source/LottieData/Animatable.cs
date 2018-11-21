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
#if !WINDOWS_UWP
    public
#endif
    class Animatable<T> : IAnimatableValue<T>
        where T : IEquatable<T>
    {
        internal static readonly IEnumerable<KeyFrame<T>> EmptyKeyFrames = new KeyFrame<T>[0];
        readonly KeyFrame<T>[] _keyFrames;

        /// <summary>
        /// Initializes a new instance of the <see cref="Animatable{T}"/> class with
        /// a non-animated value.
        /// </summary>
        public Animatable(T value, int? propertyIndex)
            : this(value, EmptyKeyFrames, propertyIndex)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Animatable{T}"/> class with
        /// the given key frames.
        /// </summary>
        public Animatable(T initialValue, IEnumerable<KeyFrame<T>> keyFrames, int? propertyIndex)
        {
            _keyFrames = keyFrames.ToArray();
            InitialValue = initialValue;
            PropertyIndex = propertyIndex;

            Debug.Assert(initialValue != null, "Precondition");
            Debug.Assert(keyFrames.All(kf => kf != null), "Precondition");
        }

        /// <summary>
        /// Gets the initial value.
        /// </summary>
        public T InitialValue { get; }

        /// <summary>
        /// Gets the keyframes that describe how the value should be animated.
        /// </summary>
        public IEnumerable<KeyFrame<T>> KeyFrames => _keyFrames;

        /// <summary>
        /// Gets the property index used for expressions.
        /// </summary>
        public int? PropertyIndex { get; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="Animatable{T}"/> has any key frames.
        /// </summary>
        public bool IsAnimated => _keyFrames.Length > 0;

        /// <summary>
        /// Returns true if this value is always equal to the given value.
        /// </summary>
        public bool AlwaysEquals(T value) => !IsAnimated && value.Equals(InitialValue);

        // Not a great hash code because it ignore the KeyFrames, but quick.
        /// <inheritdoc/>
        public override int GetHashCode() => InitialValue.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() =>
            IsAnimated
                ? string.Join(" -> ", KeyFrames.Select(kf => kf.Value.ToString()))
                : InitialValue.ToString();
    }
}
