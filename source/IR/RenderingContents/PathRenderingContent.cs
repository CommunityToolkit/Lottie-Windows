// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    abstract class PathRenderingContent : RenderingContent
    {
        PathRenderingContent()
        {
        }

        public static PathRenderingContent Create(Animatable<PathGeometry> geometry)
            => geometry.IsAnimated ? new Animated(geometry) : new Static(geometry.InitialValue);

        public sealed class Animated : PathRenderingContent
        {
            internal Animated(Animatable<PathGeometry> geometry)
            {
                Geometry = geometry;
            }

            public Animatable<PathGeometry> Geometry { get; }

            public override bool IsAnimated => true;

            public override RenderingContent WithTimeOffset(double timeOffset)
                => new Animated(Geometry.WithTimeOffset(timeOffset));

            public override string ToString() => $"Animated Path";
        }

        public sealed class Static : PathRenderingContent
        {
            internal Static(PathGeometry geometry)
            {
                Geometry = geometry;
            }

            public PathGeometry Geometry { get; }

            public override bool IsAnimated => false;

            public override RenderingContent WithTimeOffset(double timeOffset) => this;

            public override string ToString() => $"Static Path";
        }
    }
}