// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    /// <summary>
    /// Copies shapes and applies a transform to the copies.
    /// </summary>
#if PUBLIC_IR
    public
#endif
    sealed class Repeater : ShapeLayerContent
    {
        public Repeater(
            in ShapeLayerContentArgs args,
            Animatable<double> count,
            Animatable<double> offset,
            RepeaterTransform transform)
            : base(in args)
        {
            Count = count;
            Offset = offset;
            Transform = transform;
        }

        /// <summary>
        /// Gets the number of copies to make.
        /// </summary>
        public Animatable<double> Count { get; }

        /// <summary>
        /// Gets the offset of each copy.
        /// </summary>
        public Animatable<double> Offset { get; }

        /// <summary>
        /// Gets the transform to apply. The transform is applied n times to the n-th copy.
        /// </summary>
        public RepeaterTransform Transform { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Repeater;
    }
}
