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
    sealed class ShapeGroup : ShapeLayerContent
    {
        readonly ShapeLayerContent[] _items;

        public ShapeGroup(
            in ShapeLayerContentArgs args,
            IEnumerable<ShapeLayerContent> items)
            : base(in args)
        {
            _items = items.ToArray();
        }

        public ReadOnlySpan<ShapeLayerContent> Items => _items;

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Group;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.ShapeGroup;
    }
}
