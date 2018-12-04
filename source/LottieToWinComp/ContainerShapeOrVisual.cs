// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Helper for abstracting <see cref="CompositionContainerShape"/> and
    /// <see cref="ContainerVisual"/> so that they can be treated the same
    /// in some circumstances.
    /// </summary>
    /// <remarks>
    /// Properties that are Vector2 on CompositionContainerShape but Vector3
    /// on ContainerVisual are exposed as Vector2 and converted to Vector3
    /// as necessary.
    /// </remarks>
    abstract class ContainerShapeOrVisual
    {
        readonly CompositionObject _compositionObject;

        ContainerShapeOrVisual(CompositionObject compositionObject)
        {
            _compositionObject = compositionObject;
        }

        public abstract bool IsShape { get; }

        public CompositionPropertySet Properties => _compositionObject.Properties;

        public abstract Vector2? CenterPoint { get; set; }

        public abstract Vector2? Offset { get; set; }

        public abstract Vector2? Scale { get; set; }

        public abstract float? RotationAngleInDegrees { get; set; }

        sealed class WrappedContainerShape : ContainerShapeOrVisual
        {
            readonly CompositionContainerShape _wrapped;

            internal WrappedContainerShape(CompositionContainerShape wrapped)
                : base(wrapped)
            {
                _wrapped = wrapped;
            }

            public override bool IsShape => true;

            public override Vector2? CenterPoint
            {
                get => _wrapped.CenterPoint;
                set => _wrapped.CenterPoint = value;
            }

            public override Vector2? Offset
            {
                get => _wrapped.Offset;
                set => _wrapped.Offset = value;
            }

            public override Vector2? Scale
            {
                get => _wrapped.Scale;
                set => _wrapped.Scale = value;
            }

            public override float? RotationAngleInDegrees
            {
                get => _wrapped.RotationAngleInDegrees;
                set => _wrapped.RotationAngleInDegrees = value;
            }
        }

        sealed class WrappedContainerVisual : ContainerShapeOrVisual
        {
            readonly ContainerVisual _wrapped;

            internal WrappedContainerVisual(ContainerVisual wrapped)
                : base(wrapped)
            {
                _wrapped = wrapped;
            }

            public override bool IsShape => false;

            public override Vector2? CenterPoint
            {
                get => Vector2(_wrapped.CenterPoint);
                set => _wrapped.CenterPoint = Vector3(value);
            }

            public override Vector2? Offset
            {
                get => Vector2(_wrapped.Offset);
                set => _wrapped.Offset = Vector3(value);
            }

            public override Vector2? Scale
            {
                get => Vector2(_wrapped.Scale);
                set => _wrapped.Scale = Vector3(value);
            }

            public override float? RotationAngleInDegrees
            {
                get => _wrapped.RotationAngleInDegrees;
                set => _wrapped.RotationAngleInDegrees = value;
            }
        }

        public static implicit operator ContainerShapeOrVisual(CompositionContainerShape shape) => new WrappedContainerShape(shape);

        public static implicit operator ContainerShapeOrVisual(ContainerVisual visual) => new WrappedContainerVisual(visual);

        public static implicit operator CompositionObject(ContainerShapeOrVisual containerShapeOrVisual) => containerShapeOrVisual._compositionObject;

        static Vector2? Vector2(Vector3? value) => value.HasValue ? new Vector2(value.Value.X, value.Value.Y) : default(Vector2?);

        static Vector3? Vector3(Vector2? value) => value.HasValue ? new Vector3(value.Value.X, value.Value.Y, 0) : default(Vector3?);
    }
}