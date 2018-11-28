// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgc;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wg;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class CanvasGeometry : IGeometrySource2D, IDescribable
    {
        CanvasGeometry()
        {
        }

        public enum GeometryType
        {
            Combination,
            Ellipse,
            Path,
            RoundedRectangle,
            TransformedGeometry,
        }

        public static CanvasGeometry CreatePath(CanvasPathBuilder pathBuilder)
            => new Path(pathBuilder);

        public static CanvasGeometry CreateRoundedRectangle(CanvasDevice device, float x, float y, float w, float h, float radiusX, float radiusY)
            => new RoundedRectangle
            {
                X = x,
                Y = y,
                W = w,
                H = h,
                RadiusX = radiusX,
                RadiusY = radiusY,
            };

        public static CanvasGeometry CreateEllipse(CanvasDevice device, float x, float y, float radiusX, float radiusY)
            => new Ellipse
            {
                X = x,
                Y = y,
                RadiusX = radiusX,
                RadiusY = radiusY,
            };

        public CanvasGeometry CombineWith(CanvasGeometry other, Matrix3x2 matrix, CanvasGeometryCombine combineMode)
         => new Combination
         {
             A = this,
             B = other,
             Matrix = matrix,
             CombineMode = combineMode,
         };

        public CanvasGeometry Transform(Matrix3x2 transformMatrix) =>
            transformMatrix.IsIdentity
            ? this
            : new TransformedGeometry
            {
                SourceGeometry = this,
                TransformMatrix = transformMatrix,
                LongDescription = LongDescription,
                ShortDescription = ShortDescription,
            };

        public abstract GeometryType Type { get; }

        /// <inheritdoc/>
        public string LongDescription { get; set; }

        /// <inheritdoc/>
        public string ShortDescription { get; set; }

        public sealed class Combination : CanvasGeometry
        {
            public CanvasGeometry A { get; internal set; }

            public CanvasGeometry B { get; internal set; }

            public Matrix3x2 Matrix { get; internal set; }

            public CanvasGeometryCombine CombineMode { get; internal set; }

            /// <inheritdoc/>
            public override GeometryType Type => GeometryType.Combination;
        }

        public sealed class Ellipse : CanvasGeometry
        {
            internal Ellipse()
            {
            }

            public float X { get; internal set; }

            public float Y { get; internal set; }

            public float RadiusX { get; internal set; }

            public float RadiusY { get; internal set; }

            /// <inheritdoc/>
            public override GeometryType Type => GeometryType.Ellipse;
        }

        public sealed class Path : CanvasGeometry, IEquatable<Path>
        {
            internal Path(CanvasPathBuilder builder)
            {
                FilledRegionDetermination = builder.FilledRegionDetermination;
                Commands = builder.Commands.ToArray();
            }

            public CanvasPathBuilder.Command[] Commands { get; }

            public CanvasFilledRegionDetermination FilledRegionDetermination { get; }

            /// <inheritdoc/>
            public override GeometryType Type => GeometryType.Path;

            /// <inheritdoc/>
            public bool Equals(Path other)
            {
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (other == null)
                {
                    return false;
                }

                if (other.FilledRegionDetermination != FilledRegionDetermination)
                {
                    return false;
                }

                if (other.Commands.Length != Commands.Length)
                {
                    return false;
                }

                for (var i = 0; i < Commands.Length; i++)
                {
                    var thisCommand = Commands[i];
                    var otherCommand = other.Commands[i];

                    if (!thisCommand.Equals(otherCommand))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                // Less than ideal but cheap hash function.
                return Commands.Length;
            }
        }

        public sealed class RoundedRectangle : CanvasGeometry
        {
            public float X { get; internal set; }

            public float Y { get; internal set; }

            public float W { get; internal set; }

            public float H { get; internal set; }

            public float RadiusX { get; internal set; }

            public float RadiusY { get; internal set; }

            /// <inheritdoc/>
            public override GeometryType Type => GeometryType.RoundedRectangle;
        }

        public sealed class TransformedGeometry : CanvasGeometry
        {
            public CanvasGeometry SourceGeometry { get; internal set; }

            public Matrix3x2 TransformMatrix { get; internal set; }

            /// <inheritdoc/>
            public override GeometryType Type => GeometryType.TransformedGeometry;
        }
    }
}
