// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
    /// <summary>
    /// A sequence of <see cref="BezierSegment"/>s that describes the shape of a path.
    /// </summary>
#if PUBLIC_Animatables
    public
#endif
    sealed class PathGeometry : IEquatable<PathGeometry>
    {
        public PathGeometry(Sequence<BezierSegment> bezierSegments, bool isClosed)
        {
            BezierSegments = bezierSegments;
            IsClosed = isClosed;
        }

        /// <summary>
        /// Indicates whether the path is closed. A closed path will use mitering to join
        /// the final segment end back to the start, synthesizing a segment if necessary,
        /// whereas an open path will never synthesize an extra segment and will use end caps.
        /// </summary>
        public bool IsClosed { get; }

        /// <summary>
        /// The segments that describe the path.
        /// </summary>
        public Sequence<BezierSegment> BezierSegments { get; }

        /// <summary>
        /// Returns the value of the smallest X and Y values of the vertices in the
        /// geometry. Note that this is not the same as the corner of a bounding box
        /// because a segment may curve to values that are smaller than these
        /// between vertices.
        /// </summary>
        /// <returns>The minimum X and Y values.</returns>
        public Vector2 GetMinimumXandY()
        {
            var smallestX = BezierSegments.Min(seg => Math.Min(seg.ControlPoint0.X, seg.ControlPoint3.X));
            var smallestY = BezierSegments.Min(seg => Math.Min(seg.ControlPoint0.Y, seg.ControlPoint3.Y));
            return new Vector2(smallestX, smallestY);
        }

        /// <summary>
        /// Returns the value of the largest X and Y values of the vertices in the
        /// geometry. Note that this is not the same as the corner of a bounding box
        /// because a segment may curve to values that are smaller than these
        /// between vertices.
        /// </summary>
        /// <returns>The maximum X and Y values.</returns>
        public Vector2 GetMaximumXandY()
        {
            var largestX = BezierSegments.Max(seg => Math.Max(seg.ControlPoint0.X, seg.ControlPoint3.X));
            var largestY = BezierSegments.Max(seg => Math.Max(seg.ControlPoint0.Y, seg.ControlPoint3.Y));
            return new Vector2(largestX, largestY);
        }

        public PathGeometry WithScale(Vector2 scale)
            => scale == Vector2.One
                ? this
                : new PathGeometry(
                    new Sequence<BezierSegment>(
                        BezierSegments.Select(seg => seg.WithScale(scale))),
                    IsClosed);

        public PathGeometry WithOffset(Vector2 offset)
            => offset == Vector2.Zero
                ? this
                : new PathGeometry(
                    new Sequence<BezierSegment>(
                        BezierSegments.Select(seg => seg.WithOffset(offset))),
                    IsClosed);

        /// <summary>
        /// An empty <see cref="PathGeometry"/>.
        /// </summary>
        public static PathGeometry Empty { get; } = new PathGeometry(Sequence<BezierSegment>.Empty, false);

        public bool Equals(PathGeometry? other) =>
            other != null && other.IsClosed == IsClosed && other.BezierSegments.Equals(BezierSegments);

        public override bool Equals(object? obj) => Equals(obj as PathGeometry);

        public override int GetHashCode() => BezierSegments.GetHashCode();

        public override string ToString()
        {
            var approximateSize = GetMaximumXandY() - GetMinimumXandY();
            return $"Path with {BezierSegments.Count} segments. Approx: {approximateSize.X}x{approximateSize.Y}";
        }
    }
}
