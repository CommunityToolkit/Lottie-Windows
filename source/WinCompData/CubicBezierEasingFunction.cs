// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CubicBezierEasingFunction : CompositionEasingFunction
    {
        internal CubicBezierEasingFunction(Vector2 controlPoint1, Vector2 controlPoint2)
        {
            ControlPoint1 = controlPoint1;
            ControlPoint2 = controlPoint2;
        }

        public Vector2 ControlPoint1 { get; }

        public Vector2 ControlPoint2 { get; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CubicBezierEasingFunction;
    }
}
