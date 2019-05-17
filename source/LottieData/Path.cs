// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Path : ShapeLayerContent
    {
        public Path(
            in ShapeLayerContentArgs args,
            bool direction,
            Animatable<Sequence<BezierSegment>> geometryData)
            : base(in args)
        {
            Direction = direction;
            Data = geometryData;
        }

        public bool Direction { get; }

        public Animatable<Sequence<BezierSegment>> Data { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Path;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Shape;
    }
}