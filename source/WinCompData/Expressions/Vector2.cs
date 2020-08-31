// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class Vector2 : Expression_<Vector2>
    {
        protected Vector2()
        {
        }

        public virtual Scalar X => Channel("X");

        public virtual Scalar Y => Channel("Y");

        public static Vector2 operator -(Vector2 left, Vector2 right) => new Subtract(left, right);

        public static Vector2 operator +(Vector2 left, Vector2 right) => new Add(left, right);

        public static Vector2 operator *(Scalar left, Vector2 right) => new ScalarMultiply(left, right);

        public static Vector2 operator /(Vector2 left, Vector2 right) => new Divide(left, right);

        /// <inheritdoc/>
        public override sealed ExpressionType Type => ExpressionType.Vector2;

        internal static Vector2 Zero { get; } = new Constructed(Expressions.Scalar.Zero, Expressions.Scalar.Zero);

        internal static Vector2 One { get; } = new Constructed(Expressions.Scalar.One, Expressions.Scalar.One);

        internal bool IsZero => Simplified is Constructed constructed && constructed.X.IsZero && constructed.Y.IsZero;

        internal bool IsOne => Simplified is Constructed constructed && constructed.X.IsOne && constructed.Y.IsOne;

        internal sealed class Add : BinaryExpression
        {
            internal Add(Vector2 left, Vector2 right)
                : base(left, right)
            {
            }

            /// <inheritdoc/>
            protected override Vector2 Simplify()
            {
                var left = Left.Simplified;
                var right = Right.Simplified;

                if (left.IsZero)
                {
                    return right;
                }

                if (right.IsZero)
                {
                    return left;
                }

                return left != Left || right != Right
                    ? new Add(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
            {
                var left = Left is Add ? Left.ToText() : Parenthesize(Left);
                var right = Right is Add ? Right.ToText() : Parenthesize(Right);
                return $"{left}+{right}";
            }
        }

        internal sealed class Asserted : Vector2
        {
            readonly string _text;

            public Asserted(string text)
            {
                _text = text;
            }

            /// <inheritdoc/>
            protected override bool IsAtomic => true;

            /// <inheritdoc/>
            // We don't actually know the operation count because the text could
            // be an expression. We just assume that it doesn't involve an expression.
            public override int OperationsCount => 0;

            /// <inheritdoc/>
            protected override string CreateExpressionText() => _text;
        }

        internal abstract class BinaryExpression : Vector2
        {
            internal BinaryExpression(Vector2 left, Vector2 right)
            {
                Left = left;
                Right = right;
            }

            public Vector2 Left { get; }

            public Vector2 Right { get; }

            /// <inheritdoc/>
            public override int OperationsCount => Left.OperationsCount + Right.OperationsCount;
        }

        internal sealed class Constructed : Vector2
        {
            internal Constructed(Scalar x, Scalar y)
            {
                X = x;
                Y = y;
            }

            public override Scalar X { get; }

            public override Scalar Y { get; }

            /// <inheritdoc/>
            public override int OperationsCount => X.OperationsCount + Y.OperationsCount;

            /// <inheritdoc/>
            protected override Vector2 Simplify()
            {
                var x = X.Simplified;
                var y = Y.Simplified;

                return x != X || y != Y
                    ? new Constructed(x, y)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"Vector2({X},{Y})";

            protected override bool IsAtomic => true;
        }

        internal sealed class Divide : BinaryExpression
        {
            internal Divide(Vector2 left, Vector2 right)
                : base(left, right)
            {
            }

            /// <inheritdoc/>
            protected override Vector2 Simplify()
            {
                var left = Left.Simplified;
                var right = Right.Simplified;

                if (right.IsZero)
                {
                    throw new InvalidOperationException();
                }

                if (left.IsZero)
                {
                    return left;
                }

                if (right.IsOne)
                {
                    return left;
                }

                return left != Left || right != Right
                    ? new Divide(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"{Parenthesize(Left)}/{Parenthesize(Right)}";
        }

        internal sealed class Multiply : BinaryExpression
        {
            internal Multiply(Vector2 left, Vector2 right)
                : base(left, right)
            {
            }

            /// <inheritdoc/>
            protected override Vector2 Simplify()
            {
                var left = Left.Simplified;
                var right = Right.Simplified;

                if (left.IsZero)
                {
                    return left;
                }

                if (right.IsZero)
                {
                    return right;
                }

                if (left.IsOne)
                {
                    return right;
                }

                if (right.IsOne)
                {
                    return left;
                }

                return left != Left || right != Right
                    ? new Multiply(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
            {
                var left = Left is Multiply ? Left.ToText() : Parenthesize(Left);
                var right = Right is Multiply ? Right.ToText() : Parenthesize(Right);
                return $"{left}*{right}";
            }
        }

        internal sealed class ScalarMultiply : Vector2
        {
            internal ScalarMultiply(Scalar left, Vector2 right)
            {
                Left = left;
                Right = right;
            }

            public Scalar Left { get; }

            public Vector2 Right { get; }

            /// <inheritdoc/>
            public override int OperationsCount => Left.OperationsCount + Right.OperationsCount;

            /// <inheritdoc/>
            protected override Vector2 Simplify()
            {
                var left = Left.Simplified;
                var right = Right.Simplified;

                if (left.IsZero || right.IsZero)
                {
                    return Zero;
                }

                if (left.IsOne)
                {
                    return right;
                }

                return left != Left || right != Right
                    ? new ScalarMultiply(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => Left is Scalar.BinaryExpression.Multiply left
                    ? $"{left.ToText()}*{Parenthesize(Right)}"
                    : $"{Parenthesize(Left)}*{Parenthesize(Right)}";
        }

        internal sealed class Subtract : BinaryExpression
        {
            internal Subtract(Vector2 left, Vector2 right)
                : base(left, right)
            {
            }

            /// <inheritdoc/>
            protected override Vector2 Simplify()
            {
                var left = Left.Simplified;
                var right = Right.Simplified;

                if (right.IsZero)
                {
                    return left;
                }

                return left != Left || right != Right
                    ? new Subtract(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"{Parenthesize(Left)}-{Parenthesize(Right)}";
        }

        Scalar Channel(string channelName) => Expressions.Scalar.Channel(this, channelName);
    }
}
