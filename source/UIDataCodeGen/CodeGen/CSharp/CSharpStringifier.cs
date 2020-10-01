// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Globalization;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;
using Mgcg = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.CSharp
{
    /// <summary>
    /// Stringifiers for C# syntax.
    /// </summary>
    sealed class CSharpStringifier : Stringifier
    {
        public override string CanvasFigureLoop(CanvasFigureLoop value)
        {
            var typeName = nameof(CanvasFigureLoop);
            return value switch
            {
                Mgcg.CanvasFigureLoop.Open => $"{typeName}{ScopeResolve}Open",
                Mgcg.CanvasFigureLoop.Closed => $"{typeName}{ScopeResolve}Closed",
                _ => throw new InvalidOperationException(),
            };
        }

        public override string CanvasGeometryCombine(CanvasGeometryCombine value)
        {
            var typeName = nameof(CanvasGeometryCombine);
            return value switch
            {
                Mgcg.CanvasGeometryCombine.Union => $"{typeName}{ScopeResolve}Union",
                Mgcg.CanvasGeometryCombine.Exclude => $"{typeName}{ScopeResolve}Exclude",
                Mgcg.CanvasGeometryCombine.Intersect => $"{typeName}{ScopeResolve}Intersect",
                Mgcg.CanvasGeometryCombine.Xor => $"{typeName}{ScopeResolve}Xor",
                _ => throw new InvalidOperationException(),
            };
        }

        public override string CanvasGeometryFactoryCall(string value) => value;

        public override string Color(Color value) => $"Color.FromArgb({Hex(value.A)}, {Hex(value.R)}, {Hex(value.G)}, {Hex(value.B)})";

        public override string ConstExprField(string type, string name, string value) => $"const {type} {name} = {value};";

        public override string ConstVar => "var";

        public override string DefaultInitialize => string.Empty;

        public override string Deref => ".";

        public override string Double(double value) =>
            Math.Floor(value) == value
                    ? value.ToString("0", CultureInfo.InvariantCulture) + "d"
                    : value.ToString("G15", CultureInfo.InvariantCulture);

        public override string FilledRegionDetermination(CanvasFilledRegionDetermination value)
        {
            var typeName = nameof(CanvasFilledRegionDetermination);
            return value switch
            {
                CanvasFilledRegionDetermination.Alternate => $"{typeName}{ScopeResolve}Alternate",
                CanvasFilledRegionDetermination.Winding => $"{typeName}{ScopeResolve}Winding",
                _ => throw new InvalidOperationException(),
            };
        }

        public override string Float(float value) =>
            (Math.Floor(value) == value
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("G9", CultureInfo.InvariantCulture)) + "F";

        public override string IListAdd => "Add";

        public override string Int32(int value) => value.ToString();

        public override string Int64(long value) => value.ToString();

        public override string Matrix3x2(Matrix3x2 value)
            => $"new Matrix3x2({Float(value.M11)}, {Float(value.M12)}, {Float(value.M21)}, {Float(value.M22)}, {Float(value.M31)}, {Float(value.M32)})";

        public override string Matrix4x4(Matrix4x4 value)
             => $"new Matrix4x4({Float(value.M11)}, {Float(value.M12)}, {Float(value.M13)}, {Float(value.M14)}, {Float(value.M21)}, {Float(value.M22)}, {Float(value.M23)}, {Float(value.M24)}, {Float(value.M31)}, {Float(value.M32)}, {Float(value.M33)}, {Float(value.M34)}, {Float(value.M41)}, {Float(value.M42)}, {Float(value.M43)}, {Float(value.M44)})";

        public override string Namespace(string value) => value;

        public override string New(string typeName) => $"new {typeName}";

        public override string Null => "null";

        public override string PropertyGet(string target, string propertyName) => $"{target}.{propertyName}";

        public override string PropertySet(string target, string propertyName, string value) => $"{target}.{propertyName} = {value}";

        public override string Readonly(string value) => $"readonly {value}";

        public override string ReferenceTypeName(string value) => value;

        public override string ScopeResolve => ".";

        public override string String(string value) => $"\"{value}\"";

        public override string TimeSpan(TimeSpan value) => TimeSpan(Int64(value.Ticks));

        public override string TimeSpan(string ticks) => $"TimeSpan.FromTicks({ticks})";

        public override string TypeInt64 => "long";

        public override string TypeMatrix3x2 => "Matrix3x2";

        public override string TypeString => "string";

        public override string TypeVector2 => "Vector2";

        public override string TypeVector3 => "Vector3";

        public override string TypeVector4 => "Vector4";

        public override string Var => "var";

        public override string VariableInitialization(string value) => $" = {value}";

        public override string Vector2(Vector2 value) => $"new Vector2({Float(value.X)}, {Float(value.Y)})";

        public override string Vector3(Vector3 value) => $"new Vector3({Float(value.X)}, {Float(value.Y)}, {Float(value.Z)})";

        public override string Vector4(Vector4 value) => $"new Vector4({Float(value.X)}, {Float(value.Y)}, {Float(value.Z)}, {Float(value.W)})";
    }
}
