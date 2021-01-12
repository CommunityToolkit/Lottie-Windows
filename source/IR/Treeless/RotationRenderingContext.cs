// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless
{
    sealed class RotationRenderingContext : RenderingContext
    {
        public Animatable<Rotation>? Rotation { get; set; }

        public override string ToString() => $"Rotation {Rotation}";
    }
}
