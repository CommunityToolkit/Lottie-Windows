// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Stand-ins for the Windows.UI.Composition, Windows.UI.Xaml.Media and Win2D APIs
// that CompositionInterpreter is written against.
//
// The interpreter's whole job is to make the right sequence of calls into those
// APIs, and those APIs are only present on Windows. Rather than testing the
// interpreter by looking at pixels on a Windows machine, these types stand in for
// the real ones: they are declared in the real namespaces with the real names and
// the members that the interpreter uses, and they record what was done to them so
// that the resulting tree can be dumped and compared against the WinCompData graph
// that produced the buffer.
//
// They are deliberately dumb. They contain no logic beyond recording, so a test
// failure is always a fault in the interpreter rather than in the stand-ins. The
// same technique is used by tests/Mocks.cs, which mocks Win2D so that LottieGen's
// generated code can be compiled off Windows.
//
// Two details are modelled rather than merely recorded, because the dump depends
// on them:
//
//  * A property that has never been set is distinguishable from one that has been
//    set to its default value, which is what makes the dump comparable with the
//    WinCompData dump. That is why the properties are nullable.
//  * StartAnimation creates an AnimationController for every animation that is not
//    an ExpressionAnimation, which is what Windows.UI.Composition does and what
//    WinCompData models.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Windows.Graphics.Effects;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Mgc = Microsoft.Graphics.Canvas;
using Mgcg = Microsoft.Graphics.Canvas.Geometry;

namespace Windows.UI
{
    struct Color
    {
        public byte A { get; private set; }

        public byte R { get; private set; }

        public byte G { get; private set; }

        public byte B { get; private set; }

        public static Color FromArgb(byte a, byte r, byte g, byte b)
            => new Color { A = a, R = r, G = g, B = b };
    }
}

namespace Windows.Foundation.Metadata
{
    static class ApiInformation
    {
        // The version of the API contract that the stand-ins pretend to implement.
        // It is well beyond any version that the translator asks for, so that a
        // composition from the corpus always loads, and a test that wants the check
        // to fail asks for a version that no version of Windows will ever have.
        public const ushort UniversalApiContractVersion = 20;

        public static bool IsApiContractPresent(string contractName, ushort majorVersion)
            => contractName == "Windows.Foundation.UniversalApiContract" &&
               majorVersion <= UniversalApiContractVersion;
    }
}

namespace Windows.Graphics.Effects
{
    interface IGraphicsEffectSource
    {
    }

    interface IGraphicsEffect : IGraphicsEffectSource
    {
    }
}

namespace Windows.Storage.Streams
{
    interface IBuffer
    {
        byte[] Bytes { get; }
    }

    interface IRandomAccessStream
    {
        byte[] Bytes { get; }
    }

    sealed class ByteArrayBuffer : IBuffer
    {
        internal ByteArrayBuffer(byte[] bytes) => Bytes = bytes;

        public byte[] Bytes { get; }
    }

    sealed class RandomAccessStream : IRandomAccessStream
    {
        internal RandomAccessStream(byte[] bytes) => Bytes = bytes;

        public byte[] Bytes { get; }
    }

    // Stands in for the stream that a byte array is wrapped in on the way to
    // LoadedImageSurface. The bytes are carried through unchanged so that the dump
    // can show what the surface was given.
    sealed class ByteArrayStream : MemoryStream
    {
        internal ByteArrayStream(byte[] bytes)
            : base(bytes, writable: false)
        {
            Bytes = bytes;
        }

        public byte[] Bytes { get; }
    }
}

namespace System.Runtime.InteropServices.WindowsRuntime
{
    static class WindowsRuntimeBufferExtensions
    {
        public static IBuffer AsBuffer(this byte[] source) => new ByteArrayBuffer(source);

        public static Stream AsStream(this IBuffer source) => new ByteArrayStream(source.Bytes);
    }
}

namespace System.IO
{
    static class WindowsRuntimeStreamExtensions
    {
        public static IRandomAccessStream AsRandomAccessStream(this Stream source)
            => new RandomAccessStream(((ByteArrayStream)source).Bytes);
    }
}

