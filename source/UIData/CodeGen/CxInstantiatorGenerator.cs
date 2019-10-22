// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData;
using Mgce = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIData
    public
#endif
    sealed class CxInstantiatorGenerator : InstantiatorGeneratorBase
    {
        readonly CppStringifier _s;
        readonly string _headerFileName;

        CxInstantiatorGenerator(
            string className,
            Vector2 size,
            IReadOnlyList<(CompositionObject graphRoot, uint requiredUapVersion)> graphs,
            TimeSpan duration,
            bool setCommentProperties,
            bool disableFieldOptimization,
            CppStringifier stringifier,
            string headerFileName)
            : base(
                  className: className,
                  compositionDeclaredSize: size,
                  graphs: graphs,
                  duration: duration,
                  setCommentProperties: setCommentProperties,
                  disableFieldOptimization: false,
                  stringifier: stringifier)
        {
            _s = stringifier;
            _headerFileName = headerFileName;
        }

        /// <summary>
        /// Returns the Cx code for a factory that will instantiate the given <see cref="Visual"/> as a
        /// Windows.UI.Composition Visual.
        /// </summary>
        /// <returns>A value tuple containing the cpp code, header code, and list of referenced asset files.</returns>
        public static (string cppText, string hText, IEnumerable<Uri> assetList) CreateFactoryCode(
            string className,
            IReadOnlyList<(CompositionObject graphRoot, uint requiredUapVersion)> graphs,
            float width,
            float height,
            TimeSpan duration,
            string headerFileName,
            bool disableFieldOptimization)
        {
            var generator = new CxInstantiatorGenerator(
                className: className,
                size: new Vector2(width, height),
                graphs: graphs,
                duration: duration,
                disableFieldOptimization: disableFieldOptimization,
                setCommentProperties: false,
                stringifier: new CppStringifier(),
                headerFileName: headerFileName);

            var cppText = generator.GenerateCode();

            var hText = generator.GenerateHeaderText(className);

            var assetList = generator.GetAssetsList();

            return (cppText, hText, assetList);
        }

        // Generates the .h file contents.
        string GenerateHeaderText(string className)
        {
            var loadedImageSurfacesInfo = GetLoadedImageSurfacesInfo();

            // Returns the header text that implements IAnimatedVisualSource if loadedImageSurfacesNodes is null or empty.
            // Otherwise, return the header text that implements IDynamicAnimatedVisualSource.
            return !loadedImageSurfacesInfo.Any()
                ? IAnimatedVisualSourceHeaderText(className)
                : IDynamicAnimatedVisualSourceHeaderText(className, loadedImageSurfacesInfo);
        }

        /// <inheritdoc/>
        // Called by the base class to write the start of the file (i.e. everything up to the body of the Instantiator class).
        protected override void WriteFileStart(
            CodeBuilder builder,
            AnimatedVisualSourceInfo info)
        {
            builder.WriteLine("#include \"pch.h\"");
            builder.WriteLine($"#include \"{_headerFileName}\"");

            // floatY, floatYxZ
            builder.WriteLine("#include \"WindowsNumerics.h\"");

            if (info.UsesCanvasEffects ||
                info.UsesCanvasGeometry)
            {
                // D2D
                builder.WriteLine("#include \"d2d1.h\"");
                builder.WriteLine("#include <d2d1_1.h>");
                builder.WriteLine("#include <d2d1helper.h>");

                // Interop
                builder.WriteLine("#include <Windows.Graphics.Interop.h>");

                // ComPtr
                builder.WriteLine("#include <wrl.h>");
            }

            if (info.UsesStreams)
            {
                builder.WriteLine("#include <iostream>");
            }

            if (info.UsesCompositeEffect)
            {
                // The CompsiteEffect class requires std::vector.
                builder.WriteLine("#include <vector>");
            }

            builder.WriteLine();

            // A sorted set to hold the namespaces that the generated code will use. The set is maintained in sorted order.
            var namespaces = new SortedSet<string>();

            if (info.UsesCanvasEffects ||
                info.UsesCanvas)
            {
                // Interop
                builder.WriteLine("#include <Windows.Graphics.Effects.h>");
                builder.WriteLine("#include <Windows.Graphics.Effects.Interop.h>");
            }

            namespaces.Add("Windows::Foundation");
            namespaces.Add("Windows::Foundation::Numerics");
            namespaces.Add("Windows::UI");
            namespaces.Add("Windows::UI::Composition");
            namespaces.Add("Windows::Graphics");
            namespaces.Add("Microsoft::WRL");
            if (info.UsesNamespaceWindowsUIXamlMedia)
            {
                namespaces.Add("Windows::UI::Xaml::Media");
            }

            if (info.UsesStreams)
            {
                namespaces.Add("Platform");
                namespaces.Add("Windows::Storage::Streams");
            }

            // Write out each namespace using.
            foreach (var n in namespaces)
            {
                builder.WriteLine($"using namespace {n};");
            }

            builder.WriteLine();

            // Put the Instantiator class in an anonymous namespace.
            builder.WriteLine("namespace");
            builder.WriteLine("{");

            if (info.UsesCanvasEffects ||
                info.UsesCanvasGeometry)
            {
                // Write GeoSource to allow it's use in function definitions
                builder.WriteLine($"{_s.GeoSourceClass}");

                // Typedef to simplify generation
                builder.WriteLine("typedef ComPtr<GeoSource> CanvasGeometry;");
            }

            if (info.UsesCompositeEffect)
            {
                // Write the composite effect class that will allow the use
                // of this effect without win2d.
                builder.WriteLine($"{CompositionEffectClass}");
            }
        }

        /// <inheritdoc/>
        protected override void WriteAnimatedVisualStart(CodeBuilder builder, AnimatedVisualInfo info)
        {
            // Start writing the instantiator.
            builder.WriteLine($"ref class {info.ClassName} sealed : public Microsoft::UI::Xaml::Controls::IAnimatedVisual");
            builder.OpenScope();

            if (info.AnimatedVisualSourceInfo.UsesCanvasEffects ||
                info.AnimatedVisualSourceInfo.UsesCanvasGeometry)
            {
                // D2D factory field.
                builder.WriteLine("ComPtr<ID2D1Factory> _d2dFactory;");
            }
        }

        /// <inheritdoc/>
        // Called by the base class to write the end of the AnimatedVisual class.
        protected override void WriteAnimatedVisualEnd(
            CodeBuilder builder,
            AnimatedVisualInfo info)
        {
            if (info.AnimatedVisualSourceInfo.UsesCanvasEffects ||
                info.AnimatedVisualSourceInfo.UsesCanvasGeometry)
            {
                // Utility method for D2D geometries
                builder.WriteLine("static IGeometrySource2D^ CanvasGeometryToIGeometrySource2D(CanvasGeometry geo)");
                builder.OpenScope();
                builder.WriteLine("ComPtr<ABI::Windows::Graphics::IGeometrySource2D> interop = geo.Detach();");
                builder.WriteLine("return reinterpret_cast<IGeometrySource2D^>(interop.Get());");
                builder.CloseScope();
                builder.WriteLine();

                // Utility method for fail-fasting on bad HRESULTs from d2d operations
                builder.WriteLine("static void FFHR(HRESULT hr)");
                builder.OpenScope();
                builder.WriteLine("if (hr != S_OK)");
                builder.OpenScope();
                builder.WriteLine("RoFailFastWithErrorContext(hr);");
                builder.CloseScope();
                builder.CloseScope();
                builder.WriteLine();
            }

            // Write the constructor for the AnimatedVisual class.
            builder.UnIndent();
            builder.WriteLine("public:");
            builder.Indent();

            if (info.AnimatedVisualSourceInfo.HasLoadedImageSurface)
            {
                builder.WriteLine($"{info.ClassName}(Compositor^ compositor,");
                builder.Indent();

                builder.WriteCommaSeparatedLines(info.AnimatedVisualSourceInfo.LoadedImageSurfaceNodes.Select(n => $"{_s.ReferenceTypeName(n.TypeName)} {_s.CamelCase(n.Name)}"));

                // Initializer list.
                builder.WriteLine(") : _c(compositor)");

                // Instantiate the reusable ExpressionAnimation.
                builder.WriteLine($", {info.AnimatedVisualSourceInfo.ReusableExpressionAnimationFieldName}(compositor->CreateExpressionAnimation())");

                // Initialize the image surfaces.
                var nodes = info.AnimatedVisualSourceInfo.LoadedImageSurfaceNodes.ToArray();
                foreach (var n in nodes)
                {
                    builder.WriteLine($", {n.FieldName}({_s.CamelCase(n.Name)})");
                }

                builder.UnIndent();
            }
            else
            {
                builder.WriteLine($"{info.ClassName}(Compositor^ compositor)");

                // Initializer list.
                builder.Indent();
                builder.WriteLine(": _c(compositor)");

                // Instantiate the reusable ExpressionAnimation.
                builder.WriteLine($", {info.AnimatedVisualSourceInfo.ReusableExpressionAnimationFieldName}(compositor->CreateExpressionAnimation())");
                builder.UnIndent();
            }

            builder.OpenScope();
            if (info.AnimatedVisualSourceInfo.UsesCanvasEffects ||
                info.AnimatedVisualSourceInfo.UsesCanvasGeometry)
            {
                builder.WriteLine($"{FailFastWrapper("D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, _d2dFactory.GetAddressOf())")};");
            }

            // Instantiate the root. This will cause the whole Visual tree to be built and animations started.
            builder.WriteLine("Root();");
            builder.CloseScope();

            // Write the destructor. This is how CX implements IClosable/IDisposable.
            builder.WriteLine();
            builder.WriteLine($"virtual ~{info.ClassName}() {{ }}");

            // Write the members on IAnimatedVisual.
            builder.WriteLine();
            builder.WriteLine("property TimeSpan Duration");
            builder.OpenScope();
            builder.WriteLine("virtual TimeSpan get() { return { c_durationTicks }; }");
            builder.CloseScope();
            builder.WriteLine();
            builder.WriteLine("property Visual^ RootVisual");
            builder.OpenScope();
            builder.WriteLine("virtual Visual^ get() { return _root; }");
            builder.CloseScope();
            builder.WriteLine();
            builder.WriteLine("property float2 Size");
            builder.OpenScope();
            builder.WriteLine($"virtual float2 get() {{ return {Vector2(info.AnimatedVisualSourceInfo.CompositionDeclaredSize)}; }}");
            builder.CloseScope();
            builder.WriteLine();

            // Close the scope for the instantiator class.
            builder.UnIndent();
            builder.WriteLine("};");
        }

        /// <inheritdoc/>
        // Called by the base class to write the end of the file (i.e. everything after the body of the AnimatedVisual class).
        protected override void WriteFileEnd(
            CodeBuilder builder,
            AnimatedVisualSourceInfo info)
        {
            // Close the anonymous namespace.
            builder.WriteLine("} // end namespace");
            builder.WriteLine();

            // Generate the method that creates an instance of the composition on the IAnimatedVisualSource
            builder.WriteLine($"Microsoft::UI::Xaml::Controls::IAnimatedVisual^ AnimatedVisuals::{info.ClassName}::TryCreateAnimatedVisual(");
            builder.Indent();
            builder.WriteLine("Compositor^ compositor,");
            builder.WriteLine("Object^* diagnostics)");
            builder.UnIndent();
            builder.OpenScope();

            if (info.HasLoadedImageSurface)
            {
                WriteTryCreateInstantiatorWithImageLoading(builder, info);
            }
            else
            {
                builder.WriteLine("diagnostics = nullptr;");
                builder.WriteLine("if (!IsRuntimeCompatible())");
                builder.OpenScope();
                builder.WriteLine("return nullptr;");
                builder.CloseScope();
                builder.WriteLine("return ref new AnimatedVisual(compositor);");
                builder.CloseScope();
            }

            if (info.HasLoadedImageSurface)
            {
                // Generate the get() and set() methods of IsAnimatedVisualSourceDynamic property.
                WriteIsAnimatedVisualSourceDynamicGetSet(builder, info);

                // Generate the method that load all the LoadedImageSurfaces.
                WriteEnsureImageLoadingStarted(builder, info);

                // Generate the method that handle the LoadCompleted event of the LoadedImageSurface objects.
                WriteHandleLoadCompleted(builder, info);
            }
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryCombinationFactory(CodeBuilder builder, CanvasGeometry.Combination obj, string typeName, string fieldName)
        {
            builder.WriteLine($"{typeName} result;");
            builder.WriteLine("ID2D1Geometry *geoA = nullptr, *geoB = nullptr;");
            builder.WriteLine($"{CallFactoryFor(obj.A)}->GetGeometry(&geoA);");
            builder.WriteLine($"{CallFactoryFor(obj.B)}->GetGeometry(&geoB);");
            builder.WriteLine("ComPtr<ID2D1PathGeometry> path;");
            builder.WriteLine($"{FailFastWrapper("_d2dFactory->CreatePathGeometry(&path)")};");
            builder.WriteLine("ComPtr<ID2D1GeometrySink> sink;");
            builder.WriteLine($"{FailFastWrapper("path->Open(&sink)")};");
            builder.WriteLine($"FFHR(geoA->CombineWithGeometry(");
            builder.Indent();
            builder.WriteLine($"geoB,");
            builder.WriteLine($"{_s.CanvasGeometryCombine(obj.CombineMode)},");
            builder.WriteLine($"{_s.Matrix3x2(obj.Matrix)},");
            builder.WriteLine($"sink.Get()));");
            builder.UnIndent();
            builder.WriteLine("geoA->Release();");
            builder.WriteLine("geoB->Release();");
            builder.WriteLine($"{FailFastWrapper("sink->Close()")};");
            builder.WriteLine($"result = {FieldAssignment(fieldName)}new GeoSource(path.Get());");
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryEllipseFactory(CodeBuilder builder, CanvasGeometry.Ellipse obj, string typeName, string fieldName)
        {
            builder.WriteLine($"{typeName} result;");
            builder.WriteLine("ComPtr<ID2D1EllipseGeometry> ellipse;");
            builder.WriteLine("FFHR(_d2dFactory->CreateEllipseGeometry(");
            builder.Indent();
            builder.WriteLine($"D2D1::Ellipse({{{Float(obj.X)},{Float(obj.Y)}}}, {Float(obj.RadiusX)}, {Float(obj.RadiusY)}),");
            builder.WriteLine("&ellipse));");
            builder.UnIndent();
            builder.WriteLine($"result = {FieldAssignment(fieldName)}new GeoSource(ellipse.Get());");
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryGroupFactory(CodeBuilder builder, CanvasGeometry.Group obj, string typeName, string fieldName)
        {
            builder.WriteLine($"ComPtr<ID2D1Geometry> geometries[{obj.Geometries.Length}];");
            builder.OpenScope();
            for (var i = 0; i < obj.Geometries.Length; i++)
            {
                var geometry = obj.Geometries[i];
                builder.WriteLine($"{CallFactoryFor(geometry)}.Get()->GetGeometry(&geometries[{i}]);");
            }

            builder.CloseScope();
            builder.WriteLine($"{typeName} result;");
            builder.WriteLine("ComPtr<ID2D1GeometryGroup> group;");
            builder.WriteLine("FFHR(_d2dFactory->CreateGeometryGroup(");
            builder.Indent();
            builder.WriteLine($"{FilledRegionDetermination(obj.FilledRegionDetermination)},");
            builder.WriteLine("geometries[0].GetAddressOf(),");
            builder.WriteLine($"{obj.Geometries.Length},");
            builder.WriteLine("&group));");
            builder.UnIndent();
            builder.WriteLine($"result = {FieldAssignment(fieldName)}new GeoSource(group.Get());");
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryPathFactory(CodeBuilder builder, CanvasGeometry.Path obj, string typeName, string fieldName)
        {
            builder.WriteLine($"{typeName} result;");

            // D2D Setup
            builder.WriteLine("ComPtr<ID2D1PathGeometry> path;");
            builder.WriteLine($"{FailFastWrapper("_d2dFactory->CreatePathGeometry(&path)")};");
            builder.WriteLine("ComPtr<ID2D1GeometrySink> sink;");
            builder.WriteLine($"{FailFastWrapper("path->Open(&sink)")};");

            if (obj.FilledRegionDetermination != CanvasFilledRegionDetermination.Alternate)
            {
                builder.WriteLine($"sink->SetFillMode({FilledRegionDetermination(obj.FilledRegionDetermination)});");
            }

            foreach (var command in obj.Commands)
            {
                switch (command.Type)
                {
                    case CanvasPathBuilder.CommandType.BeginFigure:
                        // Assume D2D1_FIGURE_BEGIN_FILLED
                        builder.WriteLine($"sink->BeginFigure({Vector2(((CanvasPathBuilder.Command.BeginFigure)command).StartPoint)}, D2D1_FIGURE_BEGIN_FILLED);");
                        break;
                    case CanvasPathBuilder.CommandType.EndFigure:
                        builder.WriteLine($"sink->EndFigure({CanvasFigureLoop(((CanvasPathBuilder.Command.EndFigure)command).FigureLoop)});");
                        break;
                    case CanvasPathBuilder.CommandType.AddLine:
                        builder.WriteLine($"sink->AddLine({Vector2(((CanvasPathBuilder.Command.AddLine)command).EndPoint)});");
                        break;
                    case CanvasPathBuilder.CommandType.AddCubicBezier:
                        var cb = (CanvasPathBuilder.Command.AddCubicBezier)command;
                        builder.WriteLine($"sink->AddBezier({{ {Vector2(cb.ControlPoint1)}, {Vector2(cb.ControlPoint2)}, {Vector2(cb.EndPoint)} }});");
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            builder.WriteLine($"{FailFastWrapper("sink->Close()")};");
            builder.WriteLine("GeoSource* rawResult = new GeoSource(path.Get());");
            builder.WriteLine($"result = {FieldAssignment(fieldName)}rawResult;");
            builder.WriteLine("rawResult->Release();");
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryRoundedRectangleFactory(CodeBuilder builder, CanvasGeometry.RoundedRectangle obj, string typeName, string fieldName)
        {
            builder.WriteLine($"{typeName} result;");
            builder.WriteLine("ComPtr<ID2D1RoundedRectangleGeometry> rect;");
            builder.WriteLine("FFHR(_d2dFactory->CreateRoundedRectangleGeometry(");
            builder.Indent();
            builder.WriteLine($"D2D1::RoundedRect({{{Float(obj.X)},{Float(obj.Y)}}}, {Float(obj.RadiusX)}, {Float(obj.RadiusY)}),");
            builder.WriteLine("&rect));");
            builder.UnIndent();
            builder.WriteLine($"result = {FieldAssignment(fieldName)}new GeoSource(rect.Get());");
        }

        /// <inheritdoc/>
        protected override void WriteCanvasGeometryTransformedGeometryFactory(CodeBuilder builder, CanvasGeometry.TransformedGeometry obj, string typeName, string fieldName)
        {
            builder.WriteLine($"{typeName} result;");
            builder.WriteLine("ID2D1Geometry *geoA = nullptr;");
            builder.WriteLine("ID2D1TransformedGeometry *transformed;");
            builder.WriteLine($"D2D1_MATRIX_3X2_F transformMatrix{_s.Matrix3x2(obj.TransformMatrix)};");
            builder.WriteLine();
            builder.WriteLine($"{CallFactoryFor(obj.SourceGeometry)}->GetGeometry(&geoA);");
            builder.WriteLine("FFHR(_d2dFactory->CreateTransformedGeometry(geoA, transformMatrix, &transformed));");
            builder.WriteLine("geoA->Release();");
            builder.WriteLine($"result = {FieldAssignment(fieldName)}new GeoSource(transformed);");
        }

        /// <summary>
        /// Generate the body of the TryCreateAnimatedVisual() method for the composition that contains LoadedImageSurfaces.
        /// </summary>
        void WriteTryCreateInstantiatorWithImageLoading(CodeBuilder builder, AnimatedVisualSourceInfo info)
        {
            builder.WriteLine("m_isTryCreateAnimatedVisualCalled = true;");
            builder.WriteLine();
            builder.WriteLine("diagnostics = nullptr;");
            builder.WriteLine("if (!IsRuntimeCompatible())");
            builder.OpenScope();
            builder.WriteLine("return nullptr;");
            builder.CloseScope();
            builder.WriteLine();
            builder.WriteLine("EnsureImageLoadingStarted();");
            builder.WriteLine();
            builder.WriteLine("if (m_isAnimatedVisualSourceDynamic && m_loadCompleteEventCount != c_loadedImageSurfaceCount)");
            builder.OpenScope();
            builder.WriteLine("return nullptr;");
            builder.CloseScope();
            builder.WriteLine("return ref new AnimatedVisual(compositor,");
            builder.Indent();
            builder.WriteCommaSeparatedLines(info.LoadedImageSurfaceNodes.Select(n => MakeFieldName(n.Name)));
            builder.UnIndent();
            builder.WriteLine(");");
            builder.CloseScope();
            builder.WriteLine();
        }

        void WriteIsAnimatedVisualSourceDynamicGetSet(CodeBuilder builder, AnimatedVisualSourceInfo info)
        {
            builder.WriteLine($"bool AnimatedVisuals::{info.ClassName}::IsAnimatedVisualSourceDynamic::get()");
            builder.OpenScope();
            builder.WriteLine("return m_isAnimatedVisualSourceDynamic;");
            builder.CloseScope();
            builder.WriteLine();
            builder.WriteLine($"void AnimatedVisuals::{info.ClassName}::IsAnimatedVisualSourceDynamic::set(bool isAnimatedVisualSourceDynamic)");
            builder.OpenScope();
            builder.WriteLine("if (!m_isTryCreateAnimatedVisualCalled && m_isAnimatedVisualSourceDynamic != isAnimatedVisualSourceDynamic)");
            builder.OpenScope();
            builder.WriteLine("m_isAnimatedVisualSourceDynamic = isAnimatedVisualSourceDynamic;");
            builder.WriteLine("PropertyChanged(this, ref new PropertyChangedEventArgs(\"IsAnimatedVisualSourceDynamic\"));");
            builder.CloseScope();
            builder.CloseScope();
        }

        void WriteEnsureImageLoadingStarted(CodeBuilder builder, AnimatedVisualSourceInfo info)
        {
            builder.WriteLine($"void AnimatedVisuals::{info.ClassName}::EnsureImageLoadingStarted()");
            builder.OpenScope();
            builder.WriteLine("if (!m_isImageLoadingStarted)");
            builder.OpenScope();
            builder.WriteLine($"auto eventHandler = ref new TypedEventHandler<LoadedImageSurface^, LoadedImageSourceLoadCompletedEventArgs^>(this, &AnimatedVisuals::{info.ClassName}::HandleLoadCompleted);");

            var nodes = info.LoadedImageSurfaceNodes.ToArray();
            foreach (var n in nodes)
            {
                var imageMemberName = MakeFieldName(n.Name);
                switch (n.LoadedImageSurfaceType)
                {
                    case LoadedImageSurface.LoadedImageSurfaceType.FromStream:
                        var streamName = $"stream_{n.Name}";
                        var dataWriterName = $"dataWriter_{n.Name}";
                        builder.WriteLine($"auto {streamName} = ref new InMemoryRandomAccessStream();");
                        builder.WriteLine($"auto {dataWriterName} = ref new DataWriter({streamName}->GetOutputStreamAt(0));");
                        builder.WriteLine($"{dataWriterName}->WriteBytes({n.BytesFieldName});");
                        builder.WriteLine($"{dataWriterName}->StoreAsync();");
                        builder.WriteLine($"{dataWriterName}->FlushAsync();");
                        builder.WriteLine($"{streamName}->Seek(0);");
                        builder.WriteLine($"{imageMemberName} = Windows::UI::Xaml::Media::LoadedImageSurface::StartLoadFromStream({streamName});");
                        break;
                    case LoadedImageSurface.LoadedImageSurfaceType.FromUri:
                        builder.WriteLine($"{imageMemberName} = Windows::UI::Xaml::Media::LoadedImageSurface::StartLoadFromUri(ref new Uri(\"{n.ImageUri}\"));");
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                builder.WriteLine($"{imageMemberName}->LoadCompleted += eventHandler;");
            }

            builder.WriteLine("m_isImageLoadingStarted = true;");
            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();
        }

        void WriteHandleLoadCompleted(CodeBuilder builder, AnimatedVisualSourceInfo info)
        {
            builder.WriteLine($"void AnimatedVisuals::{info.ClassName}::HandleLoadCompleted(LoadedImageSurface^ sender, LoadedImageSourceLoadCompletedEventArgs^ e)");
            builder.OpenScope();
            builder.WriteLine("m_loadCompleteEventCount++;");
            builder.WriteLine("if (e->Status == LoadedImageSourceLoadStatus::Success)");
            builder.OpenScope();
            builder.WriteLine("if (m_isAnimatedVisualSourceDynamic && m_loadCompleteEventCount == c_loadedImageSurfaceCount)");
            builder.OpenScope();
            builder.WriteLine("RaiseAnimatedVisualInvalidatedEvent(this, nullptr);");
            builder.CloseScope();
            builder.WriteLine("m_imageSuccessfulLoadingProgress = (double)m_loadCompleteEventCount / c_loadedImageSurfaceCount;");
            builder.WriteLine("PropertyChanged(this, ref new PropertyChangedEventArgs(\"ImageSuccessfulLoadingProgress\"));");
            builder.CloseScope();
            builder.WriteLine();
            builder.WriteLine("if (m_loadCompleteEventCount == c_loadedImageSurfaceCount)");
            builder.OpenScope();
            builder.WriteLine("m_isImageLoadingCompleted = true;");
            builder.WriteLine("PropertyChanged(this, ref new PropertyChangedEventArgs(\"IsImageLoadingCompleted\"));");
            builder.CloseScope();
            builder.CloseScope();
            builder.WriteLine();
        }

        /// <inheritdoc/>
        protected override string WriteCompositeEffectFactory(CodeBuilder builder, Mgce.CompositeEffect compositeEffect)
        {
            builder.WriteLine("ComPtr<CompositeEffect> compositeEffect(new CompositeEffect());");
            builder.WriteLine($"compositeEffect->SetMode({_s.CanvasCompositeMode(compositeEffect.Mode)});");
            foreach (var source in compositeEffect.Sources)
            {
                builder.OpenScope();
                builder.WriteLine($"auto sourceParameter = ref new CompositionEffectSourceParameter({String(source.Name)});");
                builder.WriteLine("compositeEffect->AddSource(reinterpret_cast<ABI::Windows::Graphics::Effects::IGraphicsEffectSource*>(sourceParameter));");
                builder.CloseScope();
            }

            return "reinterpret_cast<Windows::Graphics::Effects::IGraphicsEffect^>(compositeEffect.Get())";
        }

        string CanvasFigureLoop(CanvasFigureLoop value) => _s.CanvasFigureLoop(value);

        static string FieldAssignment(string fieldName) => fieldName != null ? $"{fieldName} = " : string.Empty;

        string FilledRegionDetermination(CanvasFilledRegionDetermination value) => _s.FilledRegionDetermination(value);

        string Float(float value) => _s.Float(value);

        string FailFastWrapper(string value) => _s.FailFastWrapper(value);

        string String(string value) => _s.String(value);

        string Vector2(Vector2 value) => _s.Vector2(value);

        string MakeFieldName(string value) => $"m_{_s.CamelCase(value)}";

        static string CommonHeaderText => "#pragma once\r\n" + string.Join("\r\n", AutoGeneratedHeaderText);

        static string IAnimatedVisualSourceHeaderText(string className)
        {
            return
$@"{CommonHeaderText}

namespace AnimatedVisuals 
{{
public ref class {className} sealed : public Microsoft::UI::Xaml::Controls::IAnimatedVisualSource
{{
public:
    virtual Microsoft::UI::Xaml::Controls::IAnimatedVisual^ TryCreateAnimatedVisual(
        Windows::UI::Composition::Compositor^ compositor,
        Platform::Object^* diagnostics);
}};
}}";
        }

        string IDynamicAnimatedVisualSourceHeaderText(string className, IEnumerable<LoadedImageSurfaceInfo> loadedImageSurfaceInfo)
        {
            var nodes = loadedImageSurfaceInfo.ToArray();
            var imageFieldsText = new StringBuilder();

            foreach (var n in nodes)
            {
                imageFieldsText.AppendLine($"    {_s.ReferenceTypeName(n.TypeName)} {MakeFieldName(n.Name)}{{}};");
            }

            return
$@"{CommonHeaderText}
using namespace Microsoft::UI::Xaml::Controls;
using namespace Platform;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Data;
using namespace Windows::UI::Xaml::Media;

namespace AnimatedVisuals
{{
public ref class {className} sealed : public IDynamicAnimatedVisualSource, INotifyPropertyChanged
{{
public:
    virtual event Windows::Foundation::TypedEventHandler<IDynamicAnimatedVisualSource^, Platform::Object^>^ AnimatedVisualInvalidated
    {{
        Windows::Foundation::EventRegistrationToken add(Windows::Foundation::TypedEventHandler<IDynamicAnimatedVisualSource^, Platform::Object ^>^ value)
        {{
            return m_InternalHandler::add(value);
        }}
        void remove(Windows::Foundation::EventRegistrationToken token)
        {{
            m_InternalHandler::remove(token);
        }}
    }}

    virtual Microsoft::UI::Xaml::Controls::IAnimatedVisual^ TryCreateAnimatedVisual(
        Windows::UI::Composition::Compositor^ compositor,
        Platform::Object^* diagnostics);

    virtual event PropertyChangedEventHandler^ PropertyChanged;

    /// <summary>
    /// If this property is set to true, <see cref=""TryCreateAnimatedVisual""/> will return null until all
    /// images have loaded. When all images have loaded, <see cref=""TryCreateAnimatedVisual""/> will return
    /// the AnimatedVisual. To use, set it when declaring the AnimatedVisualSource. Once <see cref=""TryCreateAnimatedVisual""/> 
    /// is called, changes made to this property will be ignored.
    /// Default value is true.
    /// </summary>
    property bool IsAnimatedVisualSourceDynamic
    {{
        bool get();
        void set(bool value);
    }}

    /// <summary>
    /// Returns true if all images have loaded. To see if the images succeeded to load, see <see cref=""ImageSuccessfulLoadingProgress""/>.
    /// </summary>
    property bool IsImageLoadingCompleted
    {{
        bool get() {{ return m_isImageLoadingCompleted; }}
    }}

    /// <summary>
    /// Represents the progress of successful image loading. Returns value between 0 and 1. 0
    /// means none of the images succeeded to load. 1 means all images succeeded to load.
    /// </summary>
    property double ImageSuccessfulLoadingProgress
    {{
        double get() {{ return m_imageSuccessfulLoadingProgress; }}
    }}

private:
    const int c_loadedImageSurfaceCount = {loadedImageSurfaceInfo.Distinct().Count()};
    double m_imageSuccessfulLoadingProgress{{}};
    int m_loadCompleteEventCount{{}};
    bool m_isAnimatedVisualSourceDynamic{{ true }};
    bool m_isImageLoadingCompleted{{}};
    bool m_isTryCreateAnimatedVisualCalled{{}};
    bool m_isImageLoadingStarted{{}};
    event Windows::Foundation::TypedEventHandler<IDynamicAnimatedVisualSource^, Platform::Object^>^ m_InternalHandler;
{imageFieldsText.ToString()}

    void EnsureImageLoadingStarted();
    void HandleLoadCompleted(LoadedImageSurface^ sender, LoadedImageSourceLoadCompletedEventArgs^ e);
    void RaiseAnimatedVisualInvalidatedEvent(IDynamicAnimatedVisualSource^ sender, Platform::Object^ object)
    {{
        m_InternalHandler::raise(sender, object);
    }}
}};
}}";
        }

        string CompositionEffectClass =>
@"

enum CanvasComposite : int
{
    SourceOver = 0,
    DestinationOver = 1,
    SourceIn = 2,
    DestinationIn = 3,
    SourceOut = 4,
    DestinationOut = 5,
    SourceAtop = 6,
    DestinationAtop = 7,
    Xor = 8,
    Add = 9,
    Copy = 10,
    BoundedCopy = 11,
    MaskInvert = 12,
};

// This class is a substitute for the Microsoft::Graphics::Canvas::Effects::CompositeEffect
// class so that composite effects can be used with 
// Windows::UI::Composition::CompositionEffectBrush without requiring Win2d. This is
// achieved by implementing the interfaces Windows::UI::Composition requires for it
// to consume an effect.
class CompositeEffect final :
    public ABI::Windows::Graphics::Effects::IGraphicsEffect,
    public ABI::Windows::Graphics::Effects::IGraphicsEffectSource,
    public ABI::Windows::Graphics::Effects::IGraphicsEffectD2D1Interop
{
public:
    void SetMode(CanvasComposite mode) { m_mode = mode; }

    void AddSource(IGraphicsEffectSource* source)
    {
        m_sources.emplace_back(Microsoft::WRL::ComPtr<IGraphicsEffectSource>(source));
    }

    // IGraphicsEffect
    IFACEMETHODIMP get_Name(HSTRING* name) override { return m_name.CopyTo(name); }

    IFACEMETHODIMP put_Name(HSTRING name) override { return m_name.Set(name); }

    // IGraphicsEffectD2D1Interop
    IFACEMETHODIMP GetEffectId(GUID* id) override 
    { 
        if (id != nullptr)
        {
            // set CLSID_D2D1Composite value
            *id = { 0x48fc9f51, 0xf6ac, 0x48f1, { 0x8b, 0x58,  0x3b,  0x28,  0xac,  0x46,  0xf7,  0x6d } };
        }

        return S_OK; 
    }

    IFACEMETHODIMP GetSourceCount(UINT* count) override
    {
        if (count != nullptr)
        {
            *count = m_sources.size();
        }

        return S_OK;
    }

    IFACEMETHODIMP GetSource(
        UINT index, 
        IGraphicsEffectSource** source) override
    {
        if (index >= m_sources.size() ||
            source == nullptr)
        {
            return E_INVALIDARG;
        }

        *source = m_sources.at(index).Get();
        (*source)->AddRef();

        return S_OK;
    }

    IFACEMETHODIMP GetPropertyCount(UINT * count) override { *count = 1; return S_OK; }

    IFACEMETHODIMP GetProperty(
        UINT index, 
        ABI::Windows::Foundation::IPropertyValue ** value) override
    {
        Microsoft::WRL::ComPtr<ABI::Windows::Foundation::IPropertyValueStatics> propertyValueFactory;
        Microsoft::WRL::Wrappers::HStringReference activatableClassId{ RuntimeClass_Windows_Foundation_PropertyValue };
        HRESULT hr = ABI::Windows::Foundation::GetActivationFactory(activatableClassId.Get(), &propertyValueFactory);

        if (SUCCEEDED(hr))
        {
            switch (index)
            {
                case D2D1_COMPOSITE_PROP_MODE: 
                    return propertyValueFactory->CreateUInt32(m_mode, (IInspectable**)value);
                default: 
                    return E_INVALIDARG;
            }
        }

        return hr;
    }

    IFACEMETHODIMP GetNamedPropertyMapping(
        LPCWSTR, 
        UINT*,
        ABI::Windows::Graphics::Effects::GRAPHICS_EFFECT_PROPERTY_MAPPING*) override
    {
        return E_INVALIDARG;
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(
        REFIID iid,
        void ** ppvObject) override
    {
        if (ppvObject != nullptr)
        {
            *ppvObject = nullptr;

            if (iid == __uuidof(IUnknown))
            {
                *ppvObject = static_cast<IUnknown*>(static_cast<IGraphicsEffect*>(this));
            }
            else if (iid == __uuidof(IInspectable))
            {
                *ppvObject = static_cast<IInspectable*>(static_cast<IGraphicsEffect*>(this));
            }
            else if (iid == __uuidof(IGraphicsEffect))
            {
                *ppvObject = static_cast<IGraphicsEffect*>(this);
            }
            else if (iid == __uuidof(IGraphicsEffectSource))
            {
                *ppvObject = static_cast<IGraphicsEffectSource*>(this);
            }
            else if (iid == __uuidof(IGraphicsEffectD2D1Interop))
            {
                *ppvObject = static_cast<IGraphicsEffectD2D1Interop*>(this);
            }

            if (*ppvObject != nullptr)
            {
                AddRef();
                return S_OK;
            }
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&m_cRef);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        ULONG cRef = InterlockedDecrement(&m_cRef);
        if (0 == cRef)
        {
            delete this;
        }

        return cRef;
    }

    // IInspectable
    IFACEMETHODIMP GetIids(
        ULONG * iidCount,
        IID ** iids) override
    {
        if (iidCount != nullptr)
        {
            *iidCount = 0;
        }

        if (iids != nullptr)
        {
            *iids = nullptr;
        }

        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetRuntimeClassName(
        HSTRING * /*runtimeName*/) override
    {
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetTrustLevel(
        TrustLevel* trustLvl) override
    {
        if (trustLvl != nullptr)
        {
            *trustLvl = BaseTrust;
        }

        return S_OK;
    }

private:
    ULONG m_cRef = 0;

    CanvasComposite m_mode{};

    Microsoft::WRL::Wrappers::HString m_name{};

    std::vector<Microsoft::WRL::ComPtr<IGraphicsEffectSource>> m_sources;
};
";
    }
}
