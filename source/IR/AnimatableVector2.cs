// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    /// <summary>
    /// An animatable Vector2 value expressed as a single animatable Vector3 value.
    /// </summary>
#if PUBLIC_IR
    public
#endif
    sealed class AnimatableVector2 : Animatable<Vector2>, IAnimatableVector2
    {
        public AnimatableVector2(Vector2 initialValue)
            : base(initialValue)
        {
        }

        public AnimatableVector2(IEnumerable<KeyFrame<Vector2>> keyFrames)
            : base(keyFrames)
        {
        }

        public AnimatableVector2 WithOffset(Vector2 offset)
            => Select(vector2 => vector2 + offset);

        public new AnimatableVector2 WithTimeOffset(double timeOffset)
            => timeOffset == 0
                ? this
                : new AnimatableVector2(KeyFrames.Select(kf => kf.WithTimeOffset(timeOffset)));

        IAnimatableVector2 IAnimatableVector2.WithTimeOffset(double timeOffset)
            => WithTimeOffset(timeOffset);

        public new AnimatableVector2 Select(Func<Vector2, Vector2> selector)
            => new AnimatableVector2(KeyFrames.Select(kf => new KeyFrame<Vector2>(kf.Frame, selector(kf.Value), kf.Easing)));

        /// <inheritdoc/>
        public AnimatableVector2Type Type => AnimatableVector2Type.Vector2;
    }
}
