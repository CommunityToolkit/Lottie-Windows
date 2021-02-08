// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
    /// <summary>
    /// An animatable Vector2 value expressed as 2 animatable floating point values.
    /// </summary>
#if PUBLIC_Animatables
    public
#endif
    sealed class AnimatableXY : IAnimatableVector2
    {
        public AnimatableXY(Animatable<double> x, Animatable<double> y)
        {
            InitialValue = new Vector2(x.InitialValue, y.InitialValue);
            X = x;
            Y = y;
        }

        public AnimatableVector2Type Type => AnimatableVector2Type.XY;

        public Vector2 InitialValue { get; }

        public Animatable<double> X { get; }

        public Animatable<double> Y { get; }

        public static implicit operator AnimatableXY(AnimatableXYZ value)
            => new AnimatableXY(value.X, value.Y);

        public AnimatableXY Select(Func<double, double> selectorX, Func<double, double> selectorY)
            => new AnimatableXY(
                X.Select(selectorX),
                Y.Select(selectorY));

        public AnimatableXY Inverted()
            => Select(x => -x, y => -y);

        public AnimatableXY WithOffset(Vector2 offset)
            => Select(x => x + offset.X, y => y + offset.Y);

        public AnimatableXY WithScale(Vector2 scale)
            => Select(x => x * scale.X, y => y * scale.Y);

        public AnimatableXY WithTimeOffset(double timeOffset)
            => timeOffset == 0
                ? this
                : new AnimatableXY(X.WithTimeOffset(timeOffset), Y.WithTimeOffset(timeOffset));

        IAnimatableVector2 IAnimatableVector2.WithOffset(Vector2 offset)
            => WithOffset(offset);

        IAnimatableVector2 IAnimatableVector2.WithScale(Vector2 scale)
            => WithScale(scale);

        IAnimatableVector2 IAnimatableVector2.WithTimeOffset(double timeOffset)
            => WithTimeOffset(timeOffset);

        IAnimatableValue<Vector2> IAnimatableValue<Vector2>.WithTimeOffset(double timeOffset)
        {
            throw Exceptions.TODO;
        }

        IAnimatableVector2 IAnimatableVector2.Inverted()
            => Inverted();

        public bool IsAnimated => X.IsAnimated || Y.IsAnimated;

        AnimatableVector2Type IAnimatableVector2.Type => throw new NotImplementedException();

        Vector2 IAnimatableValue<Vector2>.InitialValue => throw new NotImplementedException();

        bool IAnimatableValue<Vector2>.IsAnimated => throw new NotImplementedException();
    }
}
