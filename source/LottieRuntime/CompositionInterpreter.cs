// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer;
using Google.FlatBuffers;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation.Metadata;
using Windows.Graphics.Effects;
using Fb = CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Schema;
using Mgc = Microsoft.Graphics.Canvas;
using Mgce = Microsoft.Graphics.Canvas.Effects;
#if WINAPPSDK
using Wc = Microsoft.UI.Composition;
using Wm = Microsoft.UI.Xaml.Media;
#else
using Wc = Windows.UI.Composition;
using Wm = Windows.UI.Xaml.Media;
#endif
using Wui = Windows.UI;

namespace CommunityToolkit.WinUI.Lottie.LottieRuntime
{
    /// <summary>
    /// Builds a <see cref="Wc.Visual"/> tree directly from a serialized composition.
    /// </summary>
    /// <remarks>
    /// This is the managed counterpart of the native interpreter in
    /// <c>dlls/LottieRuntime</c>. Both read the same FlatBuffer and build the same
    /// visual tree, and both are structured the same way, so that a change to the
    /// format only ever has to be made twice rather than in a different shape each
    /// time.
    /// <para/>
    /// It differs from <c>Instantiator</c> in that it never builds a WinCompData
    /// graph: the buffer is read in place and the composition objects are created as
    /// the walk reaches them. That makes an animation data rather than code, so it can
    /// be downloaded, themed or swapped without a rebuild, and the amount of code in
    /// the application does not grow with the number of animations.
    /// <para/>
    /// A buffer is untrusted input. It is checked for the <c>LCMP</c> identifier, run
    /// through the FlatBuffers verifier, and every index read out of it is range
    /// checked. A malformed buffer is reported as a
    /// <see cref="FlatBufferFormatException"/>; a buffer that needs something this
    /// build cannot provide is reported as a <see cref="NotSupportedException"/>.
    /// Neither reads outside the buffer.
    /// </remarks>
#if PUBLIC_LottieRuntime
    public
#endif
    sealed class CompositionInterpreter
    {
        // The smallest buffer that could possibly be a composition: a root offset
        // followed by the four byte file identifier. Checked before the identifier is
        // read so that a short buffer is reported as a format error.
        const int MinimumBufferLength = 8;

        readonly Wc.Compositor _compositor;
        readonly Fb.LottieComposition _root;
        readonly string[] _strings;

        // Realization caches. These are indexed identically to the node vectors in the
        // buffer, so looking up an already realized node is an array index rather than
        // a hash lookup, and a node that is reached by more than one path is realized
        // exactly once. Each holds the least derived type that a reference to that
        // category needs, which is what keeps the interpreter free of downcasts.
        readonly Wc.Visual?[] _visuals;
        readonly Wc.CompositionShape?[] _shapes;
        readonly Wc.CompositionGeometry?[] _geometries;
        readonly CanvasGeometry?[] _canvasGeometries;
        readonly CanvasGeometryState[] _canvasGeometryStates;
        readonly Wc.CompositionBrush?[] _brushes;
        readonly Wc.CompositionColorGradientStop?[] _gradientStops;
        readonly Wc.CompositionViewBox?[] _viewBoxes;
        readonly Wc.CompositionClip?[] _clips;
        readonly Wc.CompositionShadow?[] _shadows;
        readonly Wc.ICompositionSurface?[] _surfaces;
        readonly IGraphicsEffect?[] _effects;
        readonly Wc.CompositionEasingFunction?[] _easings;
        readonly Wc.CompositionAnimation?[] _animations;
        readonly Wc.CompositionPropertySet?[] _propertySets;
        readonly Wc.AnimationController?[] _controllers;

        CompositionInterpreter(Wc.Compositor compositor, Fb.LottieComposition root)
        {
            _compositor = compositor;
            _root = root;

            _strings = new string[root.StringsLength];
            for (var i = 0; i < _strings.Length; i++)
            {
                _strings[i] = root.Strings(i) ?? string.Empty;
            }

            _visuals = new Wc.Visual?[root.VisualsLength];
            _shapes = new Wc.CompositionShape?[root.ShapesLength];
            _geometries = new Wc.CompositionGeometry?[root.GeometriesLength];
            _canvasGeometries = new CanvasGeometry?[root.CanvasGeometriesLength];
            _canvasGeometryStates = new CanvasGeometryState[root.CanvasGeometriesLength];
            _brushes = new Wc.CompositionBrush?[root.BrushesLength];
            _gradientStops = new Wc.CompositionColorGradientStop?[root.GradientStopsLength];
            _viewBoxes = new Wc.CompositionViewBox?[root.ViewBoxesLength];
            _clips = new Wc.CompositionClip?[root.ClipsLength];
            _shadows = new Wc.CompositionShadow?[root.ShadowsLength];
            _surfaces = new Wc.ICompositionSurface?[root.SurfacesLength];
            _effects = new IGraphicsEffect?[root.EffectsLength];
            _easings = new Wc.CompositionEasingFunction?[root.EasingsLength];
            _animations = new Wc.CompositionAnimation?[root.AnimationsLength];
            _propertySets = new Wc.CompositionPropertySet?[root.PropertySetsLength];
            _controllers = new Wc.AnimationController?[root.ControllersLength];
        }

        // A canvas geometry is built from other canvas geometries, so the walk has to
        // be able to tell a shared geometry from a cycle.
        enum CanvasGeometryState
        {
            Unrealized,
            Realizing,
            Realized,
        }

        /// <summary>
        /// Builds the visual tree described by a serialized composition.
        /// </summary>
        /// <param name="compositor">The compositor that creates the objects.</param>
        /// <param name="bytes">A buffer produced by <c>CompositionSerializer</c>.</param>
        /// <returns>The root of the visual tree.</returns>
        /// <exception cref="FlatBufferFormatException">The buffer is not a well formed composition.</exception>
        /// <exception cref="NotSupportedException">The composition needs a newer schema or a newer version of Windows.</exception>
        public static Wc.Visual LoadComposition(Wc.Compositor compositor, byte[] bytes)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return LoadComposition(compositor, new ByteBuffer(bytes), bytes.Length);
        }

        /// <summary>
        /// Builds the visual tree described by a serialized composition.
        /// </summary>
        /// <param name="compositor">The compositor that creates the objects.</param>
        /// <param name="bytes">A buffer produced by <c>CompositionSerializer</c>.</param>
        /// <returns>The root of the visual tree.</returns>
        /// <exception cref="FlatBufferFormatException">The buffer is not a well formed composition.</exception>
        /// <exception cref="NotSupportedException">The composition needs a newer schema or a newer version of Windows.</exception>
        public static Wc.Visual LoadComposition(Wc.Compositor compositor, ReadOnlySpan<byte> bytes)
            => LoadComposition(compositor, new ByteBuffer(bytes.ToArray()), bytes.Length);

