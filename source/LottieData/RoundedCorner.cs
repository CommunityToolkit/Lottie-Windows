// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class RoundedCorner : ShapeLayerContent
    {
        public RoundedCorner(
            string name,
            string matchName,
            Animatable<double> radius)
            : base(name, matchName)
        {
            Radius = radius;
        }

        public Animatable<double> Radius { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.RoundedCorner;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.RoundedCorner;
    }
}
