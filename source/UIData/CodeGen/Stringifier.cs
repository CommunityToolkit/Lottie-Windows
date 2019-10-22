// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgc;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;
using Mgcg = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Converts various language keywords and values to strings. The strings returned
    /// from here are useful for C# and may need to be overridden for other languages.
    /// </summary>
    class Stringifier
    {
        public virtual string Deref => ".";

        public virtual string IListAdd => "Add";

        public virtual string Int32TypeName => "int";

        public virtual string Int64TypeName => "long";

        public virtual string New => "new";

        public virtual string Null => "null";

        public virtual string ScopeResolve => ".";

        public virtual string Var => "var";

        public string Const(string value) => $"const {value}";

        public virtual string Readonly(string value) => $"readonly {value}";

        public string Bool(bool value) => value ? "true" : "false";

        public virtual string Color(Color value) => $"Color.FromArgb({Hex(value.A)}, {Hex(value.R)}, {Hex(value.G)}, {Hex(value.B)})";

        public virtual string FactoryCall(string value) => value;

        public virtual string Float(float value) =>
            Math.Floor(value) == value
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("G9", CultureInfo.InvariantCulture) + "F";

        public virtual string Int32(int value) => value.ToString();

        public virtual string Int64(long value) => value.ToString();

        public virtual string Matrix3x2(Matrix3x2 value)
            => $"new Matrix3x2({Float(value.M11)}, {Float(value.M12)}, {Float(value.M21)}, {Float(value.M22)}, {Float(value.M31)}, {Float(value.M32)})";

        public virtual string Matrix4x4(Matrix4x4 value)
            => $"new Matrix4x4({Float(value.M11)}, {Float(value.M12)}, {Float(value.M13)}, {Float(value.M14)}, {Float(value.M21)}, {Float(value.M22)}, {Float(value.M23)}, {Float(value.M24)}, {Float(value.M31)}, {Float(value.M32)}, {Float(value.M33)}, {Float(value.M34)}, {Float(value.M41)}, {Float(value.M42)}, {Float(value.M43)}, {Float(value.M44)})";

        public virtual string ReferenceTypeName(string value) => value;

        public virtual string String(string value) => $"\"{value}\"";

        public virtual string TimeSpan(TimeSpan value) => TimeSpan(Int64(value.Ticks));

        public virtual string TimeSpan(string ticks) => $"TimeSpan.FromTicks({ticks})";

        public virtual string Vector2(Vector2 value) => $"new Vector2({Float(value.X)}, {Float(value.Y)})";

        public virtual string Vector3(Vector3 value) => $"new Vector3({Float(value.X)}, {Float(value.Y)}, {Float(value.Z)})";

        public string Static => "static";

        public virtual string ByteArray => "byte[]";

        public virtual string CanvasFigureLoop(CanvasFigureLoop value)
        {
            var typeName = nameof(CanvasFigureLoop);
            return value switch
            {
                Mgcg.CanvasFigureLoop.Open => $"{typeName}{ScopeResolve}Open",
                Mgcg.CanvasFigureLoop.Closed => $"{typeName}{ScopeResolve}Closed",
                _ => throw new InvalidOperationException(),
            };
        }

        public virtual string CanvasGeometryCombine(CanvasGeometryCombine value)
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

        public virtual string FilledRegionDetermination(CanvasFilledRegionDetermination value)
        {
            var typeName = nameof(CanvasFilledRegionDetermination);
            return value switch
            {
                CanvasFilledRegionDetermination.Alternate => $"{typeName}{ScopeResolve}Alternate",
                CanvasFilledRegionDetermination.Winding => $"{typeName}{ScopeResolve}Winding",
                _ => throw new InvalidOperationException(),
            };
        }

        public string CanvasCompositeMode(CanvasComposite value)
        {
            var typeName = nameof(CanvasComposite);
            return value switch
            {
                CanvasComposite.SourceOver => $"{typeName}{ScopeResolve}SourceOver",
                CanvasComposite.DestinationOver => $"{typeName}{ScopeResolve}DestinationOver",
                CanvasComposite.SourceIn => $"{typeName}{ScopeResolve}SourceIn",
                CanvasComposite.DestinationIn => $"{typeName}{ScopeResolve}DestinationIn",
                CanvasComposite.SourceOut => $"{typeName}{ScopeResolve}SourceOut",
                CanvasComposite.DestinationOut => $"{typeName}{ScopeResolve}DestinationOut",
                CanvasComposite.SourceAtop => $"{typeName}{ScopeResolve}SourceAtop",
                CanvasComposite.DestinationAtop => $"{typeName}{ScopeResolve}DestinationAtop",
                CanvasComposite.Xor => $"{typeName}{ScopeResolve}Xor",
                CanvasComposite.Add => $"{typeName}{ScopeResolve}Add",
                CanvasComposite.Copy => $"{typeName}{ScopeResolve}Copy",
                CanvasComposite.BoundedCopy => $"{typeName}{ScopeResolve}BoundedCopy",
                CanvasComposite.MaskInvert => $"{typeName}{ScopeResolve}MaskInvert",
                _ => throw new InvalidOperationException(),
            };
        }

        public string ColorSpace(CompositionColorSpace value)
        {
            const string typeName = nameof(CompositionColorSpace);
            return value switch
            {
                CompositionColorSpace.Auto => $"{typeName}{ScopeResolve}Auto",
                CompositionColorSpace.Hsl => $"{typeName}{ScopeResolve}Hsl",
                CompositionColorSpace.Rgb => $"{typeName}{ScopeResolve}Rgb",
                CompositionColorSpace.HslLinear => $"{typeName}{ScopeResolve}HslLinear",
                CompositionColorSpace.RgbLinear => $"{typeName}{ScopeResolve}RgbLinear",
                _ => throw new InvalidOperationException(),
            };
        }

        public string ExtendMode(CompositionGradientExtendMode value)
        {
            const string typeName = nameof(CompositionGradientExtendMode);
            return value switch
            {
                CompositionGradientExtendMode.Clamp => $"{typeName}{ScopeResolve}Clamp",
                CompositionGradientExtendMode.Wrap => $"{typeName}{ScopeResolve}Wrap",
                CompositionGradientExtendMode.Mirror => $"{typeName}{ScopeResolve}Mirror",
                _ => throw new InvalidOperationException(),
            };
        }

        public string MappingMode(CompositionMappingMode value)
        {
            const string typeName = nameof(CompositionMappingMode);
            return value switch
            {
                CompositionMappingMode.Absolute => $"{typeName}{ScopeResolve}Absolute",
                CompositionMappingMode.Relative => $"{typeName}{ScopeResolve}Relative",
                _ => throw new InvalidOperationException(),
            };
        }

        public string StrokeCap(CompositionStrokeCap value)
        {
            const string typeName = nameof(CompositionStrokeCap);
            return value switch
            {
                CompositionStrokeCap.Flat => $"{typeName}{ScopeResolve}Flat",
                CompositionStrokeCap.Square => $"{typeName}{ScopeResolve}Square",
                CompositionStrokeCap.Round => $"{typeName}{ScopeResolve}Round",
                CompositionStrokeCap.Triangle => $"{typeName}{ScopeResolve}Triangle",
                _ => throw new InvalidOperationException(),
            };
        }

        public string StrokeLineJoin(CompositionStrokeLineJoin value)
        {
            const string typeName = nameof(CompositionStrokeLineJoin);
            return value switch
            {
                CompositionStrokeLineJoin.Miter => $"{typeName}{ScopeResolve}Miter",
                CompositionStrokeLineJoin.Bevel => $"{typeName}{ScopeResolve}Bevel",
                CompositionStrokeLineJoin.Round => $"{typeName}{ScopeResolve}Round",
                CompositionStrokeLineJoin.MiterOrBevel => $"{typeName}{ScopeResolve}MiterOrBevel",
                _ => throw new InvalidOperationException(),
            };
        }

        public virtual string Hex(int value) => $"0x{value.ToString("X2")}";

        // Sets the first character to lower case.
        public string CamelCase(string value) => $"{char.ToLowerInvariant(value[0])}{value.Substring(1)}";
    }
}
