// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    /// <summary>
    /// Abstract base class for <see cref="ShapeLayerContent"/> that
    /// can be stroked and filled.
    /// </summary>
#if PUBLIC_IR
    public
#endif
    abstract class Shape : ShapeLayerContent
    {
        private protected Shape(
            in ShapeLayerContentArgs args,
            DrawingDirection drawingDirection)
            : base(args)
        {
            DrawingDirection = drawingDirection;
        }

        public DrawingDirection DrawingDirection { get; }

        public abstract ShapeType ShapeType { get; }
    }
}
