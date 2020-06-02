// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Path : Shape
    {
        public Path(
            in ShapeLayerContentArgs args,
            DrawingDirection drawingDirection,
            Animatable<PathGeometry> geometryData)
            : base(in args, drawingDirection)
        {
            Data = geometryData;
        }

        public Animatable<PathGeometry> Data { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Path;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Shape;

        /// <summary>
        /// Returns a path with the same properties except with the given
        /// <paramref name="geometryData"/> in place of <see cref="Data"/>.
        /// </summary>
        /// <param name="geometryData">The geometry to use in place of <see cref="Data"/>.</param>
        /// <returns>The cloned path.</returns>
        public Path CloneWithNewGeometry(Animatable<PathGeometry> geometryData)
            => new Path(
                    new ShapeLayerContent.ShapeLayerContentArgs
                    {
                        BlendMode = BlendMode,
                        MatchName = MatchName,
                        Name = Name,
                    },
                    DrawingDirection,
                    geometryData);
    }
}