// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    /// <summary>
    /// Helper for abstracting <see cref="CompositionRectangleGeometry"/> and
    /// <see cref="CompositionRoundedRectangleGeometry"/> so that they can be
    /// treated the same in some circumstances.
    /// </summary>
    abstract class RectangleOrRoundedRectangleGeometry
    {
        readonly CompositionGeometry _compositionGeometry;

        RectangleOrRoundedRectangleGeometry(CompositionGeometry compositionGeometry)
        {
            _compositionGeometry = compositionGeometry;
        }

        public abstract bool IsRoundedRectangle { get; }

        public CompositionPropertySet Properties => _compositionGeometry.Properties;

        public abstract Vector2? Offset { get; set; }

        public abstract Vector2? Size { get; set; }

        sealed class WrappedRectangleGeometry : RectangleOrRoundedRectangleGeometry
        {
            readonly CompositionRectangleGeometry _wrapped;

            internal WrappedRectangleGeometry(CompositionRectangleGeometry wrapped)
                : base(wrapped)
            {
                _wrapped = wrapped;
            }

            public override bool IsRoundedRectangle => false;

            public override Vector2? Offset { get => _wrapped.Offset; set => _wrapped.Offset = value; }

            public override Vector2? Size { get => _wrapped.Size; set => _wrapped.Size = value; }
        }

        sealed class WrappedRoundedRectangleGeometry : RectangleOrRoundedRectangleGeometry
        {
            readonly CompositionRoundedRectangleGeometry _wrapped;

            internal WrappedRoundedRectangleGeometry(CompositionRoundedRectangleGeometry wrapped)
                : base(wrapped)
            {
                _wrapped = wrapped;
            }

            public override bool IsRoundedRectangle => true;

            public override Vector2? Offset { get => _wrapped.Offset; set => _wrapped.Offset = value; }

            public override Vector2? Size { get => _wrapped.Size; set => _wrapped.Size = value; }
        }

        public static implicit operator RectangleOrRoundedRectangleGeometry(CompositionRectangleGeometry rectangle) => new WrappedRectangleGeometry(rectangle);

        public static implicit operator RectangleOrRoundedRectangleGeometry(CompositionRoundedRectangleGeometry roundedRectangle) => new WrappedRoundedRectangleGeometry(roundedRectangle);

        public static implicit operator CompositionGeometry(RectangleOrRoundedRectangleGeometry rectangle) => rectangle._compositionGeometry;
    }
}