namespace Microsoft.Graphics.Canvas
{
    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.Mgc.CanvasComposite.
    enum CanvasComposite
    {
        SourceOver,
        DestinationOver,
        SourceIn,
        DestinationIn,
        SourceOut,
        DestinationOut,
        SourceAtop,
        DestinationAtop,
        Xor,
        Add,
        Copy,
        BoundedCopy,
        MaskInvert,
    }
}

namespace Microsoft.Graphics.Canvas.Effects
{
    sealed class CompositeEffect : IGraphicsEffect
    {
        public Mgc.CanvasComposite Mode { get; set; }

        public IList<IGraphicsEffectSource> Sources { get; } = new List<IGraphicsEffectSource>();
    }

    sealed class GaussianBlurEffect : IGraphicsEffect
    {
        public float BlurAmount { get; set; }

        public IGraphicsEffectSource? Source { get; set; }
    }
}

namespace Microsoft.Graphics.Canvas.Geometry
{
    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg.CanvasFigureLoop.
    enum CanvasFigureLoop
    {
        Open,
        Closed,
    }

    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg.CanvasFilledRegionDetermination.
    enum CanvasFilledRegionDetermination
    {
        Alternate,
        Winding,
    }

    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg.CanvasGeometryCombine.
    enum CanvasGeometryCombine
    {
        Union,
        Exclude,
        Intersect,
        Xor,
    }

    // The shape of a geometry is recorded rather than rasterized. The nested types
    // mirror the ones in WinCompData.Mgcg.CanvasGeometry so that the two can be
    // compared.
    abstract class CanvasGeometry
    {
        public static CanvasGeometry CreateEllipse(object? device, float x, float y, float radiusX, float radiusY)
            => new Ellipse(x, y, radiusX, radiusY);

        public static CanvasGeometry CreateRoundedRectangle(
            object? device, float x, float y, float w, float h, float radiusX, float radiusY)
            => new RoundedRectangle(x, y, w, h, radiusX, radiusY);

        public static CanvasGeometry CreateGroup(
            object? device, CanvasGeometry[] geometries, CanvasFilledRegionDetermination filledRegionDetermination)
            => new Group(geometries, filledRegionDetermination);

        public static CanvasGeometry CreatePath(CanvasPathBuilder builder)
            => new Path(builder.Commands, builder.FilledRegionDetermination);

        public CanvasGeometry CombineWith(
            CanvasGeometry other, Matrix3x2 matrix, CanvasGeometryCombine combineMode)
            => new Combination(this, other, matrix, combineMode);

        public CanvasGeometry Transform(Matrix3x2 transformMatrix)
            => new TransformedGeometry(this, transformMatrix);

        public sealed class Combination : CanvasGeometry
        {
            internal Combination(CanvasGeometry a, CanvasGeometry b, Matrix3x2 matrix, CanvasGeometryCombine combineMode)
                => (A, B, Matrix, CombineMode) = (a, b, matrix, combineMode);

            public CanvasGeometry A { get; }

            public CanvasGeometry B { get; }

            public Matrix3x2 Matrix { get; }

            public CanvasGeometryCombine CombineMode { get; }
        }

        public sealed class TransformedGeometry : CanvasGeometry
        {
            internal TransformedGeometry(CanvasGeometry sourceGeometry, Matrix3x2 transformMatrix)
                => (SourceGeometry, TransformMatrix) = (sourceGeometry, transformMatrix);

            public CanvasGeometry SourceGeometry { get; }

            public Matrix3x2 TransformMatrix { get; }
        }

        public sealed class Group : CanvasGeometry
        {
            internal Group(CanvasGeometry[] geometries, CanvasFilledRegionDetermination filledRegionDetermination)
                => (Geometries, FilledRegionDetermination) = (geometries, filledRegionDetermination);

            public CanvasGeometry[] Geometries { get; }

            public CanvasFilledRegionDetermination FilledRegionDetermination { get; }
        }

        public sealed class Path : CanvasGeometry
        {
            internal Path(
                IReadOnlyList<CanvasPathBuilder.Command> commands,
                CanvasFilledRegionDetermination filledRegionDetermination)
                => (Commands, FilledRegionDetermination) = (commands, filledRegionDetermination);

