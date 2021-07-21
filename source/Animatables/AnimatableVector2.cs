// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
    /// <summary>
    /// An animatable Vector2 value expressed as a single animatable Vector2 value.
    /// </summary>
#if PUBLIC_Animatables
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
            => timeOffset != 0 && IsAnimated
                ? new AnimatableVector2(KeyFrames.Select(kf => kf.WithTimeOffset(timeOffset)))
                : this;

        IAnimatableVector2 IAnimatableVector2.WithOffset(Vector2 offset)
            => WithOffset(offset);

        IAnimatableVector2 IAnimatableVector2.WithTimeOffset(double timeOffset)
            => WithTimeOffset(timeOffset);

        public AnimatableVector2 Select(Func<Vector2, Vector2> selector)
            => IsAnimated
                    ? new AnimatableVector2(KeyFrames.Select(kf => kf.CloneWithNewValue(selector(kf.Value))))
                    : new AnimatableVector2(selector(InitialValue));

        /// <inheritdoc/>
        public AnimatableVector2Type Type => AnimatableVector2Type.Vector2;
    }
}
