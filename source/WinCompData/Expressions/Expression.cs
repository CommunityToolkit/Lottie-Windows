// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class Expression
    {
        string _expressionStringCache;
        Expression _simplifiedExpressionCache;

        protected Expression()
        {
        }

        public virtual ExpressionType InferredType { get; } = new ExpressionType(TypeConstraint.NoType);

        public static Number Scalar(double value) => new Number(value);

        public static Divide Divide(Expression x, Expression y) => new Divide(x, y);

        public static Vector2 Constant(Sn.Vector2 value) => new Vector2(Scalar(value.X), Scalar(value.Y));

        public static TypeAssert Scalar(string name) => Name(name, TypeConstraint.Scalar);

        public static Max Max(Expression x, Expression y) => new Max(x, y);

        public static Min Min(Expression x, Expression y) => new Min(x, y);

        static TypeAssert Name(string name, TypeConstraint typeConstraint) => new TypeAssert(new Name(name), typeConstraint);

        public static TypeAssert Vector2(string name) => Name(name, TypeConstraint.Vector2);

        public static Vector2 Vector2(Expression x, Expression y) => new Vector2(x, y);

        public static Vector2 Vector2(double x, double y) => new Vector2(Scalar(x), Scalar(y));

        public static Vector2 Vector2(Sn.Vector2 value) => new Vector2(Scalar(value.X), Scalar(value.Y));

        public static Vector3 Vector3(Expression x, Expression y, Expression z) => new Vector3(x, y, z);

        public static Vector3 Vector3(Expression x, Expression y) => new Vector3(x, y, Scalar(0));

        protected static Squared Squared(Expression expression) => new Squared(expression);

        protected static Cubed Cubed(Expression expression) => new Cubed(expression);

        public static Sum Sum(Expression a, Expression b) => new Sum(a, b);

        public static Sum Sum(Expression a, Expression b, params Expression[] parameters)
        {
            var result = new Sum(a, b);
            foreach (var parameter in parameters)
            {
                result = new Sum(result, parameter);
            }

            return result;
        }

        public static Number Sum(Number a, Number b) => Scalar(a.Value + b.Value);

        public static Subtract Subtract(Expression a, Expression b) => new Subtract(a, b);

        public static Multiply Multiply(Expression a, Expression b) => new Multiply(a, b);

        public static Matrix3x2 Matrix3x2Zero => Matrix3x2.Zero;

        public static Matrix3x2 Matrix3x2Identity => Matrix3x2.Identity;

        protected static Multiply Multiply(Expression a, Expression b, params Expression[] parameters)
        {
            var result = new Multiply(a, b);
            foreach (var parameter in parameters)
            {
                result = new Multiply(result, parameter);
            }

            return result;
        }

        /// <summary>
        /// Gets a value indicating whether the string form of the expression can be unambigiously
        /// parsed without parentheses.
        /// </summary>
        internal virtual bool IsAtomic => false;

        /// <inheritdoc/>
        public sealed override string ToString()
        {
            // Expressions are immutable, so it's always safe to return a cached string representation.
            return _expressionStringCache != null
                ? _expressionStringCache
                : _expressionStringCache = Simplified.CreateExpressionString();
        }

        /// <summary>
        /// Gets a simplified form of the expression. May be the same as this.
        /// </summary>
        public Expression Simplified
        {
            get
            {
                return _simplifiedExpressionCache != null
                    ? _simplifiedExpressionCache
                    : _simplifiedExpressionCache = Simplify();
            }
        }

        /// <summary>
        /// Returns an equivalent expression, simplified if possible.
        /// </summary>
        protected abstract Expression Simplify();

        /// <summary>
        /// Returns the expression as a string for use by WinComp animations.
        /// </summary>
        protected abstract string CreateExpressionString();

        protected static string Parenthesize(Expression expression) =>
            expression.IsAtomic ? expression.ToString() : $"({expression})";

        protected static bool IsZero(Expression expression)
        {
            if (expression is Number numberExpression)
            {
                // Cast to a float for purposes of determining if it is equal to 1 because
                // Composition will do this internally.
                return (float)numberExpression.Value == 0;
            }
            else if (expression is Vector2 vector2Expression)
            {
                return IsZero(vector2Expression.X) && IsZero(vector2Expression.Y);
            }
            else
            {
                return false;
            }
        }

        protected static bool IsOne(Expression expression)
        {
            if (expression is Number numberExpression)
            {
                // Cast to a float for purposes of determining if it is equal to 1 because
                // Composition will do this internally.
                return (float)numberExpression.Value == 1;
            }
            else if (expression is Vector2 vector2Expression)
            {
                return IsOne(vector2Expression.X) && IsOne(vector2Expression.Y);
            }
            else
            {
                return false;
            }
        }
    }
}
