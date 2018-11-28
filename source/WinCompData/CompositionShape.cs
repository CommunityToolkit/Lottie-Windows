// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if !WINDOWS_UWP
    public
#endif
    abstract class CompositionShape : CompositionObject
    {
        protected private CompositionShape()
        {
        }

        public Vector2? CenterPoint { get; set; }

        public Vector2? Offset { get; set; }

        public float? RotationAngleInDegrees { get; set; }

        public Vector2? Scale { get; set; }

        public Matrix3x2? TransformMatrix { get; set; }
    }
}