            public IReadOnlyList<CanvasPathBuilder.Command> Commands { get; }

            public CanvasFilledRegionDetermination FilledRegionDetermination { get; }
        }

        public sealed class Ellipse : CanvasGeometry
        {
            internal Ellipse(float x, float y, float radiusX, float radiusY)
                => (X, Y, RadiusX, RadiusY) = (x, y, radiusX, radiusY);

            public float X { get; }

            public float Y { get; }

            public float RadiusX { get; }

            public float RadiusY { get; }
        }

        public sealed class RoundedRectangle : CanvasGeometry
        {
            internal RoundedRectangle(float x, float y, float w, float h, float radiusX, float radiusY)
                => (X, Y, W, H, RadiusX, RadiusY) = (x, y, w, h, radiusX, radiusY);

            public float X { get; }

            public float Y { get; }

            public float W { get; }

            public float H { get; }

            public float RadiusX { get; }

            public float RadiusY { get; }
        }
    }

    sealed class CanvasPathBuilder : IDisposable
    {
        readonly List<Command> _commands = new List<Command>();

        public CanvasPathBuilder(object? device)
        {
        }

        internal IReadOnlyList<Command> Commands => _commands;

        internal CanvasFilledRegionDetermination FilledRegionDetermination { get; private set; }

        public void SetFilledRegionDetermination(CanvasFilledRegionDetermination value)
            => FilledRegionDetermination = value;

        public void BeginFigure(Vector2 startPoint) => _commands.Add(new Command.BeginFigure(startPoint));

        public void EndFigure(CanvasFigureLoop figureLoop) => _commands.Add(new Command.EndFigure(figureLoop));

        public void AddLine(Vector2 endPoint) => _commands.Add(new Command.AddLine(endPoint));

        public void AddCubicBezier(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 endPoint)
            => _commands.Add(new Command.AddCubicBezier(controlPoint1, controlPoint2, endPoint));

        public void Dispose()
        {
        }

        internal abstract class Command
        {
            internal sealed class BeginFigure : Command
            {
                internal BeginFigure(Vector2 startPoint) => StartPoint = startPoint;

                public Vector2 StartPoint { get; }
            }

            internal sealed class EndFigure : Command
            {
                internal EndFigure(CanvasFigureLoop figureLoop) => FigureLoop = figureLoop;

                public CanvasFigureLoop FigureLoop { get; }
            }

            internal sealed class AddLine : Command
            {
                internal AddLine(Vector2 endPoint) => EndPoint = endPoint;

                public Vector2 EndPoint { get; }
            }

            internal sealed class AddCubicBezier : Command
            {
                internal AddCubicBezier(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 endPoint)
                    => (ControlPoint1, ControlPoint2, EndPoint) = (controlPoint1, controlPoint2, endPoint);

                public Vector2 ControlPoint1 { get; }

                public Vector2 ControlPoint2 { get; }

                public Vector2 EndPoint { get; }
            }
        }
    }
}

namespace Windows.UI.Xaml.Media
{
    // The two loaded image surfaces are separate types here, unlike in the real API
    // where one type is returned by both factories, so that the dump can tell them
    // apart in the same way that the WinCompData dump does.
    abstract class LoadedImageSurface : ICompositionSurface
    {
        public static LoadedImageSurface StartLoadFromUri(Uri uri) => new LoadedImageSurfaceFromUri(uri);

        public static LoadedImageSurface StartLoadFromStream(IRandomAccessStream stream)
            => new LoadedImageSurfaceFromStream(stream.Bytes);
    }

    sealed class LoadedImageSurfaceFromUri : LoadedImageSurface
    {
        internal LoadedImageSurfaceFromUri(Uri uri) => Uri = uri;

        public Uri Uri { get; }
    }

    sealed class LoadedImageSurfaceFromStream : LoadedImageSurface
    {
        internal LoadedImageSurfaceFromStream(byte[] bytes) => Bytes = bytes;

        public byte[] Bytes { get; }
    }
}

