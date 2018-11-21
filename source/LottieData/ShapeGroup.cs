// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class ShapeGroup : ShapeLayerContent
    {
        public ShapeGroup(
            string name,
            string matchName,
            IEnumerable<ShapeLayerContent> items)
            : base(name, matchName)
        {
            Items = items;
        }

        public IEnumerable<ShapeLayerContent> Items { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Group;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.ShapeGroup;
    }
}
