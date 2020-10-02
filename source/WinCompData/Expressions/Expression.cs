// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable // Temporary while enabling nullable everywhere.

using System;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class Expression : IEquatable<Expression>
    {
        private protected Expression()
        {
        }

        public static Boolean Boolean(string text) => new Boolean.Asserted(text);

        public static Color Color(Scalar r, Scalar g, Scalar b, Scalar a) => new Color.Constructed(r, g, b, a);

        public static Color Color(string text) => new Color.Asserted(text);

        public static Boolean LessThan(Scalar left, Scalar right) => new Boolean.LessThan(left, right);

        public static Matrix3x2 Matrix3x2Identity => Expressions.Matrix3x2.Identity;

        public static Matrix3x2 Matrix3x2Zero => Expressions.Matrix3x2.Zero;

        public static Matrix3x2 Matrix3x2(string text) => new Matrix3x2.Asserted(text);

        public static Scalar Max(Scalar x, Scalar y) => new Scalar.Max(x, y);

        public static Scalar Min(Scalar x, Scalar y) => new Scalar.Min(x, y);

        protected static Scalar Pow(Scalar value, Scalar power) => new Scalar.Pow(value, power);

        public static Scalar Scalar(string text) => new Scalar.Asserted(text);

        protected static Scalar Squared(Scalar value) => new Scalar.Squared(value);

        public static Scalar Ternary(Boolean condition, Scalar trueValue, Scalar falseValue) => new Scalar.Ternary(condition, trueValue, falseValue);

        public static Vector2 Vector2(Scalar x, Scalar y) => new Vector2.Constructed(x, y);

        public static Vector2 Vector2(string text) => new Vector2.Asserted(text);

        public static Vector2 Vector2(Sn.Vector2 value) => Vector2(value.X, value.Y);

        public static Vector3 Vector3(Sn.Vector2 value) => Vector3(value.X, value.Y, 0);

        public static Vector3 Vector3(Scalar x, Scalar y, Scalar z) => new Vector3.Constructed(x, y, z);

        public static Vector3 Vector3(string text) => new Vector3.Asserted(text);

        public static Vector4 Vector4(Scalar x, Scalar y, Scalar z, Scalar w) => new Vector4.Constructed(x, y, z, w);

        public static Vector4 Vector4(string text) => new Vector4.Asserted(text);

        public abstract ExpressionType Type { get; }

        /// <inheritdoc/>
        public override string ToString() => CreateExpressionText();

        /// <summary>
        /// Returns a textual representation of the <see cref="Expression"/> that can be
        /// used in Windows.UI.Composition.ExpressionAnimation expressions.
        /// </summary>
        /// <returns>A textual representation of the <see cref="Expression"/>.</returns>
        public abstract string ToText();

        /// <summary>
        /// Gets a value indicating whether the string form of the expression can be unambigiously
        /// parsed without parentheses.
        /// </summary>
        protected virtual bool IsAtomic => false;

        /// <summary>
        /// The number of operations in this <see cref="Expression"/>. An operation is something that
        /// requires evaluation, for example an addition.
        /// </summary>
        public abstract int OperationsCount { get; }

        protected static string Parenthesize(Expression expression)
            => expression.IsAtomic
                ? expression.ToText()
                : $"({expression.ToText()})";

        /// <summary>
        /// Returns the expression as a string for use by Windows.UI.Composition animations.
        /// </summary>
        /// <returns>The expression as a string suitable for use in the Windows.UI.Composition animation APIs.</returns>
        protected abstract string CreateExpressionText();

        public bool Equals(Expression other) => other is Expression && other.ToText() == ToText();

        public override sealed bool Equals(object obj) => Equals(obj as Expression);

        public override sealed int GetHashCode() => ToText().GetHashCode();

        public static bool operator ==(Expression a, Expression b) => (a is null && b is null) || a.ToText() == b.ToText();

        public static bool operator !=(Expression a, Expression b) => !(a == b);
    }
}
