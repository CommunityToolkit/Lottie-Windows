// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
#if PUBLIC_IR
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

        public override ShapeType ShapeType => ShapeType.Path;

        public Path WithOffset(Vector2 offset)
            => CloneWithNewGeometry(Data.IsAnimated
                    ? Data.Select(geo => geo.WithOffset(offset))
                    : new Animatable<PathGeometry>(Data.InitialValue.WithOffset(offset)));

        /// <summary>
        /// Returns a path with the same properties except with the given
        /// <paramref name="geometryData"/> in place of <see cref="Data"/>.
        /// </summary>
        /// <param name="geometryData">The geometry to use in place of <see cref="Data"/>.</param>
        /// <returns>The cloned path.</returns>
        public Path CloneWithNewGeometry(Animatable<PathGeometry> geometryData)
            => new Path(
                    new ShapeLayerContentArgs
                    {
                        BlendMode = BlendMode,
                        MatchName = MatchName,
                        Name = Name,
                    },
                    DrawingDirection,
                    geometryData);
    }
}
