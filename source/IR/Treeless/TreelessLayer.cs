// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeful;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless
{
    /// <summary>
    /// Base class for layer objects.
    /// </summary>
    /// <remarks>
    /// Each <see cref="Layer"/>, apart from the root <see cref="PreCompLayer"/> belongs to a <see cref="PreCompLayer"/> and has
    /// an index that determines its rendering order, and is also used to identify it as the owner of a set of transforms that
    /// can be inherited by other <see cref="Layer"/>s.</remarks>
#if PUBLIC_IR
    public
#endif
    abstract class TreelessLayer
    {
        private protected TreelessLayer(
            BlendMode blendMode,
            bool is3d,
            MatteType matteType,
            IReadOnlyList<Mask> masks)
        {
            BlendMode = blendMode;
            Is3d = is3d;
            MatteType = matteType;
            Masks = masks;
        }

        public BlendMode BlendMode { get; }

        public bool Is3d { get; }

        public MatteType MatteType { get; }

        /// <summary>
        /// Gets the list of masks appplied to the layer.
        /// </summary>
        public IReadOnlyList<Mask> Masks { get; set; }

        public abstract TreelessLayerType Type { get; }
    }
}
