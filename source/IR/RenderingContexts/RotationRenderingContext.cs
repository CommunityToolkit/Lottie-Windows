// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class RotationRenderingContext : RenderingContext
    {
        internal RotationRenderingContext(Animatable<Rotation> rotation)
            => Rotation = rotation;

        public Animatable<Rotation> Rotation { get; }

        public override string ToString() => $"Rotation {Rotation}";
    }
}
