// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// Determines the relative precedence of an operation in the string
    /// form of an expression. For example, multiplication has higher
    /// precedence than addition.
    /// </summary>
    enum Precedence : uint
    {
        Unknown = 0,
        Subtraction,
        Addition,
        Multiplication,
        Division,
        Atomic,
    }
}