        static Wc.Visual LoadComposition(Wc.Compositor compositor, ByteBuffer buffer, int length)
        {
            if (compositor is null)
            {
                throw new ArgumentNullException(nameof(compositor));
            }

            // The buffer is untrusted, so nothing is read out of it until the verifier
            // has agreed that every offset in it is inside it.
            if (length < MinimumBufferLength ||
                !Fb.LottieComposition.LottieCompositionBufferHasIdentifier(buffer))
            {
                throw new FlatBufferFormatException("The buffer is not a composition.");
            }

            Fb.LottieComposition root;

            // Verification proves that the buffer is structurally sound, but it cannot
            // prove that the indices stored in it point at the right things. Neither
            // the verifier nor the reader reports a malformed buffer purely by
            // returning false; both also throw. Everything that reads the buffer is
            // therefore done inside a handler, so that a corrupt buffer always surfaces
            // as a format error and never as an exception that a caller could not have
            // anticipated.
            try
            {
                if (!Fb.LottieComposition.VerifyLottieComposition(buffer))
                {
                    throw new FlatBufferFormatException("The buffer is not a valid composition.");
                }

                root = Fb.LottieComposition.GetRootAsLottieComposition(buffer);
            }
            catch (Exception e) when (IsBufferFailure(e))
            {
                throw new FlatBufferFormatException("The buffer is malformed.", e);
            }

            // A newer schema may store things in fields this build does not read, so a
            // buffer that declares one is refused rather than silently misinterpreted.
            if (root.SchemaVersion > Format.Version)
            {
                throw new NotSupportedException(
                    $"Schema version {root.SchemaVersion} is newer than the supported version {Format.Version}.");
            }

            // The translator records the API contract that the graph needs. Checking it
            // before anything is created means that an animation which cannot run on
            // this version of Windows is refused rather than half built.
            var requiredUapVersion = root.RequiredUapVersion;
            if (requiredUapVersion != 0 &&
                !ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", requiredUapVersion))
            {
                throw new NotSupportedException(
                    $"The composition requires UniversalApiContract {requiredUapVersion}.");
            }

            try
            {
                var interpreter = new CompositionInterpreter(compositor, root);

                return interpreter.GetVisual(root.RootVisual)
                    ?? throw new FlatBufferFormatException("The composition has no root visual.");
            }
            catch (Exception e) when (IsBufferFailure(e))
            {
                throw new FlatBufferFormatException("The buffer is malformed.", e);
            }
        }

        /// <summary>
        /// Returns the property set that drives an interpreted composition.
        /// </summary>
        /// <param name="root">A root returned by <c>LoadComposition</c>.</param>
        /// <returns>The property set that holds the animation's Progress property.</returns>
        public static Wc.CompositionPropertySet ProgressPropertySet(Wc.Visual root)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            return root.Properties;
        }

        // The failures that reading a structurally valid but semantically wrong buffer
        // can produce. Reading past the end of the buffer is not a safety problem
        // because ByteBuffer is bounds checked; the only thing being corrected here is
        // the type of the exception.
        static bool IsBufferFailure(Exception e)
            => e is ArgumentException or IndexOutOfRangeException or
                    InvalidOperationException or OverflowException or
                    FormatException or NullReferenceException;

        // ---------------------------------------------------------------------
        // Table accessors. Every index is bounds checked, because a buffer that
        // passes verification can still contain an out of range index.
        // ---------------------------------------------------------------------
        static T Require<T>(T? value, string what)
            where T : struct
            => value ?? throw new FlatBufferFormatException($"A {what} is missing.");

        static uint RequireIndex(uint index, string what)
            => index != Format.NullIndex
                ? index
                : throw new FlatBufferFormatException($"A required {what} is missing.");

        string? GetString(uint index)
            => index == Format.NullIndex
                ? null
                : index < (uint)_strings.Length
                    ? _strings[index]
                    : throw new FlatBufferFormatException($"String index {index} is out of range.");

        string GetRequiredString(uint index)
            => GetString(index) ?? throw new FlatBufferFormatException("A required string is missing.");

        // ---------------------------------------------------------------------
        // Shared state.
        // ---------------------------------------------------------------------
        // Applies the state that every CompositionObject has. Called after the object
        // has been placed in its realization cache, so that a cycle terminates.
        void InitializeCompositionObject(Wc.CompositionObject target, Fb.CompObj? source)
        {
            if (source is null)
            {
                return;
            }

            var value = source.Value;

            if (GetString(value.Comment) is string comment)
            {
                target.Comment = comment;
            }

            // The property set is realized through the object that owns it, so that the
            // owner and its property set can never both create it.
            if (value.Properties != Format.NullIndex)
            {
                RealizePropertySet(value.Properties, target.Properties);
            }

            // Animations are started last, because setting a property stops any
            // animation that is running on it.
            for (var i = 0; i < value.AnimatorsLength; i++)
            {
                var animator = Require(value.Animators(i), "animator");
                var property = GetRequiredString(animator.Property);
                var animation = GetAnimation(animator.Animation)
                    ?? throw new FlatBufferFormatException($"Animator '{property}' has no animation.");

                // A non custom controller comes into existence when the animation is
                // started, so only a custom one is passed in. Either way the resulting
                // controller is cached under the index that the buffer gave it.
                var controllerIndex = animator.Controller;
                var customController = controllerIndex != Format.NullIndex && IsCustomController(controllerIndex)
                    ? GetController(controllerIndex)
                    : null;

                // An expression animation is driven by its inputs and so cannot have a
                // controller. This is checked here in order to reject the buffer rather
                // than build an unusable tree.
                if (customController is not null && animation is Wc.ExpressionAnimation)
                {
                    throw new FlatBufferFormatException(
                        $"Animator '{property}' gives a controller to an expression animation.");
                }

                if (customController is null)
                {
                    target.StartAnimation(property, animation);
                }
                else
                {
                    target.StartAnimation(property, animation, customController);
                }

                if (controllerIndex != Format.NullIndex && customController is null)
                {
                    RealizeController(
                        controllerIndex,
                        target.TryGetAnimationController(property)
                        ?? throw new FlatBufferFormatException($"Animator '{property}' has no controller."));
                }
            }
        }

        bool IsCustomController(uint index)
            => GetControllerTable(index).IsCustom;

        Fb.Controller GetControllerTable(uint index)
            => index < (uint)_controllers.Length
                ? Require(_root.Controllers((int)index), "controller")
                : throw new FlatBufferFormatException($"Controller index {index} is out of range.");

        void RealizeController(uint index, Wc.AnimationController controller)
        {
            if (_controllers[index] is not null)
            {
                return;
            }

            _controllers[index] = controller;

            var table = GetControllerTable(index);

            if (table.IsPaused)
            {
                controller.Pause();
            }

            InitializeCompositionObject(controller, table.Base);
        }

        Wc.AnimationController? GetController(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            var table = GetControllerTable(index);

            if (_controllers[index] is null)
            {
                if (table.IsCustom)
                {
                    RealizeController(index, _compositor.CreateAnimationController());
                }
                else
                {
                    // A non custom controller comes into existence when its target
                    // object starts the animation, so realizing the target realizes the
                    // controller.
                    GetObjectReference(table.TargetObject);
                }
            }

            return _controllers[index]
                ?? throw new FlatBufferFormatException($"Controller {index} could not be realized.");
        }

