﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    sealed class RectangleRenderingContent : RenderingContent
    {
        internal RectangleRenderingContent(
            IAnimatableVector3 size,
            Animatable<double> roundness)
        {
            Size = size;
            Roundness = roundness;
        }

        public Animatable<double> Roundness { get; }

        public IAnimatableVector3 Size { get; }

        public override string ToString() => $"Rectangle";
    }
}