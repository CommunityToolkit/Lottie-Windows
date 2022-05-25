// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// An expression that has no type. The type is used for generic type
    /// parameters to indicate that no expression can satisfy the type.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    abstract class Void : Expression_<Void>
    {
        // Abstract class with private constructor and no nested classes means
        // this class can never be instantiated.
        Void()
        {
        }

        /// <inheritdoc/>
        public override sealed ExpressionType Type => ExpressionType.Void;
    }
}
