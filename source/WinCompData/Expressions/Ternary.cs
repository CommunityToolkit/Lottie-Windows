// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class Ternary : Expression
    {
        public Ternary(Expression condition, Expression trueValue, Expression falseValue)
        {
            Condition = condition;
            TrueValue = trueValue;
            FalseValue = falseValue;
        }

        public Expression Condition { get; }

        public Expression TrueValue { get; }

        public Expression FalseValue { get; }

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            var c = Condition.Simplified;
            var t = TrueValue.Simplified;
            var f = FalseValue.Simplified;

            if (c is Boolean cBool)
            {
                return cBool.Value ? t : f;
            }

            if (t != TrueValue || f != FalseValue)
            {
                return new Ternary(c, t, f);
            }

            return this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString()
            => $"{Parenthesize(Condition)} ? {Parenthesize(TrueValue)} : {Parenthesize(FalseValue)}";

        /// <inheritdoc/>
        public override ExpressionType InferredType
        {
            get {
                var trueType = TrueValue.InferredType;
                var falseType = FalseValue.InferredType;

                return ExpressionType.AssertMatchingTypes(
                    TypeConstraint.AllValidTypes,
                    trueType,
                    falseType,
                    ExpressionType.IntersectConstraints(trueType.Constraints, falseType.Constraints));
            }
        }
    }
}
