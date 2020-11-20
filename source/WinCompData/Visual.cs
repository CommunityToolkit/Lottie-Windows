// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class Visual : CompositionObject
    {
        private protected Visual()
        {
        }

        // Defaults to Inherit.
        // Note that trees rooted by the desktop have Soft passed into them so
        // they inherit Soft unless overridden.
        // Non-rooted trees have Hard passed into them so they inherit Hard unless
        // overridden.
        public CompositionBorderMode? BorderMode { get; set; }

        // Defaults to Vector3.Zero.
        public Vector3? CenterPoint { get; set; }

        public CompositionClip? Clip { get; set; }

        // Defaults to true.
        public bool? IsVisible { get; set; }

        // Defaults to Vector3.Zero.
        public Vector3? Offset { get; set; }

        // Defaults to 1.
        public float? Opacity { get; set; }

        // Defaults to 0.
        public float? RotationAngleInDegrees { get; set; }

        // Defaults to Vector3.UnitZ.
        public Vector3? RotationAxis { get; set; }

        // Defaults to Vector3.One.
        public Vector3? Scale { get; set; }

        // Defaults to Vector2.Zero.
        public Vector2? Size { get; set; }

        // Defaults to Matrix4x4.Identity.
        public Matrix4x4? TransformMatrix { get; set; }
    }
}
