// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
    /// <summary>
    /// An animatable Vector3 value expressed as a single animatable Vector3 value.
    /// </summary>
#if PUBLIC_Animatables
    public
#endif
    sealed class AnimatableVector3 : Animatable<Vector3>, IAnimatableVector3
    {
        public AnimatableVector3(Vector3 initialValue)
            : base(initialValue)
        {
        }

        public AnimatableVector3(IEnumerable<KeyFrame<Vector3>> keyFrames)
            : base(keyFrames)
        {
        }

        public AnimatableVector3 WithOffset(Vector2 offset)
            => Select(vector3 => vector3 + new Vector3(offset.X, offset.Y, 0));

        public new AnimatableVector3 WithTimeOffset(double timeOffset)
            => timeOffset != 0 || IsAnimated
                ? new AnimatableVector3(KeyFrames.Select(kf => kf.WithTimeOffset(timeOffset)))
                : this;

        IAnimatableVector3 IAnimatableVector3.WithTimeOffset(double timeOffset)
            => WithTimeOffset(timeOffset);

        public AnimatableVector3 Select(Func<Vector3, Vector3> selector)
            => IsAnimated
                    ? new AnimatableVector3(KeyFrames.Select(kf => kf.CloneWithNewValue(selector(kf.Value))))
                    : new AnimatableVector3(selector(InitialValue));

        /// <inheritdoc/>
        public AnimatableVector3Type Type => AnimatableVector3Type.Vector3;
    }
}