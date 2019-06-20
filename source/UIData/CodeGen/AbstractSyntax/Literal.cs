// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax
{
    /// <summary>
    /// An <see cref="Expression"/> that has a value that is known at compile time.
    /// </summary>
    abstract class Literal : Expression
    {
        Literal()
        {
        }

        internal override ExpressionType ExpressionType => ExpressionType.Literal;

        public abstract TypeReference.BuiltIn Type { get; }

        public override TypeReference ResultType => Type;

        internal sealed class Boolean : Literal
        {
            public Boolean(bool value)
            {
                Value = value;
            }

            public bool Value { get; }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.Boolean;
        }

        internal sealed class Float : Literal
        {
            public Float(float value)
            {
                Value = value;
            }

            public float Value { get; }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.Float;
        }

        internal sealed class Int32 : Literal
        {
            public Int32(int value)
            {
                Value = value;
            }

            public int Value { get; }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.Int32;
        }

        internal sealed class Matrix3x2 : Literal
        {
            public Matrix3x2(System.Numerics.Matrix3x2 value)
            {
                Value = value;
            }

            public System.Numerics.Matrix3x2 Value { get; }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.Matrix3x2;
        }

        internal sealed class Matrix4x4 : Literal
        {
            public Matrix4x4(System.Numerics.Matrix4x4 value)
            {
                Value = value;
            }

            public System.Numerics.Matrix4x4 Value { get; }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.Matrix4x4;
        }

        internal sealed class String : Literal
        {
            public String(string value)
            {
                Value = value;
            }

            public string Value { get; }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.String;
        }

        internal sealed class TimeSpan: Literal
        {
            public TimeSpan(string value)
            {
                Value = value;
            }

            public string Value { get; }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.TimeSpan;
        }

        internal sealed class Uri : Literal
        {
            public Uri(System.Uri value)
            {
                Value = value;
            }

            public System.Uri Value { get; }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.Uri;
        }

        internal sealed class Vector2 : Literal
        {
            public Vector2(System.Numerics.Vector2 value)
            {
                Value = value;
            }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.Vector2;

            public System.Numerics.Vector2 Value { get; }
        }

        internal sealed class Vector3 : Literal
        {
            public Vector3(System.Numerics.Vector3 value)
            {
                Value = value;
            }

            public System.Numerics.Vector3 Value { get; }

            public override TypeReference.BuiltIn Type => TypeReference.BuiltIn.Vector3;
        }
    }
}
