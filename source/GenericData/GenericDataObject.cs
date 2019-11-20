﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.GenericData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class GenericDataObject
    {
        public abstract GenericDataObjectType Type { get; }

        // Converts an object to a string. This method exists to support
        // stringifying of objects that may be null.
        internal static string ToString(GenericDataObject obj)
            => obj is null ? "null" : obj.ToString();
    }
}