// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class ShapeLayer : Layer
    {
        readonly ShapeLayerContent[] _shapes;

        public ShapeLayer(
            in LayerArgs args,
            IEnumerable<ShapeLayerContent> shapes)
         : base(in args)
        {
            _shapes = shapes.ToArray();
        }

        public ReadOnlySpan<ShapeLayerContent> Contents => _shapes;

        /// <inheritdoc/>
        public override LayerType Type => LayerType.Shape;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.ShapeLayer;
    }
}
