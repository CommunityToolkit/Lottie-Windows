// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable // Temporary while enabling nullable everywhere.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// A sequence of <see cref="BezierSegment"/>s that describes the shape of a path.
    /// </summary>
#if PUBLIC_LottieData
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
        /// An empty <see cref="PathGeometry"/>.
        /// </summary>
        public static PathGeometry Empty { get; } = new PathGeometry(Sequence<BezierSegment>.Empty, false);

        public bool Equals(PathGeometry other) =>
            other != null && other.IsClosed == IsClosed && other.BezierSegments.Equals(BezierSegments);

        public override bool Equals(object obj) => Equals(obj as PathGeometry);

        public override int GetHashCode() => BezierSegments.GetHashCode();
    }
}
