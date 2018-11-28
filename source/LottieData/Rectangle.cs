// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class Rectangle : ShapeLayerContent
    {
        public Rectangle(
            string name,
            string matchName,
            bool direction,
            IAnimatableVector3 position,
            IAnimatableVector3 size,
            Animatable<double> cornerRadius)
            : base(name, matchName)
        {
            Direction = direction;
            Position = position;
            Size = size;
            CornerRadius = cornerRadius;
        }

        public bool Direction { get; }

        public Animatable<double> CornerRadius { get; }

        public IAnimatableVector3 Size { get; }

        public IAnimatableVector3 Position { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Rectangle;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Rectangle;
    }
}