namespace Windows.UI.Composition
{
    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.CompositionBorderMode.
    enum CompositionBorderMode
    {
        Inherit = 0,
        Soft = 1,
        Hard = 2,
    }

    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.CompositionStrokeCap.
    enum CompositionStrokeCap
    {
        Flat,
        Square,
        Round,
        Triangle,
    }

    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.CompositionStrokeLineJoin.
    enum CompositionStrokeLineJoin
    {
        Miter,
        Bevel,
        Round,
        MiterOrBevel,
    }

    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.CompositionGradientExtendMode.
    enum CompositionGradientExtendMode
    {
        Clamp = 0,
        Wrap = 1,
        Mirror = 2,
    }

    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.CompositionColorSpace.
    enum CompositionColorSpace
    {
        Auto = 0,
        Hsl = 1,
        Rgb = 2,
        HslLinear = 3,
        RgbLinear = 4,
    }

    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.CompositionMappingMode.
    enum CompositionMappingMode
    {
        Absolute = 0,
        Relative = 1,
    }

    // Values match CommunityToolkit.WinUI.Lottie.WinCompData.CompositionDropShadowSourcePolicy.
    enum CompositionDropShadowSourcePolicy
    {
        Default = 0,
        InheritFromVisualContent = 1,
    }

    interface ICompositionSurface
    {
    }

    abstract class CompositionObject
    {
        readonly List<Animator> _animators = new List<Animator>();

        private protected CompositionObject()
            => Properties = this as CompositionPropertySet ?? new CompositionPropertySet(this);

        public string? Comment { get; set; }

        public CompositionPropertySet Properties { get; }

        internal IReadOnlyList<Animator> Animators => _animators;

        public void StartAnimation(string propertyName, CompositionAnimation animation)
            => StartAnimation(propertyName, animation, null);

        public void StartAnimation(
            string propertyName, CompositionAnimation animation, AnimationController? animationController)
        {
            // A controller comes into existence with the animation, unless the
            // animation is an expression, which is driven by its inputs rather than by
            // a clock and so has nothing to control.
            var controller = animationController ??
                (animation is ExpressionAnimation ? null : new AnimationController(this, propertyName));

            _animators.Add(new Animator(propertyName, animation, controller));
        }

        public AnimationController? TryGetAnimationController(string propertyName)
        {
            foreach (var animator in _animators)
            {
                if (animator.AnimatedProperty == propertyName)
                {
                    return animator.Controller;
                }
            }

            return null;
        }

        internal sealed class Animator
        {
            internal Animator(string animatedProperty, CompositionAnimation animation, AnimationController? controller)
                => (AnimatedProperty, Animation, Controller) = (animatedProperty, animation, controller);

            public string AnimatedProperty { get; }

            public CompositionAnimation Animation { get; }

            public AnimationController? Controller { get; }
        }
    }

    sealed class AnimationController : CompositionObject
    {
        internal AnimationController(CompositionObject targetObject, string targetProperty)
            => (TargetObject, TargetProperty) = (targetObject, targetProperty);

        internal AnimationController()
        {
        }

        public bool IsCustom => TargetObject is null;

        public CompositionObject? TargetObject { get; }

        public string? TargetProperty { get; }

        public bool IsPaused { get; private set; }

        public void Pause() => IsPaused = true;
    }

    enum PropertySetValueType
    {
        Color,
        Scalar,
        Vector2,
        Vector3,
        Vector4,
    }

    sealed class CompositionPropertySet : CompositionObject
    {
        // A SortedDictionary with the default comparer, exactly as WinCompData uses,
        // so that the dump of a property set is in the same order on both sides.
        readonly SortedDictionary<string, PropertySetValueType> _names =
            new SortedDictionary<string, PropertySetValueType>();

        readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        internal CompositionPropertySet(CompositionObject? owner) => Owner = owner;

        public CompositionObject? Owner { get; }

        internal IReadOnlyDictionary<string, PropertySetValueType> Names => _names;

        public void InsertColor(string propertyName, UI.Color value)
            => Insert(propertyName, PropertySetValueType.Color, value);

        public void InsertScalar(string propertyName, float value)
            => Insert(propertyName, PropertySetValueType.Scalar, value);

        public void InsertVector2(string propertyName, Vector2 value)
            => Insert(propertyName, PropertySetValueType.Vector2, value);

