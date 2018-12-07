// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class BinaryExpression : Expression
    {
        public Expression Left { get; }

        public Expression Right { get; }

        private protected BinaryExpression(Expression left, Expression right)
        {
            Left = left;
            Right = right;
        }
    }
}
