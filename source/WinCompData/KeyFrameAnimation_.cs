// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class KeyFrameAnimation_ : CompositionAnimation
    {
        private protected KeyFrameAnimation_(KeyFrameAnimation_ other)
            : base(other)
        {
        }

        public TimeSpan Duration { get; set; }

        public abstract int KeyFrameCount { get; }

        public abstract IEnumerable<KeyFrame> KeyFrames { get; }

        public abstract class KeyFrame
        {
            private protected KeyFrame(float progress, CompositionEasingFunction easing)
            {
                Progress = progress;
                Easing = easing;
            }

            public float Progress { get; }

            public CompositionEasingFunction Easing { get; }

            public abstract KeyFrameType Type { get; }
        }

        public abstract class ExpressionKeyFrame : KeyFrame
        {
            private protected ExpressionKeyFrame(float progress, CompositionEasingFunction easing, Expression expression)
                : base(progress, easing)
            {
                Expression = expression;
            }

            public Expression Expression { get; }

            /// <inheritdoc/>
            public override KeyFrameType Type => KeyFrameType.Expression;
        }
    }
}