        public void InsertVector3(string propertyName, Vector3 value)
            => Insert(propertyName, PropertySetValueType.Vector3, value);

        public void InsertVector4(string propertyName, Vector4 value)
            => Insert(propertyName, PropertySetValueType.Vector4, value);

        internal object GetValue(string propertyName) => _values[propertyName];

        void Insert(string propertyName, PropertySetValueType type, object value)
        {
            _names[propertyName] = type;
            _values[propertyName] = value;
        }
    }

    sealed class CompositionPath
    {
        public CompositionPath(Mgcg.CanvasGeometry source) => Source = source;

        public Mgcg.CanvasGeometry Source { get; }
    }

    sealed class CompositionEffectSourceParameter : IGraphicsEffectSource
    {
        public CompositionEffectSourceParameter(string name) => Name = name;

        public string Name { get; }
    }

    abstract class Visual : CompositionObject
    {
        public CompositionBorderMode? BorderMode { get; set; }

        public Vector3? CenterPoint { get; set; }

        public CompositionClip? Clip { get; set; }

        public bool? IsVisible { get; set; }

        public Vector3? Offset { get; set; }

        public float? Opacity { get; set; }

        public float? RotationAngleInDegrees { get; set; }

        public Vector3? RotationAxis { get; set; }

        public Vector3? Scale { get; set; }

        public Vector2? Size { get; set; }

        public Matrix4x4? TransformMatrix { get; set; }
    }

    sealed class VisualCollection
    {
        readonly List<Visual> _visuals = new List<Visual>();

        internal IReadOnlyList<Visual> Visuals => _visuals;

        public void InsertAtTop(Visual visual) => _visuals.Add(visual);
    }

    class ContainerVisual : Visual
    {
        public VisualCollection Children { get; } = new VisualCollection();
    }

    sealed class ShapeVisual : ContainerVisual
    {
        public IList<CompositionShape> Shapes { get; } = new List<CompositionShape>();

        public CompositionViewBox? ViewBox { get; set; }
    }

    sealed class SpriteVisual : ContainerVisual
    {
        public CompositionBrush? Brush { get; set; }

        public CompositionShadow? Shadow { get; set; }
    }

    sealed class LayerVisual : ContainerVisual
    {
        public CompositionShadow? Shadow { get; set; }
    }

    abstract class CompositionShape : CompositionObject
    {
        public Vector2? CenterPoint { get; set; }

        public Vector2? Offset { get; set; }

        public float? RotationAngleInDegrees { get; set; }

        public Vector2? Scale { get; set; }

        public Matrix3x2? TransformMatrix { get; set; }
    }

    sealed class CompositionContainerShape : CompositionShape
    {
        public IList<CompositionShape> Shapes { get; } = new List<CompositionShape>();
    }

    sealed class CompositionSpriteShape : CompositionShape
    {
        public CompositionBrush? FillBrush { get; set; }

        public CompositionBrush? StrokeBrush { get; set; }

        public CompositionGeometry? Geometry { get; set; }

        public bool? IsStrokeNonScaling { get; set; }

        public float? StrokeDashOffset { get; set; }

        public IList<float> StrokeDashArray { get; } = new List<float>();

        public CompositionStrokeCap? StrokeDashCap { get; set; }

        public CompositionStrokeCap? StrokeStartCap { get; set; }

        public CompositionStrokeCap? StrokeEndCap { get; set; }

        public CompositionStrokeLineJoin? StrokeLineJoin { get; set; }

        public float? StrokeMiterLimit { get; set; }

        public float? StrokeThickness { get; set; }
    }

    abstract class CompositionGeometry : CompositionObject
    {
        public float? TrimStart { get; set; }

        public float? TrimEnd { get; set; }

        public float? TrimOffset { get; set; }
    }

    sealed class CompositionPathGeometry : CompositionGeometry
    {
        public CompositionPath? Path { get; set; }
    }

    sealed class CompositionRectangleGeometry : CompositionGeometry
    {
        public Vector2? Offset { get; set; }

        public Vector2? Size { get; set; }
    }

    sealed class CompositionRoundedRectangleGeometry : CompositionGeometry
    {
        public Vector2? Offset { get; set; }

        public Vector2? Size { get; set; }

