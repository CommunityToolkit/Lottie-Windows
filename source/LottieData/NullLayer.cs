// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
#if PUBLIC_LottieData
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

        public override Layer WithIndicesChanged(int index, int? parentIndex = null)
        {
            return new NullLayer(GetArgsWithIndicesChanged(index, parentIndex));
        }

        public override Layer WithTimeOffset(double shiftFrames)
        {
            return new NullLayer(GetArgsWithTimeOffset(shiftFrames));
        }
    }
}
