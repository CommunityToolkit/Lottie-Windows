// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if !WINDOWS_UWP
    public
#endif
    abstract class KeyFrameAnimation<T> : KeyFrameAnimation_
    {
        readonly SortedList<float, KeyFrame> _keyFrames = new SortedList<float, KeyFrame>();

        protected private KeyFrameAnimation(KeyFrameAnimation<T> other)
            : base(other)
        {
            if (other != null)
            {
                CopyStateFrom(other);
            }
        }

        public void InsertExpressionKeyFrame(float progress, string expression, CompositionEasingFunction easing)
        {
            if (progress < 0 || progress > 1)
            {
                throw new ArgumentException($"Progress must be >=0 and <=1. Value: {progress}");
            }

            _keyFrames.Add(progress, new ExpressionKeyFrame { Progress = progress, Expression = expression, Easing = easing });
        }

        public void InsertKeyFrame(float progress, T value, CompositionEasingFunction easing)
        {
            if (progress < 0 || progress > 1)
            {
                throw new ArgumentException($"Progress must be >=0 and <=1. Value: {progress}");
            }

            _keyFrames.Add(progress, new ValueKeyFrame { Progress = progress, Value = value, Easing = easing });
        }

        public IEnumerable<KeyFrame> KeyFrames => _keyFrames.Values;

        /// <inheritdoc/>
        public override int KeyFrameCount => _keyFrames.Count;

        void CopyStateFrom(KeyFrameAnimation<T> other)
        {
            _keyFrames.Clear();
            foreach (var pair in other._keyFrames)
            {
                _keyFrames.Add(pair.Key, pair.Value);
            }

            Duration = other.Duration;
            Target = other.Target;
        }

        public enum KeyFrameType
        {
            Expression,
            Value,
        }

        public abstract class KeyFrame
        {
            protected private KeyFrame()
            {
            }

            public float Progress { get; internal set; }

            public CompositionEasingFunction Easing { get; internal set; }

            public abstract KeyFrameType Type { get; }
        }

        public sealed class ValueKeyFrame : KeyFrame
        {
            public T Value { get; internal set; }

            /// <inheritdoc/>
            public override KeyFrameType Type => KeyFrameType.Value;
        }

        public sealed class ExpressionKeyFrame : KeyFrame
        {
            public string Expression { get; internal set; }

            /// <inheritdoc/>
            public override KeyFrameType Type => KeyFrameType.Expression;
        }
    }
}