        public Vector2? CornerRadius { get; set; }
    }

    sealed class CompositionEllipseGeometry : CompositionGeometry
    {
        public Vector2? Center { get; set; }

        public Vector2? Radius { get; set; }
    }

    abstract class CompositionBrush : CompositionObject
    {
    }

    sealed class CompositionColorBrush : CompositionBrush
    {
        public UI.Color? Color { get; set; }
    }

    abstract class CompositionGradientBrush : CompositionBrush
    {
        public Vector2? AnchorPoint { get; set; }

        public Vector2? CenterPoint { get; set; }

        public IList<CompositionColorGradientStop> ColorStops { get; } = new List<CompositionColorGradientStop>();

        public CompositionGradientExtendMode? ExtendMode { get; set; }

        public CompositionColorSpace? InterpolationSpace { get; set; }

        public CompositionMappingMode? MappingMode { get; set; }

        public Vector2? Offset { get; set; }

        public float? RotationAngleInDegrees { get; set; }

        public Vector2? Scale { get; set; }

        public Matrix3x2? TransformMatrix { get; set; }
    }

    sealed class CompositionLinearGradientBrush : CompositionGradientBrush
    {
        public Vector2? StartPoint { get; set; }

        public Vector2? EndPoint { get; set; }
    }

    sealed class CompositionRadialGradientBrush : CompositionGradientBrush
    {
        public Vector2? EllipseCenter { get; set; }

        public Vector2? EllipseRadius { get; set; }

        public Vector2? GradientOriginOffset { get; set; }
    }

    sealed class CompositionMaskBrush : CompositionBrush
    {
        public CompositionBrush? Source { get; set; }

        public CompositionBrush? Mask { get; set; }
    }

    sealed class CompositionSurfaceBrush : CompositionBrush
    {
        internal CompositionSurfaceBrush(ICompositionSurface? surface) => Surface = surface;

        public ICompositionSurface? Surface { get; set; }
    }

    sealed class CompositionEffectBrush : CompositionBrush
    {
        readonly Dictionary<string, CompositionBrush> _sources = new Dictionary<string, CompositionBrush>();
        readonly CompositionEffectFactory _factory;

        internal CompositionEffectBrush(CompositionEffectFactory factory) => _factory = factory;

        public void SetSourceParameter(string name, CompositionBrush source) => _sources[name] = source;

        internal CompositionEffectFactory GetEffectFactory() => _factory;

        internal CompositionBrush? GetSourceParameter(string name)
            => _sources.TryGetValue(name, out var result) ? result : null;
    }

    sealed class CompositionEffectFactory : CompositionObject
    {
        internal CompositionEffectFactory(IGraphicsEffect effect) => Effect = effect;

        internal IGraphicsEffect Effect { get; }

        public CompositionEffectBrush CreateBrush() => new CompositionEffectBrush(this);
    }

    sealed class CompositionColorGradientStop : CompositionObject
    {
        public UI.Color? Color { get; set; }

        public float? Offset { get; set; }
    }

    sealed class CompositionViewBox : CompositionObject
    {
        public Vector2? Size { get; set; }
    }

    abstract class CompositionClip : CompositionObject
    {
        public Vector2? CenterPoint { get; set; }

        public Vector2? Scale { get; set; }
    }

    sealed class InsetClip : CompositionClip
    {
        public float? LeftInset { get; set; }

        public float? RightInset { get; set; }

        public float? TopInset { get; set; }

        public float? BottomInset { get; set; }
    }

    sealed class CompositionGeometricClip : CompositionClip
    {
        public CompositionGeometry? Geometry { get; set; }
    }

    abstract class CompositionShadow : CompositionObject
    {
    }

    sealed class DropShadow : CompositionShadow
    {
        public float? BlurRadius { get; set; }

        public UI.Color? Color { get; set; }

        public CompositionBrush? Mask { get; set; }

        public Vector3? Offset { get; set; }

        public float? Opacity { get; set; }

        public CompositionDropShadowSourcePolicy? SourcePolicy { get; set; }
    }

    sealed class CompositionVisualSurface : CompositionObject, ICompositionSurface
    {
        public Visual? SourceVisual { get; set; }

