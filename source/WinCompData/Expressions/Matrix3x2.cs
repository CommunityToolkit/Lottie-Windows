// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class Matrix3x2 : Expression_<Matrix3x2>
    {
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Consistency with Windows.UI.Composition.Composition")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Consistency with Windows.UI.Composition.Composition")]
        public virtual Scalar _11 => Channel("_11");

        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Consistency with Windows.UI.Composition.Composition")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Consistency with Windows.UI.Composition.Composition")]
        public virtual Scalar _12 => Channel("_12");

        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Consistency with Windows.UI.Composition.Composition")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Consistency with Windows.UI.Composition.Composition")]
        public virtual Scalar _21 => Channel("_21");

        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Consistency with Windows.UI.Composition.Composition")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Consistency with Windows.UI.Composition.Composition")]
        public virtual Scalar _22 => Channel("_11");

        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Consistency with Windows.UI.Composition.Composition")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Consistency with Windows.UI.Composition.Composition")]
        public virtual Scalar _31 => Channel("_31");

        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Consistency with Windows.UI.Composition.Composition")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Consistency with Windows.UI.Composition.Composition")]
        public virtual Scalar _32 => Channel("_32");

        /// <inheritdoc/>
        public override sealed ExpressionType Type => ExpressionType.Matrix3x2;

        public static Matrix3x2 Zero { get; } = new Constructed(0, 0, 0, 0, 0, 0);

        public static Matrix3x2 Identity { get; } = new Constructed(1, 0, 0, 1, 0, 0);

        /// <inheritdoc/>
        public override int OperationsCount =>
            _11.OperationsCount + _12.OperationsCount +
            _21.OperationsCount + _32.OperationsCount +
            _31.OperationsCount + _32.OperationsCount;

        internal sealed class Asserted : Matrix3x2
        {
            readonly string _text;

            internal Asserted(string text)
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

        internal sealed class Constructed : Matrix3x2
        {
            internal Constructed(Scalar m11, Scalar m12, Scalar m21, Scalar m22, Scalar m31, Scalar m32)
            {
                _11 = m11;
                _12 = m12;

                _21 = m21;
                _22 = m22;

                _31 = m31;
                _32 = m32;
            }

            public override Scalar _11 { get; }

            public override Scalar _12 { get; }

            public override Scalar _21 { get; }

            public override Scalar _22 { get; }

            public override Scalar _31 { get; }

            public override Scalar _32 { get; }

            /// <inheritdoc/>
            protected override Matrix3x2 Simplify()
            {
                var m11 = _11.Simplified;
                var m12 = _12.Simplified;

                var m21 = _21.Simplified;
                var m22 = _22.Simplified;

                var m31 = _31.Simplified;
                var m32 = _32.Simplified;

                return
                    ReferenceEquals(m11, _11) && ReferenceEquals(m12, _12) &&
                    ReferenceEquals(m21, _21) && ReferenceEquals(m22, _22) &&
                    ReferenceEquals(m31, _31) && ReferenceEquals(m32, _32)
                        ? this
                        : new Constructed(m11, m12, m21, m22, m31, m32);
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"Matrix3x2({Parenthesize(_11)},{Parenthesize(_12)},{Parenthesize(_21)},{Parenthesize(_22)},{Parenthesize(_31)},{Parenthesize(_32)})";

            internal override Precedence Precedence => Precedence.Atomic;
        }

        Scalar Channel(string channelName) => Expressions.Scalar.Channel(this, channelName);
    }
}
