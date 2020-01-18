// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class Color : Expression_<Color>
    {
        Color()
        {
        }

        /// <inheritdoc/>
        public override sealed ExpressionType Type => ExpressionType.Color;

        internal sealed class Asserted : Color
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

        /// <summary>
        /// Constructs a <see cref="Color"/> from RGBA values.
        /// </summary>
        internal sealed class Constructed : Color
        {
            public Constructed(Scalar r, Scalar g, Scalar b, Scalar a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public Scalar R { get; }

            public Scalar G { get; }

            public Scalar B { get; }

            public Scalar A { get; }

            /// <inheritdoc/>
            protected override Color Simplify()
            {
                var r = R.Simplified;
                var g = G.Simplified;
                var b = B.Simplified;
                var a = A.Simplified;

                return r != R || g != G || b != B || a != A
                            ? new Constructed(r, g, b, a)
                            : this;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText() => $"ColorRGB({A},{R},{G},{B})";

            protected override bool IsAtomic => true;
        }
    }
}
