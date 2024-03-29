// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class LinearEasingFunction : CompositionEasingFunction
    {
        internal LinearEasingFunction()
        {
        }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.LinearEasingFunction;
    }
}
