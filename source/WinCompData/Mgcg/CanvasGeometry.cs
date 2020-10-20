// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgc;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wg;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class CanvasGeometry : IGeometrySource2D, IDescribable, IEquatable<CanvasGeometry>
    {
        CanvasGeometry()
        {
        }

        public abstract bool Equals(CanvasGeometry? other);

        public static CanvasGeometry CreateGroup(
            CanvasDevice? device,
            CanvasGeometry[] geometries,
            CanvasFilledRegionDetermination filledRegionDetermination)
            => new Group(geometries)
            {
                FilledRegionDetermination = filledRegionDetermination,
            };

        public static CanvasGeometry CreatePath(CanvasPathBuilder pathBuilder)
            => new Path(pathBuilder);

        public static CanvasGeometry CreateRoundedRectangle(CanvasDevice? device, float x, float y, float w, float h, float radiusX, float radiusY)
            => new RoundedRectangle
            {
                X = x,
                Y = y,
                W = w,
                H = h,
                RadiusX = radiusX,
                RadiusY = radiusY,
            };

        public static CanvasGeometry CreateEllipse(CanvasDevice? device, float x, float y, float radiusX, float radiusY)
            => new Ellipse
            {
                X = x,
                Y = y,
                RadiusX = radiusX,
                RadiusY = radiusY,
            };

        public CanvasGeometry CombineWith(CanvasGeometry other, Matrix3x2 matrix, CanvasGeometryCombine combineMode)
         => new Combination(this, other)
         {
             Matrix = matrix,
             CombineMode = combineMode,
         };

        public CanvasGeometry Transform(Matrix3x2 transformMatrix) =>
            transformMatrix.IsIdentity
            ? this
            : new TransformedGeometry(this)
            {
                TransformMatrix = transformMatrix,
            };

        public abstract GeometryType Type { get; }

        /// <inheritdoc/>
        string? IDescribable.LongDescription { get; set; }

        /// <inheritdoc/>
        string? IDescribable.ShortDescription { get; set; }

        /// <inheritdoc/>
        string? IDescribable.Name { get; set; }

        /// <summary>
        /// The type of a <see cref="CanvasGeometry"/>.
        /// </summary>
        public enum GeometryType
        {
            Combination,
            Ellipse,
            Group,
            Path,
            RoundedRectangle,
            TransformedGeometry,
        }

        public sealed class Combination : CanvasGeometry, IEquatable<Combination>
        {
            internal Combination(CanvasGeometry a, CanvasGeometry b) => (A, B) = (a, b);

            public CanvasGeometry A { get; }

            public CanvasGeometry B { get; }

            public Matrix3x2 Matrix { get; internal set; }

            public CanvasGeometryCombine CombineMode { get; internal set; }

            /// <inheritdoc/>
            public override GeometryType Type => GeometryType.Combination;

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                return A.GetHashCode() ^ B.GetHashCode();
            }

            /// <inheritdoc/>
            public override bool Equals(CanvasGeometry? other) => Equals(other as Combination);

            public bool Equals(Combination? other)
            {
                return
                    other != null &&
                    CombineMode == other.CombineMode &&
                    Matrix == other.Matrix &&
                    A.Equals(B);
            }
        }

        public sealed class Ellipse : CanvasGeometry, IEquatable<Ellipse>
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

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                var hashHelper = default(HashHelper);

                return
                    hashHelper.GetIntBits(X) ^
                    hashHelper.GetIntBits(Y) ^
                    hashHelper.GetIntBits(RadiusX) ^
                    hashHelper.GetIntBits(RadiusY);
            }

            public bool Equals(Ellipse? other)
            {
                return
                    other != null &&
                    X == other.X &&
                    Y == other.Y &&
                    RadiusX == other.RadiusX &&
                    RadiusY == other.RadiusY;
            }

            /// <inheritdoc/>
            public override bool Equals(CanvasGeometry? other) => Equals(other as Ellipse);
        }

        public sealed class Group : CanvasGeometry, IEquatable<Group>
        {
            internal Group(CanvasGeometry[] geometries) => Geometries = geometries;

            public CanvasGeometry[] Geometries { get; }

            public CanvasFilledRegionDetermination FilledRegionDetermination { get; internal set; }

            /// <inheritdoc/>
            public override GeometryType Type => GeometryType.Group;

            public bool Equals(Group? other)
            {
                if (other is null || other.Geometries.Length != Geometries.Length)
                {
                    return false;
                }

                for (var i = 0; i < Geometries.Length; i++)
                {
                    if (!Geometries[i].Equals(other.Geometries[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <inheritdoc/>
            public override bool Equals(CanvasGeometry? other) => Equals(other as Group);

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                var result = 0;
                for (var i = 0; i < Geometries.Length; i++)
                {
                    result ^= Geometries[i].GetHashCode();
                }

                return result;
            }
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
            public bool Equals(Path? other)
            {
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (other is null)
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
            public override bool Equals(CanvasGeometry? other) => Equals(other as Path);

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                // Less than ideal but cheap hash function.
                return Commands.Length;
            }
        }

        public sealed class RoundedRectangle : CanvasGeometry, IEquatable<RoundedRectangle>
        {
            public float X { get; internal set; }

            public float Y { get; internal set; }

            public float W { get; internal set; }

            public float H { get; internal set; }

            public float RadiusX { get; internal set; }

            public float RadiusY { get; internal set; }

            /// <inheritdoc/>
            public override GeometryType Type => GeometryType.RoundedRectangle;

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                var hashHelper = default(HashHelper);
                return
                    hashHelper.GetIntBits(X) ^
                    hashHelper.GetIntBits(Y) ^
                    hashHelper.GetIntBits(W) ^
                    hashHelper.GetIntBits(H) ^
                    hashHelper.GetIntBits(RadiusX) ^
                    hashHelper.GetIntBits(RadiusY);
            }

            public bool Equals(RoundedRectangle? other)
            {
                return
                    other != null &&
                    X == other.X &&
                    Y == other.Y &&
                    W == other.W &&
                    H == other.H &&
                    RadiusX == other.RadiusX &&
                    RadiusY == other.RadiusY;
            }

            /// <inheritdoc/>
            public override bool Equals(CanvasGeometry? other) => Equals(other as RoundedRectangle);
        }

        public sealed class TransformedGeometry : CanvasGeometry, IEquatable<TransformedGeometry>
        {
            internal TransformedGeometry(CanvasGeometry sourceGeometry) => SourceGeometry = sourceGeometry;

            public CanvasGeometry SourceGeometry { get; }

            public Matrix3x2 TransformMatrix { get; internal set; }

            /// <inheritdoc/>
            public override GeometryType Type => GeometryType.TransformedGeometry;

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                return SourceGeometry.GetHashCode();
            }

            public bool Equals(TransformedGeometry? other)
            {
                return
                    other != null &&
                    SourceGeometry.Equals(other.SourceGeometry) &&
                    TransformMatrix == other.TransformMatrix;
            }

            /// <inheritdoc/>
            public override bool Equals(CanvasGeometry? other) => Equals(other as TransformedGeometry);
        }

        [StructLayout(LayoutKind.Explicit)]
        struct HashHelper
        {
            [FieldOffset(0)]
            readonly int _intValue;

            [FieldOffset(0)]
            float _floatValue;

            public int GetIntBits(float value)
            {
                _floatValue = value;
                return _intValue;
            }
        }
    }
}
