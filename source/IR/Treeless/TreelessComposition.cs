// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless
{
#if PUBLIC_IR
    public
#endif
    sealed class TreelessComposition
    {
        readonly IRComposition _sourceComposition;
        readonly IReadOnlyList<(RenderingContext, TreelessLayer)> _layers;

        public TreelessComposition(
            IRComposition sourceComposition,
            IReadOnlyList<(RenderingContext, TreelessLayer)> layers)
        {
            _sourceComposition = sourceComposition;
            _layers = layers;
        }
    }
}
