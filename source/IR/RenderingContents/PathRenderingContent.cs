// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    abstract class PathRenderingContent :
        RenderingContent,
        IEquatable<PathRenderingContent>
    {
        PathRenderingContent()
        {
        }

        public abstract int BezierSegmentCount { get; }

        public static PathRenderingContent Create(Animatable<PathGeometry> geometry)
            => geometry.IsAnimated ? new Animated(geometry) : new Static(geometry.InitialValue);

        public abstract bool Equals([AllowNull] PathRenderingContent other);

        public sealed class Animated : PathRenderingContent
        {
            internal Animated(Animatable<PathGeometry> geometry)
            {
                Geometry = geometry;
            }

            public Animatable<PathGeometry> Geometry { get; }

            public override bool IsAnimated => true;

            public override int BezierSegmentCount => Geometry.InitialValue.BezierSegments.Count;

            public override RenderingContent WithScale(Vector2 scale) => throw new System.NotImplementedException();

            public override RenderingContent WithTimeOffset(double timeOffset)
                => new Animated(Geometry.WithTimeOffset(timeOffset));

            public override string ToString() => $"Animated Path";

            public override bool Equals([AllowNull] PathRenderingContent other)
            {
                if (other is Animated otherAnimated)
                {
                    // TODO - Animatable<T> is not equatable - it needs to be for this to work.
                    return otherAnimated.Geometry.Equals(Geometry);
                }

                return false;
            }
        }

        public sealed class Static : PathRenderingContent
        {
            internal Static(PathGeometry geometry)
            {
                Geometry = geometry;
            }

            public PathGeometry Geometry { get; }

            public override bool IsAnimated => false;

            public override int BezierSegmentCount => Geometry.BezierSegments.Count;

            public override RenderingContent WithScale(Vector2 scale)
                 => new Static(Geometry.WithScale(scale));

            public override RenderingContent WithTimeOffset(double timeOffset) => this;

            public override string ToString() => $"Static Path";

            public override bool Equals([AllowNull] PathRenderingContent other)
            {
                if (other is Static otherStatic)
                {
                    return otherStatic.Geometry.Equals(Geometry);
                }

                return false;
            }
        }
    }
}