// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.WinCompData.Mgce
{
    /// <summary>
    /// This class is the base type for effects. It is used to ensure
    /// that all effects have a known type.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    abstract class GraphicsEffectBase
    {
        public abstract IReadOnlyList<CompositionEffectSourceParameter> Sources { get; }

        public abstract GraphicsEffectType Type { get; }
    }
}
