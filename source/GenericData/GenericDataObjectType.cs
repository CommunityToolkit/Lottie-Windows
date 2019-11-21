// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.GenericData
{
#if PUBLIC_LottieData
    public
#endif
    enum GenericDataObjectType
    {
        Unknown = 0,
        Bool,
        List,
        Map,
        Number,
        String,
    }
}