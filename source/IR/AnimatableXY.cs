// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    /// <summary>
    /// An animatable Vector3 value expressed as 3 animatable floating point values.
    /// </summary>
#if PUBLIC_IR
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

        /// <inheritdoc/>
        public AnimatableVector2Type Type => AnimatableVector2Type.XY;

        /// <inheritdoc/>
        public Vector2 InitialValue { get; }

        public Animatable<double> X { get; }

        public Animatable<double> Y { get; }

        public AnimatableXY WithOffset(Vector2 offset)
            => new AnimatableXY(X.Select(x => x + offset.X), Y.Select(y => y + offset.Y));

        public AnimatableXY WithTimeOffset(double timeOffset)
            => timeOffset == 0
                ? this
                : new AnimatableXY(X.WithTimeOffset(timeOffset), Y.WithTimeOffset(timeOffset));

        IAnimatableVector2 IAnimatableVector2.WithTimeOffset(double timeOffset)
            => WithTimeOffset(timeOffset);

        IAnimatableValue<Vector2> IAnimatableValue<Vector2>.WithTimeOffset(double timeOffset)
        {
            throw Exceptions.TODO;
        }

        /// <inheritdoc/>
        public bool IsAnimated => X.IsAnimated || Y.IsAnimated;
    }
}
