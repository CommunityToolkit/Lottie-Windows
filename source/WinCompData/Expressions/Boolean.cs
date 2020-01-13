// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// A literal boolean, i.e. "true" or "false".
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    abstract class Boolean : Expression_<Boolean>
    {
        /// <inheritdoc/>
        public override sealed ExpressionType Type => ExpressionType.Boolean;

        // Allow any bool to be treated as a Boolean.Literal.
        public static implicit operator Boolean(bool value) => value ? True : False;

        public static Boolean True { get; } = new Literal(true);

        public static Boolean False { get; } = new Literal(false);

        internal sealed class Asserted : Boolean
        {
            readonly string _text;

            public Asserted(string text)
            {
                _text = text;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText() => _text;

            protected override bool IsAtomic => true;
        }

        internal abstract class BinaryScalarExpression : Boolean
        {
            internal BinaryScalarExpression(Scalar left, Scalar right)
            {
                Left = left;
                Right = right;
            }

            internal Scalar Left { get; }

            internal Scalar Right { get; }
        }

        internal sealed new class LessThan : BinaryScalarExpression
        {
            public LessThan(Scalar left, Scalar right)
                : base(left, right)
            {
            }

            /// <inheritdoc/>
            protected override Boolean Simplify()
            {
                var left = Left.Simplified;
                var right = Right.Simplified;

                if (left is Scalar.Literal literalLeft && right is Scalar.Literal literalRight)
                {
                    // They're both constants. Evaluate them.
                    return new Literal(literalLeft.Value < literalRight.Value);
                }

                return left != Left || right != Right
                    ? new LessThan(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"{Parenthesize(Left)} < {Parenthesize(Right)}";
        }

        sealed class Literal : Boolean
        {
            public bool Value { get; }

            public Literal(bool value)
            {
                Value = value;
            }

            /// <inheritdoc/>
            protected override Boolean Simplify() => Value ? True : False;

            /// <inheritdoc/>
            protected override string CreateExpressionText() => Value ? "true" : "false";

            protected override bool IsAtomic => true;
        }
    }
}
