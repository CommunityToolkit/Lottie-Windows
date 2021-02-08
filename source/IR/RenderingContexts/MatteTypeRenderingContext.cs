﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class MatteTypeRenderingContext : RenderingContext
    {
        public MatteTypeRenderingContext(MatteType matteType)
            => MatteType = matteType;

        public MatteType MatteType { get; }

        public override bool IsAnimated => false;

        public override RenderingContext WithTimeOffset(double timeOffset) => this;

        public override string ToString() => $"MatteType {MatteType}";
    }
}