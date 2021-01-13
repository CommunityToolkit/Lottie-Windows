// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Layers
{
#if PUBLIC_IR
    public
#endif
    sealed class ShapeLayer : Layer
    {
        readonly ShapeLayerContent[] _contents;

        public ShapeLayer(
            in LayerArgs args,
            IEnumerable<ShapeLayerContent> contents)
         : base(in args)
        {
            _contents = contents.ToArray();
        }

        public IReadOnlyList<ShapeLayerContent> Contents => _contents;

        /// <inheritdoc/>
        public override LayerType Type => LayerType.Shape;
    }
}