        // ---------------------------------------------------------------------
        // Property sets.
        // ---------------------------------------------------------------------
        void RealizePropertySet(uint index, Wc.CompositionPropertySet propertySet)
        {
            if (index >= (uint)_propertySets.Length)
            {
                throw new FlatBufferFormatException($"Property set index {index} is out of range.");
            }

            if (_propertySets[index] is not null)
            {
                return;
            }

            // Cached before the values are applied, because a value may be an
            // expression that refers back to this property set.
            _propertySets[index] = propertySet;

            var table = Require(_root.PropertySets((int)index), "property set");

            for (var i = 0; i < table.ValuesLength; i++)
            {
                var value = Require(table.Values(i), "property value");
                var name = GetRequiredString(value.Name);

                switch (value.Type)
                {
                    case Fb.PropertyValueType.Color:
                        propertySet.InsertColor(name, ToColor(Require(value.Color, "color")));
                        break;
                    case Fb.PropertyValueType.Scalar:
                        propertySet.InsertScalar(name, value.Scalar);
                        break;
                    case Fb.PropertyValueType.Vector2:
                        propertySet.InsertVector2(name, ToVector2(value.Vector));
                        break;
                    case Fb.PropertyValueType.Vector3:
                        propertySet.InsertVector3(name, ToVector3(value.Vector));
                        break;
                    case Fb.PropertyValueType.Vector4:
                        propertySet.InsertVector4(name, ToVector4(value.Vector));
                        break;
                    case Fb.PropertyValueType.None:
                        break;
                    default:
                        throw new FlatBufferFormatException($"Unsupported property value type {value.Type}.");
                }
            }

            InitializeCompositionObject(propertySet, table.Base);
        }

        Wc.CompositionPropertySet? GetPropertySet(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_propertySets.Length)
            {
                throw new FlatBufferFormatException($"Property set index {index} is out of range.");
            }

            if (_propertySets[index] is null)
            {
                var table = Require(_root.PropertySets((int)index), "property set");

                if (table.Owner == Format.NullIndex)
                {
                    // A property set with no owner is a standalone one, which is how the
                    // translator exposes theming properties.
                    RealizePropertySet(index, _compositor.CreatePropertySet());
                }
                else
                {
                    // Realizing the owner realizes its property set as a side effect.
                    GetObjectReference(table.Owner);
                }
            }

