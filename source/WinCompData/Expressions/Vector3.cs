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

        /// <inheritdoc/>
        public override sealed ExpressionType Type => ExpressionType.Vector3;

        internal static Vector3 Zero { get; } = new Constructed(Expressions.Scalar.Zero, Expressions.Scalar.Zero, Expressions.Scalar.Zero);

        internal static Vector3 One { get; } = new Constructed(Expressions.Scalar.One, Expressions.Scalar.One, Expressions.Scalar.One);

        internal bool IsZero => Simplified is Constructed constructed && constructed.X.IsZero && constructed.Y.IsZero && constructed.Z.IsZero;

        internal bool IsOne => Simplified is Constructed constructed && constructed.X.IsOne && constructed.Y.IsOne && constructed.Z.IsOne;

        internal sealed class Asserted : Vector3
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
            protected override Vector3 Simplify()
            {
                var x = X.Simplified;
                var y = Y.Simplified;
                var z = Z.Simplified;

                return x == X && y == Y && z == Z
                    ? this
                    : Vector3(x, y, z);
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"Vector3({Parenthesize(X)},{Parenthesize(Y)},{Parenthesize(Z)})";

            protected override bool IsAtomic => true;
        }

        Scalar Channel(string channelName) => Expressions.Scalar.Channel(this, channelName);
    }
}
