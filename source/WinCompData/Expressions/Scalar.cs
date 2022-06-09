// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Globalization;

namespace CommunityToolkit.WinUI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class Scalar : Expression_<Scalar>
    {
        protected Scalar()
        {
        }

        /// <inheritdoc/>
        public override sealed ExpressionType Type => ExpressionType.Scalar;

        // Allow any double to be treated as a Scalar.Literal.
        public static implicit operator Scalar(double value) => new Literal(value);

        public static Scalar operator -(Scalar left, Scalar right) => new Subtract(left, right);

        public static Scalar operator +(Scalar left, Scalar right) => new Add(left, right);

        public static Scalar operator *(Scalar left, Scalar right) => new Multiply(left, right);

        public static Scalar operator /(Scalar left, Scalar right) => new Divide(left, right);

        internal static Scalar Channel<T>(T expression, string channelName)
            where T : Expression_<T> => new Subchannel<T>(expression, channelName);

        internal static Scalar Zero { get; } = new Literal(0);

        internal static Scalar One { get; } = new Literal(1);

        // Cast to a float for purposes of determining if it is equal to 0 because
        // Composition will do this internally.
        internal bool IsZero => Simplified is Literal literal && ((float)literal.Value) == 0;

        // Cast to a float for purposes of determining if it is equal to 0 because
        // Composition will do this internally.
        internal bool IsOne => Simplified is Literal literal && ((float)literal.Value) == 1;

        internal sealed class Add : BinaryOperatorExpression
        {
            public Add(Scalar left, Scalar right)
                : base(left, right)
            {
            }

            internal override Precedence Precedence => Precedence.Addition;

            private protected override string ExpressionOperator => "+";

            /// <inheritdoc/>
            protected override Scalar Simplify()
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

                if (left is Literal literalLeft && right is Literal literalRight)
                {
                    return new Literal(literalLeft.Value + literalRight.Value);
                }

                return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
                    ? this
                    : new Add(left, right);
            }
        }

        internal sealed class Asserted : Scalar
        {
            readonly string _text;

            public Asserted(string text)
            {
                _text = text;
            }

            internal override Precedence Precedence => Precedence.Atomic;

            /// <inheritdoc/>
            // We don't actually know the operation count because the text could
            // be an expression. We just assume that it doesn't involve an expression.
            public override int OperationsCount => 0;

            /// <inheritdoc/>
            protected override string CreateExpressionText() => _text;
        }

        internal abstract class BinaryExpression : Scalar
        {
            internal BinaryExpression(Scalar left, Scalar right)
            {
                Left = left;
                Right = right;
            }

            public Scalar Left { get; }

            public Scalar Right { get; }

            /// <inheritdoc/>
            public override int OperationsCount => Left.OperationsCount + Right.OperationsCount;
        }

        internal abstract class BinaryOperatorExpression : BinaryExpression
        {
            internal BinaryOperatorExpression(Scalar left, Scalar right)
                : base(left, right)
            {
            }

            private protected abstract string ExpressionOperator { get; }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
            {
                var left = Left.Precedence == Precedence.Unknown
                    ? Parenthesize(Left)
                    : Left.ToText();

                var right = Right.Precedence != Precedence && Right.Precedence != Precedence.Atomic
                    ? Parenthesize(Right)
                    : Right.ToText();

                return $"{left}{ExpressionOperator}{right}";
            }
        }

        internal sealed class Divide : BinaryOperatorExpression
        {
            internal Divide(Scalar left, Scalar right)
                : base(left, right)
            {
            }

            private protected override string ExpressionOperator => "/";

            internal override Precedence Precedence => Precedence.Division;

            /// <inheritdoc/>
            protected override Scalar Simplify()
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

                if (left is Literal literalLeft && right is Literal literalRight)
                {
                    // They're both constants. Evaluate them.
                    return new Literal(literalLeft.Value / literalRight.Value);
                }

                return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
                    ? this
                    : new Divide(left, right);
            }
        }

        /// <summary>
        /// A literal number.
        /// </summary>
        internal sealed class Literal : Scalar
        {
            public Literal(double value)
            {
                Value = value;
            }

            internal override Precedence Precedence =>
                Value >= 0
                    ? Precedence.Atomic
                    : Precedence.Unknown;

            /// <inheritdoc/>
            public override int OperationsCount => 0;

            public double Value { get; }

            /// <inheritdoc/>
            protected override string CreateExpressionText() => ToString(Value);

            static string ToString(double value)
            {
                // Do not use "G9" here - Composition expressions do not understand
                // scientific notation (e.g. 1.2E06)
                var fValue = (float)value;
                return Math.Floor(fValue) == fValue
                    ? fValue.ToString("0", CultureInfo.InvariantCulture)
                    : fValue.ToString("0.0####################", CultureInfo.InvariantCulture);
            }
        }

        internal sealed new class Max : BinaryExpression
        {
            public Max(Scalar left, Scalar right)
                : base(left, right)
            {
            }

            internal override Precedence Precedence => Precedence.Atomic;

            /// <inheritdoc/>
            protected override Scalar Simplify()
            {
                var left = Left.Simplified;
                var right = Right.Simplified;

                if (left is Literal literalLeft && right is Literal literalRight)
                {
                    // They're both constants. Evaluate them.
                    return new Literal(Math.Max(literalLeft.Value, literalRight.Value));
                }

                return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
                    ? this
                    : new Max(left, right);
            }

            protected override string CreateExpressionText()
            {
                var left = Left.Precedence == Precedence.Unknown
                    ? Parenthesize(Left)
                    : Left.ToText();

                var right = Right.Precedence != Precedence && Right.Precedence != Precedence.Atomic
                    ? Parenthesize(Right)
                    : Right.ToText();

                return $"Max({left},{right})";
            }
        }

        internal sealed new class Min : BinaryExpression
        {
            public Min(Scalar left, Scalar right)
                : base(left, right)
            {
            }

            internal override Precedence Precedence => Precedence.Atomic;

            /// <inheritdoc/>
            protected override Scalar Simplify()
            {
                var left = Left.Simplified;
                var right = Right.Simplified;

                if (left is Literal literalLeft && right is Literal literalRight)
                {
                    // They're both constants. Evaluate them.
                    return new Literal(Math.Min(literalLeft.Value, literalRight.Value));
                }

                return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
                    ? this
                    : new Min(left, right);
            }

            protected override string CreateExpressionText()
            {
                var left = Left.Precedence == Precedence.Unknown
                    ? Parenthesize(Left)
                    : Left.ToText();

                var right = Right.Precedence != Precedence && Right.Precedence != Precedence.Atomic
                    ? Parenthesize(Right)
                    : Right.ToText();

                return $"Min({left},{right})";
            }
        }

        internal sealed class Multiply : BinaryOperatorExpression
        {
            public Multiply(Scalar left, Scalar right)
                : base(left, right)
            {
            }

            private protected override string ExpressionOperator => "*";

            internal override Precedence Precedence => Precedence.Multiplication;

            /// <inheritdoc/>
            protected override Scalar Simplify()
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

                if (left == right)
                {
                    return new Squared(left);
                }

                if (left is Literal literalLeft && right is Literal literalRight)
                {
                    // They're both constants. Evaluate them.
                    return new Literal(literalLeft.Value * literalRight.Value);
                }

                return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
                    ? this
                    : new Multiply(left, right);
            }
        }

        internal sealed new class Pow : Scalar
        {
            public Pow(Scalar value, Scalar power)
            {
                Value = value;
                Power = power;
            }

            public Scalar Power { get; }

            public Scalar Value { get; }

            /// <inheritdoc/>
            public override int OperationsCount => Power.OperationsCount + Value.OperationsCount;

            internal override Precedence Precedence => Precedence.Atomic;

            /// <inheritdoc/>
            protected override Scalar Simplify()
            {
                var value = Value.Simplified;
                var power = Power.Simplified;

                if (power is Literal numberPower)
                {
                    // The power is a literal. Special case some well-known powers.
                    if (numberPower.Value == 0)
                    {
                        // n^0 == 1
                        return new Literal(1);
                    }
                    else if (numberPower.Value == 1)
                    {
                        // n^1 == n
                        return value;
                    }
                    else if (numberPower.Value == 2)
                    {
                        return new Squared(value);
                    }

                    if (value is Literal numberValue)
                    {
                        // Value and power are both literals. Evaluate them.
                        return new Literal(Math.Pow(numberValue.Value, numberPower.Value));
                    }
                }

                return ReferenceEquals(value, Value) && ReferenceEquals(power, Power)
                    ? this
                    : new Pow(value, power);
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"Pow({Value.ToText()},{Power.ToText()})";
        }

        internal sealed new class Squared : Scalar
        {
            public Squared(Scalar value)
            {
                Value = value;
            }

            public Scalar Value { get; }

            /// <inheritdoc/>
            public override int OperationsCount => Value.OperationsCount;

            /// <inheritdoc/>
            protected override Scalar Simplify()
            {
                return Value.Simplified is Literal numberValue
                    ? new Literal(numberValue.Value * numberValue.Value)
                    : (Scalar)this;
            }

            internal override Precedence Precedence => Precedence.Atomic;

            /// <inheritdoc/>
            protected override string CreateExpressionText() => $"Square({Value.ToText()})";
        }

        sealed class Subchannel<T> : Scalar
            where T : Expression_<T>
        {
            readonly T _value;
            readonly string _channelName;

            internal Subchannel(T value, string channelName)
            {
                _value = value;
                _channelName = channelName.ToUpperInvariant();
            }

            /// <inheritdoc/>
            public override int OperationsCount => 1;

            /// <inheritdoc/>
            protected override Scalar Simplify()
            {
                var valueSimplified = _value.Simplified;
                return ReferenceEquals(valueSimplified, _value)
                    ? this
                    : new Subchannel<T>(valueSimplified, _channelName);
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText() => $"{Parenthesize(_value)}.{_channelName}";

            internal override Precedence Precedence => Precedence.Atomic;
        }

        internal sealed class Subtract : BinaryOperatorExpression
        {
            public Subtract(Scalar left, Scalar right)
                : base(left, right)
            {
            }

            private protected override string ExpressionOperator => "-";

            internal override Precedence Precedence => Precedence.Subtraction;

            /// <inheritdoc/>
            protected override Scalar Simplify()
            {
                var left = Left.Simplified;
                var right = Right.Simplified;

                if (right.IsZero)
                {
                    return left;
                }

                // If both are numbers, simplify to the calculated value.
                if (left is Literal literalLeft && right is Literal literalRight)
                {
                    return new Literal(literalLeft.Value - literalRight.Value);
                }

                return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
                    ? this
                    : new Subtract(left, right);
            }
        }

        internal sealed new class Ternary : Scalar
        {
            public Ternary(Boolean condition, Scalar trueValue, Scalar falseValue)
            {
                Condition = condition;
                TrueValue = trueValue;
                FalseValue = falseValue;
            }

            public Boolean Condition { get; }

            public Scalar TrueValue { get; }

            public Scalar FalseValue { get; }

            /// <inheritdoc/>
            public override int OperationsCount => Condition.OperationsCount + FalseValue.OperationsCount + TrueValue.OperationsCount;

            /// <inheritdoc/>
            protected override Scalar Simplify()
            {
                var c = Condition.Simplified;
                var t = TrueValue.Simplified;
                var f = FalseValue.Simplified;

                if (c == Expressions.Boolean.True)
                {
                    return t;
                }

                if (c == Expressions.Boolean.False)
                {
                    return f;
                }

                return
                    ReferenceEquals(c, Condition) &&
                    ReferenceEquals(t, TrueValue) &&
                    ReferenceEquals(f, FalseValue)
                        ? this
                        : new Ternary(c, t, f);
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"{Parenthesize(Condition)}?{Parenthesize(TrueValue)}:{Parenthesize(FalseValue)}";
        }
    }
}