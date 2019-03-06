// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionEffectSourceParameter
    {
        internal CompositionEffectSourceParameter(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