        public Vector2? SourceSize { get; set; }

        public Vector2? SourceOffset { get; set; }
    }

    abstract class CompositionEasingFunction : CompositionObject
    {
    }

    sealed class LinearEasingFunction : CompositionEasingFunction
    {
    }

    sealed class CubicBezierEasingFunction : CompositionEasingFunction
    {
        internal CubicBezierEasingFunction(Vector2 controlPoint1, Vector2 controlPoint2)
            => (ControlPoint1, ControlPoint2) = (controlPoint1, controlPoint2);

        public Vector2 ControlPoint1 { get; }

        public Vector2 ControlPoint2 { get; }
    }

    sealed class StepEasingFunction : CompositionEasingFunction
    {
        public int? StepCount { get; set; }

        public int? InitialStep { get; set; }

        public int? FinalStep { get; set; }

        public bool? IsInitialStepSingleFrame { get; set; }

        public bool? IsFinalStepSingleFrame { get; set; }
    }

    abstract class CompositionAnimation : CompositionObject
    {
        readonly SortedDictionary<string, CompositionObject> _referenceParameters =
            new SortedDictionary<string, CompositionObject>(StringComparer.Ordinal);

        public string? Target { get; set; }

        internal IReadOnlyDictionary<string, CompositionObject> ReferenceParameters => _referenceParameters;

        public void SetReferenceParameter(string key, CompositionObject compositionObject)
            => _referenceParameters[key] = compositionObject;
    }

    sealed class ExpressionAnimation : CompositionAnimation
    {
        internal ExpressionAnimation(string expression) => Expression = expression;

        public string Expression { get; }
    }

    abstract class KeyFrameAnimation : CompositionAnimation
    {
        // Key frames are held in progress order and one deep per progress, which is
        // what Windows.UI.Composition does and what makes the dump canonical.
        readonly SortedList<float, KeyFrame> _keyFrames = new SortedList<float, KeyFrame>();

        public TimeSpan Duration { get; set; }

        internal IEnumerable<KeyFrame> KeyFrames => _keyFrames.Values;

        public void InsertExpressionKeyFrame(float progress, string expression, CompositionEasingFunction? easing)
            => Insert(new ExpressionKeyFrame(progress, easing, expression));

        private protected void Insert(KeyFrame keyFrame) => _keyFrames[keyFrame.Progress] = keyFrame;

        internal abstract class KeyFrame
        {
            private protected KeyFrame(float progress, CompositionEasingFunction? easing)
                => (Progress, Easing) = (progress, easing);

            public float Progress { get; }

            public CompositionEasingFunction? Easing { get; }
        }

        internal sealed class ExpressionKeyFrame : KeyFrame
        {
            internal ExpressionKeyFrame(float progress, CompositionEasingFunction? easing, string expression)
                : base(progress, easing)
                => Expression = expression;

            public string Expression { get; }
        }

        internal sealed class ValueKeyFrame : KeyFrame
        {
            internal ValueKeyFrame(float progress, CompositionEasingFunction? easing, object value)
                : base(progress, easing)
                => Value = value;

            public object Value { get; }
        }
    }

    sealed class ScalarKeyFrameAnimation : KeyFrameAnimation
    {
        public void InsertKeyFrame(float progress, float value, CompositionEasingFunction? easing)
            => Insert(new ValueKeyFrame(progress, easing, value));
    }

    sealed class Vector2KeyFrameAnimation : KeyFrameAnimation
    {
        public void InsertKeyFrame(float progress, Vector2 value, CompositionEasingFunction? easing)
            => Insert(new ValueKeyFrame(progress, easing, value));
    }

    sealed class Vector3KeyFrameAnimation : KeyFrameAnimation
    {
        public void InsertKeyFrame(float progress, Vector3 value, CompositionEasingFunction? easing)
            => Insert(new ValueKeyFrame(progress, easing, value));
    }

    sealed class Vector4KeyFrameAnimation : KeyFrameAnimation
    {
        public void InsertKeyFrame(float progress, Vector4 value, CompositionEasingFunction? easing)
            => Insert(new ValueKeyFrame(progress, easing, value));
    }

