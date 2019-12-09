// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    enum CompositionGetValueStatus
    {
        /// <summary>
        /// The value was successfully retrieved.
        /// </summary>
        Succeeded = 0,

        /// <summary>
        /// The value type of the key-value pair is different from the value type requested.
        /// </summary>
        TypeMismatch = 1,

        /// <summary>
        /// The key-value pair does not exist.
        /// </summary>
        NotFound = 2,
    }
}
