// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// An <see cref="LottieData.Animatable{T}"/> that has had its key frames trimmed
    /// to include only those that affect a particular time period.
    /// </summary>
    /// <typeparam name="T">The type of the key frames.</typeparam>
    ref struct TrimmedAnimatable<T>
        where T : IEquatable<T>
    {
        internal TrimmedAnimatable(TranslationContext context, T initialValue, ReadOnlySpan<KeyFrame<T>> keyFrames)
        {
            Context = context;
            InitialValue = initialValue;
            KeyFrames = keyFrames;
        }

        internal TrimmedAnimatable(TranslationContext context, T initialValue)
        {
            Context = context;
            InitialValue = initialValue;
            KeyFrames = default(ReadOnlySpan<KeyFrame<T>>);
        }

        /// <summary>
        /// Gets the initial value.
        /// </summary>
        internal T InitialValue { get; }

        /// <summary>
        /// Gets the keyframes that describe how the value should be animated.
        /// </summary>
        internal ReadOnlySpan<KeyFrame<T>> KeyFrames { get; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="Animatable{T}"/> has any key frames.
        /// </summary>
        internal bool IsAnimated => KeyFrames.Length > 1;

        internal TranslationContext Context { get; }
    }
}