// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    sealed class PathRenderingContent : RenderingContent
    {
        internal PathRenderingContent(Animatable<PathGeometry> geometry)
        {
            Geometry = geometry;
        }

        public Animatable<PathGeometry> Geometry { get; }

        public override string ToString() => $"Path";
    }
}