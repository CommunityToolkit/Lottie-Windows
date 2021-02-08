// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeful
{
#if PUBLIC_IR
    public
#endif
    sealed class NullLayer : Layer
    {
        public NullLayer(in LayerArgs args)
            : base(in args)
        {
        }

        /// <inheritdoc/>
        public override LayerType Type => LayerType.Null;
    }
}
