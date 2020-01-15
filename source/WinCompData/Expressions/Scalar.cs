// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
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

        public static Scalar operator -(Scalar left, Scalar right) => new Difference(left, right);

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

        internal sealed class Add : BinaryExpression
        {
            public Add(Scalar left, Scalar right)
                : base(left, right)
            {
            }

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

                return left != Left || right != Right
                    ? new Add(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
            {
                var left = Left is Add ? Left.ToText() : Parenthesize(Left);
                var right = Right is Add ? Right.ToText() : Parenthesize(Right);
                return $"{left} + {right}";
            }
        }

        internal sealed class Asserted : Scalar
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

        internal abstract class BinaryExpression : Scalar
        {
            internal BinaryExpression(Scalar left, Scalar right)
            {
                Left = left;
                Right = right;
            }

            public Scalar Left { get; }

            public Scalar Right { get; }
        }

        internal sealed class Difference : BinaryExpression
        {
            public Difference(Scalar left, Scalar right)
                : base(left, right)
            {
            }

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

                return left != Left || right != Right
                    ? new Difference(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText() => $"{Parenthesize(Left)} - {Parenthesize(Right)}";
        }

        internal sealed class Divide : BinaryExpression
        {
            internal Divide(Scalar left, Scalar right)
                : base(left, right)
            {
            }

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

                return left != Left || right != Right
                    ? new Divide(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText() => $"{Parenthesize(Left)} / {Parenthesize(Right)}";
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

            public double Value { get; }

            /// <inheritdoc/>
            protected override string CreateExpressionText() => ToString(Value);

            protected override bool IsAtomic => Value >= 0;

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

                return left != Left || right != Right
                    ? new Max(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"Max({Parenthesize(Left)}, {Parenthesize(Right)})";
        }

        internal sealed new class Min : BinaryExpression
        {
            public Min(Scalar left, Scalar right)
                : base(left, right)
            {
            }

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

                return left != Left || right != Right
                    ? new Min(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"Min({Parenthesize(Left)}, {Parenthesize(Right)})";
        }

        internal sealed class Multiply : BinaryExpression
        {
            public Multiply(Scalar left, Scalar right)
                : base(left, right)
            {
            }

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

                return left != Left || right != Right
                    ? new Multiply(left, right)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
            {
                var left = Left is Multiply ? Left.ToText() : Parenthesize(Left);
                var right = Right is Multiply ? Right.ToText() : Parenthesize(Right);
                return $"{left} * {right}";
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

                return value != Value || power != Power
                    ? new Pow(value, power)
                    : this;
            }

            protected override bool IsAtomic => true;

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"Pow({Value.ToText()}, {Power.ToText()})";
        }

        internal sealed new class Squared : Scalar
        {
            public Squared(Scalar value)
            {
                Value = value;
            }

            public Scalar Value { get; }

            /// <inheritdoc/>
            protected override Scalar Simplify()
            {
                return Value.Simplified is Literal numberValue
                    ? new Literal(numberValue.Value * numberValue.Value)
                    : (Scalar)this;
            }

            protected override bool IsAtomic => true;

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
            protected override Scalar Simplify()
            {
                var valueSimplified = _value.Simplified;
                return valueSimplified != _value
                    ? new Subchannel<T>(valueSimplified, _channelName)
                    : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText() => $"{Parenthesize(_value)}.{_channelName}";

            /// <inheritdoc/>
            protected override bool IsAtomic => true;
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

                if (c != Condition || t != TrueValue || f != FalseValue)
                {
                    return new Ternary(c, t, f);
                }

                return this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"{Parenthesize(Condition)} ? {Parenthesize(TrueValue)} : {Parenthesize(FalseValue)}";
        }
    }
}