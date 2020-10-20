// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgc;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Converts various language keywords and values to strings.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    abstract class Stringifier
    {
        public string BorderMode(CompositionBorderMode value)
        {
            var typeName = nameof(CompositionBorderMode);
            return value switch
            {
                CompositionBorderMode.Hard => $"{typeName}{ScopeResolve}{nameof(CompositionBorderMode.Hard)}",
                CompositionBorderMode.Soft => $"{typeName}{ScopeResolve}{nameof(CompositionBorderMode.Soft)}",
                CompositionBorderMode.Inherit => $"{typeName}{ScopeResolve}{nameof(CompositionBorderMode.Inherit)}",
                _ => throw new InvalidOperationException(),
            };
        }

        // Sets the first character to lower case.
        public string CamelCase(string value) => $"{char.ToLowerInvariant(value[0])}{value.Substring(1)}";

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

        public abstract string CanvasFigureLoop(CanvasFigureLoop value);

        public abstract string CanvasGeometryCombine(CanvasGeometryCombine value);

        public abstract string Color(Color value);

        public abstract string ConstExprField(string type, string name, string value);

        public abstract string ConstVar { get; }

        public abstract string DefaultInitialize { get; }

        public abstract string Deref { get; }

        public abstract string Double(double value);

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

        public abstract string CanvasGeometryFactoryCall(string value);

        public abstract string FilledRegionDetermination(CanvasFilledRegionDetermination value);

        public abstract string Float(float value);

        public string Float(double value) => Float((float)value);

        public string Hex(int value) => $"0x{value:X2}";

        public abstract string IListAdd { get; }

        public abstract string Int32(int value);

        public abstract string Int64(long value);

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

        public abstract string Matrix3x2(Matrix3x2 value);

        public abstract string Matrix4x4(Matrix4x4 value);

        public abstract string Namespace(string value);

        public abstract string New(string typeName);

        public abstract string Null { get; }

        public abstract string PropertyGet(string target, string propertyName);

        public abstract string PropertySet(string target, string propertyName, string value);

        public abstract string Readonly(string value);

        public abstract string ReferenceTypeName(string value);

        public abstract string ScopeResolve { get; }

        public abstract string String(string value);

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

        public abstract string TimeSpan(string ticks);

        public abstract string TimeSpan(TimeSpan value);

        public string TypeFloat32 => "float";

        public abstract string TypeInt64 { get; }

        public abstract string TypeMatrix3x2 { get; }

        public abstract string TypeString { get; }

        public abstract string TypeVector2 { get; }

        public abstract string TypeVector3 { get; }

        public abstract string TypeVector4 { get; }

        public abstract string Var { get; }

        public abstract string VariableInitialization(string value);

        public abstract string Vector2(Vector2 value);

        public abstract string Vector3(Vector3 value);

        public abstract string Vector4(Vector4 value);
    }
}
