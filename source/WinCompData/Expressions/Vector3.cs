// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class Vector3 : Expression_<Vector3>
    {
        protected Vector3()
        {
        }

        public virtual Scalar X => Channel("X");

        public virtual Scalar Y => Channel("Y");

        public virtual Scalar Z => Channel("Z");

        public static Vector3 operator +(Vector3 left, Vector3 right) => new Add(left, right);

        public static Vector3 operator *(Scalar left, Vector3 right) => new ScalarMultiply(left, right);

        /// <inheritdoc/>
        public override sealed ExpressionType Type => ExpressionType.Vector3;

        internal static Vector3 Zero { get; } = new Constructed(Expressions.Scalar.Zero, Expressions.Scalar.Zero, Expressions.Scalar.Zero);

        internal static Vector3 One { get; } = new Constructed(Expressions.Scalar.One, Expressions.Scalar.One, Expressions.Scalar.One);

        internal bool IsZero => Simplified is Constructed constructed && constructed.X.IsZero && constructed.Y.IsZero && constructed.Z.IsZero;

        internal bool IsOne => Simplified is Constructed constructed && constructed.X.IsOne && constructed.Y.IsOne && constructed.Z.IsOne;

        internal sealed class Add : BinaryExpression
        {
            internal Add(Vector3 left, Vector3 right)
                : base(left, right)
            {
            }

            /// <inheritdoc/>
            protected override Vector3 Simplify()
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

                return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
                    ? this
                    : new Add(left, right);
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
            {
                var left = Left is Add ? Left.ToText() : Parenthesize(Left);
                var right = Right is Add ? Right.ToText() : Parenthesize(Right);
                return $"{left}+{right}";
            }
        }

        internal sealed class Asserted : Vector3
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

        internal abstract class BinaryExpression : Vector3
        {
            internal BinaryExpression(Vector3 left, Vector3 right)
            {
                Left = left;
                Right = right;
            }

            public Vector3 Left { get; }

            public Vector3 Right { get; }

            /// <inheritdoc/>
            public override int OperationsCount => Left.OperationsCount + Right.OperationsCount;
        }

        internal sealed class Constructed : Vector3
        {
            internal Constructed(Scalar x, Scalar y, Scalar z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public override Scalar X { get; }

            public override Scalar Y { get; }

            public override Scalar Z { get; }

            /// <inheritdoc/>
            public override int OperationsCount => X.OperationsCount + Y.OperationsCount + Z.OperationsCount;

            /// <inheritdoc/>
            protected override Vector3 Simplify()
            {
                var x = X.Simplified;
                var y = Y.Simplified;
                var z = Z.Simplified;

                return ReferenceEquals(x, X) && ReferenceEquals(y, Y) && ReferenceEquals(z, Z)
                    ? this
                    : Vector3(x, y, z);
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"Vector3({X},{Y},{Z})";

            protected override bool IsAtomic => true;
        }

        internal sealed class ScalarMultiply : Vector3
        {
            internal ScalarMultiply(Scalar left, Vector3 right)
            {
                Left = left;
                Right = right;
            }

            public Scalar Left { get; }

            public Vector3 Right { get; }

            /// <inheritdoc/>
            public override int OperationsCount => Left.OperationsCount + Right.OperationsCount;

            /// <inheritdoc/>
            protected override Vector3 Simplify()
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

                return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
                    ? this
                    : new ScalarMultiply(left, right);
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => Left is Scalar.BinaryExpression.Multiply left
                    ? $"{left.ToText()}*{Parenthesize(Right)}"
                    : $"{Parenthesize(Left)}*{Parenthesize(Right)}";
        }

        Scalar Channel(string channelName) => Expressions.Scalar.Channel(this, channelName);
    }
}
