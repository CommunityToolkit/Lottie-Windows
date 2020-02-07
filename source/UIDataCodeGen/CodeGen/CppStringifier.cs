// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;
using Mgcg = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Stringifiers for C++ syntax.
    /// </summary>
    abstract class CppStringifier : Stringifier
    {
        private protected CppStringifier()
        {
        }

        public override string DefaultInitialize => "{}";

        public virtual string Hatted(string typeName) => typeName;

        public sealed override string VariableInitialization(string value) => $"{{ {value} }}";

        public sealed override string Namespace(string value) => value.Replace(".", "::");

        public sealed override string CanvasFigureLoop(Mgcg.CanvasFigureLoop value)
        {
            switch (value)
            {
                case Mgcg.CanvasFigureLoop.Open:
                    return "D2D1_FIGURE_END_OPEN";
                case Mgcg.CanvasFigureLoop.Closed:
                    return "D2D1_FIGURE_END_CLOSED";
                default:
                    throw new InvalidOperationException();
            }
        }

        public sealed override string CanvasGeometryCombine(Mgcg.CanvasGeometryCombine value)
        {
            switch (value)
            {
                case Mgcg.CanvasGeometryCombine.Union:
                    return "D2D1_COMBINE_MODE_UNION";
                case Mgcg.CanvasGeometryCombine.Exclude:
                    return "D2D1_COMBINE_MODE_EXCLUDE";
                case Mgcg.CanvasGeometryCombine.Intersect:
                    return "D2D1_COMBINE_MODE_INTERSECT";
                case Mgcg.CanvasGeometryCombine.Xor:
                    return "D2D1_COMBINE_MODE_XOR";
                default:
                    throw new InvalidOperationException();
            }
        }

        public sealed override string Color(Color value) => $"{{ {ColorArgs(value)} }}";

        public string ColorArgs(Color value) => $"{Hex(value.A)}, {Hex(value.R)}, {Hex(value.G)}, {Hex(value.B)}";

        public override string Deref => "->";

        public sealed override string FilledRegionDetermination(Mgcg.CanvasFilledRegionDetermination value)
        {
            switch (value)
            {
                case Mgcg.CanvasFilledRegionDetermination.Alternate:
                    return "D2D1_FILL_MODE_ALTERNATE";
                case Mgcg.CanvasFilledRegionDetermination.Winding:
                    return "D2D1_FILL_MODE_WINDING";
                default:
                    throw new InvalidOperationException();
            }
        }

        public sealed override string Float(float value) =>
            (Math.Floor(value) == value
                ? value.ToString("0.0", CultureInfo.InvariantCulture)
                : value.ToString("G9", CultureInfo.InvariantCulture)) + "F";

        public sealed override string Double(double value) =>
            Math.Floor(value) == value
                    ? value.ToString("0.0", CultureInfo.InvariantCulture) + "d"
                    : value.ToString("G15", CultureInfo.InvariantCulture);

        public sealed override string Int64(long value) => $"{value}L";

        public sealed override string ScopeResolve => "::";

        public override string New(string typeName) => $"ref new {typeName}";

        public sealed override string Null => "nullptr";

        public sealed override string Matrix3x2(Matrix3x2 value)
        {
            return $"{{ {Float(value.M11)}, {Float(value.M12)}, {Float(value.M21)}, {Float(value.M22)}, {Float(value.M31)}, {Float(value.M32)} }}";
        }

        public sealed override string Matrix4x4(Matrix4x4 value)
        {
            return $"{{ {Float(value.M11)}, {Float(value.M12)}, {Float(value.M13)}, {Float(value.M14)}, {Float(value.M21)}, {Float(value.M22)}, {Float(value.M23)}, {Float(value.M24)}, {Float(value.M31)}, {Float(value.M32)}, {Float(value.M33)}, {Float(value.M34)}, {Float(value.M41)}, {Float(value.M42)}, {Float(value.M43)}, {Float(value.M44)} }}";
        }

        public sealed override string Readonly(string value) => $"{value} const";

        public sealed override string ConstExprField(string type, string name, string value) => $"static constexpr {type} {name}{{ {value} }};";

        public sealed override string TimeSpan(TimeSpan value) => TimeSpan(Int64(value.Ticks));

        public override string TimeSpan(string ticks) => $"{{ {ticks} }}";

        public sealed override string Var => "auto";

        public sealed override string ConstVar => "const auto";

        public sealed override string Vector2(Vector2 value) => $"{{ {Vector2Args(value)} }}";

        public string Vector2Args(Vector2 value) => $"{Float(value.X)}, {Float(value.Y)}";

        public sealed override string Vector3(Vector3 value) => $"{{ {Vector3Args(value)} }}";

        public string Vector3Args(Vector3 value) => $"{Float(value.X)}, {Float(value.Y)}, {Float(value.Z)}";

        public sealed override string Vector4(Vector4 value) => $"{{ {Vector4Args(value)} }}";

        public string Vector4Args(Vector4 value) => $"{Float(value.X)}, {Float(value.Y)}, {Float(value.Z)}, {Float(value.W)}";

        public sealed override string IListAdd => "Append";

        public sealed override string FactoryCall(string value) => $"CanvasGeometryToIGeometrySource2D({value})";

        public sealed override string FieldName(string value) => $"m_{CamelCase(value)}";

        public sealed override string ByteArray => "Array<byte>";

        public string FailFastWrapper(string value) => $"FFHR({value})";

        public sealed override string TypeInt64 => "int64_t";

        public sealed override string TypeVector2 { get; } = "float2";

        public sealed override string TypeVector3 { get; } = "float3";

        public sealed override string TypeVector4 { get; } = "float4";

        public sealed override string TypeMatrix3x2 { get; } = "float3x2";

        /// <summary>
        /// Gets the code for a class that wraps an ID2D1Geometry in an IGeometrySource2DInterop
        /// as required by CompositionPath.
        /// This class will be included inline in every codegen so that the generated code
        /// doesn't need to depend on another file.
        /// The implementation is very simple - just enough to satisfy CompositionPath.
        /// </summary>
        public string GeoSourceClass =>
@"class GeoSource final :
    public ABI::Windows::Graphics::IGeometrySource2D,
    public ABI::Windows::Graphics::IGeometrySource2DInterop
 {
    ULONG _cRef;
    ComPtr<ID2D1Geometry> _cpGeometry;

public:
    GeoSource(ID2D1Geometry* pGeometry)
        : _cRef(1)
        , _cpGeometry(pGeometry)
    { }

    IFACEMETHODIMP QueryInterface(REFIID iid, void ** ppvObject) override
    {
        if (iid == __uuidof(ABI::Windows::Graphics::IGeometrySource2DInterop))
        {
            AddRef();
            *ppvObject = static_cast<ABI::Windows::Graphics::IGeometrySource2DInterop*>(this);
            return S_OK;
        }
        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&_cRef);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        ULONG cRef = InterlockedDecrement(&_cRef);
        if (cRef == 0)
        {
            delete this;
        }
        return cRef;
    }

    IFACEMETHODIMP GetIids(ULONG*, IID**) override
    {
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetRuntimeClassName(HSTRING*) override
    {
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetTrustLevel(TrustLevel*) override
    {
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetGeometry(ID2D1Geometry** value) override
    {
        *value = _cpGeometry.Get();
        (*value)->AddRef();
        return S_OK;
    }

    IFACEMETHODIMP TryGetGeometryUsingFactory(ID2D1Factory*, ID2D1Geometry**) override
    {
        return E_NOTIMPL;
    }
};
";
    }
}
