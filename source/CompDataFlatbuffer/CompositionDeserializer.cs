// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.MetaData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgc;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;
using CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;
using Google.FlatBuffers;
using Expressions = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using Fb = CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Schema;
using Wui = CommunityToolkit.WinUI.Lottie.WinCompData.Wui;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer
{
    /// <summary>
    /// Rebuilds a WinCompData object graph from a FlatBuffer produced by
    /// <see cref="CompositionSerializer"/>.
    /// </summary>
    /// <remarks>
    /// This is the managed counterpart of the native interpreter. It exists so that the
    /// wire format can be tested by round tripping a graph through it and comparing the
    /// result with the original, which is a much stronger check than testing the
    /// serializer alone.
    /// <para/>
    /// Expressions are stored in the buffer as text - which is all that
    /// Windows.UI.Composition itself accepts - so the static type of an expression tree
    /// is not preserved. Rebuilt expressions are "asserted" expressions with the same
    /// text, which compare equal to the originals.
    /// </remarks>
#if PUBLIC_CompDataFlatbuffer
    public
#endif
    sealed class CompositionDeserializer
    {
        // The smallest buffer that could possibly be a composition: a root offset
        // followed by the four byte file identifier. Checked before the identifier is
        // read so that a short buffer is reported as a format error.
        const int MinimumBufferLength = 8;

        readonly Compositor _compositor = new Compositor();
        readonly Fb.LottieComposition _root;
        readonly string[] _strings;

        // Realization caches. These are indexed identically to the node vectors in the
        // buffer, so looking up an already realized node is an array index rather than a
        // hash lookup, and shared nodes are realized exactly once.
        readonly Visual?[] _visuals;
        readonly CompositionShape?[] _shapes;
        readonly CompositionGeometry?[] _geometries;
        readonly CanvasGeometry?[] _canvasGeometries;
        readonly CompositionBrush?[] _brushes;
        readonly CompositionColorGradientStop?[] _gradientStops;
        readonly CompositionViewBox?[] _viewBoxes;
        readonly CompositionClip?[] _clips;
        readonly CompositionShadow?[] _shadows;
        readonly ICompositionSurface?[] _surfaces;
        readonly GraphicsEffectBase?[] _effects;
        readonly CompositionEasingFunction?[] _easings;
        readonly CompositionAnimation?[] _animations;
        readonly CompositionPropertySet?[] _propertySets;
        readonly AnimationController?[] _controllers;

        CompositionDeserializer(Fb.LottieComposition root)
        {
            _root = root;

            _strings = new string[root.StringsLength];
            for (var i = 0; i < _strings.Length; i++)
            {
                _strings[i] = root.Strings(i) ?? string.Empty;
            }

            _visuals = new Visual?[root.VisualsLength];
            _shapes = new CompositionShape?[root.ShapesLength];
            _geometries = new CompositionGeometry?[root.GeometriesLength];
            _canvasGeometries = new CanvasGeometry?[root.CanvasGeometriesLength];
            _brushes = new CompositionBrush?[root.BrushesLength];
            _gradientStops = new CompositionColorGradientStop?[root.GradientStopsLength];
            _viewBoxes = new CompositionViewBox?[root.ViewBoxesLength];
            _clips = new CompositionClip?[root.ClipsLength];
            _shadows = new CompositionShadow?[root.ShadowsLength];
            _surfaces = new ICompositionSurface?[root.SurfacesLength];
            _effects = new GraphicsEffectBase?[root.EffectsLength];
            _easings = new CompositionEasingFunction?[root.EasingsLength];
            _animations = new CompositionAnimation?[root.AnimationsLength];
            _propertySets = new CompositionPropertySet?[root.PropertySetsLength];
            _controllers = new AnimationController?[root.ControllersLength];
        }

        /// <summary>
        /// Rebuilds the graph described by a FlatBuffer.
        /// </summary>
        /// <param name="bytes">A buffer produced by <see cref="CompositionSerializer"/>.</param>
        /// <returns>The root visual of the rebuilt graph.</returns>
        /// <exception cref="FlatBufferFormatException">The buffer is not a well formed composition.</exception>
        public static Visual Deserialize(ReadOnlySpan<byte> bytes)
        {
            // The buffer is untrusted, so it is fully verified before any of it is read.
            var buffer = new ByteBuffer(bytes.ToArray());

            if (bytes.Length < MinimumBufferLength ||
                !Fb.LottieComposition.LottieCompositionBufferHasIdentifier(buffer))
            {
                throw new FlatBufferFormatException("The buffer is not a composition.");
            }

            // Verification proves that the buffer is structurally sound, but it cannot
            // prove that the indices stored in it point at the right things. Neither the
            // verifier nor the reader reports a malformed buffer purely by returning
            // false; both also throw. Everything is therefore done inside this handler so
            // that a corrupt buffer always surfaces as a format error and never as an
            // exception that a caller could not have anticipated. Reading past the end of
            // the buffer is not a safety problem because ByteBuffer is bounds checked;
            // the only thing being corrected here is the type of the exception.
            try
            {
                if (!Fb.LottieComposition.VerifyLottieComposition(buffer))
                {
                    throw new FlatBufferFormatException("The buffer is not a valid composition.");
                }

                var root = Fb.LottieComposition.GetRootAsLottieComposition(buffer);

                if (root.SchemaVersion > Format.Version)
                {
                    throw new FlatBufferFormatException(
                        $"Schema version {root.SchemaVersion} is newer than the supported version {Format.Version}.");
                }

                var deserializer = new CompositionDeserializer(root);
                var rootVisual = deserializer.GetVisual(root.RootVisual);

                return rootVisual ?? throw new FlatBufferFormatException("The composition has no root visual.");
            }
            catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or
                                          InvalidOperationException or OverflowException or
                                          FormatException or NullReferenceException)
            {
                throw new FlatBufferFormatException("The buffer is malformed.", e);
            }
        }

        // ---------------------------------------------------------------------
        // Table accessors. Every index is bounds checked, because a buffer that
        // passes verification can still contain an out of range index.
        // ---------------------------------------------------------------------
        static T Require<T>(T? value, string what)
            where T : struct
            => value ?? throw new FlatBufferFormatException($"A {what} is missing.");

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
        // Applies the state that is common to every CompositionObject. Called after the
        // object has been placed in its realization cache, so that a cycle through a
        // reference parameter terminates.
        void InitializeCompositionObject(CompositionObject target, Fb.CompObj? source)
        {
            if (source is null)
            {
                return;
            }

            var value = source.Value;

            target.Comment = GetString(value.Comment);

            // The property set is realized through the object that owns it, so that the
            // owner and its property set can never be created twice.
            if (value.Properties != Format.NullIndex)
            {
                RealizePropertySet(value.Properties, target.Properties);
            }

            // Animations are started last, because setting a property stops any animation
            // that is running on it.
            for (var i = 0; i < value.AnimatorsLength; i++)
            {
                var animator = Require(value.Animators(i), "animator");
                var property = GetRequiredString(animator.Property);
                var animation = GetAnimation(animator.Animation)
                    ?? throw new FlatBufferFormatException($"Animator '{property}' has no animation.");

                // A non custom controller is created by StartAnimation, so only a custom
                // one is passed in. Either way the resulting controller is cached under
                // the index that the buffer gave it.
                var controllerIndex = animator.Controller;
                var customController = controllerIndex != Format.NullIndex && IsCustomController(controllerIndex)
                    ? GetController(controllerIndex)
                    : null;

                // An expression animation is driven by its inputs and so cannot have a
                // controller. StartAnimation only asserts this, so it is checked here in
                // order to reject the buffer rather than build an unusable graph.
                if (customController is not null && animation is ExpressionAnimation)
                {
                    throw new FlatBufferFormatException(
                        $"Animator '{property}' gives a controller to an expression animation.");
                }

                var created = target.StartAnimation(property, animation, customController);

                if (controllerIndex != Format.NullIndex && created.Controller is not null)
                {
                    RealizeController(controllerIndex, created.Controller);
                }
            }
        }

        bool IsCustomController(uint index)
            => GetControllerTable(index).IsCustom;

        Fb.Controller GetControllerTable(uint index)
            => index < (uint)_controllers.Length
                ? Require(_root.Controllers((int)index), "controller")
                : throw new FlatBufferFormatException($"Controller index {index} is out of range.");

        void RealizeController(uint index, AnimationController controller)
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

        AnimationController? GetController(uint index)
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
                    // A non custom controller comes into existence when its target object
                    // starts the animation, so realizing the target realizes the controller.
                    GetObjectReference(table.TargetObject);
                }
            }

            return _controllers[index]
                ?? throw new FlatBufferFormatException($"Controller {index} could not be realized.");
        }

        void RealizePropertySet(uint index, CompositionPropertySet propertySet)
        {
            if (index >= (uint)_propertySets.Length)
            {
                throw new FlatBufferFormatException($"Property set index {index} is out of range.");
            }

            if (_propertySets[index] is not null)
            {
                return;
            }

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
                }
            }

            InitializeCompositionObject(propertySet, table.Base);
        }

        CompositionPropertySet? GetPropertySet(uint index)
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

        // Resolves a packed (category, index) reference. Only used by the three fields
        // that can point at an object of any category.
        CompositionObject? GetObjectReference(uint reference)
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
                Fb.ObjectCategory.Surface => GetSurface(index) as CompositionObject,
                Fb.ObjectCategory.Clip => GetClip(index),
                Fb.ObjectCategory.Controller => GetController(index),
                Fb.ObjectCategory.Shadow => GetShadow(index),
                Fb.ObjectCategory.GradientStop => GetGradientStop(index),
                Fb.ObjectCategory.ViewBox => GetViewBox(index),
                _ => throw new FlatBufferFormatException($"Unsupported object category {category}."),
            };
        }

        // ---------------------------------------------------------------------
        // Nodes.
        // ---------------------------------------------------------------------
        Visual? GetVisual(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_visuals.Length)
            {
                throw new FlatBufferFormatException($"Visual index {index} is out of range.");
            }

            if (_visuals[index] is Visual cached)
            {
                return cached;
            }

            var table = Require(_root.Visuals((int)index), "visual");

            Visual result = table.Kind switch
            {
                Fb.VisualKind.Shape => _compositor.CreateShapeVisual(),
                Fb.VisualKind.Sprite => _compositor.CreateSpriteVisual(),
                Fb.VisualKind.Layer => _compositor.CreateLayerVisual(),
                Fb.VisualKind.Container => _compositor.CreateContainerVisual(),
                _ => throw new FlatBufferFormatException($"Unsupported visual kind {table.Kind}."),
            };

            _visuals[index] = result;

            if (table.BorderMode.HasValue)
            {
                result.BorderMode = (CompositionBorderMode)table.BorderMode.Value;
            }

            result.CenterPoint = ToVector3(table.CenterPoint);
            result.Clip = GetClip(table.Clip);
            result.IsVisible = table.IsVisible;
            result.Offset = ToVector3(table.Offset);
            result.Opacity = table.Opacity;
            result.RotationAngleInDegrees = table.RotationAngleInDegrees;
            result.RotationAxis = ToVector3(table.RotationAxis);
            result.Scale = ToVector3(table.Scale);
            result.Size = ToVector2(table.Size);
            result.TransformMatrix = ToMatrix4x4(table.TransformMatrix);

            if (result is ContainerVisual container)
            {
                for (var i = 0; i < table.ChildrenLength; i++)
                {
                    container.Children.Add(
                        GetVisual(table.Children(i))
                        ?? throw new FlatBufferFormatException("A visual child is missing."));
                }
            }

            switch (result)
            {
                case ShapeVisual shapeVisual:
                    for (var i = 0; i < table.ShapesLength; i++)
                    {
                        shapeVisual.Shapes.Add(
                            GetShape(table.Shapes(i))
                            ?? throw new FlatBufferFormatException("A shape is missing."));
                    }

                    shapeVisual.ViewBox = GetViewBox(table.ViewBox);
                    break;
                case SpriteVisual spriteVisual:
                    spriteVisual.Brush = GetBrush(table.Brush);
                    spriteVisual.Shadow = GetShadow(table.Shadow);
                    break;
                case LayerVisual layerVisual:
                    layerVisual.Shadow = GetShadow(table.Shadow);
                    break;
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        CompositionShape? GetShape(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_shapes.Length)
            {
                throw new FlatBufferFormatException($"Shape index {index} is out of range.");
            }

            if (_shapes[index] is CompositionShape cached)
            {
                return cached;
            }

            var table = Require(_root.Shapes((int)index), "shape");

            CompositionShape result = table.Kind switch
            {
                Fb.ShapeKind.Sprite => _compositor.CreateSpriteShape(),
                Fb.ShapeKind.Container => _compositor.CreateContainerShape(),
                _ => throw new FlatBufferFormatException($"Unsupported shape kind {table.Kind}."),
            };

            _shapes[index] = result;

            result.CenterPoint = ToVector2(table.CenterPoint);
            result.Offset = ToVector2(table.Offset);
            result.RotationAngleInDegrees = table.RotationAngleInDegrees;
            result.Scale = ToVector2(table.Scale);
            result.TransformMatrix = ToMatrix3x2(table.TransformMatrix);

            switch (result)
            {
                case CompositionContainerShape containerShape:
                    for (var i = 0; i < table.ShapesLength; i++)
                    {
                        containerShape.Shapes.Add(
                            GetShape(table.Shapes(i))
                            ?? throw new FlatBufferFormatException("A shape is missing."));
                    }

                    break;
                case CompositionSpriteShape spriteShape:
                    spriteShape.FillBrush = GetBrush(table.FillBrush);
                    spriteShape.StrokeBrush = GetBrush(table.StrokeBrush);
                    spriteShape.Geometry = GetGeometry(table.Geometry);
                    spriteShape.IsStrokeNonScaling = table.IsStrokeNonScaling;
                    spriteShape.StrokeDashOffset = table.StrokeDashOffset;

                    for (var i = 0; i < table.StrokeDashArrayLength; i++)
                    {
                        spriteShape.StrokeDashArray.Add(table.StrokeDashArray(i));
                    }

                    if (table.StrokeDashCap.HasValue)
                    {
                        spriteShape.StrokeDashCap = (CompositionStrokeCap)table.StrokeDashCap.Value;
                    }

                    if (table.StrokeStartCap.HasValue)
                    {
                        spriteShape.StrokeStartCap = (CompositionStrokeCap)table.StrokeStartCap.Value;
                    }

                    if (table.StrokeEndCap.HasValue)
                    {
                        spriteShape.StrokeEndCap = (CompositionStrokeCap)table.StrokeEndCap.Value;
                    }

                    if (table.StrokeLineJoin.HasValue)
                    {
                        spriteShape.StrokeLineJoin = (CompositionStrokeLineJoin)table.StrokeLineJoin.Value;
                    }

                    spriteShape.StrokeMiterLimit = table.StrokeMiterLimit;
                    spriteShape.StrokeThickness = table.StrokeThickness;
                    break;
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        CompositionGeometry? GetGeometry(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_geometries.Length)
            {
                throw new FlatBufferFormatException($"Geometry index {index} is out of range.");
            }

            if (_geometries[index] is CompositionGeometry cached)
            {
                return cached;
            }

            var table = Require(_root.Geometries((int)index), "geometry");

            CompositionGeometry result = table.Kind switch
            {
                Fb.GeometryKind.Rectangle => _compositor.CreateRectangleGeometry(),
                Fb.GeometryKind.RoundedRectangle => _compositor.CreateRoundedRectangleGeometry(),
                Fb.GeometryKind.Ellipse => _compositor.CreateEllipseGeometry(),
                Fb.GeometryKind.Path => _compositor.CreatePathGeometry(),
                _ => throw new FlatBufferFormatException($"Unsupported geometry kind {table.Kind}."),
            };

            _geometries[index] = result;

            result.TrimStart = table.TrimStart;
            result.TrimEnd = table.TrimEnd;
            result.TrimOffset = table.TrimOffset;

            switch (result)
            {
                case CompositionPathGeometry pathGeometry:
                    var path = GetCanvasGeometry(table.Path);
                    pathGeometry.Path = path is null ? null : new CompositionPath(path);
                    break;
                case CompositionRoundedRectangleGeometry roundedRectangle:
                    roundedRectangle.Offset = ToVector2(table.Offset);
                    roundedRectangle.Size = ToVector2(table.Size);
                    roundedRectangle.CornerRadius = ToVector2(table.CornerRadius) ?? Vector2.Zero;
                    break;
                case CompositionRectangleGeometry rectangle:
                    rectangle.Offset = ToVector2(table.Offset);
                    rectangle.Size = ToVector2(table.Size);
                    break;
                case CompositionEllipseGeometry ellipse:
                    ellipse.Center = ToVector2(table.Center) ?? Vector2.Zero;
                    ellipse.Radius = ToVector2(table.Radius) ?? Vector2.Zero;
                    break;
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        CompositionBrush? GetBrush(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_brushes.Length)
            {
                throw new FlatBufferFormatException($"Brush index {index} is out of range.");
            }

            if (_brushes[index] is CompositionBrush cached)
            {
                return cached;
            }

            var table = Require(_root.Brushes((int)index), "brush");

            CompositionBrush result;
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
                    // The surface is needed to construct the brush, so it is realized
                    // before the brush is placed in the cache.
                    result = _compositor.CreateSurfaceBrush(
                        GetSurface(table.Surface)
                        ?? throw new FlatBufferFormatException("A surface brush has no surface."));
                    break;
                case Fb.BrushKind.Effect:
                    var effect = GetEffect(table.Effect)
                        ?? throw new FlatBufferFormatException("An effect brush has no effect.");
                    result = CompositionEffectFactory.GetFactoryCached(effect).CreateBrush();
                    break;
                default:
                    throw new FlatBufferFormatException($"Unsupported brush kind {table.Kind}.");
            }

            _brushes[index] = result;

            switch (result)
            {
                case CompositionColorBrush colorBrush:
                    colorBrush.Color = ToColorOrNull(table.Color);
                    break;
                case CompositionMaskBrush maskBrush:
                    maskBrush.Source = GetBrush(table.Source);
                    maskBrush.Mask = GetBrush(table.Mask);
                    break;
                case CompositionEffectBrush effectBrush:
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

            if (result is CompositionGradientBrush gradientBrush)
            {
                gradientBrush.AnchorPoint = ToVector2(table.AnchorPoint);
                gradientBrush.CenterPoint = ToVector2(table.CenterPoint);

                for (var i = 0; i < table.ColorStopsLength; i++)
                {
                    gradientBrush.ColorStops.Add(
                        GetGradientStop(table.ColorStops(i))
                        ?? throw new FlatBufferFormatException("A gradient stop is missing."));
                }

                if (table.ExtendMode.HasValue)
                {
                    gradientBrush.ExtendMode = (CompositionGradientExtendMode)table.ExtendMode.Value;
                }

                if (table.InterpolationSpace.HasValue)
                {
                    gradientBrush.InterpolationSpace = (CompositionColorSpace)table.InterpolationSpace.Value;
                }

                if (table.MappingMode.HasValue)
                {
                    gradientBrush.MappingMode = (CompositionMappingMode)table.MappingMode.Value;
                }

                gradientBrush.Offset = ToVector2(table.Offset);
                gradientBrush.RotationAngleInDegrees = table.RotationAngleInDegrees;
                gradientBrush.Scale = ToVector2(table.Scale);
                gradientBrush.TransformMatrix = ToMatrix3x2(table.TransformMatrix);

                switch (gradientBrush)
                {
                    case CompositionLinearGradientBrush linear:
                        linear.StartPoint = ToVector2(table.StartPoint);
                        linear.EndPoint = ToVector2(table.EndPoint);
                        break;
                    case CompositionRadialGradientBrush radial:
                        radial.EllipseCenter = ToVector2(table.EllipseCenter);
                        radial.EllipseRadius = ToVector2(table.EllipseRadius);
                        radial.GradientOriginOffset = ToVector2(table.GradientOriginOffset);
                        break;
                }
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        CompositionColorGradientStop? GetGradientStop(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_gradientStops.Length)
            {
                throw new FlatBufferFormatException($"Gradient stop index {index} is out of range.");
            }

            if (_gradientStops[index] is CompositionColorGradientStop cached)
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

        CompositionViewBox? GetViewBox(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_viewBoxes.Length)
            {
                throw new FlatBufferFormatException($"View box index {index} is out of range.");
            }

            if (_viewBoxes[index] is CompositionViewBox cached)
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

        CompositionClip? GetClip(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_clips.Length)
            {
                throw new FlatBufferFormatException($"Clip index {index} is out of range.");
            }

            if (_clips[index] is CompositionClip cached)
            {
                return cached;
            }

            var table = Require(_root.Clips((int)index), "clip");

            CompositionClip result = table.Kind switch
            {
                Fb.ClipKind.Geometric => _compositor.CreateGeometricClip(),
                Fb.ClipKind.Inset => _compositor.CreateInsetClip(),
                _ => throw new FlatBufferFormatException($"Unsupported clip kind {table.Kind}."),
            };

            _clips[index] = result;

            result.CenterPoint = ToVector2(table.CenterPoint);
            result.Scale = ToVector2(table.Scale);

            switch (result)
            {
                case InsetClip inset:
                    inset.LeftInset = table.LeftInset;
                    inset.RightInset = table.RightInset;
                    inset.TopInset = table.TopInset;
                    inset.BottomInset = table.BottomInset;
                    break;
                case CompositionGeometricClip geometric:
                    geometric.Geometry = GetGeometry(table.Geometry);
                    break;
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        CompositionShadow? GetShadow(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_shadows.Length)
            {
                throw new FlatBufferFormatException($"Shadow index {index} is out of range.");
            }

            if (_shadows[index] is CompositionShadow cached)
            {
                return cached;
            }

            var table = Require(_root.Shadows((int)index), "shadow");

            CompositionShadow result = table.Kind switch
            {
                Fb.ShadowKind.Drop => _compositor.CreateDropShadow(),
                _ => throw new FlatBufferFormatException($"Unsupported shadow kind {table.Kind}."),
            };

            _shadows[index] = result;

            if (result is DropShadow dropShadow)
            {
                dropShadow.BlurRadius = table.BlurRadius;
                dropShadow.Color = ToColorOrNull(table.Color);
                dropShadow.Mask = GetBrush(table.Mask);
                dropShadow.Offset = ToVector3(table.Offset);
                dropShadow.Opacity = table.Opacity;

                if (table.SourcePolicy.HasValue)
                {
                    dropShadow.SourcePolicy = (CompositionDropShadowSourcePolicy)table.SourcePolicy.Value;
                }
            }

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        ICompositionSurface? GetSurface(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_surfaces.Length)
            {
                throw new FlatBufferFormatException($"Surface index {index} is out of range.");
            }

            if (_surfaces[index] is ICompositionSurface cached)
            {
                return cached;
            }

            var table = Require(_root.Surfaces((int)index), "surface");

            switch (table.Kind)
            {
                case Fb.SurfaceKind.VisualSurface:
                    var visualSurface = _compositor.CreateVisualSurface();
                    _surfaces[index] = visualSurface;
                    visualSurface.SourceVisual = GetVisual(table.SourceVisual);
                    visualSurface.SourceSize = ToVector2(table.SourceSize);
                    visualSurface.SourceOffset = ToVector2(table.SourceOffset);
                    InitializeCompositionObject(visualSurface, table.Base);
                    return visualSurface;

                case Fb.SurfaceKind.LoadedImageFromUri:
                    var uri = GetRequiredString(table.Uri);
                    var fromUri = LoadedImageSurface.StartLoadFromUri(new Uri(uri, UriKind.RelativeOrAbsolute));
                    _surfaces[index] = fromUri;
                    return fromUri;

                case Fb.SurfaceKind.LoadedImageFromStream:
                    var bytes = new byte[table.BytesLength];
                    for (var i = 0; i < bytes.Length; i++)
                    {
                        bytes[i] = table.Bytes(i);
                    }

                    var fromStream = LoadedImageSurface.StartLoadFromStream(bytes);
                    _surfaces[index] = fromStream;
                    return fromStream;

                default:
                    throw new FlatBufferFormatException($"Unsupported surface kind {table.Kind}.");
            }
        }

        GraphicsEffectBase? GetEffect(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_effects.Length)
            {
                throw new FlatBufferFormatException($"Effect index {index} is out of range.");
            }

            if (_effects[index] is GraphicsEffectBase cached)
            {
                return cached;
            }

            var table = Require(_root.Effects((int)index), "effect");

            var sources = new List<CompositionEffectSourceParameter>(table.SourcesLength);
            for (var i = 0; i < table.SourcesLength; i++)
            {
                sources.Add(new CompositionEffectSourceParameter(GetRequiredString(table.Sources(i))));
            }

            GraphicsEffectBase result = table.Kind switch
            {
                Fb.EffectKind.Composite => new CompositeEffect((CanvasComposite)table.Mode, sources),
                Fb.EffectKind.GaussianBlur => new GaussianBlurEffect(
                    table.BlurAmount,
                    sources.Count == 1
                        ? sources[0]
                        : throw new FlatBufferFormatException("A blur effect must have exactly one source.")),
                _ => throw new FlatBufferFormatException($"Unsupported effect kind {table.Kind}."),
            };

            _effects[index] = result;
            return result;
        }

        CompositionEasingFunction? GetEasing(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_easings.Length)
            {
                throw new FlatBufferFormatException($"Easing index {index} is out of range.");
            }

            if (_easings[index] is CompositionEasingFunction cached)
            {
                return cached;
            }

            var table = Require(_root.Easings((int)index), "easing");

            CompositionEasingFunction result;
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
                    step.StepCount = table.StepCount;
                    step.InitialStep = table.InitialStep;
                    step.FinalStep = table.FinalStep;
                    step.IsInitialStepSingleFrame = table.IsInitialStepSingleFrame;
                    step.IsFinalStepSingleFrame = table.IsFinalStepSingleFrame;
                    result = step;
                    break;
                default:
                    throw new FlatBufferFormatException($"Unsupported easing kind {table.Kind}.");
            }

            _easings[index] = result;

            InitializeCompositionObject(result, table.Base);
            return result;
        }

        CompositionAnimation? GetAnimation(uint index)
        {
            if (index == Format.NullIndex)
            {
                return null;
            }

            if (index >= (uint)_animations.Length)
            {
                throw new FlatBufferFormatException($"Animation index {index} is out of range.");
            }

            if (_animations[index] is CompositionAnimation cached)
            {
                return cached;
            }

            var table = Require(_root.Animations((int)index), "animation");

            var result = table.Kind == Fb.AnimationKind.Expression
                ? (CompositionAnimation)_compositor.CreateExpressionAnimation(
                    Expressions.Expression.Scalar(GetRequiredString(table.Expression)))
                : CreateKeyFrameAnimation(table);

            _animations[index] = result;

            result.Target = GetString(table.Target);

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

        CompositionAnimation CreateKeyFrameAnimation(Fb.Animation table)
        {
            KeyFrameAnimation_ result = table.Kind switch
            {
                Fb.AnimationKind.Scalar => _compositor.CreateScalarKeyFrameAnimation(),
                Fb.AnimationKind.Vector2 => _compositor.CreateVector2KeyFrameAnimation(),
                Fb.AnimationKind.Vector3 => _compositor.CreateVector3KeyFrameAnimation(),
                Fb.AnimationKind.Vector4 => _compositor.CreateVector4KeyFrameAnimation(),
                Fb.AnimationKind.Color => _compositor.CreateColorKeyFrameAnimation(),
                Fb.AnimationKind.Boolean => _compositor.CreateBooleanKeyFrameAnimation(),
                Fb.AnimationKind.Path => _compositor.CreatePathKeyFrameAnimation(),
                _ => throw new FlatBufferFormatException($"Unsupported animation kind {table.Kind}."),
            };

            result.Duration = TimeSpan.FromTicks(table.DurationTicks);

            if (result is ColorKeyFrameAnimation colorAnimation && table.InterpolationColorSpace.HasValue)
            {
                colorAnimation.InterpolationColorSpace =
                    (CompositionColorSpace)table.InterpolationColorSpace.Value;
            }

            for (var i = 0; i < table.KeyFramesLength; i++)
            {
                InsertKeyFrame(result, Require(table.KeyFrames(i), "key frame"));
            }

            return result;
        }

        void InsertKeyFrame(KeyFrameAnimation_ animation, Fb.KeyFrame frame)
        {
            var progress = frame.Progress;
            var easing = GetEasing(frame.Easing);

            if (frame.Kind == Fb.KeyFrameKind.Expression)
            {
                var text = GetRequiredString(frame.Expression);

                switch (animation)
                {
                    case ScalarKeyFrameAnimation scalar:
                        scalar.InsertExpressionKeyFrame(progress, Expressions.Expression.Scalar(text), easing);
                        break;
                    case Vector2KeyFrameAnimation vector2:
                        vector2.InsertExpressionKeyFrame(progress, Expressions.Expression.Vector2(text), easing);
                        break;
                    case Vector3KeyFrameAnimation vector3:
                        vector3.InsertExpressionKeyFrame(progress, Expressions.Expression.Vector3(text), easing);
                        break;
                    case Vector4KeyFrameAnimation vector4:
                        vector4.InsertExpressionKeyFrame(progress, Expressions.Expression.Vector4(text), easing);
                        break;
                    case ColorKeyFrameAnimation color:
                        color.InsertExpressionKeyFrame(progress, Expressions.Expression.Color(text), easing);
                        break;
                    case BooleanKeyFrameAnimation boolean:
                        boolean.InsertExpressionKeyFrame(progress, Expressions.Expression.Boolean(text), easing);
                        break;
                    default:
                        throw new FlatBufferFormatException(
                            $"{animation.Type} does not support expression key frames.");
                }

                return;
            }

            switch (animation)
            {
                case ScalarKeyFrameAnimation scalar:
                    scalar.InsertKeyFrame(progress, frame.Scalar, easing);
                    break;
                case Vector2KeyFrameAnimation vector2:
                    vector2.InsertKeyFrame(progress, ToVector2(frame.Vector), easing);
                    break;
                case Vector3KeyFrameAnimation vector3:
                    vector3.InsertKeyFrame(progress, ToVector3(frame.Vector), easing);
                    break;
                case Vector4KeyFrameAnimation vector4:
                    vector4.InsertKeyFrame(progress, ToVector4(frame.Vector), easing);
                    break;
                case ColorKeyFrameAnimation color:
                    color.InsertKeyFrame(progress, ToColor(Require(frame.Color, "color")), easing);
                    break;
                case BooleanKeyFrameAnimation boolean:
                    boolean.InsertKeyFrame(progress, frame.Scalar != 0, easing);
                    break;
                case PathKeyFrameAnimation path:
                    path.InsertKeyFrame(
                        progress,
                        new CompositionPath(
                            GetCanvasGeometry(frame.Path)
                            ?? throw new FlatBufferFormatException("A path key frame has no path.")),
                        easing);
                    break;
                default:
                    throw new FlatBufferFormatException($"Unsupported animation {animation.Type}.");
            }
        }

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

            if (_canvasGeometries[index] is CanvasGeometry cached)
            {
                return cached;
            }

            var table = Require(_root.CanvasGeometries((int)index), "canvas geometry");

            CanvasGeometry result;
            switch (table.Kind)
            {
                case Fb.CanvasGeometryKind.Combination:
                    var a = GetCanvasGeometry(table.A)
                        ?? throw new FlatBufferFormatException("A combination has no first geometry.");
                    var b = GetCanvasGeometry(table.B)
                        ?? throw new FlatBufferFormatException("A combination has no second geometry.");
                    result = a.CombineWith(
                        b,
                        ToMatrix3x2(table.Matrix) ?? Matrix3x2.Identity,
                        (CanvasGeometryCombine)table.CombineMode);
                    break;

                case Fb.CanvasGeometryKind.TransformedGeometry:
                    var source = GetCanvasGeometry(table.Source)
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
            return result;
        }

        // Replays the flattened opcode and operand streams of a path.
        static CanvasGeometry CreatePath(Fb.CanvasGeometry table)
        {
            using var builder = new CanvasPathBuilder(null);
            builder.SetFilledRegionDetermination((CanvasFilledRegionDetermination)table.FillRule);

            var operand = 0;

            float Next()
                => operand < table.OperandsLength
                    ? table.Operands(operand++)
                    : throw new FlatBufferFormatException("A path ran out of operands.");

            for (var i = 0; i < table.OpsLength; i++)
            {
                switch ((Fb.PathOp)table.Ops(i))
                {
                    case Fb.PathOp.BeginFigure:
                        builder.BeginFigure(new Vector2(Next(), Next()));
                        break;
                    case Fb.PathOp.EndFigure:
                        builder.EndFigure((CanvasFigureLoop)Next());
                        break;
                    case Fb.PathOp.AddLine:
                        builder.AddLine(new Vector2(Next(), Next()));
                        break;
                    case Fb.PathOp.AddCubicBezier:
                        builder.AddCubicBezier(
                            new Vector2(Next(), Next()),
                            new Vector2(Next(), Next()),
                            new Vector2(Next(), Next()));
                        break;
                    default:
                        throw new FlatBufferFormatException($"Unsupported path opcode {table.Ops(i)}.");
                }
            }

            return CanvasGeometry.CreatePath(builder);
        }

        // ---------------------------------------------------------------------
        // Value helpers. An absent struct field means the property was never set,
        // so these return null rather than a default value.
        // ---------------------------------------------------------------------
        static Wui.Color ToColor(Fb.Color value)
            => Wui.Color.FromArgb(value.A, value.R, value.G, value.B);

        static Wui.Color? ToColorOrNull(Fb.Color? value)
            => value.HasValue ? ToColor(value.Value) : null;

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
