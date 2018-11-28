// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Ellipse : ShapeLayerContent
    {
        public Ellipse(
            string name,
            string matchName,
            bool direction,
            IAnimatableVector3 position,
            IAnimatableVector3 diameter)
            : base(name, matchName)
        {
            Direction = direction;
            Position = position;
            Diameter = diameter;
        }

        public bool Direction { get; }

        public IAnimatableVector3 Position { get; }

        public IAnimatableVector3 Diameter { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Ellipse;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Ellipse;
    }
}
