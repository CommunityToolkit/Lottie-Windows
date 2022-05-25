// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class Vector4 : Expression_<Vector4>
    {
        Vector4()
        {
        }

        public virtual Scalar W => Channel("W");

        public virtual Scalar X => Channel("X");

        public virtual Scalar Y => Channel("Y");

        public virtual Scalar Z => Channel("Z");

        /// <inheritdoc/>
        public override sealed ExpressionType Type => ExpressionType.Vector4;

        internal static Vector4 Zero { get; } = new Constructed(Expressions.Scalar.Zero, Expressions.Scalar.Zero, Expressions.Scalar.Zero, Expressions.Scalar.Zero);

        internal static Vector4 One { get; } = new Constructed(Expressions.Scalar.One, Expressions.Scalar.One, Expressions.Scalar.One, Expressions.Scalar.One);

        internal bool IsZero => Simplified is Constructed constructed && constructed.X.IsZero && constructed.Y.IsZero && constructed.Z.IsZero && constructed.W.IsZero;

        internal bool IsOne => Simplified is Constructed constructed && constructed.X.IsOne && constructed.Y.IsOne && constructed.Z.IsOne && constructed.W.IsOne;

        internal sealed class Asserted : Vector4
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

        internal sealed class Constructed : Vector4
        {
            internal Constructed(Scalar x, Scalar y, Scalar z, Scalar w)
            {
                X = x;
                Y = y;
                Z = z;
                W = w;
            }

            public override Scalar X { get; }

            public override Scalar Y { get; }

            public override Scalar Z { get; }

            public override Scalar W { get; }

            internal override Precedence Precedence => Precedence.Atomic;

            /// <inheritdoc/>
            public override int OperationsCount => X.OperationsCount + Y.OperationsCount + Z.OperationsCount + W.OperationsCount;

            /// <inheritdoc/>
            protected override Vector4 Simplify()
            {
                var x = X.Simplified;
                var y = Y.Simplified;
                var z = Z.Simplified;
                var w = W.Simplified;

                return
                    ReferenceEquals(x, X) &&
                    ReferenceEquals(y, Y) &&
                    ReferenceEquals(z, Z) &&
                    ReferenceEquals(w, W)
                        ? this
                        : new Constructed(x, y, z, w);
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText()
                => $"Vector4({X},{Y},{Z},{W})";
        }

        Scalar Channel(string channelName) => Expressions.Scalar.Channel(this, channelName);
    }
}
