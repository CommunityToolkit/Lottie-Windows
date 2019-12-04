// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
#if PUBLIC_UIData
    public
#endif
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

        public virtual string Double(double value) =>
            Math.Floor(value) == value
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("G15", CultureInfo.InvariantCulture);

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

        public virtual string Vector4(Vector4 value) => $"new Vector4({Float(value.X)}, {Float(value.Y)}, {Float(value.Z)}, {Float(value.W)})";

        public string Static => "static";

        public virtual string ByteArray => "byte[]";

        public string BorderMode(CompositionBorderMode value)
        {
            var typeName = nameof(CompositionBorderMode);
            switch (value)
            {
                case CompositionBorderMode.Hard: return $"{typeName}{ScopeResolve}{nameof(CompositionBorderMode.Hard)}";
                case CompositionBorderMode.Soft: return $"{typeName}{ScopeResolve}{nameof(CompositionBorderMode.Soft)}";
                case CompositionBorderMode.Inherit: return $"{typeName}{ScopeResolve}{nameof(CompositionBorderMode.Inherit)}";
                default: throw new InvalidOperationException();
            }
        }

        public virtual string CanvasFigureLoop(CanvasFigureLoop value)
        {
            var typeName = nameof(CanvasFigureLoop);
            switch (value)
            {
                case Mgcg.CanvasFigureLoop.Open: return $"{typeName}{ScopeResolve}Open";
                case Mgcg.CanvasFigureLoop.Closed: return $"{typeName}{ScopeResolve}Closed";
                default: throw new InvalidOperationException();
            }
        }

        public virtual string CanvasGeometryCombine(CanvasGeometryCombine value)
        {
            var typeName = nameof(CanvasGeometryCombine);
            switch (value)
            {
                case Mgcg.CanvasGeometryCombine.Union: return $"{typeName}{ScopeResolve}Union";
                case Mgcg.CanvasGeometryCombine.Exclude: return $"{typeName}{ScopeResolve}Exclude";
                case Mgcg.CanvasGeometryCombine.Intersect: return $"{typeName}{ScopeResolve}Intersect";
                case Mgcg.CanvasGeometryCombine.Xor: return $"{typeName}{ScopeResolve}Xor";
                default: throw new InvalidOperationException();
            }
        }

        public virtual string FilledRegionDetermination(CanvasFilledRegionDetermination value)
        {
            var typeName = nameof(CanvasFilledRegionDetermination);
            switch (value)
            {
                case CanvasFilledRegionDetermination.Alternate: return $"{typeName}{ScopeResolve}Alternate";
                case CanvasFilledRegionDetermination.Winding: return $"{typeName}{ScopeResolve}Winding";
                default: throw new InvalidOperationException();
            }
        }

        public string CanvasCompositeMode(CanvasComposite value)
        {
            var typeName = nameof(CanvasComposite);
            switch (value)
            {
                case CanvasComposite.SourceOver: return $"{typeName}{ScopeResolve}SourceOver";
                case CanvasComposite.DestinationOver: return $"{typeName}{ScopeResolve}DestinationOver";
                case CanvasComposite.SourceIn: return $"{typeName}{ScopeResolve}SourceIn";
                case CanvasComposite.DestinationIn: return $"{typeName}{ScopeResolve}DestinationIn";
                case CanvasComposite.SourceOut: return $"{typeName}{ScopeResolve}SourceOut";
                case CanvasComposite.DestinationOut: return $"{typeName}{ScopeResolve}DestinationOut";
                case CanvasComposite.SourceAtop: return $"{typeName}{ScopeResolve}SourceAtop";
                case CanvasComposite.DestinationAtop: return $"{typeName}{ScopeResolve}DestinationAtop";
                case CanvasComposite.Xor: return $"{typeName}{ScopeResolve}Xor";
                case CanvasComposite.Add: return $"{typeName}{ScopeResolve}Add";
                case CanvasComposite.Copy: return $"{typeName}{ScopeResolve}Copy";
                case CanvasComposite.BoundedCopy: return $"{typeName}{ScopeResolve}BoundedCopy";
                case CanvasComposite.MaskInvert: return $"{typeName}{ScopeResolve}MaskInvert";
                default: throw new InvalidOperationException();
            }
        }

        public string ColorSpace(CompositionColorSpace value)
        {
            const string typeName = nameof(CompositionColorSpace);
            switch (value)
            {
                case CompositionColorSpace.Auto: return $"{typeName}{ScopeResolve}Auto";
                case CompositionColorSpace.Hsl: return $"{typeName}{ScopeResolve}Hsl";
                case CompositionColorSpace.Rgb: return $"{typeName}{ScopeResolve}Rgb";
                case CompositionColorSpace.HslLinear: return $"{typeName}{ScopeResolve}HslLinear";
                case CompositionColorSpace.RgbLinear: return $"{typeName}{ScopeResolve}RgbLinear";
                default: throw new InvalidOperationException();
            }
        }

        public string ExtendMode(CompositionGradientExtendMode value)
        {
            const string typeName = nameof(CompositionGradientExtendMode);
            switch (value)
            {
                case CompositionGradientExtendMode.Clamp: return $"{typeName}{ScopeResolve}Clamp";
                case CompositionGradientExtendMode.Wrap: return $"{typeName}{ScopeResolve}Wrap";
                case CompositionGradientExtendMode.Mirror: return $"{typeName}{ScopeResolve}Mirror";
                default: throw new InvalidOperationException();
            }
        }

        public string MappingMode(CompositionMappingMode value)
        {
            const string typeName = nameof(CompositionMappingMode);
            switch (value)
            {
                case CompositionMappingMode.Absolute: return $"{typeName}{ScopeResolve}Absolute";
                case CompositionMappingMode.Relative: return $"{typeName}{ScopeResolve}Relative";
                default: throw new InvalidOperationException();
            }
        }

        public string StrokeCap(CompositionStrokeCap value)
        {
            const string typeName = nameof(CompositionStrokeCap);
            switch (value)
            {
                case CompositionStrokeCap.Flat: return $"{typeName}{ScopeResolve}Flat";
                case CompositionStrokeCap.Square: return $"{typeName}{ScopeResolve}Square";
                case CompositionStrokeCap.Round: return $"{typeName}{ScopeResolve}Round";
                case CompositionStrokeCap.Triangle: return $"{typeName}{ScopeResolve}Triangle";
                default: throw new InvalidOperationException();
            }
        }

        public string StrokeLineJoin(CompositionStrokeLineJoin value)
        {
            const string typeName = nameof(CompositionStrokeLineJoin);
            switch (value)
            {
                case CompositionStrokeLineJoin.Miter: return $"{typeName}{ScopeResolve}Miter";
                case CompositionStrokeLineJoin.Bevel: return $"{typeName}{ScopeResolve}Bevel";
                case CompositionStrokeLineJoin.Round: return $"{typeName}{ScopeResolve}Round";
                case CompositionStrokeLineJoin.MiterOrBevel: return $"{typeName}{ScopeResolve}MiterOrBevel";
                default: throw new InvalidOperationException();
            }
        }

        public virtual string Hex(int value) => $"0x{value.ToString("X2")}";

        // Sets the first character to lower case.
        public string CamelCase(string value) => $"{char.ToLowerInvariant(value[0])}{value.Substring(1)}";
    }
}
