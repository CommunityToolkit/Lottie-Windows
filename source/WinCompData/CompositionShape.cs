// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(6)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionShape : CompositionObject
    {
        private protected CompositionShape()
        {
        }

        // Default is 0, 0.
        public Vector2? CenterPoint { get; set; }

        // Default is 0, 0.
        public Vector2? Offset { get; set; }

        // Default is 0.
        public float? RotationAngleInDegrees { get; set; }

        // Default is 1, 1.
        public Vector2? Scale { get; set; }

        public Matrix3x2? TransformMatrix { get; set; }
    }
}
