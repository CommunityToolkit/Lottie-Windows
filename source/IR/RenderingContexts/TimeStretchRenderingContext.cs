// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class TimeStretchRenderingContext : RenderingContext
    {
        internal TimeStretchRenderingContext(double stretch) => Stretch = stretch;

        public double Stretch { get; }

        public override string ToString() => $"Time-stretch: {Stretch}";
    }
}