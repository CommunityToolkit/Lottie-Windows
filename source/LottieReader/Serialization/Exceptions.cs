﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace CommunityToolkit.WinUI.Lottie.LottieData.Serialization
{
    static class Exceptions
    {
        // The code we hit is supposed to be unreachable. This indicates a bug.
        internal static Exception Unreachable => new InvalidOperationException("Unreachable code executed.");
    }
}