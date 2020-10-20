// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class KeyFrameAnimation<T, TExpression> : KeyFrameAnimation_
        where TExpression : Expression_<TExpression>
    {
        readonly SortedList<float, KeyFrame> _keyFrames = new SortedList<float, KeyFrame>();

        private protected KeyFrameAnimation(KeyFrameAnimation<T, TExpression>? other)
            : base(other)
        {
            if (other != null)
            {
                CopyStateFrom(other);
            }
        }

        public void InsertExpressionKeyFrame(float progress, TExpression expression, CompositionEasingFunction? easing)
        {
            if (progress < 0 || progress > 1)
            {
                throw new ArgumentException($"Progress must be >=0 and <=1. Value: {progress}");
            }

            _keyFrames.Add(progress, new ExpressionKeyFrame(progress, easing, expression));
        }

        // NOTE: this method does not exist on Windows.UI.Composition.BooleanKeyFrameAnimation - it does not support easing.
        //       The method is inherited by the WinCompData.BooleanKeyFrameAnimation but it is not valid to call it.
        public void InsertKeyFrame(float progress, T value, CompositionEasingFunction? easing)
        {
            if (typeof(T) == typeof(bool))
            {
                throw new ArgumentException($"This method cannot be called on {nameof(BooleanKeyFrameAnimation)}.");
            }

            InsertKeyFrameCommon(progress, value, easing);
        }

        public void InsertKeyFrame(float progress, T value)
            => InsertKeyFrameCommon(progress, value, easing: null);

        void InsertKeyFrameCommon(float progress, T value, CompositionEasingFunction? easing)
        {
            if (progress < 0 || progress > 1)
            {
                throw new ArgumentException($"Progress must be >=0 and <=1. Value: {progress}");
            }

            // It is legal to insert a key frame at a progress value that already has
            // a key frame. Last one wins.
            _keyFrames[progress] = new ValueKeyFrame(progress, easing, value);
        }

        public override IEnumerable<KeyFrame> KeyFrames => _keyFrames.Values;

        /// <inheritdoc/>
        public override int KeyFrameCount => _keyFrames.Count;

        void CopyStateFrom(KeyFrameAnimation<T, TExpression> other)
        {
            _keyFrames.Clear();
            foreach (var pair in other._keyFrames)
            {
                _keyFrames.Add(pair.Key, pair.Value);
            }

            Duration = other.Duration;
            Target = other.Target;
        }

        public sealed class ValueKeyFrame : KeyFrame
        {
            internal ValueKeyFrame(float progress, CompositionEasingFunction? easing, T value)
                : base(progress, easing)
            {
                Value = value;
            }

            public T Value { get; }

            /// <inheritdoc/>
            public override KeyFrameType Type => KeyFrameType.Value;

            // For debugging only.
            public override string ToString() => $"ValueKeyFrame: {Value}@{Progress} {Easing}";
        }

        public new sealed class ExpressionKeyFrame : KeyFrameAnimation_.ExpressionKeyFrame
        {
            internal ExpressionKeyFrame(float progress, CompositionEasingFunction? easing, TExpression expression)
                : base(progress, easing, expression)
            {
                Expression = expression;
            }

            public new TExpression Expression { get; }

            /// <inheritdoc/>
            public override KeyFrameType Type => KeyFrameType.Expression;
        }
    }
}
