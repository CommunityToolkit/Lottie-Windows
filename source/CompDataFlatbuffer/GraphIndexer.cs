// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;
using CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer
{
    /// <summary>
    /// Walks a WinCompData graph and assigns a dense index to every node, grouped by
    /// the <see cref="Schema.ObjectCategory"/> that the node is serialized into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Indices are assigned in the order that nodes are first reached by a depth first
    /// walk from the root. The walk order is fully determined by the shape of the graph,
    /// so serializing the same graph twice produces identical output.
    /// </para>
    /// <para>
    /// Nodes are identified by reference, not by value. This preserves the sharing in
    /// the graph: a node that is referenced from several places is stored once and
    /// referenced by index, which is what lets the interpreter realize it once.
    /// </para>
    /// </remarks>
    sealed class GraphIndexer
    {
        // Nodes are keyed by reference so that distinct but equal objects stay distinct,
        // matching the reference-identity caching that Instantiator performs at runtime.
        readonly Dictionary<object, int> _visuals = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _shapes = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _geometries = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _canvasGeometries = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _brushes = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _gradientStops = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _viewBoxes = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _clips = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _shadows = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _surfaces = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _effects = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _easings = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _animations = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _propertySets = new Dictionary<object, int>(ReferenceComparer.Instance);
        readonly Dictionary<object, int> _controllers = new Dictionary<object, int>(ReferenceComparer.Instance);

        readonly Dictionary<string, int> _strings = new Dictionary<string, int>(StringComparer.Ordinal);

        // Set once the string table has been written to the buffer. Interning a new
        // string after that point would hand out an index that is past the end of the
        // serialized table, so it is treated as a programming error rather than being
        // allowed to silently corrupt the output.
        bool _stringsAreFrozen;

        GraphIndexer()
        {
        }

        internal List<Visual> VisualList { get; } = new List<Visual>();

        internal List<CompositionShape> ShapeList { get; } = new List<CompositionShape>();

        internal List<CompositionGeometry> GeometryList { get; } = new List<CompositionGeometry>();

        internal List<CanvasGeometry> CanvasGeometryList { get; } = new List<CanvasGeometry>();

        internal List<CompositionBrush> BrushList { get; } = new List<CompositionBrush>();

        internal List<CompositionColorGradientStop> GradientStopList { get; } = new List<CompositionColorGradientStop>();

        internal List<CompositionViewBox> ViewBoxList { get; } = new List<CompositionViewBox>();

        internal List<CompositionClip> ClipList { get; } = new List<CompositionClip>();

        internal List<CompositionShadow> ShadowList { get; } = new List<CompositionShadow>();

        internal List<ICompositionSurface> SurfaceList { get; } = new List<ICompositionSurface>();

        internal List<GraphicsEffectBase> EffectList { get; } = new List<GraphicsEffectBase>();

        internal List<CompositionEasingFunction> EasingList { get; } = new List<CompositionEasingFunction>();

        internal List<CompositionAnimation> AnimationList { get; } = new List<CompositionAnimation>();

        internal List<CompositionPropertySet> PropertySetList { get; } = new List<CompositionPropertySet>();

        internal List<AnimationController> ControllerList { get; } = new List<AnimationController>();

        internal List<string> StringList { get; } = new List<string>();

        /// <summary>
        /// Walks the graph rooted at <paramref name="root"/> and returns the resulting index.
        /// </summary>
        /// <param name="root">The root visual of the graph.</param>
        /// <returns>An indexer holding every node reachable from <paramref name="root"/>.</returns>
        internal static GraphIndexer Index(Visual root)
        {
            var result = new GraphIndexer();
            result.GetVisual(root);
            return result;
        }

        /// <summary>
        /// Returns the index of a string, adding it to the string table if necessary.
        /// </summary>
        /// <param name="value">The string, or null.</param>
        /// <returns>The index of the string, or <see cref="Format.NullIndex"/> if null.</returns>
        internal uint GetString(string? value)
        {
            if (value is null)
            {
                return Format.NullIndex;
            }

            if (!_strings.TryGetValue(value, out var index))
            {
                if (_stringsAreFrozen)
                {
                    throw new InvalidOperationException(
                        $"The string \"{value}\" was interned after the string table was written.");
                }

                index = StringList.Count;
                StringList.Add(value);
                _strings.Add(value, index);
            }

            return (uint)index;
        }

        /// <summary>
        /// Prevents any further strings from being added. Called once the string table has
        /// been written, so that a missed interning site fails loudly instead of producing
        /// an out of range string index.
        /// </summary>
        internal void FreezeStrings() => _stringsAreFrozen = true;

        internal uint GetVisual(Visual? value) => Index(_visuals, value);

        internal uint GetShape(CompositionShape? value) => Index(_shapes, value);

        internal uint GetGeometry(CompositionGeometry? value) => Index(_geometries, value);

        internal uint GetCanvasGeometry(CanvasGeometry? value) => Index(_canvasGeometries, value);

        internal uint GetBrush(CompositionBrush? value) => Index(_brushes, value);

        internal uint GetGradientStop(CompositionColorGradientStop? value) => Index(_gradientStops, value);

        internal uint GetViewBox(CompositionViewBox? value) => Index(_viewBoxes, value);

        internal uint GetClip(CompositionClip? value) => Index(_clips, value);

        internal uint GetShadow(CompositionShadow? value) => Index(_shadows, value);

        internal uint GetSurface(ICompositionSurface? value) => Index(_surfaces, value);

        internal uint GetEffect(GraphicsEffectBase? value) => Index(_effects, value);

        internal uint GetEasing(CompositionEasingFunction? value) => Index(_easings, value);

        internal uint GetAnimation(CompositionAnimation? value) => Index(_animations, value);

        internal uint GetPropertySet(CompositionPropertySet? value) => Index(_propertySets, value);

        internal uint GetController(AnimationController? value) => Index(_controllers, value);

        /// <summary>
        /// Returns the index of an already-discovered object as a packed object reference.
        /// Used for the fields that can refer to any kind of object, such as the target of
        /// an animation reference parameter.
        /// </summary>
        /// <param name="value">The object, or null.</param>
        /// <returns>The packed object reference, or <see cref="Format.NullIndex"/>.</returns>
        internal uint GetObjectReference(CompositionObject? value)
        {
            if (value is null)
            {
                return Format.NullIndex;
            }

            var (category, index) = value switch
            {
                Visual visual => (Schema.ObjectCategory.Visual, GetVisual(visual)),
                CompositionShape shape => (Schema.ObjectCategory.Shape, GetShape(shape)),
                CompositionGeometry geometry => (Schema.ObjectCategory.Geometry, GetGeometry(geometry)),
                CompositionBrush brush => (Schema.ObjectCategory.Brush, GetBrush(brush)),
                CompositionAnimation animation => (Schema.ObjectCategory.Animation, GetAnimation(animation)),
                CompositionEasingFunction easing => (Schema.ObjectCategory.Easing, GetEasing(easing)),
                CompositionPropertySet propertySet => (Schema.ObjectCategory.PropertySet, GetPropertySet(propertySet)),
                CompositionVisualSurface surface => (Schema.ObjectCategory.Surface, GetSurface(surface)),
                CompositionClip clip => (Schema.ObjectCategory.Clip, GetClip(clip)),
                AnimationController controller => (Schema.ObjectCategory.Controller, GetController(controller)),
                CompositionShadow shadow => (Schema.ObjectCategory.Shadow, GetShadow(shadow)),
                CompositionColorGradientStop stop => (Schema.ObjectCategory.GradientStop, GetGradientStop(stop)),
                CompositionViewBox viewBox => (Schema.ObjectCategory.ViewBox, GetViewBox(viewBox)),
                _ => throw new InvalidOperationException($"Cannot reference {value.Type}."),
            };

            return Format.PackObjectReference(category, (int)index);
        }

        // Returns the plain index of a node, adding it to the graph if it has not been
        // seen before. The node is registered before its children are walked so that
        // cycles terminate.
        uint Index<T>(Dictionary<object, int> map, T? value)
            where T : class
        {
            if (value is null)
            {
                return Format.NullIndex;
            }

            if (map.TryGetValue(value, out var existing))
            {
                return (uint)existing;
            }

            var index = map.Count;
            map.Add(value, index);
            Add(value);
            return (uint)index;
        }

        // Appends a newly discovered node to its category list and walks its children.
        void Add(object value)
        {
            switch (value)
            {
                case Visual visual:
                    VisualList.Add(visual);
                    WalkVisual(visual);
                    break;
                case CompositionShape shape:
                    ShapeList.Add(shape);
                    WalkShape(shape);
                    break;
                case CompositionGeometry geometry:
                    GeometryList.Add(geometry);
                    WalkGeometry(geometry);
                    break;
                case CanvasGeometry canvasGeometry:
                    CanvasGeometryList.Add(canvasGeometry);
                    WalkCanvasGeometry(canvasGeometry);
                    break;
                case CompositionBrush brush:
                    BrushList.Add(brush);
                    WalkBrush(brush);
                    break;
                case CompositionColorGradientStop stop:
                    GradientStopList.Add(stop);
                    WalkCompositionObject(stop);
                    break;
                case CompositionViewBox viewBox:
                    ViewBoxList.Add(viewBox);
                    WalkCompositionObject(viewBox);
                    break;
                case CompositionClip clip:
                    ClipList.Add(clip);
                    WalkClip(clip);
                    break;
                case CompositionShadow shadow:
                    ShadowList.Add(shadow);
                    WalkShadow(shadow);
                    break;
                case ICompositionSurface surface:
                    SurfaceList.Add(surface);
                    WalkSurface(surface);
                    break;
                case GraphicsEffectBase effect:
                    EffectList.Add(effect);
                    break;
                case CompositionEasingFunction easing:
                    EasingList.Add(easing);
                    WalkCompositionObject(easing);
                    break;
                case CompositionAnimation animation:
                    AnimationList.Add(animation);
                    WalkAnimation(animation);
                    break;
                case CompositionPropertySet propertySet:
                    PropertySetList.Add(propertySet);
                    WalkPropertySet(propertySet);
                    break;
                case AnimationController controller:
                    ControllerList.Add(controller);
                    WalkController(controller);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected node {value.GetType().Name}.");
            }
        }

        // Walks the state that every CompositionObject has: its comment, its animators,
        // and its property set.
        void WalkCompositionObject(CompositionObject value)
        {
            GetString(value.Comment);

            foreach (var animator in value.Animators)
            {
                GetString(animator.AnimatedProperty);
                GetAnimation(animator.Animation);
                GetController(animator.Controller);
            }

            // A property set is only serialized if it carries state. The property set of a
            // property set is itself, so recursing into it unconditionally would not
            // terminate.
            if (value.Type != CompositionObjectType.CompositionPropertySet && HasState(value.Properties))
            {
                GetPropertySet(value.Properties);
            }
        }

        internal static bool HasState(CompositionPropertySet propertySet)
        {
            if (propertySet.Names.Count > 0)
            {
                return true;
            }

            foreach (var unused in propertySet.Animators)
            {
                return true;
            }

            return propertySet.Comment is not null;
        }

        void WalkVisual(Visual value)
        {
            WalkCompositionObject(value);
            GetClip(value.Clip);

            switch (value)
            {
                case ShapeVisual shapeVisual:
                    foreach (var shape in shapeVisual.Shapes)
                    {
                        GetShape(shape);
                    }

                    GetViewBox(shapeVisual.ViewBox);
                    break;
                case SpriteVisual spriteVisual:
                    GetBrush(spriteVisual.Brush);
                    GetShadow(spriteVisual.Shadow);
                    break;
                case LayerVisual layerVisual:
                    GetShadow(layerVisual.Shadow);
                    break;
            }

            if (value is ContainerVisual container)
            {
                foreach (var child in container.Children)
                {
                    GetVisual(child);
                }
            }
        }

        void WalkShape(CompositionShape value)
        {
            WalkCompositionObject(value);

            switch (value)
            {
                case CompositionContainerShape containerShape:
                    foreach (var shape in containerShape.Shapes)
                    {
                        GetShape(shape);
                    }

                    break;
                case CompositionSpriteShape spriteShape:
                    GetBrush(spriteShape.FillBrush);
                    GetBrush(spriteShape.StrokeBrush);
                    GetGeometry(spriteShape.Geometry);
                    break;
            }
        }

        void WalkGeometry(CompositionGeometry value)
        {
            WalkCompositionObject(value);

            if (value is CompositionPathGeometry pathGeometry && pathGeometry.Path is not null)
            {
                GetCanvasGeometry((CanvasGeometry)pathGeometry.Path.Source);
            }
        }

        void WalkCanvasGeometry(CanvasGeometry value)
        {
            switch (value)
            {
                case CanvasGeometry.Combination combination:
                    GetCanvasGeometry(combination.A);
                    GetCanvasGeometry(combination.B);
                    break;
                case CanvasGeometry.Group group:
                    foreach (var geometry in group.Geometries)
                    {
                        GetCanvasGeometry(geometry);
                    }

                    break;
                case CanvasGeometry.TransformedGeometry transformed:
                    GetCanvasGeometry(transformed.SourceGeometry);
                    break;
            }
        }

        void WalkBrush(CompositionBrush value)
        {
            WalkCompositionObject(value);

            switch (value)
            {
                case CompositionGradientBrush gradientBrush:
                    foreach (var stop in gradientBrush.ColorStops)
                    {
                        GetGradientStop(stop);
                    }

                    break;
                case CompositionSurfaceBrush surfaceBrush:
                    GetSurface(surfaceBrush.Surface);
                    break;
                case CompositionMaskBrush maskBrush:
                    GetBrush(maskBrush.Source);
                    GetBrush(maskBrush.Mask);
                    break;
                case CompositionEffectBrush effectBrush:
                    var effect = effectBrush.GetEffectFactory().Effect;
                    GetEffect(effect);
                    foreach (var source in effect.Sources)
                    {
                        GetString(source.Name);
                        GetBrush(effectBrush.GetSourceParameter(source.Name));
                    }

                    break;
            }
        }

        void WalkClip(CompositionClip value)
        {
            WalkCompositionObject(value);

            if (value is CompositionGeometricClip geometricClip)
            {
                GetGeometry(geometricClip.Geometry);
            }
        }

        void WalkShadow(CompositionShadow value)
        {
            WalkCompositionObject(value);

            if (value is DropShadow dropShadow)
            {
                GetBrush(dropShadow.Mask);
            }
        }

        void WalkSurface(ICompositionSurface value)
        {
            switch (value)
            {
                case CompositionVisualSurface visualSurface:
                    WalkCompositionObject(visualSurface);
                    GetVisual(visualSurface.SourceVisual);
                    break;
                case LoadedImageSurfaceFromUri fromUri:
                    GetString(fromUri.Uri.ToString());
                    break;
            }
        }

        void WalkAnimation(CompositionAnimation value)
        {
            WalkCompositionObject(value);
            GetString(value.Target);

            foreach (var parameter in value.ReferenceParameters)
            {
                GetString(parameter.Key);
                GetObjectReference(parameter.Value);
            }

            switch (value)
            {
                case ExpressionAnimation expressionAnimation:
                    GetString(expressionAnimation.Expression.ToText());
                    break;
                case KeyFrameAnimation_ keyFrameAnimation:
                    foreach (var keyFrame in keyFrameAnimation.KeyFrames)
                    {
                        GetEasing(keyFrame.Easing);

                        switch (keyFrame)
                        {
                            case KeyFrameAnimation_.ExpressionKeyFrame expressionKeyFrame:
                                GetString(expressionKeyFrame.Expression.ToText());
                                break;
                            case KeyFrameAnimation<CompositionPath, WinCompData.Expressions.Void>.ValueKeyFrame pathKeyFrame:
                                GetCanvasGeometry((CanvasGeometry)pathKeyFrame.Value.Source);
                                break;
                        }
                    }

                    break;
            }
        }

        void WalkPropertySet(CompositionPropertySet value)
        {
            GetString(value.Comment);

            foreach (var animator in value.Animators)
            {
                GetString(animator.AnimatedProperty);
                GetAnimation(animator.Animation);
                GetController(animator.Controller);
            }

            foreach (var name in value.Names)
            {
                GetString(name.Key);
            }

            if (value.Owner is not null)
            {
                GetObjectReference(value.Owner);
            }
        }

        void WalkController(AnimationController value)
        {
            WalkCompositionObject(value);
            GetString(value.TargetProperty);

            if (value.TargetObject is not null)
            {
                GetObjectReference(value.TargetObject);
            }
        }

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();

            ReferenceComparer()
            {
            }

            bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj)
                => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
