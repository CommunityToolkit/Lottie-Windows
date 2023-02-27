// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;
using CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;
using Expr = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using Wg = CommunityToolkit.WinUI.Lottie.WinCompData.Wg;

namespace CommunityToolkit.WinUI.Lottie.UIData.Tools
{
    /// <summary>
    /// The graph of creatable objects reachable from a <see cref="CompositionObject"/>.
    /// </summary>
    /// <typeparam name="T">The type of the data associated with each node.</typeparam>
#if PUBLIC_UIData
    public
#endif
    sealed class ObjectGraph<T> : Graph
        where T : Graph.Node<T>, new()
    {
        readonly bool _includeVertices;
        readonly Dictionary<Wg.IGeometrySource2D, T> _canvasGeometryReferences = new Dictionary<Wg.IGeometrySource2D, T>();
        readonly Dictionary<CompositionObject, T> _compositionObjectReferences = new Dictionary<CompositionObject, T>();
        readonly Dictionary<CompositionPath, T> _compositionPathReferences = new Dictionary<CompositionPath, T>();
        readonly Dictionary<LoadedImageSurface, T> _loadedImageSurfaceReferences = new Dictionary<LoadedImageSurface, T>();

        int _positionCounter;

        ObjectGraph(CompositionObject root, bool includeVertices)
        {
            _includeVertices = includeVertices;
            Root = Reference(null, root);
        }

        /// <summary>
        /// Returns the graph of nodes reachable from the given <see cref="CompositionObject"/> root.
        /// </summary>
        /// <returns>A <see cref="Graph"/> for the given Composition tree.</returns>
        public static new ObjectGraph<T> FromCompositionObject(CompositionObject root, bool includeVertices)
            => new ObjectGraph<T>(root, includeVertices);

        /// <summary>
        /// The root of the graph.
        /// </summary>
        public T Root { get; }

        public IEnumerable<T> Nodes =>
             _compositionObjectReferences.Values.Concat(_compositionPathReferences.Values).Concat(_canvasGeometryReferences.Values).Concat(_loadedImageSurfaceReferences.Values);

        public IEnumerable<(T Node, CanvasGeometry Object)> CanvasGeometryNodes =>
            _canvasGeometryReferences.Values.Select(n => (n, (CanvasGeometry)n.Object));

        public IEnumerable<(T Node, CompositionObject Object)> CompositionObjectNodes =>
            _compositionObjectReferences.Values.Select(n => (n, (CompositionObject)n.Object));

        public IEnumerable<(T Node, CompositionPath Object)> CompositionPathNodes =>
            _compositionPathReferences.Values.Select(n => (n, (CompositionPath)n.Object));

        public IEnumerable<(T Node, LoadedImageSurface Object)> LoadedImageSurfaceNodes =>
            _loadedImageSurfaceReferences.Values.Select(n => (n, (LoadedImageSurface)n.Object));

        public T this[Wg.IGeometrySource2D obj] => _canvasGeometryReferences[obj];

        public T this[CompositionObject obj] => _compositionObjectReferences[obj];

        public T this[CompositionPath obj] => _compositionPathReferences[obj];

        public T this[LoadedImageSurface obj] => _loadedImageSurfaceReferences[obj];

        [return: NotNullIfNotNull("obj")]
        T? Reference(T? from, CompositionObject? obj)
        {
            if (obj is null)
            {
                return null;
            }

            if (_compositionObjectReferences.TryGetValue(obj, out var node))
            {
                // Object has been seen before. Just add the reference.
                if (_includeVertices && from is not null)
                {
                    AddVertex(from, node);
                }

                return node;
            }

            // Object has not been seen before. Register it, and visit it.
            // Create a node for the object.
            node = new T();
            node.SetObject(obj);

            InitializeNode(node, NodeType.CompositionObject, _positionCounter++);

            // Link the nodes in the graph.
            if (_includeVertices && from is not null)
            {
                AddVertex(from, node);
            }

            _compositionObjectReferences.Add(obj, node);

            switch (obj.Type)
            {
                case CompositionObjectType.AnimationController:
                    VisitAnimationController((AnimationController)obj, node);
                    break;
                case CompositionObjectType.BooleanKeyFrameAnimation:
                    VisitBooleanKeyFrameAnimation((BooleanKeyFrameAnimation)obj, node);
                    break;
                case CompositionObjectType.ColorKeyFrameAnimation:
                    VisitColorKeyFrameAnimation((ColorKeyFrameAnimation)obj, node);
                    break;
                case CompositionObjectType.CompositionColorBrush:
                    VisitCompositionColorBrush((CompositionColorBrush)obj, node);
                    break;
                case CompositionObjectType.CompositionColorGradientStop:
                    VisitCompositionColorGradientStop((CompositionColorGradientStop)obj, node);
                    break;
                case CompositionObjectType.CompositionContainerShape:
                    VisitCompositionContainerShape((CompositionContainerShape)obj, node);
                    break;
                case CompositionObjectType.CompositionEffectBrush:
                    VisitCompositionEffectBrush((CompositionEffectBrush)obj, node);
                    break;
                case CompositionObjectType.CompositionEllipseGeometry:
                    VisitCompositionEllipseGeometry((CompositionEllipseGeometry)obj, node);
                    break;
                case CompositionObjectType.CompositionGeometricClip:
                    VisitCompositionGeometricClip((CompositionGeometricClip)obj, node);
                    break;
                case CompositionObjectType.CompositionLinearGradientBrush:
                    VisitCompositionLinearGradientBrush((CompositionLinearGradientBrush)obj, node);
                    break;
                case CompositionObjectType.CompositionMaskBrush:
                    VisitCompositionMaskBrush((CompositionMaskBrush)obj, node);
                    break;
                case CompositionObjectType.CompositionPathGeometry:
                    VisitCompositionPathGeometry((CompositionPathGeometry)obj, node);
                    break;
                case CompositionObjectType.CompositionPropertySet:
                    VisitCompositionPropertySet((CompositionPropertySet)obj, node);
                    break;
                case CompositionObjectType.CompositionRadialGradientBrush:
                    VisitCompositionRadialGradientBrush((CompositionRadialGradientBrush)obj, node);
                    break;
                case CompositionObjectType.CompositionRectangleGeometry:
                    VisitCompositionRectangleGeometry((CompositionRectangleGeometry)obj, node);
                    break;
                case CompositionObjectType.CompositionRoundedRectangleGeometry:
                    VisitCompositionRoundedRectangleGeometry((CompositionRoundedRectangleGeometry)obj, node);
                    break;
                case CompositionObjectType.CompositionSpriteShape:
                    VisitCompositionSpriteShape((CompositionSpriteShape)obj, node);
                    break;
                case CompositionObjectType.CompositionSurfaceBrush:
                    VisitCompositionSurfaceBrush((CompositionSurfaceBrush)obj, node);
                    break;
                case CompositionObjectType.CompositionViewBox:
                    VisitCompositionViewBox((CompositionViewBox)obj, node);
                    break;
                case CompositionObjectType.CompositionVisualSurface:
                    VisitCompositionVisualSurface((CompositionVisualSurface)obj, node);
                    break;
                case CompositionObjectType.ContainerVisual:
                    VisitContainerVisual((ContainerVisual)obj, node);
                    break;
                case CompositionObjectType.CubicBezierEasingFunction:
                    VisitCubicBezierEasingFunction((CubicBezierEasingFunction)obj, node);
                    break;
                case CompositionObjectType.DropShadow:
                    VisitDropShadow((DropShadow)obj, node);
                    break;
                case CompositionObjectType.ExpressionAnimation:
                    VisitExpressionAnimation((ExpressionAnimation)obj, node);
                    break;
                case CompositionObjectType.InsetClip:
                    VisitInsetClip((InsetClip)obj, node);
                    break;
                case CompositionObjectType.LayerVisual:
                    VisitLayerVisual((LayerVisual)obj, node);
                    break;
                case CompositionObjectType.LinearEasingFunction:
                    VisitLinearEasingFunction((LinearEasingFunction)obj, node);
                    break;
                case CompositionObjectType.PathKeyFrameAnimation:
                    VisitPathKeyFrameAnimation((PathKeyFrameAnimation)obj, node);
                    break;
                case CompositionObjectType.ScalarKeyFrameAnimation:
                    VisitScalarKeyFrameAnimation((ScalarKeyFrameAnimation)obj, node);
                    break;
                case CompositionObjectType.ShapeVisual:
                    VisitShapeVisual((ShapeVisual)obj, node);
                    break;
                case CompositionObjectType.SpriteVisual:
                    VisitSpriteVisual((SpriteVisual)obj, node);
                    break;
                case CompositionObjectType.StepEasingFunction:
                    VisitStepEasingFunction((StepEasingFunction)obj, node);
                    break;
                case CompositionObjectType.Vector2KeyFrameAnimation:
                    VisitVector2KeyFrameAnimation((Vector2KeyFrameAnimation)obj, node);
                    break;
                case CompositionObjectType.Vector3KeyFrameAnimation:
                    VisitVector3KeyFrameAnimation((Vector3KeyFrameAnimation)obj, node);
                    break;
                case CompositionObjectType.Vector4KeyFrameAnimation:
                    VisitVector4KeyFrameAnimation((Vector4KeyFrameAnimation)obj, node);
                    break;
                case CompositionObjectType.CompositionEffectFactory:
                    VisitCompositionEffectFactory((CompositionEffectFactory)obj, node);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            // Reference the animators after referencing all the other contents of the object. This is
            // done to ensure the Position of animators is greater than the position of other
            // references. This ordering is consistent with how CompositionObjects are initialized:
            // 1. Instantiate the object
            // 2. Assign the values for the object's properties and property set properties
            // 3. Start the animations for the object's properties
            // 4. Start the animations for the object's property set properties.
            //
            // Treat CompositionPropertySet specially: Start the animations on an object's property
            // set when starting the animations on the owning object, unless there is no owning object
            // (i.e. it's a property set that was directly created rather than being implicitly
            // created by a CompositionObject's .Properties property).
            if (obj.Type == CompositionObjectType.CompositionPropertySet)
            {
                if (((CompositionPropertySet)obj).Owner is null)
                {
                    // Unowned CompositionPropertySet - can't have animations referenced
                    // from its owner, so reference them here.
                    foreach (var animator in obj.Animators)
                    {
                        Reference(node, animator.Animation);
                        Reference(node, animator.Controller);
                    }
                }
            }
            else
            {
                // Reference the animations for the object's properties
                foreach (var animator in obj.Animators)
                {
                    Reference(node, animator.Animation);
                    Reference(node, animator.Controller);
                }

                var propertySet = obj.Properties;
                var propertySetNode = this[propertySet];
                foreach (var animator in propertySet.Animators)
                {
                    Reference(propertySetNode, animator.Animation);
                    Reference(propertySetNode, animator.Controller);
                }
            }

            return node;
        }

        bool Reference(T from, ICompositionSurface obj)
        {
            switch (obj)
            {
                case CompositionObject compositionObject:
                    Reference(from, compositionObject);
                    break;
                case LoadedImageSurface loadedImageSurface:
                    Reference(from, loadedImageSurface);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return true;
        }

        bool Reference(T from, CompositionPath obj)
        {
            if (_compositionPathReferences.TryGetValue(obj, out var node))
            {
                AddVertex(from, node);
                return true;
            }
            else
            {
                node = new T();
                node.SetObject(obj);

                InitializeNode(node, NodeType.CompositionPath, _positionCounter++);
                AddVertex(from, node);
                _compositionPathReferences.Add(obj, node);
            }

            Reference(node, (CanvasGeometry)obj.Source);
            return true;
        }

        bool Reference(T from, CanvasGeometry obj)
        {
            if (_canvasGeometryReferences.TryGetValue(obj, out var node))
            {
                AddVertex(from, node);
                return true;
            }
            else
            {
                node = new T();
                node.SetObject(obj);

                InitializeNode(node, NodeType.CanvasGeometry, _positionCounter++);
                AddVertex(from, node);
                _canvasGeometryReferences.Add(obj, node);
            }

            return obj.Type switch
            {
                CanvasGeometry.GeometryType.Combination => VisitCombination((CanvasGeometry.Combination)obj, node),
                CanvasGeometry.GeometryType.Ellipse => VisitEllipse((CanvasGeometry.Ellipse)obj, node),
                CanvasGeometry.GeometryType.Group => VisitGroup((CanvasGeometry.Group)obj, node),
                CanvasGeometry.GeometryType.Path => VisitPath((CanvasGeometry.Path)obj, node),
                CanvasGeometry.GeometryType.RoundedRectangle => VisitRoundedRectangle((CanvasGeometry.RoundedRectangle)obj, node),
                CanvasGeometry.GeometryType.TransformedGeometry => VisitTransformedGeometry((CanvasGeometry.TransformedGeometry)obj, node),
                _ => throw new InvalidOperationException(),
            };
        }

        bool Reference(T from, LoadedImageSurface obj)
        {
            if (_loadedImageSurfaceReferences.TryGetValue(obj, out var node))
            {
                AddVertex(from, node);
                return true;
            }
            else
            {
                node = new T();
                node.SetObject(obj);

                InitializeNode(node, NodeType.LoadedImageSurface, _positionCounter++);
                AddVertex(from, node);
                _loadedImageSurfaceReferences.Add(obj, node);
            }

            return VisitLoadedImageSurface(obj, node);
        }

        bool VisitAnimationController(AnimationController obj, T node)
        {
            VisitCompositionObject(obj, node);
            return true;
        }

        bool VisitAnimationControllerList(AnimationController obj, T node)
        {
            VisitCompositionObject(obj, node);
            return true;
        }

        bool VisitCanvasGeometry(CanvasGeometry obj, T node)
        {
            return true;
        }

        bool VisitCombination(CanvasGeometry.Combination obj, T node)
        {
            Reference(node, obj.A);
            Reference(node, obj.B);
            return VisitCanvasGeometry(obj, node);
        }

        bool VisitEllipse(CanvasGeometry.Ellipse obj, T node)
        {
            return VisitCanvasGeometry(obj, node);
        }

        bool VisitGroup(CanvasGeometry.Group obj, T node)
        {
            foreach (var geometry in obj.Geometries)
            {
                Reference(node, geometry);
            }

            return VisitCanvasGeometry(obj, node);
        }

        bool VisitPath(CanvasGeometry.Path obj, T node)
        {
            return VisitCanvasGeometry(obj, node);
        }

        bool VisitRoundedRectangle(CanvasGeometry.RoundedRectangle obj, T node)
        {
            return VisitCanvasGeometry(obj, node);
        }

        bool VisitTransformedGeometry(CanvasGeometry.TransformedGeometry obj, T node)
        {
            Reference(node, obj.SourceGeometry);
            return VisitCanvasGeometry(obj, node);
        }

        bool VisitCompositionObject(CompositionObject obj, T node)
        {
            // Prevent infinite recursion on CompositionPropertySet (its Properties
            // refer back to itself).
            if (obj.Type != CompositionObjectType.CompositionPropertySet)
            {
                Reference(node, obj.Properties);
            }

            // Do not visit the animators here. That is done after visiting the
            // references from the derived class' properties. This is to be consistent
            // the with the order of initialization of objects.
            return true;
        }

        bool VisitCompositionPropertySet(CompositionPropertySet obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitCompositionRectangleGeometry(CompositionRectangleGeometry obj, T node)
        {
            return VisitCompositionGeometry(obj, node);
        }

        bool VisitCompositionRoundedRectangleGeometry(CompositionRoundedRectangleGeometry obj, T node)
        {
            return VisitCompositionGeometry(obj, node);
        }

        bool VisitCompositionShadow(CompositionShadow obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitDropShadow(DropShadow obj, T node)
        {
            VisitCompositionShadow(obj, node);

            if (obj.Mask is not null)
            {
                Reference(node, obj.Mask);
            }

            return true;
        }

        bool VisitExpressionAnimation(ExpressionAnimation obj, T node)
        {
            return VisitCompositionAnimation(obj, node);
        }

        bool VisitCompositionClip(CompositionClip obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitInsetClip(InsetClip obj, T node)
        {
            return VisitCompositionClip(obj, node);
        }

        bool VisitCompositionEasingFunction(CompositionEasingFunction obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitCubicBezierEasingFunction(CubicBezierEasingFunction obj, T node)
        {
            return VisitCompositionEasingFunction(obj, node);
        }

        bool VisitLayerVisual(LayerVisual obj, T node)
        {
            VisitContainerVisual(obj, node);
            Reference(node, obj.Shadow);
            return true;
        }

        bool VisitLinearEasingFunction(LinearEasingFunction obj, T node)
        {
            return VisitCompositionEasingFunction(obj, node);
        }

        bool VisitPathKeyFrameAnimation(PathKeyFrameAnimation obj, T node)
        {
            VisitKeyFrameAnimation(obj, node);
            foreach (var keyFrame in obj.KeyFrames)
            {
                Reference(node, ((KeyFrameAnimation<CompositionPath, Expr.Void>.ValueKeyFrame)keyFrame).Value);
            }

            return true;
        }

        bool VisitCompositionAnimation(CompositionAnimation obj, T node)
        {
            VisitCompositionObject(obj, node);
            foreach (var parameter in obj.ReferenceParameters)
            {
                Reference(node, parameter.Value);
            }

            return true;
        }

        bool VisitKeyFrameAnimation<TKFA, TExpression>(KeyFrameAnimation<TKFA, TExpression> obj, T node)
            where TExpression : Expr.Expression_<TExpression>
        {
            VisitCompositionAnimation(obj, node);
            foreach (var keyFrame in obj.KeyFrames)
            {
                Reference(node, keyFrame.Easing);
            }

            return true;
        }

        bool VisitScalarKeyFrameAnimation(ScalarKeyFrameAnimation obj, T node)
        {
            return VisitKeyFrameAnimation(obj, node);
        }

        bool VisitCompositionShape(CompositionShape obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitCompositionSpriteShape(CompositionSpriteShape obj, T node)
        {
            VisitCompositionShape(obj, node);

            // The .Geometry must be visited first it is a constructor parameter
            // and therefore will be called before any properties are set.
            Reference(node, obj.Geometry);
            Reference(node, obj.FillBrush);
            Reference(node, obj.StrokeBrush);
            return true;
        }

        bool VisitCompositionSurfaceBrush(CompositionSurfaceBrush obj, T node)
        {
            VisitCompositionBrush(obj, node);
            Reference(node, obj.Surface);
            return true;
        }

        bool VisitCompositionViewBox(CompositionViewBox obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitCompositionVisualSurface(CompositionVisualSurface obj, T node)
        {
            VisitCompositionObject(obj, node);
            Reference(node, obj.SourceVisual);
            return true;
        }

        bool VisitCompositionGeometry(CompositionGeometry obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitCompositionEllipseGeometry(CompositionEllipseGeometry obj, T node)
        {
            return VisitCompositionGeometry(obj, node);
        }

        bool VisitCompositionGeometricClip(CompositionGeometricClip obj, T node)
        {
            VisitCompositionClip(obj, node);
            Reference(node, obj.Geometry);
            return true;
        }

        bool VisitCompositionGradientBrush(CompositionGradientBrush obj, T node)
        {
            VisitCompositionBrush(obj, node);
            foreach (var stop in obj.ColorStops)
            {
                Reference(node, stop);
            }

            return true;
        }

        bool VisitCompositionLinearGradientBrush(CompositionLinearGradientBrush obj, T node)
        {
            return VisitCompositionGradientBrush(obj, node);
        }

        bool VisitCompositionPathGeometry(CompositionPathGeometry obj, T node)
        {
            VisitCompositionGeometry(obj, node);
            if (obj.Path is not null)
            {
                Reference(node, obj.Path);
            }

            return true;
        }

        bool VisitCompositionMaskBrush(CompositionMaskBrush obj, T node)
        {
            VisitCompositionBrush(obj, node);

            if (obj.Mask is not null)
            {
                Reference(node, obj.Mask);
            }

            if (obj.Source is not null)
            {
                Reference(node, obj.Source);
            }

            return true;
        }

        bool VisitCompositionRadialGradientBrush(CompositionRadialGradientBrush obj, T node)
        {
            return VisitCompositionGradientBrush(obj, node);
        }

        bool VisitCompositionBrush(CompositionBrush obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitBooleanKeyFrameAnimation(BooleanKeyFrameAnimation obj, T node)
        {
            return VisitKeyFrameAnimation(obj, node);
        }

        bool VisitColorKeyFrameAnimation(ColorKeyFrameAnimation obj, T node)
        {
            return VisitKeyFrameAnimation(obj, node);
        }

        bool VisitCompositionColorBrush(CompositionColorBrush obj, T node)
        {
            return VisitCompositionBrush(obj, node);
        }

        bool VisitCompositionColorGradientStop(CompositionColorGradientStop obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitCompositionContainerShape(CompositionContainerShape obj, T node)
        {
            VisitCompositionShape(obj, node);
            foreach (var shape in obj.Shapes)
            {
                Reference(node, shape);
            }

            return true;
        }

        bool VisitCompositionEffectFactory(CompositionEffectFactory obj, T node)
        {
            return VisitCompositionObject(obj, node);
        }

        bool VisitCompositionEffectBrush(CompositionEffectBrush obj, T node)
        {
            VisitCompositionBrush(obj, node);

            var effectFactory = obj.GetEffectFactory();

            Reference(node, effectFactory);

            foreach (var source in effectFactory.Effect.Sources)
            {
                Reference(node, obj.GetSourceParameter(source.Name));
            }

            return true;
        }

        bool VisitVisual(Visual obj, T node)
        {
            VisitCompositionObject(obj, node);
            Reference(node, obj.Clip);
            return true;
        }

        bool VisitShapeVisual(ShapeVisual obj, T node)
        {
            VisitVisual(obj, node);
            Reference(node, obj.ViewBox);
            foreach (var shape in obj.Shapes)
            {
                Reference(node, shape);
            }

            return true;
        }

        bool VisitSpriteVisual(SpriteVisual obj, T node)
        {
            VisitVisual(obj, node);
            Reference(node, obj.Brush);
            return true;
        }

        bool VisitStepEasingFunction(StepEasingFunction obj, T node)
        {
            return VisitCompositionEasingFunction(obj, node);
        }

        bool VisitVector2KeyFrameAnimation(Vector2KeyFrameAnimation obj, T node)
        {
            return VisitKeyFrameAnimation(obj, node);
        }

        bool VisitVector3KeyFrameAnimation(Vector3KeyFrameAnimation obj, T node)
        {
            return VisitKeyFrameAnimation(obj, node);
        }

        bool VisitVector4KeyFrameAnimation(Vector4KeyFrameAnimation obj, T node)
        {
            return VisitKeyFrameAnimation(obj, node);
        }

        bool VisitContainerVisual(ContainerVisual obj, T node)
        {
            VisitVisual(obj, node);
            foreach (var child in obj.Children)
            {
                Reference(node, child);
            }

            return true;
        }

        bool VisitLoadedImageSurface(LoadedImageSurface obj, T node)
        {
            return true;
        }
    }
}