    sealed class ColorKeyFrameAnimation : KeyFrameAnimation
    {
        public CompositionColorSpace? InterpolationColorSpace { get; set; }

        public void InsertKeyFrame(float progress, UI.Color value, CompositionEasingFunction? easing)
            => Insert(new ValueKeyFrame(progress, easing, value));
    }

    sealed class BooleanKeyFrameAnimation : KeyFrameAnimation
    {
        public void InsertKeyFrame(float progress, bool value)
            => Insert(new ValueKeyFrame(progress, null, value));
    }

    sealed class PathKeyFrameAnimation : KeyFrameAnimation
    {
        public void InsertKeyFrame(float progress, CompositionPath value, CompositionEasingFunction? easing)
            => Insert(new ValueKeyFrame(progress, easing, value));
    }

    sealed class Compositor
    {
        public ContainerVisual CreateContainerVisual() => new ContainerVisual();

        public ShapeVisual CreateShapeVisual() => new ShapeVisual();

        public SpriteVisual CreateSpriteVisual() => new SpriteVisual();

        public LayerVisual CreateLayerVisual() => new LayerVisual();

        public CompositionContainerShape CreateContainerShape() => new CompositionContainerShape();

        public CompositionSpriteShape CreateSpriteShape() => new CompositionSpriteShape();

        public CompositionPathGeometry CreatePathGeometry() => new CompositionPathGeometry();

        public CompositionRectangleGeometry CreateRectangleGeometry() => new CompositionRectangleGeometry();

        public CompositionRoundedRectangleGeometry CreateRoundedRectangleGeometry()
            => new CompositionRoundedRectangleGeometry();

        public CompositionEllipseGeometry CreateEllipseGeometry() => new CompositionEllipseGeometry();

        public CompositionColorBrush CreateColorBrush() => new CompositionColorBrush();

        public CompositionLinearGradientBrush CreateLinearGradientBrush() => new CompositionLinearGradientBrush();

        public CompositionRadialGradientBrush CreateRadialGradientBrush() => new CompositionRadialGradientBrush();

        public CompositionMaskBrush CreateMaskBrush() => new CompositionMaskBrush();

        public CompositionSurfaceBrush CreateSurfaceBrush(ICompositionSurface? surface)
            => new CompositionSurfaceBrush(surface);

        public CompositionEffectFactory CreateEffectFactory(IGraphicsEffect effect)
            => new CompositionEffectFactory(effect);

        public CompositionColorGradientStop CreateColorGradientStop() => new CompositionColorGradientStop();

        public CompositionViewBox CreateViewBox() => new CompositionViewBox();

        public InsetClip CreateInsetClip() => new InsetClip();

        public CompositionGeometricClip CreateGeometricClip() => new CompositionGeometricClip();

        public DropShadow CreateDropShadow() => new DropShadow();

        public CompositionVisualSurface CreateVisualSurface() => new CompositionVisualSurface();

        public CompositionPropertySet CreatePropertySet() => new CompositionPropertySet(null);

        public AnimationController CreateAnimationController() => new AnimationController();

        public LinearEasingFunction CreateLinearEasingFunction() => new LinearEasingFunction();

        public CubicBezierEasingFunction CreateCubicBezierEasingFunction(Vector2 controlPoint1, Vector2 controlPoint2)
            => new CubicBezierEasingFunction(controlPoint1, controlPoint2);

        public StepEasingFunction CreateStepEasingFunction() => new StepEasingFunction();

        public ExpressionAnimation CreateExpressionAnimation(string expression) => new ExpressionAnimation(expression);

        public ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation() => new ScalarKeyFrameAnimation();

        public Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation() => new Vector2KeyFrameAnimation();

        public Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation() => new Vector3KeyFrameAnimation();

        public Vector4KeyFrameAnimation CreateVector4KeyFrameAnimation() => new Vector4KeyFrameAnimation();

        public ColorKeyFrameAnimation CreateColorKeyFrameAnimation() => new ColorKeyFrameAnimation();

        public BooleanKeyFrameAnimation CreateBooleanKeyFrameAnimation() => new BooleanKeyFrameAnimation();

        public PathKeyFrameAnimation CreatePathKeyFrameAnimation() => new PathKeyFrameAnimation();
    }
}