            return _propertySets[index]
                ?? throw new FlatBufferFormatException($"Property set {index} could not be realized.");
        }

        // Resolves a packed (category, index) reference. Only used by the fields that
        // can point at an object of any category.
        Wc.CompositionObject? GetObjectReference(uint reference)
        {
            if (reference == Format.NullIndex)
            {
                return null;
            }

            var category = Format.UnpackCategory(reference);
            var index = (uint)Format.UnpackIndex(reference);

            return category switch
            {
                Fb.ObjectCategory.Visual => GetVisual(index),
                Fb.ObjectCategory.Shape => GetShape(index),
                Fb.ObjectCategory.Geometry => GetGeometry(index),
                Fb.ObjectCategory.Brush => GetBrush(index),
                Fb.ObjectCategory.Animation => GetAnimation(index),
                Fb.ObjectCategory.Easing => GetEasing(index),
                Fb.ObjectCategory.PropertySet => GetPropertySet(index),

                // Only a visual surface is a CompositionObject; an image surface is not,
                // and nothing can legitimately reference one this way.
                Fb.ObjectCategory.Surface => GetSurface(index) as Wc.CompositionObject
                    ?? throw new FlatBufferFormatException("An image surface cannot be referenced."),
                Fb.ObjectCategory.Clip => GetClip(index),
                Fb.ObjectCategory.Controller => GetController(index),
                Fb.ObjectCategory.Shadow => GetShadow(index),
                Fb.ObjectCategory.GradientStop => GetGradientStop(index),
                Fb.ObjectCategory.ViewBox => GetViewBox(index),
                _ => throw new FlatBufferFormatException($"Unsupported object category {category}."),
            };
        }

        // ---------------------------------------------------------------------
        // Visuals.
        // ---------------------------------------------------------------------
        Wc.Visual? GetVisual(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_visuals.Length)
            {
                throw new FlatBufferFormatException($"Visual index {index} is out of range.");
            }

            if (_visuals[index] is Wc.Visual cached)
            {
                return cached;
            }

            var table = Require(_root.Visuals((int)index), "visual");

            // The concrete type is known only here. Everything below this point,
            // including the recursive walk into the children, uses a base type.
            Wc.ContainerVisual result = table.Kind switch
            {
                Fb.VisualKind.Shape => _compositor.CreateShapeVisual(),
                Fb.VisualKind.Sprite => _compositor.CreateSpriteVisual(),
                Fb.VisualKind.Layer => _compositor.CreateLayerVisual(),
                Fb.VisualKind.Container => _compositor.CreateContainerVisual(),
                _ => throw new FlatBufferFormatException($"Unsupported visual kind {table.Kind}."),
            };

            // Cached before anything it refers to is realized, so that a cycle
            // terminates.
            _visuals[index] = result;

            if (table.BorderMode.HasValue)
            {
                result.BorderMode = (Wc.CompositionBorderMode)table.BorderMode.Value;
            }

            if (ToVector3(table.CenterPoint) is Vector3 centerPoint)
            {
                result.CenterPoint = centerPoint;
            }

            if (table.Clip != Format.NullIndex)
            {
                result.Clip = GetClip(table.Clip);
            }

            if (table.IsVisible.HasValue)
            {
                result.IsVisible = table.IsVisible.Value;
            }

            if (ToVector3(table.Offset) is Vector3 offset)
            {
                result.Offset = offset;
            }

            if (table.Opacity.HasValue)
            {
                result.Opacity = table.Opacity.Value;
            }

            if (table.RotationAngleInDegrees.HasValue)
            {
                result.RotationAngleInDegrees = table.RotationAngleInDegrees.Value;
            }

            if (ToVector3(table.RotationAxis) is Vector3 rotationAxis)
            {
                result.RotationAxis = rotationAxis;
            }

            if (ToVector3(table.Scale) is Vector3 scale)
            {
                result.Scale = scale;
            }

            if (ToVector2(table.Size) is Vector2 size)
            {
                result.Size = size;
            }

            if (ToMatrix4x4(table.TransformMatrix) is Matrix4x4 transformMatrix)
            {
                result.TransformMatrix = transformMatrix;
            }

            switch (result)
            {
                case Wc.ShapeVisual shapeVisual:
                    for (var i = 0; i < table.ShapesLength; i++)
                    {
                        shapeVisual.Shapes.Add(
                            GetShape(table.Shapes(i))
                            ?? throw new FlatBufferFormatException("A shape is missing."));
                    }

                    if (table.ViewBox != Format.NullIndex)
                    {
                        shapeVisual.ViewBox = GetViewBox(table.ViewBox);
                    }

                    break;
                case Wc.SpriteVisual spriteVisual:
                    if (table.Brush != Format.NullIndex)
                    {
                        spriteVisual.Brush = GetBrush(table.Brush);
                    }

                    if (table.Shadow != Format.NullIndex)
                    {
                        spriteVisual.Shadow = GetShadow(table.Shadow);
                    }

                    break;
                case Wc.LayerVisual layerVisual:
                    if (table.Shadow != Format.NullIndex)
                    {
                        layerVisual.Shadow = GetShadow(table.Shadow);
                    }

                    break;
            }

            for (var i = 0; i < table.ChildrenLength; i++)
            {
                result.Children.InsertAtTop(
                    GetVisual(table.Children(i))
                    ?? throw new FlatBufferFormatException("A visual child is missing."));
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        // ---------------------------------------------------------------------
        // Shapes.
        // ---------------------------------------------------------------------
        Wc.CompositionShape? GetShape(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_shapes.Length)
            {
                throw new FlatBufferFormatException($"Shape index {index} is out of range.");
            }

            if (_shapes[index] is Wc.CompositionShape cached)
            {
                return cached;
            }

            var table = Require(_root.Shapes((int)index), "shape");

            Wc.CompositionShape result = table.Kind switch
            {
                Fb.ShapeKind.Sprite => _compositor.CreateSpriteShape(),
                Fb.ShapeKind.Container => _compositor.CreateContainerShape(),
                _ => throw new FlatBufferFormatException($"Unsupported shape kind {table.Kind}."),
            };

            _shapes[index] = result;

            if (ToVector2(table.CenterPoint) is Vector2 centerPoint)
            {
                result.CenterPoint = centerPoint;
            }

            if (ToVector2(table.Offset) is Vector2 offset)
            {
                result.Offset = offset;
            }

            if (table.RotationAngleInDegrees.HasValue)
            {
                result.RotationAngleInDegrees = table.RotationAngleInDegrees.Value;
            }

            if (ToVector2(table.Scale) is Vector2 scale)
            {
                result.Scale = scale;
            }

            if (ToMatrix3x2(table.TransformMatrix) is Matrix3x2 transformMatrix)
            {
                result.TransformMatrix = transformMatrix;
            }

            switch (result)
            {
                case Wc.CompositionContainerShape containerShape:
                    for (var i = 0; i < table.ShapesLength; i++)
                    {
                        containerShape.Shapes.Add(
                            GetShape(table.Shapes(i))
                            ?? throw new FlatBufferFormatException("A shape is missing."));
                    }

                    break;
                case Wc.CompositionSpriteShape spriteShape:
                    ApplySpriteShapeProperties(spriteShape, table);
                    break;
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        void ApplySpriteShapeProperties(Wc.CompositionSpriteShape target, Fb.Shape table)
        {
            if (table.FillBrush != Format.NullIndex)
            {
                target.FillBrush = GetBrush(table.FillBrush);
            }

            if (table.StrokeBrush != Format.NullIndex)
            {
                target.StrokeBrush = GetBrush(table.StrokeBrush);
            }

            if (table.Geometry != Format.NullIndex)
            {
                target.Geometry = GetGeometry(table.Geometry);
            }

            if (table.IsStrokeNonScaling.HasValue)
            {
                target.IsStrokeNonScaling = table.IsStrokeNonScaling.Value;
            }

            if (table.StrokeDashOffset.HasValue)
            {
                target.StrokeDashOffset = table.StrokeDashOffset.Value;
            }

            for (var i = 0; i < table.StrokeDashArrayLength; i++)
            {
                target.StrokeDashArray.Add(table.StrokeDashArray(i));
            }

            if (table.StrokeDashCap.HasValue)
            {
                target.StrokeDashCap = (Wc.CompositionStrokeCap)table.StrokeDashCap.Value;
            }

            if (table.StrokeStartCap.HasValue)
            {
                target.StrokeStartCap = (Wc.CompositionStrokeCap)table.StrokeStartCap.Value;
            }

            if (table.StrokeEndCap.HasValue)
            {
                target.StrokeEndCap = (Wc.CompositionStrokeCap)table.StrokeEndCap.Value;
            }

            if (table.StrokeLineJoin.HasValue)
            {
                target.StrokeLineJoin = (Wc.CompositionStrokeLineJoin)table.StrokeLineJoin.Value;
            }

            if (table.StrokeMiterLimit.HasValue)
            {
                target.StrokeMiterLimit = table.StrokeMiterLimit.Value;
            }

            if (table.StrokeThickness.HasValue)
            {
                target.StrokeThickness = table.StrokeThickness.Value;
            }
        }

        // ---------------------------------------------------------------------
        // Geometries.
        // ---------------------------------------------------------------------
        Wc.CompositionGeometry? GetGeometry(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_geometries.Length)
            {
                throw new FlatBufferFormatException($"Geometry index {index} is out of range.");
            }

            if (_geometries[index] is Wc.CompositionGeometry cached)
            {
                return cached;
            }

            var table = Require(_root.Geometries((int)index), "geometry");

            Wc.CompositionGeometry result = table.Kind switch
            {
                Fb.GeometryKind.Rectangle => _compositor.CreateRectangleGeometry(),
                Fb.GeometryKind.RoundedRectangle => _compositor.CreateRoundedRectangleGeometry(),
                Fb.GeometryKind.Ellipse => _compositor.CreateEllipseGeometry(),
                Fb.GeometryKind.Path => _compositor.CreatePathGeometry(),
                _ => throw new FlatBufferFormatException($"Unsupported geometry kind {table.Kind}."),
            };

            _geometries[index] = result;

            if (table.TrimStart.HasValue)
            {
                result.TrimStart = table.TrimStart.Value;
            }

            if (table.TrimEnd.HasValue)
            {
                result.TrimEnd = table.TrimEnd.Value;
            }

            if (table.TrimOffset.HasValue)
            {
                result.TrimOffset = table.TrimOffset.Value;
            }

            switch (result)
            {
                case Wc.CompositionPathGeometry pathGeometry:
                    var path = GetCanvasGeometry(table.Path);
                    pathGeometry.Path = path is null ? null : new Wc.CompositionPath(path);
                    break;
                case Wc.CompositionRoundedRectangleGeometry roundedRectangle:
                    if (ToVector2(table.Offset) is Vector2 roundedOffset)
                    {
                        roundedRectangle.Offset = roundedOffset;
                    }

                    if (ToVector2(table.Size) is Vector2 roundedSize)
                    {
                        roundedRectangle.Size = roundedSize;
                    }

                    roundedRectangle.CornerRadius = ToVector2(table.CornerRadius) ?? Vector2.Zero;
                    break;
                case Wc.CompositionRectangleGeometry rectangle:
                    if (ToVector2(table.Offset) is Vector2 rectangleOffset)
                    {
                        rectangle.Offset = rectangleOffset;
                    }

                    if (ToVector2(table.Size) is Vector2 rectangleSize)
                    {
                        rectangle.Size = rectangleSize;
                    }

                    break;
                case Wc.CompositionEllipseGeometry ellipse:
                    ellipse.Center = ToVector2(table.Center) ?? Vector2.Zero;
                    ellipse.Radius = ToVector2(table.Radius) ?? Vector2.Zero;
                    break;
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        // ---------------------------------------------------------------------
        // Brushes.
        // ---------------------------------------------------------------------
        Wc.CompositionBrush? GetBrush(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_brushes.Length)
            {
                throw new FlatBufferFormatException($"Brush index {index} is out of range.");
            }

            if (_brushes[index] is Wc.CompositionBrush cached)
            {
                return cached;
            }

            var table = Require(_root.Brushes((int)index), "brush");

            Wc.CompositionBrush result;
            switch (table.Kind)
            {
                case Fb.BrushKind.Color:
                    result = _compositor.CreateColorBrush();
                    break;
                case Fb.BrushKind.LinearGradient:
                    result = _compositor.CreateLinearGradientBrush();
                    break;
                case Fb.BrushKind.RadialGradient:
                    result = _compositor.CreateRadialGradientBrush();
                    break;
                case Fb.BrushKind.Mask:
                    result = _compositor.CreateMaskBrush();
                    break;
                case Fb.BrushKind.Surface:
                    // The surface is needed in order to construct the brush, so it is
                    // realized before the brush is placed in the cache.
                    result = _compositor.CreateSurfaceBrush(
                        GetSurface(RequireIndex(table.Surface, "surface"))
                        ?? throw new FlatBufferFormatException("A surface brush has no surface."));
                    break;
                case Fb.BrushKind.Effect:
                    var effect = GetEffect(RequireIndex(table.Effect, "effect"))
                        ?? throw new FlatBufferFormatException("An effect brush has no effect.");
                    result = _compositor.CreateEffectFactory(effect).CreateBrush();
                    break;
                default:
                    throw new FlatBufferFormatException($"Unsupported brush kind {table.Kind}.");
            }

            _brushes[index] = result;

            switch (result)
            {
                case Wc.CompositionColorBrush colorBrush:
                    if (table.Color.HasValue)
                    {
                        colorBrush.Color = ToColor(table.Color.Value);
                    }

                    break;
                case Wc.CompositionMaskBrush maskBrush:
                    if (table.Source != Format.NullIndex)
                    {
                        maskBrush.Source = GetBrush(table.Source);
                    }

                    if (table.Mask != Format.NullIndex)
                    {
                        maskBrush.Mask = GetBrush(table.Mask);
                    }

                    break;
                case Wc.CompositionEffectBrush effectBrush:
                    for (var i = 0; i < table.SourceParametersLength; i++)
                    {
                        var parameter = Require(table.SourceParameters(i), "source parameter");
                        effectBrush.SetSourceParameter(
                            GetRequiredString(parameter.Name),
                            GetBrush(parameter.Brush)
                            ?? throw new FlatBufferFormatException("An effect source has no brush."));
                    }

                    break;
            }

            if (result is Wc.CompositionGradientBrush gradientBrush)
            {
                ApplyGradientBrushProperties(gradientBrush, table);
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        void ApplyGradientBrushProperties(Wc.CompositionGradientBrush target, Fb.Brush table)
        {
            if (ToVector2(table.AnchorPoint) is Vector2 anchorPoint)
            {
                target.AnchorPoint = anchorPoint;
            }

            if (ToVector2(table.CenterPoint) is Vector2 centerPoint)
            {
                target.CenterPoint = centerPoint;
            }

            for (var i = 0; i < table.ColorStopsLength; i++)
            {
                target.ColorStops.Add(
                    GetGradientStop(table.ColorStops(i))
                    ?? throw new FlatBufferFormatException("A gradient stop is missing."));
            }

            if (table.ExtendMode.HasValue)
            {
                target.ExtendMode = (Wc.CompositionGradientExtendMode)table.ExtendMode.Value;
            }

            if (table.InterpolationSpace.HasValue)
            {
                target.InterpolationSpace = (Wc.CompositionColorSpace)table.InterpolationSpace.Value;
            }

            if (table.MappingMode.HasValue)
            {
                target.MappingMode = (Wc.CompositionMappingMode)table.MappingMode.Value;
            }

            if (ToVector2(table.Offset) is Vector2 offset)
            {
                target.Offset = offset;
            }

            if (table.RotationAngleInDegrees.HasValue)
            {
                target.RotationAngleInDegrees = table.RotationAngleInDegrees.Value;
            }

            if (ToVector2(table.Scale) is Vector2 scale)
            {
                target.Scale = scale;
            }

            if (ToMatrix3x2(table.TransformMatrix) is Matrix3x2 transformMatrix)
            {
                target.TransformMatrix = transformMatrix;
            }

            switch (target)
            {
                case Wc.CompositionLinearGradientBrush linear:
                    if (ToVector2(table.StartPoint) is Vector2 startPoint)
                    {
                        linear.StartPoint = startPoint;
                    }

                    if (ToVector2(table.EndPoint) is Vector2 endPoint)
                    {
                        linear.EndPoint = endPoint;
                    }

                    break;
                case Wc.CompositionRadialGradientBrush radial:
                    if (ToVector2(table.EllipseCenter) is Vector2 ellipseCenter)
                    {
                        radial.EllipseCenter = ellipseCenter;
                    }

                    if (ToVector2(table.EllipseRadius) is Vector2 ellipseRadius)
                    {
                        radial.EllipseRadius = ellipseRadius;
                    }

                    if (ToVector2(table.GradientOriginOffset) is Vector2 gradientOriginOffset)
                    {
                        radial.GradientOriginOffset = gradientOriginOffset;
                    }

                    break;
            }
        }

        Wc.CompositionColorGradientStop? GetGradientStop(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_gradientStops.Length)
            {
                throw new FlatBufferFormatException($"Gradient stop index {index} is out of range.");
            }

            if (_gradientStops[index] is Wc.CompositionColorGradientStop cached)
            {
                return cached;
            }

            var table = Require(_root.GradientStops((int)index), "gradient stop");
            var result = _compositor.CreateColorGradientStop();
            _gradientStops[index] = result;

            result.Color = ToColor(Require(table.Color, "color"));
            result.Offset = table.Offset;

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        // ---------------------------------------------------------------------
        // Clips, view boxes, shadows and surfaces.
        // ---------------------------------------------------------------------
        Wc.CompositionViewBox? GetViewBox(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_viewBoxes.Length)
            {
                throw new FlatBufferFormatException($"View box index {index} is out of range.");
            }

            if (_viewBoxes[index] is Wc.CompositionViewBox cached)
            {
                return cached;
            }

            var table = Require(_root.ViewBoxes((int)index), "view box");
            var result = _compositor.CreateViewBox();
            _viewBoxes[index] = result;

            result.Size = ToVector2(table.Size) ?? Vector2.Zero;

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        Wc.CompositionClip? GetClip(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_clips.Length)
            {
                throw new FlatBufferFormatException($"Clip index {index} is out of range.");
            }

            if (_clips[index] is Wc.CompositionClip cached)
            {
                return cached;
            }

            var table = Require(_root.Clips((int)index), "clip");

            Wc.CompositionClip result = table.Kind switch
            {
                Fb.ClipKind.Geometric => _compositor.CreateGeometricClip(),
                Fb.ClipKind.Inset => _compositor.CreateInsetClip(),
                _ => throw new FlatBufferFormatException($"Unsupported clip kind {table.Kind}."),
            };

            _clips[index] = result;

            if (ToVector2(table.CenterPoint) is Vector2 centerPoint)
            {
                result.CenterPoint = centerPoint;
            }

            if (ToVector2(table.Scale) is Vector2 scale)
            {
                result.Scale = scale;
            }

            switch (result)
            {
                case Wc.InsetClip inset:
                    if (table.LeftInset.HasValue)
                    {
                        inset.LeftInset = table.LeftInset.Value;
                    }

                    if (table.RightInset.HasValue)
                    {
                        inset.RightInset = table.RightInset.Value;
                    }

                    if (table.TopInset.HasValue)
                    {
                        inset.TopInset = table.TopInset.Value;
                    }

                    if (table.BottomInset.HasValue)
                    {
                        inset.BottomInset = table.BottomInset.Value;
                    }

                    break;
                case Wc.CompositionGeometricClip geometric:
                    if (table.Geometry != Format.NullIndex)
                    {
                        geometric.Geometry = GetGeometry(table.Geometry);
                    }

                    break;
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        Wc.CompositionShadow? GetShadow(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_shadows.Length)
            {
                throw new FlatBufferFormatException($"Shadow index {index} is out of range.");
            }

            if (_shadows[index] is Wc.CompositionShadow cached)
            {
                return cached;
            }

            var table = Require(_root.Shadows((int)index), "shadow");

            Wc.CompositionShadow result = table.Kind switch
            {
                Fb.ShadowKind.Drop => _compositor.CreateDropShadow(),
                _ => throw new FlatBufferFormatException($"Unsupported shadow kind {table.Kind}."),
            };

            _shadows[index] = result;

            if (result is Wc.DropShadow dropShadow)
            {
                if (table.BlurRadius.HasValue)
                {
                    dropShadow.BlurRadius = table.BlurRadius.Value;
                }

                if (table.Color.HasValue)
                {
                    dropShadow.Color = ToColor(table.Color.Value);
                }

                if (table.Mask != Format.NullIndex)
                {
                    dropShadow.Mask = GetBrush(table.Mask);
                }

                if (ToVector3(table.Offset) is Vector3 offset)
                {
                    dropShadow.Offset = offset;
                }

                if (table.Opacity.HasValue)
                {
                    dropShadow.Opacity = table.Opacity.Value;
                }

                if (table.SourcePolicy.HasValue)
                {
                    dropShadow.SourcePolicy = (Wc.CompositionDropShadowSourcePolicy)table.SourcePolicy.Value;
                }
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        Wc.ICompositionSurface? GetSurface(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_surfaces.Length)
            {
                throw new FlatBufferFormatException($"Surface index {index} is out of range.");
            }

            if (_surfaces[index] is Wc.ICompositionSurface cached)
            {
                return cached;
            }

            var table = Require(_root.Surfaces((int)index), "surface");

            switch (table.Kind)
            {
                case Fb.SurfaceKind.VisualSurface:
                    var visualSurface = _compositor.CreateVisualSurface();
                    _surfaces[index] = visualSurface;

                    if (table.SourceVisual != Format.NullIndex)
                    {
                        visualSurface.SourceVisual = GetVisual(table.SourceVisual);
                    }

                    if (ToVector2(table.SourceSize) is Vector2 sourceSize)
                    {
                        visualSurface.SourceSize = sourceSize;
                    }

                    if (ToVector2(table.SourceOffset) is Vector2 sourceOffset)
                    {
                        visualSurface.SourceOffset = sourceOffset;
                    }

                    InitializeCompositionObject(visualSurface, table.Base);
                    return visualSurface;

                case Fb.SurfaceKind.LoadedImageFromUri:
                    var uri = GetRequiredString(table.Uri);
                    var fromUri = Wm.LoadedImageSurface.StartLoadFromUri(new Uri(uri, UriKind.RelativeOrAbsolute));
                    _surfaces[index] = fromUri;
                    return fromUri;

                case Fb.SurfaceKind.LoadedImageFromStream:
                    var bytes = new byte[table.BytesLength];
                    for (var i = 0; i < bytes.Length; i++)
                    {
                        bytes[i] = table.Bytes(i);
                    }

                    var fromStream = Wm.LoadedImageSurface.StartLoadFromStream(
                        bytes.AsBuffer().AsStream().AsRandomAccessStream());
                    _surfaces[index] = fromStream;
                    return fromStream;

                default:
                    throw new FlatBufferFormatException($"Unsupported surface kind {table.Kind}.");
            }
        }

        // ---------------------------------------------------------------------
        // Effects.
        // ---------------------------------------------------------------------
        IGraphicsEffect? GetEffect(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_effects.Length)
            {
                throw new FlatBufferFormatException($"Effect index {index} is out of range.");
            }

            if (_effects[index] is IGraphicsEffect cached)
            {
                return cached;
            }

            var table = Require(_root.Effects((int)index), "effect");

            // The sources are the names that the brush later binds real brushes to. The
            // composition engine matches them up by name.
            var sources = new List<Wc.CompositionEffectSourceParameter>(table.SourcesLength);
            for (var i = 0; i < table.SourcesLength; i++)
            {
                sources.Add(new Wc.CompositionEffectSourceParameter(GetRequiredString(table.Sources(i))));
            }

            IGraphicsEffect result;
            switch (table.Kind)
            {
                case Fb.EffectKind.Composite:
                    var composite = new Mgce.CompositeEffect
                    {
                        Mode = (Mgc.CanvasComposite)table.Mode,
                    };

                    foreach (var source in sources)
                    {
                        composite.Sources.Add(source);
                    }

                    result = composite;
                    break;
                case Fb.EffectKind.GaussianBlur:
                    if (sources.Count != 1)
                    {
                        throw new FlatBufferFormatException("A blur effect must have exactly one source.");
                    }

                    result = new Mgce.GaussianBlurEffect
                    {
                        BlurAmount = table.BlurAmount,
                        Source = sources[0],
                    };
                    break;
                default:
                    throw new FlatBufferFormatException($"Unsupported effect kind {table.Kind}.");
            }

            _effects[index] = result;
            return result;
        }

        // ---------------------------------------------------------------------
        // Easings.
        // ---------------------------------------------------------------------
        Wc.CompositionEasingFunction? GetEasing(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_easings.Length)
            {
                throw new FlatBufferFormatException($"Easing index {index} is out of range.");
            }

            if (_easings[index] is Wc.CompositionEasingFunction cached)
            {
                return cached;
            }

            var table = Require(_root.Easings((int)index), "easing");

            Wc.CompositionEasingFunction result;
            switch (table.Kind)
            {
                case Fb.EasingKind.Linear:
                    result = _compositor.CreateLinearEasingFunction();
                    break;
                case Fb.EasingKind.CubicBezier:
                    result = _compositor.CreateCubicBezierEasingFunction(
                        ToVector2(table.ControlPoint1) ?? Vector2.Zero,
                        ToVector2(table.ControlPoint2) ?? Vector2.Zero);
                    break;
                case Fb.EasingKind.Step:
                    var step = _compositor.CreateStepEasingFunction();

                    if (table.StepCount.HasValue)
                    {
                        step.StepCount = table.StepCount.Value;
                    }

                    if (table.InitialStep.HasValue)
                    {
                        step.InitialStep = table.InitialStep.Value;
                    }

                    if (table.FinalStep.HasValue)
                    {
                        step.FinalStep = table.FinalStep.Value;
                    }

                    if (table.IsInitialStepSingleFrame.HasValue)
                    {
                        step.IsInitialStepSingleFrame = table.IsInitialStepSingleFrame.Value;
                    }

                    if (table.IsFinalStepSingleFrame.HasValue)
                    {
                        step.IsFinalStepSingleFrame = table.IsFinalStepSingleFrame.Value;
                    }

                    result = step;
                    break;
                default:
                    throw new FlatBufferFormatException($"Unsupported easing kind {table.Kind}.");
            }

            _easings[index] = result;

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        // ---------------------------------------------------------------------
        // Animations.
        // ---------------------------------------------------------------------
        Wc.CompositionAnimation? GetAnimation(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_animations.Length)
            {
                throw new FlatBufferFormatException($"Animation index {index} is out of range.");
            }

            if (_animations[index] is Wc.CompositionAnimation cached)
            {
                return cached;
            }

            var table = Require(_root.Animations((int)index), "animation");

            Wc.CompositionAnimation result = table.Kind == Fb.AnimationKind.Expression
                ? _compositor.CreateExpressionAnimation(GetRequiredString(table.Expression))
                : CreateKeyFrameAnimation(table);

            _animations[index] = result;

            if (GetString(table.Target) is string target)
            {
                result.Target = target;
            }

            for (var i = 0; i < table.ReferenceParametersLength; i++)
            {
                var parameter = Require(table.ReferenceParameters(i), "reference parameter");
                result.SetReferenceParameter(
                    GetRequiredString(parameter.Name),
                    GetObjectReference(parameter.Target)
                    ?? throw new FlatBufferFormatException("A reference parameter has no target."));
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        // Creates the animation and inserts its key frames in one place, because the
        // concrete type is only needed in order to insert a value key frame and would
        // otherwise have to be recovered with a cast.
        Wc.CompositionAnimation CreateKeyFrameAnimation(Fb.Animation table)
        {
            Wc.KeyFrameAnimation result;
            Wc.ScalarKeyFrameAnimation? scalar = null;
            Wc.Vector2KeyFrameAnimation? vector2 = null;
            Wc.Vector3KeyFrameAnimation? vector3 = null;
            Wc.Vector4KeyFrameAnimation? vector4 = null;
            Wc.ColorKeyFrameAnimation? color = null;
            Wc.BooleanKeyFrameAnimation? boolean = null;
            Wc.PathKeyFrameAnimation? path = null;

            switch (table.Kind)
            {
                case Fb.AnimationKind.Scalar:
                    scalar = _compositor.CreateScalarKeyFrameAnimation();
                    result = scalar;
                    break;
                case Fb.AnimationKind.Vector2:
                    vector2 = _compositor.CreateVector2KeyFrameAnimation();
                    result = vector2;
                    break;
                case Fb.AnimationKind.Vector3:
                    vector3 = _compositor.CreateVector3KeyFrameAnimation();
                    result = vector3;
                    break;
                case Fb.AnimationKind.Vector4:
                    vector4 = _compositor.CreateVector4KeyFrameAnimation();
                    result = vector4;
                    break;
                case Fb.AnimationKind.Color:
                    color = _compositor.CreateColorKeyFrameAnimation();

                    if (table.InterpolationColorSpace.HasValue)
                    {
                        color.InterpolationColorSpace =
                            (Wc.CompositionColorSpace)table.InterpolationColorSpace.Value;
                    }

                    result = color;
                    break;
                case Fb.AnimationKind.Boolean:
                    boolean = _compositor.CreateBooleanKeyFrameAnimation();
                    result = boolean;
                    break;
                case Fb.AnimationKind.Path:
                    path = _compositor.CreatePathKeyFrameAnimation();
                    result = path;
                    break;
                default:
                    throw new FlatBufferFormatException($"Unsupported animation kind {table.Kind}.");
            }

            for (var i = 0; i < table.KeyFramesLength; i++)
            {
                var frame = Require(table.KeyFrames(i), "key frame");

                if (frame.Kind == Fb.KeyFrameKind.Expression)
                {
                    result.InsertExpressionKeyFrame(
                        frame.Progress,
                        GetRequiredString(frame.Expression),
                        GetEasing(frame.Easing));
                }
                else if (frame.Kind == Fb.KeyFrameKind.Value)
                {
                    switch (table.Kind)
                    {
                        case Fb.AnimationKind.Scalar:
                            scalar!.InsertKeyFrame(frame.Progress, frame.Scalar, GetEasing(frame.Easing));
                            break;
                        case Fb.AnimationKind.Vector2:
                            vector2!.InsertKeyFrame(
                                frame.Progress,
                                ToVector2(frame.Vector),
                                GetEasing(frame.Easing));
                            break;
                        case Fb.AnimationKind.Vector3:
                            vector3!.InsertKeyFrame(
                                frame.Progress,
                                ToVector3(frame.Vector),
                                GetEasing(frame.Easing));
                            break;
                        case Fb.AnimationKind.Vector4:
                            vector4!.InsertKeyFrame(
                                frame.Progress,
                                ToVector4(frame.Vector),
                                GetEasing(frame.Easing));
                            break;
                        case Fb.AnimationKind.Color:
                            color!.InsertKeyFrame(
                                frame.Progress,
                                ToColor(Require(frame.Color, "color")),
                                GetEasing(frame.Easing));
                            break;
                        case Fb.AnimationKind.Boolean:
                            // A boolean cannot be interpolated, so it has no easing.
                            boolean!.InsertKeyFrame(frame.Progress, frame.Scalar != 0);
                            break;
                        case Fb.AnimationKind.Path:
                            path!.InsertKeyFrame(
                                frame.Progress,
                                new Wc.CompositionPath(
                                    GetCanvasGeometry(frame.Path)
                                    ?? throw new FlatBufferFormatException("A path key frame has no path.")),
                                GetEasing(frame.Easing));
                            break;
                    }
                }
            }

            result.Duration = TimeSpan.FromTicks(table.DurationTicks);

            return result;
        }

        // ---------------------------------------------------------------------
        // Paths.
        //
        // A canvas geometry is not a CompositionObject, so it has no shared state and
        // no animators; it is only ever a value.
        // ---------------------------------------------------------------------
        CanvasGeometry? GetCanvasGeometry(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_canvasGeometries.Length)
            {
                throw new FlatBufferFormatException($"Canvas geometry index {index} is out of range.");
            }

            switch (_canvasGeometryStates[index])
            {
                case CanvasGeometryState.Realized:
                    return _canvasGeometries[index];
                case CanvasGeometryState.Realizing:
                    // A geometry that is built from itself would never finish.
                    throw new FlatBufferFormatException($"Canvas geometry {index} is cyclic.");
            }

            _canvasGeometryStates[index] = CanvasGeometryState.Realizing;

            var table = Require(_root.CanvasGeometries((int)index), "canvas geometry");

            CanvasGeometry result;
            switch (table.Kind)
            {
                case Fb.CanvasGeometryKind.Combination:
                    var a = GetCanvasGeometry(RequireIndex(table.A, "geometry"))
                        ?? throw new FlatBufferFormatException("A combination has no first geometry.");
                    var b = GetCanvasGeometry(RequireIndex(table.B, "geometry"))
                        ?? throw new FlatBufferFormatException("A combination has no second geometry.");
                    result = a.CombineWith(
                        b,
                        ToMatrix3x2(table.Matrix) ?? Matrix3x2.Identity,
                        (CanvasGeometryCombine)table.CombineMode);
                    break;

                case Fb.CanvasGeometryKind.TransformedGeometry:
                    var source = GetCanvasGeometry(RequireIndex(table.Source, "geometry"))
                        ?? throw new FlatBufferFormatException("A transformed geometry has no source.");
                    result = source.Transform(ToMatrix3x2(table.Matrix) ?? Matrix3x2.Identity);
                    break;

                case Fb.CanvasGeometryKind.Group:
                    var geometries = new CanvasGeometry[table.GeometriesLength];
                    for (var i = 0; i < geometries.Length; i++)
                    {
                        geometries[i] = GetCanvasGeometry(table.Geometries(i))
                            ?? throw new FlatBufferFormatException("A grouped geometry is missing.");
                    }

                    result = CanvasGeometry.CreateGroup(
                        null,
                        geometries,
                        (CanvasFilledRegionDetermination)table.FillRule);
                    break;

                case Fb.CanvasGeometryKind.Path:
                    result = CreatePath(table);
                    break;

                case Fb.CanvasGeometryKind.Ellipse:
                    result = CanvasGeometry.CreateEllipse(null, table.X, table.Y, table.RadiusX, table.RadiusY);
                    break;

                case Fb.CanvasGeometryKind.RoundedRectangle:
                    result = CanvasGeometry.CreateRoundedRectangle(
                        null, table.X, table.Y, table.W, table.H, table.RadiusX, table.RadiusY);
                    break;

                default:
                    throw new FlatBufferFormatException($"Unsupported canvas geometry kind {table.Kind}.");
            }

            _canvasGeometries[index] = result;
            _canvasGeometryStates[index] = CanvasGeometryState.Realized;
            return result;
        }

        // Replays the flattened opcode and operand streams of a path. The operands of
        // every command are consecutive in one array, which is why there is no table
        // per command.
        static CanvasGeometry CreatePath(Fb.CanvasGeometry table)
        {
            using var builder = new CanvasPathBuilder(null);
            builder.SetFilledRegionDetermination((CanvasFilledRegionDetermination)table.FillRule);

            var operand = 0;
            var inFigure = false;

            float Next()
                => operand < table.OperandsLength
                    ? table.Operands(operand++)
                    : throw new FlatBufferFormatException("A path ran out of operands.");

            void RequireFigure(bool expected)
            {
                if (inFigure != expected)
                {
                    throw new FlatBufferFormatException("A path has a figure that does not begin and end.");
                }
            }

            for (var i = 0; i < table.OpsLength; i++)
            {
                switch ((Fb.PathOp)table.Ops(i))
                {
                    case Fb.PathOp.BeginFigure:
                        RequireFigure(false);
                        builder.BeginFigure(new Vector2(Next(), Next()));
                        inFigure = true;
                        break;
                    case Fb.PathOp.EndFigure:
                        RequireFigure(true);
                        builder.EndFigure((CanvasFigureLoop)Next());
                        inFigure = false;
                        break;
                    case Fb.PathOp.AddLine:
                        RequireFigure(true);
                        builder.AddLine(new Vector2(Next(), Next()));
                        break;
                    case Fb.PathOp.AddCubicBezier:
                        RequireFigure(true);
                        builder.AddCubicBezier(
                            new Vector2(Next(), Next()),
                            new Vector2(Next(), Next()),
                            new Vector2(Next(), Next()));
                        break;
                    default:
                        throw new FlatBufferFormatException($"Unsupported path opcode {table.Ops(i)}.");
                }
            }

            RequireFigure(false);

            return CanvasGeometry.CreatePath(builder);
        }

        // ---------------------------------------------------------------------
        // Value helpers. An absent struct field means the property was never set,
        // so these return null rather than a default value.
        // ---------------------------------------------------------------------
        static Wui.Color ToColor(Fb.Color value)
            => Wui.Color.FromArgb(value.A, value.R, value.G, value.B);

        static Vector2? ToVector2(Fb.Vec2? value)
            => value.HasValue ? new Vector2(value.Value.X, value.Value.Y) : null;

        static Vector3? ToVector3(Fb.Vec3? value)
            => value.HasValue ? new Vector3(value.Value.X, value.Value.Y, value.Value.Z) : null;

        static Vector2 ToVector2(Fb.Vec4? value)
            => value.HasValue ? new Vector2(value.Value.X, value.Value.Y) : Vector2.Zero;

        static Vector3 ToVector3(Fb.Vec4? value)
            => value.HasValue ? new Vector3(value.Value.X, value.Value.Y, value.Value.Z) : Vector3.Zero;

        static Vector4 ToVector4(Fb.Vec4? value)
            => value.HasValue
                ? new Vector4(value.Value.X, value.Value.Y, value.Value.Z, value.Value.W)
                : Vector4.Zero;

        static Matrix3x2? ToMatrix3x2(Fb.Mat3x2? value)
            => value.HasValue
                ? new Matrix3x2(
                    value.Value.M11, value.Value.M12,
                    value.Value.M21, value.Value.M22,
                    value.Value.M31, value.Value.M32)
                : null;

        static Matrix4x4? ToMatrix4x4(Fb.Mat4x4? value)
            => value.HasValue
                ? new Matrix4x4(
                    value.Value.M11, value.Value.M12, value.Value.M13, value.Value.M14,
                    value.Value.M21, value.Value.M22, value.Value.M23, value.Value.M24,
                    value.Value.M31, value.Value.M32, value.Value.M33, value.Value.M34,
                    value.Value.M41, value.Value.M42, value.Value.M43, value.Value.M44)
                : null;
    }
}
