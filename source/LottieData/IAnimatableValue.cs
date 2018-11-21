// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    interface IAnimatableValue<T>
    {
        /// <summary>
        /// Gets the initial value.
        /// </summary>
        T InitialValue { get; }

        /// <summary>
        /// Gets a value indicating whether the value is animated.
        /// </summary>
        bool IsAnimated { get; }
    }
}
