// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class ShapeGroup : ShapeLayerContent
    {
        readonly ShapeLayerContent[] _contents;

        public ShapeGroup(
            in ShapeLayerContentArgs args,
            IEnumerable<ShapeLayerContent> contents)
            : base(in args)
        {
            _contents = contents.ToArray();
        }

        public IReadOnlyList<ShapeLayerContent> Contents => _contents;

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Group;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.ShapeGroup;
    }
}
