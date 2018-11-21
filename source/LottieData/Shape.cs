// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class Shape : ShapeLayerContent
    {
        public Shape(
            string name,
            string matchName,
            bool direction,
            Animatable<Sequence<BezierSegment>> geometry)
            : base(name, matchName)
        {
            Direction = direction;
            PathData = geometry;
        }

        public bool Direction { get; }

        public Animatable<Sequence<BezierSegment>> PathData { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Path;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Shape;
    }
}