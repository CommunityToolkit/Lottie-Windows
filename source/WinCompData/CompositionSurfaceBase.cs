// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    /// <summary>
    /// This is the base for Composition Surface objects and ensures that
    /// all Composition Surface objects are also CompositionObjects. Composition
    /// Surface objects use this as a base instead of an interface to avoid
    /// dynamic type casting.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionSurfaceBase : CompositionObject
    {
    }
}
