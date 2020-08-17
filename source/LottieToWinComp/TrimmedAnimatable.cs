// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// An <see cref="LottieData.Animatable{T}"/> that has had its key frames trimmed
    /// to include only those that affect a particular time period.
    /// </summary>
    /// <typeparam name="T">The type of the key frames.</typeparam>
    readonly ref struct TrimmedAnimatable<T>
        where T : IEquatable<T>
    {
        readonly IReadOnlyList<KeyFrame<T>> _keyFrames;

        internal TrimmedAnimatable(TranslationContext context, T initialValue, IReadOnlyList<KeyFrame<T>> keyFrames)
        {
            Context = context;
            InitialValue = initialValue;
            _keyFrames = keyFrames;
        }

        internal TrimmedAnimatable(TranslationContext context, T initialValue)
        {
            Context = context;
            InitialValue = initialValue;
            _keyFrames = Array.Empty<KeyFrame<T>>();
        }

        /// <summary>
        /// Gets the initial value.
        /// </summary>
        internal T InitialValue { get; }

        /// <summary>
        /// Gets the keyframes that describe how the value should be animated.
        /// </summary>
        internal IReadOnlyList<KeyFrame<T>> KeyFrames => _keyFrames ?? Array.Empty<KeyFrame<T>>();

        /// <summary>
        /// Gets a value indicating whether the <see cref="Animatable{T}"/> has any key frames.
        /// </summary>
        internal bool IsAnimated => _keyFrames?.Count > 1;

        /// <summary>
        /// Returns <c>true</c> if this value is always equal to the given value.
        /// </summary>
        /// <returns><c>true</c> if this value is always equal to the given value.</returns>
        internal bool IsAlways(T value) => !IsAnimated && value.Equals(InitialValue);

        internal TranslationContext Context { get; }
    }
}