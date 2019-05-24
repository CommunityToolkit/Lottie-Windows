// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Enable workaround for RS5 where rotated rectangles were not drawn correctly.
#define WorkAroundRectangleGeometryHalfDrawn

// Use the simple algorithm for combining trim paths. We're not sure of the correct semantics
// for multiple trim paths, so it's possible this is actually the most correct.
#define SimpleTrimPathCombining
#define SpatialBeziers

// The AnimationController.Progress value is used one frame later than expressions,
// so to keep everything in sync if one animation is using a controller tied
// to the uber Progress property, then no animation can be tied to the Progress
// property without going through a controller. Enable this to prevent flashes.
#define ControllersSynchronizationWorkaround

//#define LinearEasingOnSpatialBeziers
// Use Win2D to create paths from geometry combines when merging shape layers.
//#define PreCombineGeometries
#if DEBUG
// For diagnosing issues, give nothing a clip.
//#define NoClipping
// For diagnosing issues, give nothing scale.
//#define NoScaling
// For diagnosing issues, do not control visibility.
//#define NoInvisibility
// For diagnosing issues, do not inherit transforms.
//#define NoTransformInheritance
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp.ExpressionFactory;

using CubicBezierFunction2 = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.CubicBezierFunction2;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.Expression;
using ExpressionType = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.ExpressionType;
using Sn = System.Numerics;
using TypeConstraint = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.TypeConstraint;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates a <see cref="LottieData.LottieComposition"/> to an equivalent <see cref="Visual"/>.
    /// </summary>
    /// <remarks>See https://helpx.adobe.com/pdf/after_effects_reference.pdf"/> for the
    /// After Effects semantics.</remarks>
#if PUBLIC
    public
#endif
    sealed class LottieToWinCompTranslator : IDisposable
    {
        // Very small animation progress increment used to place keyframes as close as possible
        // to each other.
        const float KeyFrameProgressEpsilon = 0.0000001F;

        // The name used to bind to the property set that contains the Progress property.
        const string RootName = "_";
        readonly LottieComposition _lc;
        readonly TranslationIssues _issues;
        readonly bool _addDescriptions;
        readonly Compositor _c;
        readonly ContainerVisual _rootVisual;
        readonly Dictionary<ScaleAndOffset, ExpressionAnimation> _progressBindingAnimations = new Dictionary<ScaleAndOffset, ExpressionAnimation>();
        readonly Optimizer _lottieDataOptimizer = new Optimizer();

        // Holds CubicBezierEasingFunctions for reuse when they have the same parameters.
        readonly Dictionary<CubicBezierEasing, CubicBezierEasingFunction> _cubicBezierEasingFunctions = new Dictionary<CubicBezierEasing, CubicBezierEasingFunction>();

        // Holds ColorBrushes that are not animated and can therefore be reused.
        readonly Dictionary<Color, CompositionColorBrush> _nonAnimatedColorBrushes = new Dictionary<Color, CompositionColorBrush>();

        // Paths are shareable.
        readonly Dictionary<(Sequence<BezierSegment>, SolidColorFill.PathFillType, bool), CompositionPath> _compositionPaths = new Dictionary<(Sequence<BezierSegment>, SolidColorFill.PathFillType, bool), CompositionPath>();

        // Holds a LinearEasingFunction that can be reused in multiple animations.
        LinearEasingFunction _linearEasingFunction;

        // Holds a StepEasingFunction that can be reused in multiple animations.
        StepEasingFunction _holdStepEasingFunction;

        // Holds a StepEasingFunction that can be reused in multiple animations.
        StepEasingFunction _jumpStepEasingFunction;

        /// <summary>
        /// Gets the name of the property on the resulting <see cref="Visual"/> that controls the progress
        /// of the animation. Setting this property (directly or with an animation)
        /// between 0 and 1 controls the position of the animation.
        /// </summary>
        public static string ProgressPropertyName => "Progress";

        LottieToWinCompTranslator(
            LottieData.LottieComposition lottieComposition,
            Compositor compositor,
            bool strictTranslation,
            bool addDescriptions)
        {
            _lc = lottieComposition;
            _c = compositor;
            _issues = new TranslationIssues(strictTranslation);
            _addDescriptions = addDescriptions;

            // Create the root.
            _rootVisual = CreateContainerVisual();
            if (_addDescriptions)
            {
                Describe(_rootVisual, "The root of the composition.", string.Empty);
            }

            // Add the master progress property to the visual.
            _rootVisual.Properties.InsertScalar(ProgressPropertyName, 0);
        }

        /// <summary>
        /// Attempts to translates the given <see cref="LottieData.LottieComposition"/>.
        /// </summary>
        /// <param name="lottieComposition">The <see cref="LottieData.LottieComposition"/> to translate.</param>
        /// <param name="strictTranslation">If true, throw an exception if translation issues are found.</param>
        /// <param name="visual">The <see cref="Visual"/> that contains the translated Lottie.</param>
        /// <param name="translationIssues">A list of issues that were encountered during the translation.</param>
        /// <returns><c>true</c> if the <see cref="LottieComposition"/> was translated.</returns>
        public static bool TryTranslateLottieComposition(
            LottieComposition lottieComposition,
            bool strictTranslation,
            out Visual visual,
            out (string Code, string Description)[] translationIssues) =>
            TryTranslateLottieComposition(
                lottieComposition,
                strictTranslation,
                true,   // add descriptions for codegen comments
                out visual,
                out translationIssues);

        /// <summary>
        /// Attempts to translates the given <see cref="LottieData.LottieComposition"/>.
        /// </summary>
        /// <param name="lottieComposition">The <see cref="LottieComposition"/> to translate.</param>
        /// <param name="strictTranslation">If true, throw an exception if translation issues are found.</param>
        /// <param name="addCodegenDescriptions">Add descriptions to objects for comments on generated code.</param>
        /// <param name="visual">The <see cref="Visual"/> that contains the translated Lottie.</param>
        /// <param name="translationIssues">A list of issues that were encountered during the translation.</param>
        /// <returns><c>true</c> if the <see cref="LottieComposition"/> was translated.</returns>
        public static bool TryTranslateLottieComposition(
            LottieComposition lottieComposition,
            bool strictTranslation,
            bool addCodegenDescriptions,
            out Visual visual,
            out (string Code, string Description)[] translationIssues)
        {
            // Set up the translator.
            using (var translator = new LottieToWinCompTranslator(
                lottieComposition,
                new Compositor(),
                strictTranslation,
                addCodegenDescriptions))
            {
                // Translate the Lottie content to a CompositionShapeVisual tree.
                translator.Translate();

                // Set the out parameters.
                visual = translator._rootVisual;
                translationIssues = translator._issues.GetIssues();
            }

            return true;
        }

        void Translate()
        {
            var context = new TranslationContext.Root(_lc);
            AddTranslatedLayersToContainerVisual(_rootVisual, context, compositionDescription: "Root");
            if (_lc.Is3d)
            {
                _issues.ThreeDIsNotSupported();
            }
        }

        void AddTranslatedLayersToContainerVisual(
            ContainerVisual container,
            TranslationContext context,
            string compositionDescription)
        {
            var translatedLayers =
                (from layer in context.Layers.GetLayersBottomToTop()
                 let translatedLayer = TranslateLayer(context, layer)
                 where translatedLayer != null
                 select (translatedLayer: translatedLayer, layer: layer)).ToArray();

            // Set descriptions on each translate layer so that it's clear where the layer starts.
            if (_addDescriptions)
            {
                foreach (var (translatedLayer, layer) in translatedLayers)
                {
                    // Add a description if not added already.
                    if (translatedLayer.ShortDescription == null)
                    {
                        Describe(translatedLayer, $"Layer ({layer.Type}): {layer.Name}");
                    }
                }
            }

            // Layers are translated into either a Visual tree or a Shape tree. Convert the list of Visual and
            // Shape roots to a list of Visual roots by wrapping the Shape trees in ShapeVisuals.
            var translatedAsVisuals = VisualsAndShapesToVisuals(context, translatedLayers.Select(a => a.translatedLayer));

            var containerChildren = container.Children;
            foreach (var translatedVisual in translatedAsVisuals)
            {
                containerChildren.Add(translatedVisual);
            }
        }

        // Takes a list of Visuals and Shapes and returns a list of Visuals.
        IEnumerable<Visual> VisualsAndShapesToVisuals(TranslationContext context, IEnumerable<CompositionObject> items)
        {
            ShapeVisual shapeVisual = null;

            foreach (var item in items)
            {
                switch (item.Type)
                {
                    case CompositionObjectType.CompositionContainerShape:
                    case CompositionObjectType.CompositionSpriteShape:
                        if (shapeVisual == null)
                        {
                            shapeVisual = _c.CreateShapeVisual();

                            // ShapeVisual clips to its size
#if NoClipping
                            shapeVisual.Size = Vector2(float.MaxValue);
#else
                            shapeVisual.Size = Vector2(context.Width, context.Height);
#endif
                        }

                        shapeVisual.Shapes.Add((CompositionShape)item);
                        break;
                    case CompositionObjectType.ContainerVisual:
                    case CompositionObjectType.ShapeVisual:
                    case CompositionObjectType.SpriteVisual:
                        if (shapeVisual != null)
                        {
                            yield return shapeVisual;
                            shapeVisual = null;
                        }

                        yield return (Visual)item;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            if (shapeVisual != null)
            {
                yield return shapeVisual;
            }
        }

        void TranslateAndApplyMask(TranslationContext context, Visual visualForMask)
        {
#if !NoClipping
            var masks = context.Layer.Masks;
            if (masks.Any())
            {
                var mask = masks[0];

                if (mask.Inverted)
                {
                    _issues.MaskWithInvertIsNotSupported();
                }

                if (mask.Opacity.IsAnimated ||
                    mask.Opacity.InitialValue != 100)
                {
                    _issues.MaskWithAlphaIsNotSupported();
                }

                if (mask.Mode != Mask.MaskMode.Additive)
                {
                    _issues.MaskWithUnsupportedMode(mask.Mode.ToString());
                }

                // Translation currently does not support having multiple paths for masks.
                // If possible users should combine masks when exporting to json.
                if (masks.Length > 1)
                {
                    _issues.MultipleShapeMasksIsNotSupported();
                }

                // Add a mask as a clip path if it can support the mask properties
                if (!mask.Inverted &&
                    mask.Opacity.InitialValue == 100 &&
                    !mask.Opacity.IsAnimated &&
                    mask.Mode == Mask.MaskMode.Additive &&
                    masks.Length == 1)
                {
                    var geometry = context.TrimAnimatable(_lottieDataOptimizer.GetOptimized(mask.Points));

                    var compositionPathGeometry = CreatePathGeometry();
                    compositionPathGeometry.Path = CompositionPathFromPathGeometry(
                        geometry.InitialValue,
                        SolidColorFill.PathFillType.EvenOdd,
                        optimizeLines: true);

                    var compositionGeometricClip = CreateCompositionGeometricClip();
                    compositionGeometricClip.Geometry = compositionPathGeometry;

                    if (_addDescriptions)
                    {
                        Describe(compositionGeometricClip, mask.Name);
                        Describe(compositionPathGeometry, $"{mask.Name}.PathGeometry");
                    }

                    if (geometry.IsAnimated)
                    {
                        ApplyPathKeyFrameAnimation(context, geometry, SolidColorFill.PathFillType.EvenOdd, compositionPathGeometry, "Path", "Path", null);
                    }

                    visualForMask.Clip = compositionGeometricClip;
                }
            }
#endif
        }

        ContainerVisual ApplyMaskToTreeWithShapes(
            TranslationContext context,
            CompositionContainerShape containerShape,
            ContainerVisual contentContainerVisual,
            ContainerVisual rootContainerVisual)
        {
            // Add a mask to a shape tree by inserting a ShapeVisual as the CompositionContainerShape parent and then
            // adding the mask to it. A new container shape at the root of this tree is returned.
            //
            //
            //     +------------------------+
            //     | ContainerVisual parent |
            //     +------------------------+
            //            ^
            //            |
            //     +-----------------------+
            //     |  rootContainerVisual  |
            //     +-----------------------+
            //            ^
            //            |
            //     +------------------------+
            //     | contentContainerVisual | -- Mask is added to this visual
            //     +------------------------+
            //            ^
            //            |
            //     +-------------+
            //     | shapeVisual | -- The shape visual is necessary to add the shape tree to be clipped
            //     +-------------+
            //            ^
            //            |
            //     +----------------+
            //     | containerShape |
            //     +----------------+
            var shapeVisual = CreateShapeVisual();
            shapeVisual.Shapes.Add(containerShape);
            shapeVisual.Size = Vector2(context.Width, context.Height);

            contentContainerVisual.Children.Add(shapeVisual);

            // Apply the mask to the content node
            TranslateAndApplyMask(context, contentContainerVisual);

            // Add a parent container visual that has no transforms so that the tree
            // structure matches what is expected. Otherwise we assert when trying to
            // add a description when when one already exists.
            var parent = CreateContainerVisual();
            parent.Children.Add(rootContainerVisual);
            return parent;
        }

        // Translates a Lottie layer into null a Visual or a Shape.
        // Note that ShapeVisual clips to its size.
        CompositionObject TranslateLayer(TranslationContext parentContext, Layer layer)
        {
            if (layer.Is3d)
            {
                _issues.ThreeDLayerIsNotSupported();
            }

            if (layer.BlendMode != BlendMode.Normal)
            {
                _issues.BlendModeNotNormal(layer.Name, layer.BlendMode.ToString());
            }

            if (layer.TimeStretch != 1)
            {
                _issues.TimeStretchIsNotSupported();
            }

            if (layer.IsHidden)
            {
                return null;
            }

            switch (layer.Type)
            {
                case Layer.LayerType.Image:
                    return TranslateImageLayer(parentContext.SubContext((ImageLayer)layer));
                case Layer.LayerType.Null:
                    // Null layers only exist to hold transforms when declared as parents of other layers.
                    return null;
                case Layer.LayerType.PreComp:
                    return TranslatePreCompLayerToVisual(parentContext.SubContext((PreCompLayer)layer));
                case Layer.LayerType.Shape:
                    return TranslateShapeLayer(parentContext.SubContext((ShapeLayer)layer));
                case Layer.LayerType.Solid:
                    return TranslateSolidLayer(parentContext.SubContext((SolidLayer)layer));
                case Layer.LayerType.Text:
                    return TranslateTextLayer(parentContext.SubContext((TextLayer)layer));
                default:
                    throw new InvalidOperationException();
            }
        }

        // Returns a chain of ContainerShape that define the transforms for a layer.
        // The top of the chain is the rootTransform, the bottom is the contentsNode.
        bool TryCreateContainerShapeTransformChain(
            TranslationContext context,
            out CompositionContainerShape rootNode,
            out CompositionContainerShape contentsNode)
        {
            // Create containers for the contents in the layer.
            // The rootNode is the root for the layer. It may be the same object
            // as the contentsNode if there are no inherited transforms and no visibility animation.
            //
            //     +---------------+
            //     |      ...      |
            //     +---------------+
            //            ^
            //            |
            //     +-----------------+
            //     |  visiblityNode  |-- Optional visiblity node (only used if the visiblity is animated)
            //     +-----------------+
            //            ^
            //            |
            //     +-------------------+
            //     | rootTransformNode |--Transform (values are inherited from root ancestor of the transform tree)
            //     +-------------------+
            //            ^
            //            |
            //     + - - - - - - - - - - - - +
            //     | other transforms nodes  |--Transform (values inherited from the transform tree)
            //     + - - - - - - - - - - - - +
            //            ^
            //            |
            //     +-------------------+
            //     | leafTransformNode |--Transform defined on the layer
            //     +-------------------+
            //        ^        ^
            //        |        |
            // +---------+ +---------+
            // | content | | content | ...
            // +---------+ +---------+
            //

            // Convert the layer's in point and out point into absolute progress (0..1) values.
            var inProgress = GetInPointProgress(context);
            var outProgress = GetOutPointProgress(context);
            if (inProgress > 1 || outProgress <= 0)
            {
                // The layer is never visible. Don't create anything.
                rootNode = null;
                contentsNode = null;
                return false;
            }

            // Create the transforms chain.
            TranslateTransformOnContainerShapeForLayer(context, context.Layer, out var transformsRoot, out contentsNode);

            // Implement the Visibility for the layer. Only needed if the layer becomes visible after
            // the LottieComposition's in point, or it becomes invisible before the LottieComposition's out point.
            if (inProgress > 0 || outProgress < 1)
            {
                // Create a node to control visibility.
                var visibilityNode = CreateContainerShape();
                visibilityNode.Shapes.Add(transformsRoot);
                rootNode = visibilityNode;

#if !NoInvisibility
#if ControllersSynchronizationWorkaround
                // Animate between Matrix3x2(0,0,0,0,0,0) and Matrix3x2(1,0,0,1,0,0) (i.e. between 0 and identity).
                var visibilityAnimation = CreateScalarKeyFrameAnimation();
                if (inProgress > 0)
                {
                    // Set initial value to be non-visible (default is visible).
                    visibilityNode.TransformMatrix = default(Sn.Matrix3x2);
                    visibilityAnimation.InsertKeyFrame(inProgress, 1, CreateHoldThenStepEasingFunction());
                }

                if (outProgress < 1)
                {
                    visibilityAnimation.InsertKeyFrame(outProgress, 0, CreateHoldThenStepEasingFunction());
                }

                visibilityAnimation.Duration = _lc.Duration;
                StartKeyframeAnimation(visibilityNode, "TransformMatrix._11", visibilityAnimation);

                // M11 and M22 need to have the same value. Either tie them together with an expression, or
                // use the same keyframe animation for both. Probably cheaper to use an expression.
                var m11expression = CreateExpressionAnimation(ExpressionFactory.TransformMatrixM11Expression);
                m11expression.SetReferenceParameter("my", visibilityNode);
                StartExpressionAnimation(visibilityNode, "TransformMatrix._22", m11expression);

                // Alternative is to use the same key frame animation on M22.
                //StartKeyframeAnimation(visibilityNode, "TransformMatrix._22", visibilityAnimation);
#else
                var visibilityExpression =
                    ExpressionFactory.CreateProgressExpression(
                        ExpressionFactory.RootProgress,
                        new ExpressionFactory.Segment(double.MinValue, inProgress, Expr.Matrix3x2Zero),
                        new ExpressionFactory.Segment(inProgress, outProgress, Expr.Matrix3x2Identity),
                        new ExpressionFactory.Segment(outProgress, double.MaxValue, Expr.Matrix3x2Zero)
                        );

                var visibilityAnimation = CreateExpressionAnimation(visibilityExpression);
                visibilityAnimation.SetReferenceParameter(RootName, _rootVisual);
                StartExpressionAnimation(visibilityNode, "TransformMatrix", visibilityAnimation);
#endif // ControllersSynchronizationWorkaround
#endif // !NoInvisibility
            }
            else
            {
                rootNode = transformsRoot;
            }

            return true;
        }

        // Returns a chain of ContainerVisual that define the transforms for a layer.
        // The top of the chain is the rootTransform, the bottom is the leafTransform.
        // Returns false if the layer is never visible.
        bool TryCreateContainerVisualTransformChain(
            TranslationContext context,
            out ContainerVisual rootNode,
            out ContainerVisual contentsNode)
        {
            // Create containers for the contents in the layer.
            // The rootTransformNode is the root for the layer. It may be the same object
            // as the contentsNode if there are no inherited transforms.
            //
            //     +---------------+
            //     |      ...      |
            //     +---------------+
            //            ^
            //            |
            //     +-----------------+
            //     |  visiblityNode  |-- Optional visiblity node (only used if the visiblity is animated)
            //     +-----------------+
            //            ^
            //            |
            //     +-------------------+
            //     | rootTransformNode |--Transform (values are inherited from root ancestor of the transform tree)
            //     +-------------------+
            //            ^
            //            |
            //     + - - - - - - - - - - - - +
            //     | other transforms nodes  |--Transform (values inherited from the transform tree)
            //     + - - - - - - - - - - - - +
            //            ^
            //            |
            //     +---------------+
            //     | contentsNode  |--Transform defined on the layer
            //     +---------------+
            //        ^        ^
            //        |        |
            // +---------+ +---------+
            // | content | | content | ...
            // +---------+ +---------+
            //

            // Get the opacity of the layer.
            var layerOpacityPercent = context.Layer.Transform.OpacityPercent;

            // Convert the layer's in point and out point into absolute progress (0..1) values.
            var inProgress = GetInPointProgress(context);
            var outProgress = GetOutPointProgress(context);

            if (inProgress > 1 || outProgress <= 0 || layerOpacityPercent.AlwaysEquals(0))
            {
                // The layer is never visible. Don't create anything.
                rootNode = null;
                contentsNode = null;
                return false;
            }

            // Create the transforms chain.
            TranslateTransformOnContainerVisualForLayer(context, context.Layer, out rootNode, out contentsNode);

            // Implement opacity for the layer.
            if (layerOpacityPercent.IsAnimated || layerOpacityPercent.InitialValue < 100)
            {
                // Insert a new node to control opacity at the top of the chain.
                var opacityNode = CreateContainerVisual();

                if (_addDescriptions)
                {
                    Describe(opacityNode, $"Opacity for layer: {context.Layer.Name}");
                }

                opacityNode.Children.Add(rootNode);
                rootNode = opacityNode;

                if (layerOpacityPercent.IsAnimated)
                {
                    ApplyPercentKeyFrameAnimation(context, context.TrimAnimatable(layerOpacityPercent), opacityNode, "Opacity", "Layer opacity animation");
                }
                else
                {
                    opacityNode.Opacity = PercentF(layerOpacityPercent.InitialValue);
                }
            }

            // Implement the Visibility for the layer. Only needed if the layer becomes visible after
            // the LottieComposition's in point, or it becomes invisible before the LottieComposition's out point.
            if (inProgress > 0 || outProgress < 1)
            {
                // Insert a new node to control visibility at the top of the chain.
                var visibilityNode = CreateContainerVisual();

                if (_addDescriptions)
                {
                    Describe(visibilityNode, $"Visibility for layer: {context.Layer.Name}");
                }

                visibilityNode.Children.Add(rootNode);
                rootNode = visibilityNode;

#if !NoInvisibility
#if ControllersSynchronizationWorkaround
                // Animate opacity between 0 and 1.
                var visibilityAnimation = CreateScalarKeyFrameAnimation();
                if (inProgress > 0)
                {
                    // Set initial value to be non-visible.
                    visibilityNode.Opacity = 0;
                    visibilityAnimation.InsertKeyFrame(inProgress, 1, CreateHoldThenStepEasingFunction());
                }

                if (outProgress < 1)
                {
                    visibilityAnimation.InsertKeyFrame(outProgress, 0, CreateHoldThenStepEasingFunction());
                }

                visibilityAnimation.Duration = _lc.Duration;
                StartKeyframeAnimation(visibilityNode, "Opacity", visibilityAnimation);
#else
                var invisible = Expr.Scalar(0);
                var visible = Expr.Scalar(1);

                var visibilityExpression =
                    ExpressionFactory.CreateProgressExpression(
                        ExpressionFactory.RootProgress,
                        new ExpressionFactory.Segment(double.MinValue, inProgress, invisible),
                        new ExpressionFactory.Segment(inProgress, outProgress, visible),
                        new ExpressionFactory.Segment(outProgress, double.MaxValue, invisible)
                        );

                var visibilityAnimation = CreateExpressionAnimation(visibilityExpression);
                visibilityAnimation.SetReferenceParameter(RootName, _rootVisual);
                StartExpressionAnimation(visibilityNode, "Opacity", visibilityAnimation);
#endif // ControllersSynchronizationWorkaround
#endif // !NoInvisibility
            }

            return true;
        }

        Visual TranslateImageLayer(TranslationContext.For<ImageLayer> context)
        {
            if (!TryCreateContainerVisualTransformChain(context, out var containerVisualRootNode, out var containerVisualContentNode))
            {
                // The layer is never visible.
                return null;
            }

            var imageAsset = GetImageAsset(context, context.Layer.RefId);
            if (imageAsset == null)
            {
                return null;
            }

            var content = CreateSpriteVisual();
            containerVisualContentNode.Children.Add(content);
            content.Size = new Sn.Vector2((float)imageAsset.Width, (float)imageAsset.Height);

            LoadedImageSurface surface;
            var imageAssetWidth = imageAsset.Width;
            var imageAssetHeight = imageAsset.Height;

            switch (imageAsset.ImageType)
            {
                case ImageAsset.ImageAssetType.Embedded:
                    var embeddedImageAsset = (EmbeddedImageAsset)imageAsset;
                    surface = LoadedImageSurface.StartLoadFromStream(embeddedImageAsset.Bytes);
                    break;
                case ImageAsset.ImageAssetType.External:
                    var externalImageAsset = (ExternalImageAsset)imageAsset;
                    surface = LoadedImageSurface.StartLoadFromUri(new Uri($"file://localhost/{externalImageAsset.Path}{externalImageAsset.FileName}"));
                    _issues.ImageFileRequired($"{externalImageAsset.Path}{externalImageAsset.FileName}");
                    break;
                default:
                    throw new InvalidOperationException();
            }

            var imageBrush = CreateSurfaceBrush(surface);
            content.Brush = imageBrush;

            if (_addDescriptions)
            {
                Describe(surface, $"{context.Layer.Name}, {imageAssetWidth}x{imageAssetHeight}");
            }

            return containerVisualRootNode;
        }

        Visual TranslatePreCompLayerToVisual(TranslationContext.For<PreCompLayer> context)
        {
            // Create the transform chain.
            if (!TryCreateContainerVisualTransformChain(context, out var rootNode, out var contentsNode))
            {
                // The layer is never visible.
                return null;
            }

            var result = CreateContainerVisual();

            result.Children.Add(rootNode);

#if !NoClipping
            // Apply the mask to the content node
            TranslateAndApplyMask(context, contentsNode);

            // PreComps must clip to their size.
            // Create another ContainerVisual to apply clipping to.
            var clippingNode = CreateContainerVisual();
            contentsNode.Children.Add(clippingNode);
            contentsNode = clippingNode;
            contentsNode.Clip = CreateInsetClip();
            contentsNode.Size = Vector2(context.Layer.Width, context.Layer.Height);
#endif

            // TODO - the animations produced inside a PreComp need to be time-mapped.
            var referencedLayers = GetLayerCollectionByAssetId(context, context.Layer.RefId);
            if (referencedLayers == null)
            {
                return null;
            }

            // Push the reference layers onto the stack. These will be used to look up parent transforms for layers under this precomp.
            AddTranslatedLayersToContainerVisual(
                contentsNode,
                context.PreCompSubContext(referencedLayers),
                $"{context.Layer.Name}:{context.Layer.RefId}");

            return result;
        }

        LayerCollection GetLayerCollectionByAssetId(TranslationContext context, string assetId)
            => ((LayerCollectionAsset)GetAssetById(context, assetId, Asset.AssetType.LayerCollection))?.Layers;

        ImageAsset GetImageAsset(TranslationContext context, string assetId)
            => (ImageAsset)GetAssetById(context, assetId, Asset.AssetType.Image);

        Asset GetAssetById(TranslationContext context, string assetId, Asset.AssetType expectedAssetType)
        {
            var referencedAsset = _lc.Assets.GetAssetById(assetId);
            if (referencedAsset == null)
            {
                _issues.ReferencedAssetDoesNotExist(assetId);
            }
            else if (referencedAsset.Type != expectedAssetType)
            {
                _issues.InvalidAssetReferenceFromLayer(context.Layer.Type.ToString(), assetId, referencedAsset.Type.ToString(), expectedAssetType.ToString());
                referencedAsset = null;
            }

            return referencedAsset;
        }

        sealed class ShapeContentContext
        {
            static readonly Animatable<double> s_100Percent = new Animatable<double>(100, null);

            readonly LottieToWinCompTranslator _owner;

            internal SolidColorStroke Stroke { get; private set; }

            internal SolidColorFill Fill { get; private set; }

            internal TrimPath TrimPath { get; private set; }

            internal RoundedCorner RoundedCorner { get; private set; }

            internal Transform Transform { get; private set; }

            // Opacity is not part of the Lottie context for shapes. But because WinComp
            // doesn't support opacity on shapes, the opacity is inherited from
            // the Transform and passed through to the brushes here.
            internal Animatable<double> OpacityPercent { get; private set; }

            internal ShapeContentContext(LottieToWinCompTranslator owner)
            {
                _owner = owner;

                // Assume opacity is 100% unless otherwise set.
                OpacityPercent = s_100Percent;
            }

            internal void UpdateFromStack(Stack<ShapeLayerContent> stack)
            {
                while (stack.Count > 0)
                {
                    var popped = stack.Peek();
                    switch (popped.ContentType)
                    {
                        case ShapeContentType.LinearGradientFill:
                            _owner._issues.GradientFillIsNotSupported();
                            {
                                // We don't yet support gradient fill, but we can at least
                                // draw something. Use data from the first gradient stop as the fill.
                                var lgf = (LinearGradientFill)popped;
                                var lgfArgs = default(ShapeLayerContent.ShapeLayerContentArgs);
                                lgfArgs.BlendMode = lgf.BlendMode;
                                Fill = new SolidColorFill(
                                    in lgfArgs,
                                    SolidColorFill.PathFillType.EvenOdd,
                                    new Animatable<Color>(GradientStop.GetFirstColor(lgf.GradientStops.InitialValue.Items), null),
                                    lgf.OpacityPercent);
                            }

                            break;
                        case ShapeContentType.RadialGradientFill:
                            _owner._issues.GradientFillIsNotSupported();
                            {
                                // We don't yet support gradient fill, but we can at least
                                // draw something. Use data from the first gradient stop as the fill.
                                var rgf = (RadialGradientFill)popped;
                                var rgfArgs = default(ShapeLayerContent.ShapeLayerContentArgs);
                                rgfArgs.BlendMode = rgf.BlendMode;
                                Fill = new SolidColorFill(
                                    in rgfArgs,
                                    SolidColorFill.PathFillType.EvenOdd,
                                    new Animatable<Color>(GradientStop.GetFirstColor(rgf.GradientStops.InitialValue.Items), null),
                                    rgf.OpacityPercent);
                            }

                            break;

                        case ShapeContentType.LinearGradientStroke:
                        case ShapeContentType.RadialGradientStroke:
                            _owner._issues.GradientStrokeIsNotSupported();
                            break;

                        case ShapeContentType.SolidColorFill:
                            Fill = ComposeSolidColorFill(Fill, (SolidColorFill)popped);
                            break;

                        case ShapeContentType.SolidColorStroke:
                            Stroke = ComposeStrokes(Stroke, (SolidColorStroke)popped);
                            break;

                        case ShapeContentType.RoundedCorner:
                            RoundedCorner = ComposeRoundedCorners(RoundedCorner, (RoundedCorner)popped);
                            break;

                        case ShapeContentType.TrimPath:
                            TrimPath = ComposeTrimPaths(TrimPath, (TrimPath)popped);
                            break;

                        default: return;
                    }

                    stack.Pop();
                }
            }

            internal ShapeContentContext Clone()
            {
                return new ShapeContentContext(_owner)
                {
                    Fill = Fill,
                    Stroke = Stroke,
                    TrimPath = TrimPath,
                    RoundedCorner = RoundedCorner,
                    OpacityPercent = OpacityPercent,
                    Transform = Transform,
                };
            }

            internal void UpdateOpacityFromTransform(Transform transform)
            {
                if (transform == null)
                {
                    return;
                }

                OpacityPercent = ComposeOpacityPercents(OpacityPercent, transform.OpacityPercent);
            }

            // Only used when translating geometries. Layers use an extra Shape or Visual to
            // apply the transform, but geometries need to take the transform into account when
            // they're created.
            internal void SetTransform(Transform transform)
            {
                Transform = transform;
            }

            Animatable<double> ComposeOpacityPercents(Animatable<double> a, Animatable<double> b)
            {
                if (a == null || ReferenceEquals(a, s_100Percent))
                {
                    return b;
                }

                if (b == null || ReferenceEquals(b, s_100Percent))
                {
                    return a;
                }

                if (!a.IsAnimated && !b.IsAnimated)
                {
                    // Neither is animated. Just use the initial values.
                    return new Animatable<double>(a.InitialValue * (b.InitialValue / 100.0), null);
                }

                if (a.IsAnimated && b.IsAnimated)
                {
                    // Both are animated.
                    if (a.KeyFrames[0].Frame >= b.KeyFrames[b.KeyFrames.Length - 1].Frame ||
                        b.KeyFrames[0].Frame >= a.KeyFrames[a.KeyFrames.Length - 1].Frame)
                    {
                        // The animations are non-overlapping.
                        if (a.KeyFrames[0].Frame >= b.KeyFrames[b.KeyFrames.Length - 1].Frame)
                        {
                            return ComposeNonOverlappingAnimatedPercents(b, a);
                        }
                        else
                        {
                            return ComposeNonOverlappingAnimatedPercents(a, b);
                        }
                    }
                    else
                    {
                        // The animations overlap. We currently don't support this case.
                        _owner._issues.AnimationMultiplicationIsNotSupported();
                        return a;
                    }
                }

                // Only one is animated.
                if (a.IsAnimated)
                {
                    if (b.InitialValue == 100)
                    {
                        return a;
                    }
                    else
                    {
                        var bScale = b.InitialValue;
                        return new Animatable<double>(
                            initialValue: a.InitialValue * bScale,
                            keyFrames: a.KeyFrames.SelectToSpan(kf => ScaleKeyFrame(kf, bScale / 100)),
                            propertyIndex: null);
                    }
                }
                else
                {
                    return ComposeOpacityPercents(b, a);
                }
            }

            // Composes 2 animated percent values where the frames in first come before second.
            Animatable<double> ComposeNonOverlappingAnimatedPercents(Animatable<double> first, Animatable<double> second)
            {
                Debug.Assert(first.IsAnimated, "Precondition");
                Debug.Assert(second.IsAnimated, "Precondition");
                Debug.Assert(first.KeyFrames[first.KeyFrames.Length - 1].Frame <= second.KeyFrames[0].Frame, "Precondition");

                var resultFrames = new KeyFrame<double>[first.KeyFrames.Length + second.KeyFrames.Length];
                var resultCount = 0;
                var secondInitialScale = second.InitialValue / 100;
                var firstFinalScale = first.KeyFrames[first.KeyFrames.Length - 1].Value / 100;

                foreach (var kf in first.KeyFrames)
                {
                    resultFrames[resultCount] = ScaleKeyFrame(kf, secondInitialScale);
                    resultCount++;
                }

                foreach (var kf in second.KeyFrames)
                {
                    resultFrames[resultCount] = ScaleKeyFrame(kf, firstFinalScale);
                    resultCount++;
                }

                return new Animatable<double>(
                    first.InitialValue,
                    new ReadOnlySpan<KeyFrame<double>>(resultFrames, 0, resultCount),
                    null);
            }

            KeyFrame<double> ScaleKeyFrame(KeyFrame<double> keyFrame, double scale)
            {
                return new KeyFrame<double>(
                                keyFrame.Frame,
                                keyFrame.Value * scale,
                                keyFrame.SpatialControlPoint1,
                                keyFrame.SpatialControlPoint2,
                                keyFrame.Easing);
            }

            SolidColorFill ComposeSolidColorFill(SolidColorFill a, SolidColorFill b)
            {
                if (a == null)
                {
                    return b;
                }
                else if (b == null)
                {
                    return a;
                }

                if (!b.Color.IsAnimated &&
                    !b.OpacityPercent.IsAnimated)
                {
                    if (b.OpacityPercent.InitialValue == 100 &&
                        b.Color.InitialValue.A == 1)
                    {
                        // b overrides a.
                        return b;
                    }
                    else if (b.OpacityPercent.InitialValue == 0 || b.Color.InitialValue.A == 0)
                    {
                        // b is transparent, so a wins.
                        return a;
                    }
                }

                _owner._issues.MultipleFillsIsNotSupported();
                return b;
            }

            SolidColorStroke ComposeStrokes(SolidColorStroke a, SolidColorStroke b)
            {
                if (a == null)
                {
                    return b;
                }
                else if (b == null)
                {
                    return a;
                }

                if (!a.Thickness.IsAnimated && !b.Thickness.IsAnimated &&
                    !a.DashPattern.Any() && !b.DashPattern.Any() &&
                    a.OpacityPercent.AlwaysEquals(100) && b.OpacityPercent.AlwaysEquals(100))
                {
                    if (a.Thickness.InitialValue >= b.Thickness.InitialValue)
                    {
                        // a occludes b, so b can be ignored.
                        return a;
                    }
                }

                // The new stroke should be in addition to the existing stroke. And colors should blend.
                _owner._issues.MultipleStrokesIsNotSupported();
                return b;
            }

            RoundedCorner ComposeRoundedCorners(RoundedCorner a, RoundedCorner b)
            {
                if (a == null)
                {
                    return b;
                }
                else if (b == null)
                {
                    return a;
                }

                if (!b.Radius.IsAnimated)
                {
                    if (b.Radius.InitialValue >= 0)
                    {
                        // If b has a non-0 value, it wins.
                        return b;
                    }
                    else
                    {
                        // b is always 0. A wins.
                        return a;
                    }
                }

                _owner._issues.MultipleAnimatedRoundedCornersIsNotSupported();
                return b;
            }

            TrimPath ComposeTrimPaths(TrimPath a, TrimPath b)
            {
                if (a == null)
                {
                    return b;
                }
                else if (b == null)
                {
                    return a;
                }

                if (!a.StartPercent.IsAnimated && !a.StartPercent.IsAnimated && !a.OffsetDegrees.IsAnimated)
                {
                    // a is not animated.
                    if (!b.StartPercent.IsAnimated && !b.StartPercent.IsAnimated && !b.OffsetDegrees.IsAnimated)
                    {
                        // Both are not animated.
                        if (a.StartPercent.InitialValue == b.EndPercent.InitialValue)
                        {
                            // a trims out everything. b is unnecessary.
                            return a;
                        }
                        else if (b.StartPercent.InitialValue == b.EndPercent.InitialValue)
                        {
                            // b trims out everything. a is unnecessary.
                            return b;
                        }
                        else if (a.StartPercent.InitialValue == 0 && a.EndPercent.InitialValue == 100 && a.OffsetDegrees.InitialValue == 0)
                        {
                            // a is trimming nothing. a is unnecessary.
                            return b;
                        }
                        else if (b.StartPercent.InitialValue == 0 && b.EndPercent.InitialValue == 100 && b.OffsetDegrees.InitialValue == 0)
                        {
                            // b is trimming nothing. b is unnecessary.
                            return a;
                        }
                    }
                }

                _owner._issues.MultipleTrimPathsIsNotSupported();
                return b;
            }
        }

        // May return null if the layer does not produce any renderable content.
        ShapeOrVisual? TranslateShapeLayer(TranslationContext.For<ShapeLayer> context)
        {
            bool layerHasMasks = false;
#if !NoClipping
            layerHasMasks = context.Layer.Masks.Any();
#endif
            ContainerVisual containerVisualRootNode = null;
            ContainerVisual containerVisualContentNode = null;
            CompositionContainerShape containerShapeRootNode = null;
            CompositionContainerShape containerShapeContentNode = null;
            if (layerHasMasks)
            {
                if (!TryCreateContainerVisualTransformChain(context, out containerVisualRootNode, out containerVisualContentNode))
                {
                    // The layer is never visible.
                    return null;
                }

                containerShapeContentNode = CreateContainerShape();
            }
            else
            {
                if (!TryCreateContainerShapeTransformChain(context, out containerShapeRootNode, out containerShapeContentNode))
                {
                    // The layer is never visible.
                    return null;
                }
            }

            var shapeContext = new ShapeContentContext(this);

            if (!layerHasMasks)
            {
                // Only push the opacity to the context if there are no masks. If the layer has masks
                // the opacity is already set on one of the Visuals created by
                // TryCreateContainerVisualTransformChain(...)
                shapeContext.UpdateOpacityFromTransform(context.Layer.Transform);
            }

            containerShapeContentNode.Shapes.Add(TranslateShapeLayerContents(context, shapeContext, context.Layer.Contents));
            return
#if !NoClipping
                 layerHasMasks ? ApplyMaskToTreeWithShapes(context, containerShapeContentNode, containerVisualContentNode, containerVisualRootNode) :
#endif
                    (ShapeOrVisual)containerShapeRootNode;
        }

        CompositionShape TranslateGroupShapeContent(TranslationContext.For<ShapeLayer> context, ShapeContentContext shapeContext, ShapeGroup group)
        {
            var result = TranslateShapeLayerContents(context, shapeContext, group.Items);

            if (_addDescriptions)
            {
                Describe(result, $"ShapeGroup: {group.Name}");
            }

            return result;
        }

        // Discover patterns that we don't yet support and report any issues.
        void CheckForUnsupportedShapeGroup(in ReadOnlySpan<ShapeLayerContent> contents)
        {
            // Count the number of geometries. More than 1 geometry is currently not properly supported
            // unless they're all paths.
            var pathCount = 0;
            var geometryCount = 0;

            for (var i = 0; i < contents.Length; i++)
            {
                switch (contents[i].ContentType)
                {
                    case ShapeContentType.Ellipse:
                    case ShapeContentType.Polystar:
                    case ShapeContentType.Rectangle:
                        geometryCount++;
                        break;
                    case ShapeContentType.Path:
                        pathCount++;
                        geometryCount++;
                        break;
                    default:
                        break;
                }
            }

            if (geometryCount > 1 && pathCount != geometryCount)
            {
                _issues.CombiningMultipleShapesIsNotSupported();
            }
        }

        CompositionShape TranslateShapeLayerContents(
            TranslationContext.For<ShapeLayer> context,
            ShapeContentContext shapeContext,
            in ReadOnlySpan<ShapeLayerContent> contents)
        {
            // The Contents of a ShapeLayer is a list of instructions for a stack machine.

            // When evaluated, the stack of ShapeLayerContent produces a list of CompositionShape.
            // Some ShapeLayerContent modify the evaluation context (e.g. stroke, fill, trim)
            // Some ShapeLayerContent evaluate to geometries (e.g. any geometry, merge path)

            // Create a container to hold the contents.
            var container = CreateContainerShape();

            // This is the object that will be returned. Containers may be added above this
            // as necessary to hold transforms.
            var result = container;

            // If the contents contains a repeater, generate repeated contents
            if (contents.Any(slc => slc.ContentType == ShapeContentType.Repeater))
            {
                // The contents contains a repeater. Treat it as if there are n sets of items (where n
                // equals the Count of the repeater). In each set, replace the repeater with
                // the transform of the repeater, multiplied.

                // Find the index of the repeater
                var repeaterIndex = 0;
                while (contents[repeaterIndex].ContentType != ShapeContentType.Repeater)
                {
                    // Keep going until the first repeater is found.
                    repeaterIndex++;
                }

                // Get the repeater.
                var repeater = (Repeater)contents[repeaterIndex];

                var repeaterCount = context.TrimAnimatable(repeater.Count);
                var repeaterOffset = context.TrimAnimatable(repeater.Offset);

                // Make sure we can handle it.
                if (repeaterCount.IsAnimated || repeaterOffset.IsAnimated || repeaterOffset.InitialValue != 0)
                {
                    // TODO - handle all cases.
                    _issues.RepeaterIsNotSupported();
                }
                else
                {
                    // Get the items before the repeater, and the items after the repeater.
                    var itemsBeforeRepeater = contents.Slice(0, repeaterIndex).ToArray();
                    var itemsAfterRepeater = contents.Slice(repeaterIndex + 1).ToArray();

                    var nonAnimatedRepeaterCount = (int)Math.Round(repeaterCount.InitialValue);
                    for (var i = 0; i < nonAnimatedRepeaterCount; i++)
                    {
                        // Treat each repeated value as a list of items where the repeater is replaced
                        // by n transforms.
                        // TODO - currently ignoring the StartOpacityPercent and EndOpacityPercent - should generate a new transform
                        //        that interpolates that.
                        var generatedItems = itemsBeforeRepeater.Concat(Enumerable.Repeat(repeater.Transform, i + 1)).Concat(itemsAfterRepeater).ToArray();

                        // Recurse to translate the synthesized items.
                        container.Shapes.Add(TranslateShapeLayerContents(context, shapeContext, generatedItems));
                    }

                    return result;
                }
            }

            CheckForUnsupportedShapeGroup(contents);

            var stack = new Stack<ShapeLayerContent>(contents.ToArray());

            while (true)
            {
                shapeContext.UpdateFromStack(stack);
                if (stack.Count == 0)
                {
                    break;
                }

                var shapeContent = stack.Pop();

                // Complain if the BlendMode is not supported.
                if (shapeContent.BlendMode != BlendMode.Normal)
                {
                    _issues.BlendModeNotNormal(context.Layer.Name, shapeContent.BlendMode.ToString());
                }

                switch (shapeContent.ContentType)
                {
                    case ShapeContentType.Ellipse:
                        container.Shapes.Add(TranslateEllipseContent(context, shapeContext, (Ellipse)shapeContent));
                        break;
                    case ShapeContentType.Group:
                        container.Shapes.Add(TranslateGroupShapeContent(context, shapeContext.Clone(), (ShapeGroup)shapeContent));
                        break;
                    case ShapeContentType.MergePaths:
                        var mergedPaths = TranslateMergePathsContent(context, shapeContext, stack, ((MergePaths)shapeContent).Mode);
                        if (mergedPaths != null)
                        {
                            container.Shapes.Add(mergedPaths);
                        }

                        break;
                    case ShapeContentType.Path:
                        {
                            var path = (Path)shapeContent;
                            List<Path> paths = null;

                            if (!path.Data.IsAnimated)
                            {
                                while (stack.TryPeek(out var item) && item.ContentType == ShapeContentType.Path && !((Path)item).Data.IsAnimated)
                                {
                                    if (paths == null)
                                    {
                                        paths = new List<Path>();
                                        paths.Add(path);
                                    }

                                    paths.Add((Path)stack.Pop());
                                }
                            }

                            if (paths != null)
                            {
                                // There are multiple paths that are all non-animated. Group them.
                                // Note that we should be grouping paths and other shapes even if they're animated
                                // but we currently only support paths, and only if they're non-animated.
                                container.Shapes.Add(TranslatePathGroupContent(context, shapeContext, paths));
                            }
                            else
                            {
                                container.Shapes.Add(TranslatePathContent(context, shapeContext, path));
                            }
                        }

                        break;
                    case ShapeContentType.Polystar:
                        _issues.PolystarIsNotSupported();
                        break;
                    case ShapeContentType.Rectangle:
                        container.Shapes.Add(TranslateRectangleContent(context, shapeContext, (Rectangle)shapeContent));
                        break;
                    case ShapeContentType.Transform:
                        {
                            var transform = (Transform)shapeContent;

                            // Multiply the opacity in the transform.
                            shapeContext.UpdateOpacityFromTransform(transform);

                            // Insert a new container at the top. The transform will be applied to it.
                            var newContainer = CreateContainerShape();
                            newContainer.Shapes.Add(result);
                            result = newContainer;

                            // Apply the transform to the new container at the top.
                            TranslateAndApplyTransform(context, transform, result);
                        }

                        break;
                    case ShapeContentType.Repeater:
                        // TODO - handle all cases. Not clear whether this is valid. Seen on 0605.traffic_light.
                        _issues.RepeaterIsNotSupported();
                        break;
                    default:
                    case ShapeContentType.SolidColorStroke:
                    case ShapeContentType.LinearGradientStroke:
                    case ShapeContentType.RadialGradientStroke:
                    case ShapeContentType.SolidColorFill:
                    case ShapeContentType.LinearGradientFill:
                    case ShapeContentType.RadialGradientFill:
                    case ShapeContentType.TrimPath:
                    case ShapeContentType.RoundedCorner:
                        throw new InvalidOperationException();
                }
            }

            return result;
        }

        // Merge the stack into a single shape. Merging is done recursively - the top geometry on the
        // stack is merged with the merge of the remainder of the stack.
        CompositionShape TranslateMergePathsContent(TranslationContext context, ShapeContentContext shapeContext, Stack<ShapeLayerContent> stack, MergePaths.MergeMode mergeMode)
        {
            var mergedGeometry = MergeShapeLayerContent(context, shapeContext, stack, mergeMode);
            if (mergedGeometry != null)
            {
                var result = CreateSpriteShape();
                result.Geometry = CreatePathGeometry(new CompositionPath(mergedGeometry));
                TranslateAndApplyShapeContentContext(context, shapeContext, result);
                return result;
            }
            else
            {
                return null;
            }
        }

        CanvasGeometry MergeShapeLayerContent(TranslationContext context, ShapeContentContext shapeContext, Stack<ShapeLayerContent> stack, MergePaths.MergeMode mergeMode)
        {
            var pathFillType = shapeContext.Fill == null ? SolidColorFill.PathFillType.EvenOdd : shapeContext.Fill.FillType;
            var geometries = CreateCanvasGeometries(context, shapeContext, stack, pathFillType).ToArray();

            switch (geometries.Length)
            {
                case 0:
                    return null;
                case 1:
                    return geometries[0];
                default:
                    return CombineGeometries(geometries, mergeMode);
            }
        }

        // Merges the given paths with MergeMode.Merge.
        CanvasGeometry MergePaths(CanvasGeometry.Path[] paths)
        {
            Debug.Assert(paths.Length > 1, "Precondition");
            var builder = new CanvasPathBuilder(null);
            var filledRegionDetermination = paths[0].FilledRegionDetermination;
            builder.SetFilledRegionDetermination(filledRegionDetermination);
            foreach (var path in paths)
            {
                Debug.Assert(filledRegionDetermination == path.FilledRegionDetermination, "Invariant");
                foreach (var command in path.Commands)
                {
                    switch (command.Type)
                    {
                        case CanvasPathBuilder.CommandType.BeginFigure:
                            builder.BeginFigure(((CanvasPathBuilder.Command.BeginFigure)command).StartPoint);
                            break;
                        case CanvasPathBuilder.CommandType.EndFigure:
                            builder.EndFigure(((CanvasPathBuilder.Command.EndFigure)command).FigureLoop);
                            break;
                        case CanvasPathBuilder.CommandType.AddCubicBezier:
                            var cb = (CanvasPathBuilder.Command.AddCubicBezier)command;
                            builder.AddCubicBezier(cb.ControlPoint1, cb.ControlPoint2, cb.EndPoint);
                            break;
                        case CanvasPathBuilder.CommandType.AddLine:
                            builder.AddLine(((CanvasPathBuilder.Command.AddLine)command).EndPoint);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }

            return CanvasGeometry.CreatePath(builder);
        }

        // Combine all of the given geometries into a single geometry.
        CanvasGeometry CombineGeometries(CanvasGeometry[] geometries, MergePaths.MergeMode mergeMode)
        {
            switch (geometries.Length)
            {
                case 0:
                    return null;
                case 1:
                    return geometries[0];
            }

            // If MergeMode.Merge and they're all paths with the same FilledRegionDetermination,
            // combine into a single path.
            if (mergeMode == LottieData.MergePaths.MergeMode.Merge &&
                geometries.All(g => g.Type == CanvasGeometry.GeometryType.Path) &&
                geometries.Select(g => ((CanvasGeometry.Path)g).FilledRegionDetermination).Distinct().Count() == 1)
            {
                return MergePaths(geometries.Cast<CanvasGeometry.Path>().ToArray());
            }
            else
            {
                if (geometries.Length > 50)
                {
                    // There will be stack overflows if the CanvasGeometry.Combine is too large.
                    // Usually not a problem, but handle degenerate cases.
                    _issues.MergingALargeNumberOfShapesIsNotSupported();
                    geometries = geometries.Take(50).ToArray();
                }

                var combineMode = GeometryCombine(mergeMode);

#if PreCombineGeometries
            return CanvasGeometryCombiner.CombineGeometries(geometries, combineMode);
#else
                var accumulator = geometries[0];
                for (var i = 1; i < geometries.Length; i++)
                {
                    accumulator = accumulator.CombineWith(geometries[i], Sn.Matrix3x2.Identity, combineMode);
                }

                return accumulator;
#endif
            }
        }

        IEnumerable<CanvasGeometry> CreateCanvasGeometries(
            TranslationContext context,
            ShapeContentContext shapeContext,
            Stack<ShapeLayerContent> stack,
            SolidColorFill.PathFillType pathFillType)
        {
            while (stack.Count > 0)
            {
                // Ignore context on the stack - we only want geometries.
                var shapeContent = stack.Pop();
                switch (shapeContent.ContentType)
                {
                    case ShapeContentType.Group:
                        {
                            // Convert all the shapes in the group to a list of geometries
                            var group = (ShapeGroup)shapeContent;
                            var groupedGeometries = CreateCanvasGeometries(context, shapeContext.Clone(), new Stack<ShapeLayerContent>(group.Items.ToArray()), pathFillType).ToArray();
                            foreach (var geometry in groupedGeometries)
                            {
                                yield return geometry;
                            }
                        }

                        break;
                    case ShapeContentType.MergePaths:
                        yield return MergeShapeLayerContent(context, shapeContext, stack, ((MergePaths)shapeContent).Mode);
                        break;
                    case ShapeContentType.Repeater:
                        _issues.RepeaterIsNotSupported();
                        break;
                    case ShapeContentType.Transform:
                        // TODO - do we need to clear out the transform when we've finished with this call to CreateCanvasGeometries?? Maybe the caller should clone the context.
                        shapeContext.SetTransform((Transform)shapeContent);
                        break;

                    case ShapeContentType.SolidColorStroke:
                    case ShapeContentType.LinearGradientStroke:
                    case ShapeContentType.RadialGradientStroke:
                    case ShapeContentType.SolidColorFill:
                    case ShapeContentType.RadialGradientFill:
                    case ShapeContentType.LinearGradientFill:
                    case ShapeContentType.TrimPath:
                    case ShapeContentType.RoundedCorner:
                        // Ignore commands that set the context - we only want geometries.
                        break;

                    case ShapeContentType.Path:
                        yield return CreateWin2dPathGeometryFromShape(context, shapeContext, (Path)shapeContent, pathFillType, optimizeLines: true);
                        break;
                    case ShapeContentType.Ellipse:
                        yield return CreateWin2dEllipseGeometry(context, shapeContext, (Ellipse)shapeContent);
                        break;
                    case ShapeContentType.Rectangle:
                        yield return CreateWin2dRectangleGeometry(context, shapeContext, (Rectangle)shapeContent);
                        break;
                    case ShapeContentType.Polystar:
                        _issues.PolystarIsNotSupported();
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        CanvasGeometry CreateWin2dPathGeometry(
            Sequence<BezierSegment> figure,
            SolidColorFill.PathFillType fillType,
            Sn.Matrix3x2 transformMatrix,
            bool optimizeLines)
        {
            var beziers = figure.Items;
            using (var builder = new CanvasPathBuilder(null))
            {
                if (beziers.Length == 0)
                {
                    builder.BeginFigure(Vector2(0));
                    builder.EndFigure(CanvasFigureLoop.Closed);
                }
                else
                {
                    builder.SetFilledRegionDetermination(FilledRegionDetermination(fillType));
                    builder.BeginFigure(Sn.Vector2.Transform(Vector2(beziers[0].ControlPoint0), transformMatrix));

                    foreach (var segment in beziers)
                    {
                        var cp0 = Sn.Vector2.Transform(Vector2(segment.ControlPoint0), transformMatrix);
                        var cp1 = Sn.Vector2.Transform(Vector2(segment.ControlPoint1), transformMatrix);
                        var cp2 = Sn.Vector2.Transform(Vector2(segment.ControlPoint2), transformMatrix);
                        var cp3 = Sn.Vector2.Transform(Vector2(segment.ControlPoint3), transformMatrix);

                        // Add a line rather than a cubic bezier if the segment is a straight line.
                        if (optimizeLines && segment.IsALine)
                        {
                            // Ignore 0-length lines.
                            if (!cp0.Equals(cp3))
                            {
                                builder.AddLine(cp3);
                            }
                        }
                        else
                        {
                            builder.AddCubicBezier(cp1, cp2, cp3);
                        }
                    }

                    // Leave the figure open. If Lottie wanted it closed it will have defined
                    // a final bezier segment back to the start.
                    // Closed simply tells D2D to synthesize a final segment.
                    builder.EndFigure(CanvasFigureLoop.Open);
                }

                return CanvasGeometry.CreatePath(builder);
            } // end using
        }

        static Sn.Matrix3x2 CreateMatrixFromTransform(TranslationContext context, Transform transform)
        {
            if (transform == null)
            {
                return Sn.Matrix3x2.Identity;
            }

            if (transform.IsAnimated)
            {
                // TODO - report an issue. We can't handle an animated transform.
                // TODO - we could handle it if the only thing that is animated is the Opacity.
            }

            var anchor = Vector2(transform.Anchor.InitialValue);
            var position = Vector2(transform.Position.InitialValue);
            var scale = Vector2(transform.ScalePercent.InitialValue / 100.0);
            var rotation = (float)DegreesToRadians(transform.RotationDegrees.InitialValue);

            // Calculate the matrix that is equivalent to the properties.
            var combinedMatrix =
                Sn.Matrix3x2.CreateScale(scale, anchor) *
                Sn.Matrix3x2.CreateRotation(rotation, anchor) *
                Sn.Matrix3x2.CreateTranslation(position + anchor);

            return combinedMatrix;
        }

        static double DegreesToRadians(double angle) => Math.PI * angle / 180.0;

        CanvasGeometry CreateWin2dPathGeometryFromShape(
            TranslationContext context,
            ShapeContentContext shapeContext,
            Path path,
            SolidColorFill.PathFillType fillType,
            bool optimizeLines)
        {
            var pathData = context.TrimAnimatable(path.Data);

            if (pathData.IsAnimated)
            {
                _issues.CombiningAnimatedShapesIsNotSupported();
            }

            var transform = CreateMatrixFromTransform(context, shapeContext.Transform);

            var result = CreateWin2dPathGeometry(
                pathData.InitialValue,
                fillType,
                transform,
                optimizeLines: optimizeLines);

            if (_addDescriptions)
            {
                Describe(result, path.Name);
            }

            return result;
        }

        CanvasGeometry CreateWin2dEllipseGeometry(TranslationContext context, ShapeContentContext shapeContext, Ellipse ellipse)
        {
            var ellipsePosition = context.TrimAnimatable(ellipse.Position);
            var ellipseDiameter = context.TrimAnimatable(ellipse.Diameter);

            if (ellipsePosition.IsAnimated || ellipseDiameter.IsAnimated)
            {
                _issues.CombiningAnimatedShapesIsNotSupported();
            }

            var xRadius = ellipseDiameter.InitialValue.X / 2;
            var yRadius = ellipseDiameter.InitialValue.Y / 2;

            var result = CanvasGeometry.CreateEllipse(
                null,
                (float)(ellipsePosition.InitialValue.X - (xRadius / 2)),
                (float)(ellipsePosition.InitialValue.Y - (yRadius / 2)),
                (float)xRadius,
                (float)yRadius);

            var transformMatrix = CreateMatrixFromTransform(context, shapeContext.Transform);
            if (!transformMatrix.IsIdentity)
            {
                result = result.Transform(transformMatrix);
            }

            if (_addDescriptions)
            {
                Describe(result, ellipse.Name);
            }

            return result;
        }

        CanvasGeometry CreateWin2dRectangleGeometry(TranslationContext context, ShapeContentContext shapeContext, Rectangle rectangle)
        {
            var position = context.TrimAnimatable(rectangle.Position);
            var size = context.TrimAnimatable(rectangle.Size);

            // If a Rectangle is in the context, use it to override the corner radius.
            var cornerRadius = context.TrimAnimatable(shapeContext.RoundedCorner != null ? shapeContext.RoundedCorner.Radius : rectangle.CornerRadius);

            if (position.IsAnimated || size.IsAnimated || cornerRadius.IsAnimated)
            {
                _issues.CombiningAnimatedShapesIsNotSupported();
            }

            var width = size.InitialValue.X;
            var height = size.InitialValue.Y;
            var radius = cornerRadius.InitialValue;

            var result = CanvasGeometry.CreateRoundedRectangle(
                null,
                (float)(position.InitialValue.X - (width / 2)),
                (float)(position.InitialValue.Y - (height / 2)),
                (float)width,
                (float)height,
                (float)radius,
                (float)radius);

            var transformMatrix = CreateMatrixFromTransform(context, shapeContext.Transform);
            if (!transformMatrix.IsIdentity)
            {
                result = result.Transform(transformMatrix);
            }

            if (_addDescriptions)
            {
                Describe(result, rectangle.Name);
            }

            return result;
        }

        CompositionShape TranslateEllipseContent(TranslationContext context, ShapeContentContext shapeContext, Ellipse shapeContent)
        {
            // An ellipse is represented as a SpriteShape with a CompositionEllipseGeometry.
            var compositionSpriteShape = CreateSpriteShape();

            var compositionEllipseGeometry = CreateEllipseGeometry();
            compositionSpriteShape.Geometry = compositionEllipseGeometry;
            if (_addDescriptions)
            {
                Describe(compositionSpriteShape, shapeContent.Name);
                Describe(compositionEllipseGeometry, $"{shapeContent.Name}.EllipseGeometry");
            }

            var position = context.TrimAnimatable(shapeContent.Position);
            if (position.IsAnimated)
            {
                ApplyVector2KeyFrameAnimation(context, position, compositionEllipseGeometry, "Center");
            }
            else
            {
                compositionEllipseGeometry.Center = Vector2(position.InitialValue);
            }

            var diameter = context.TrimAnimatable(shapeContent.Diameter);
            if (diameter.IsAnimated)
            {
                ApplyScaledVector2KeyFrameAnimation(context, diameter, 0.5, compositionEllipseGeometry, "Radius");
            }
            else
            {
                compositionEllipseGeometry.Radius = Vector2(diameter.InitialValue) * 0.5F;
            }

            TranslateAndApplyShapeContentContext(context, shapeContext, compositionSpriteShape);

            return compositionSpriteShape;
        }

        CompositionShape TranslateRectangleContent(TranslationContext context, ShapeContentContext shapeContext, Rectangle shapeContent)
        {
            var compositionRectangle = CreateSpriteShape();
            var position = context.TrimAnimatable(shapeContent.Position);
            var size = context.TrimAnimatable(shapeContent.Size);

            if (shapeContent.CornerRadius.AlwaysEquals(0) && shapeContext.RoundedCorner == null)
            {
                // Use a non-rounded rectangle geometry.
#if WorkAroundRectangleGeometryHalfDrawn
                // Rounded rectangles do not have a problem, so create a rounded rectangle with a tiny corner
                // radius to work around the bug.
                var geometry = CreateRoundedRectangleGeometry();
                geometry.CornerRadius = new Sn.Vector2(0.000001F, 0.000001F);
#else
                var geometry = CreateRectangleGeometry();
#endif
                compositionRectangle.Geometry = geometry;

                // Convert size and position into offset. This is necessary because a geometry's offset is for
                // its top left corner, wherease a Lottie position is for its centerpoint.
                geometry.Offset = Vector2(position.InitialValue - (size.InitialValue / 2));

                if (!size.IsAnimated)
                {
                    geometry.Size = Vector2(size.InitialValue);
                }

                if (position.IsAnimated || size.IsAnimated)
                {
                    Expr offsetExpression;
                    if (position.IsAnimated)
                    {
                        ApplyVector2KeyFrameAnimation(context, position, geometry, nameof(Rectangle.Position));
                        geometry.Properties.InsertVector2(nameof(Rectangle.Position), Vector2(position.InitialValue));
                        if (size.IsAnimated)
                        {
                            // Size AND position are animated.
                            offsetExpression = ExpressionFactory.PositionAndSizeToOffsetExpression;
                            ApplyVector2KeyFrameAnimation(context, size, geometry, nameof(Rectangle.Size));
                        }
                        else
                        {
                            // Only Position is animated
                            offsetExpression = ExpressionFactory.HalfSizeToOffsetExpression(Vector2(size.InitialValue / 2));
                        }
                    }
                    else
                    {
                        // Only Size is animated.
                        offsetExpression = ExpressionFactory.PositionToOffsetExpression(Vector2(position.InitialValue));
                        ApplyVector2KeyFrameAnimation(context, size, geometry, nameof(Rectangle.Size));
                    }

                    var offsetExpressionAnimation = CreateExpressionAnimation(offsetExpression);
                    offsetExpressionAnimation.SetReferenceParameter("my", geometry);
                    StartExpressionAnimation(geometry, nameof(geometry.Offset), offsetExpressionAnimation);
                }
            }
            else
            {
                // Use a rounded rectangle geometry.
                var geometry = CreateRoundedRectangleGeometry();
                compositionRectangle.Geometry = geometry;

                // If a RoundedRectangle is in the context, use it to override the corner radius.
                var cornerRadius = context.TrimAnimatable(shapeContext.RoundedCorner != null ? shapeContext.RoundedCorner.Radius : shapeContent.CornerRadius);
                if (cornerRadius.IsAnimated)
                {
                    ApplyScalarKeyFrameAnimation(context, cornerRadius, geometry, "CornerRadius.X");
                    ApplyScalarKeyFrameAnimation(context, cornerRadius, geometry, "CornerRadius.Y");
                }
                else
                {
                    geometry.CornerRadius = Vector2((float)cornerRadius.InitialValue);
                }

                // Convert size and position into offset. This is necessary because a geometry's offset is for
                // its top left corner, wherease a Lottie position is for its centerpoint.
                geometry.Offset = Vector2(position.InitialValue - (size.InitialValue / 2));

                if (!size.IsAnimated)
                {
                    geometry.Size = Vector2(size.InitialValue);
                }

                if (position.IsAnimated || size.IsAnimated)
                {
                    Expr offsetExpression;
                    if (position.IsAnimated)
                    {
                        ApplyVector2KeyFrameAnimation(context, position, geometry, nameof(Rectangle.Position));

                        geometry.Properties.InsertVector2(nameof(Rectangle.Position), Vector2(position.InitialValue));
                        if (size.IsAnimated)
                        {
                            // Size AND position are animated.
                            offsetExpression = ExpressionFactory.PositionAndSizeToOffsetExpression;
                            ApplyVector2KeyFrameAnimation(context, size, geometry, nameof(Rectangle.Size));
                        }
                        else
                        {
                            // Only Position is animated
                            offsetExpression = ExpressionFactory.HalfSizeToOffsetExpression(Vector2(size.InitialValue / 2));
                        }
                    }
                    else
                    {
                        // Only Size is animated.
                        offsetExpression = ExpressionFactory.PositionToOffsetExpression(Vector2(position.InitialValue));
                        ApplyVector2KeyFrameAnimation(context, size, geometry, nameof(Rectangle.Size));
                    }

                    var offsetExpressionAnimation = CreateExpressionAnimation(offsetExpression);
                    offsetExpressionAnimation.SetReferenceParameter("my", geometry);
                    StartExpressionAnimation(geometry, nameof(geometry.Offset), offsetExpressionAnimation);
                }
            }

            // Lottie rectangles have 0,0 at top right. That causes problems for TrimPath which expects 0,0 to be top left.
            // Add an offset to the trim path.

            // TODO - this only works correctly if Size and TrimOffset are not animated. A complete solution requires
            //        adding another property.
            var isPartialTrimPath = shapeContext.TrimPath != null &&
                (shapeContext.TrimPath.StartPercent.IsAnimated || shapeContext.TrimPath.EndPercent.IsAnimated || shapeContext.TrimPath.OffsetDegrees.IsAnimated ||
                shapeContext.TrimPath.StartPercent.InitialValue != 0 || shapeContext.TrimPath.EndPercent.InitialValue != 100);

            if (size.IsAnimated && isPartialTrimPath)
            {
                // Warn that we might be getting things wrong
                _issues.AnimatedRectangleWithTrimPathIsNotSupported();
            }

            var width = size.InitialValue.X;
            var height = size.InitialValue.Y;
            var trimOffsetDegrees = (width / (2 * (width + height))) * 360;
            TranslateAndApplyShapeContentContext(context, shapeContext, compositionRectangle, trimOffsetDegrees: trimOffsetDegrees);

            if (_addDescriptions)
            {
                Describe(compositionRectangle, shapeContent.Name);
                Describe(compositionRectangle.Geometry, $"{shapeContent.Name}.RectangleGeometry");
            }

            return compositionRectangle;
        }

        void CheckForRoundedCornersOnPath(TranslationContext context, ShapeContentContext shapeContext)
        {
            if (shapeContext.RoundedCorner != null)
            {
                var trimmedRadius = context.TrimAnimatable(shapeContext.RoundedCorner.Radius);
                if (trimmedRadius.IsAnimated || trimmedRadius.InitialValue != 0)
                {
                    // TODO - can rounded corners be implemented by composing cubic beziers?
                    _issues.PathWithRoundedCornersIsNotSupported();
                }
            }
        }

        // Groups multiple Shapes into a D2D geometry group.
        CompositionShape TranslatePathGroupContent(TranslationContext context, ShapeContentContext shapeContext, IEnumerable<Path> paths)
        {
            Debug.Assert(paths.All(sh => !sh.Data.IsAnimated), "Precondition");

            CheckForRoundedCornersOnPath(context, shapeContext);

            var fillType = GetPathFillType(shapeContext.Fill);

            // A path is represented as a SpriteShape with a CompositionPathGeometry.
            var compositionPathGeometry = CreatePathGeometry();

            var compositionPath = new CompositionPath(
                CanvasGeometry.CreateGroup(
                    null,
                    paths.Select(sh => CreateWin2dPathGeometry(sh.Data.InitialValue, fillType, Sn.Matrix3x2.Identity, optimizeLines: true)).ToArray(),
                    FilledRegionDetermination(fillType)));

            compositionPathGeometry.Path = compositionPath;

            var compositionSpriteShape = CreateSpriteShape();
            compositionSpriteShape.Geometry = compositionPathGeometry;

            if (_addDescriptions)
            {
                var shapeContentName = string.Join("+", paths.Select(sh => sh.Name).Where(a => a != null));
                Describe(compositionSpriteShape, shapeContentName);
                Describe(compositionPathGeometry, $"{shapeContentName}.PathGeometry");
            }

            TranslateAndApplyShapeContentContext(context, shapeContext, compositionSpriteShape, 0);

            return compositionSpriteShape;
        }

        CompositionShape TranslatePathContent(TranslationContext context, ShapeContentContext shapeContext, Path path)
        {
            CheckForRoundedCornersOnPath(context, shapeContext);

            // Map Path's Geometry data to PathGeometry.Path
            var pathGeometry = context.TrimAnimatable(_lottieDataOptimizer.GetOptimized(path.Data));

            // A path is represented as a SpriteShape with a CompositionPathGeometry.
            var compositionPathGeometry = CreatePathGeometry();

            var compositionSpriteShape = CreateSpriteShape();
            compositionSpriteShape.Geometry = compositionPathGeometry;

            if (pathGeometry.IsAnimated)
            {
                ApplyPathKeyFrameAnimation(context, pathGeometry, GetPathFillType(shapeContext.Fill), compositionPathGeometry, nameof(compositionPathGeometry.Path), "Path");
            }
            else
            {
                compositionPathGeometry.Path = CompositionPathFromPathGeometry(
                    pathGeometry.InitialValue,
                    GetPathFillType(shapeContext.Fill),
                    optimizeLines: true);
            }

            if (_addDescriptions)
            {
                Describe(compositionSpriteShape, path.Name);
                Describe(compositionPathGeometry, $"{path.Name}.PathGeometry");
            }

            TranslateAndApplyShapeContentContext(context, shapeContext, compositionSpriteShape, 0);

            return compositionSpriteShape;
        }

        void TranslateAndApplyShapeContentContext(TranslationContext context, ShapeContentContext shapeContext, CompositionSpriteShape shape, double trimOffsetDegrees = 0)
        {
            shape.FillBrush = TranslateShapeFill(context, shapeContext.Fill, context.TrimAnimatable(shapeContext.OpacityPercent));
            TranslateAndApplyStroke(context, shapeContext.Stroke, shape, context.TrimAnimatable(shapeContext.OpacityPercent));
            TranslateAndApplyTrimPath(context, shapeContext.TrimPath, shape.Geometry, trimOffsetDegrees);
        }

        enum AnimatableOrder
        {
            Before,
            After,
            Equal,
            BeforeAndAfter,
        }

        static AnimatableOrder GetValueOrder(double a, double b)
        {
            if (a == b)
            {
                return AnimatableOrder.Equal;
            }
            else if (a < b)
            {
                return AnimatableOrder.Before;
            }
            else
            {
                return AnimatableOrder.After;
            }
        }

        static AnimatableOrder GetAnimatableOrder(in TrimmedAnimatable<double> a, in TrimmedAnimatable<double> b)
        {
            var initialA = a.InitialValue;
            var initialB = b.InitialValue;

            var initialOrder = GetValueOrder(initialA, initialB);
            if (!a.IsAnimated && !b.IsAnimated)
            {
                return initialOrder;
            }

            // TODO - recognize more cases. For now just handle a is always before b
            var aMin = initialA;
            var aMax = initialA;
            if (a.IsAnimated)
            {
                aMin = Math.Min(a.KeyFrames.Min(kf => kf.Value), initialA);
                aMax = Math.Max(a.KeyFrames.Max(kf => kf.Value), initialA);
            }

            var bMin = initialB;
            var bMax = initialB;
            if (b.IsAnimated)
            {
                bMin = Math.Min(b.KeyFrames.Min(kf => kf.Value), initialB);
                bMax = Math.Max(b.KeyFrames.Max(kf => kf.Value), initialB);
            }

            switch (initialOrder)
            {
                case AnimatableOrder.Before:
                    return aMax <= bMin ? initialOrder : AnimatableOrder.BeforeAndAfter;
                case AnimatableOrder.After:
                    return aMin >= bMax ? initialOrder : AnimatableOrder.BeforeAndAfter;
                case AnimatableOrder.Equal:
                    {
                        if (aMin == aMax && bMin == bMax && aMin == bMax)
                        {
                            return AnimatableOrder.Equal;
                        }
                        else if (aMin < bMax)
                        {
                            // Might be before, unless they cross over.
                            return bMin < initialA || aMax > initialA ? AnimatableOrder.BeforeAndAfter : AnimatableOrder.Before;
                        }
                        else
                        {
                            // Might be after, unless they cross over.
                            return bMin > aMax ? AnimatableOrder.BeforeAndAfter : AnimatableOrder.After;
                        }
                    }

                case AnimatableOrder.BeforeAndAfter:
                default:
                    throw new InvalidOperationException();
            }
        }

        void TranslateAndApplyTrimPath(TranslationContext context, TrimPath trimPath, CompositionGeometry geometry, double trimOffsetDegrees)
        {
            if (trimPath == null)
            {
                return;
            }

            var startPercent = context.TrimAnimatable(trimPath.StartPercent);
            var endPercent = context.TrimAnimatable(trimPath.EndPercent);
            var trimPathOffsetDegrees = context.TrimAnimatable(trimPath.OffsetDegrees);

            if (!startPercent.IsAnimated && !endPercent.IsAnimated)
            {
                // Handle some well-known static cases
                if (startPercent.InitialValue == 0 && endPercent.InitialValue == 1)
                {
                    // The trim does nothing.
                    return;
                }
                else if (startPercent.InitialValue == endPercent.InitialValue)
                {
                    // TODO - the trim trims away all of the path.
                }
            }

            var order = GetAnimatableOrder(startPercent, endPercent);

            switch (order)
            {
                case AnimatableOrder.Before:
                case AnimatableOrder.Equal:
                    break;
                case AnimatableOrder.After:
                    {
                        // Swap is necessary to match the WinComp semantics.
                        var temp = startPercent;
                        startPercent = endPercent;
                        endPercent = temp;
                    }

                    break;
                case AnimatableOrder.BeforeAndAfter:
                    break;
                default:
                    throw new InvalidOperationException();
            }

            if (order == AnimatableOrder.BeforeAndAfter)
            {
                // Add properties that will be animated. The TrimStart and TrimEnd properties
                // will be set by these values through an expression.
                geometry.Properties.InsertScalar("TStart", PercentF(startPercent.InitialValue));
                if (startPercent.IsAnimated)
                {
                    ApplyPercentKeyFrameAnimation(context, startPercent, geometry.Properties, "TStart", "TStart", null);
                }

                var trimStartExpression = CreateExpressionAnimation(ExpressionFactory.MinTStartTEnd);
                trimStartExpression.SetReferenceParameter("my", geometry);
                StartExpressionAnimation(geometry, nameof(geometry.TrimStart), trimStartExpression);

                geometry.Properties.InsertScalar("TEnd", PercentF(endPercent.InitialValue));
                if (endPercent.IsAnimated)
                {
                    ApplyPercentKeyFrameAnimation(context, endPercent, geometry.Properties, "TEnd", "TEnd", null);
                }

                var trimEndExpression = CreateExpressionAnimation(ExpressionFactory.MaxTStartTEnd);
                trimEndExpression.SetReferenceParameter("my", geometry);
                StartExpressionAnimation(geometry, nameof(geometry.TrimEnd), trimEndExpression);
            }
            else
            {
                // Directly animate the TrimStart and TrimEnd properties.
                if (startPercent.IsAnimated)
                {
                    ApplyPercentKeyFrameAnimation(context, startPercent, geometry, nameof(geometry.TrimStart), "TrimStart", null);
                }
                else
                {
                    geometry.TrimStart = PercentF(startPercent.InitialValue);
                }

                if (endPercent.IsAnimated)
                {
                    ApplyPercentKeyFrameAnimation(context, endPercent, geometry, nameof(geometry.TrimEnd), "TrimEnd", null);
                }
                else
                {
                    geometry.TrimEnd = PercentF(endPercent.InitialValue);
                }
            }

            if (trimOffsetDegrees != 0 && !trimPathOffsetDegrees.IsAnimated)
            {
                // Rectangle shapes are treated specially here to account for Lottie rectangle 0,0 being
                // top right and WinComp rectangle 0,0 being top left. As long as the TrimOffset isn't
                // being animated we can simply add an offset to the trim path.
                geometry.TrimOffset = (float)((trimPathOffsetDegrees.InitialValue + trimOffsetDegrees) / 360);
            }
            else
            {
                if (trimOffsetDegrees != 0)
                {
                    // TODO - can be handled with another property.
                    _issues.AnimatedTrimOffsetWithStaticTrimOffsetIsNotSupported();
                }

                if (trimPathOffsetDegrees.IsAnimated)
                {
                    ApplyScaledScalarKeyFrameAnimation(context, trimPathOffsetDegrees, 1 / 360.0, geometry, nameof(geometry.TrimOffset), "TrimOffset", null);
                }
                else
                {
                    geometry.TrimOffset = Float(trimPathOffsetDegrees.InitialValue / 360);
                }
            }
        }

        void TranslateAndApplyStroke(
            TranslationContext context,
            SolidColorStroke shapeStroke,
            CompositionSpriteShape sprite,
            TrimmedAnimatable<double> contextOpacityPercent)
        {
            if (shapeStroke == null || shapeStroke.Thickness.AlwaysEquals(0))
            {
                return;
            }

            // A ShapeStroke is represented as a CompositionColorBrush and Stroke properties on the relevant SpriteShape.
            // Map ShapeStroke's color to SpriteShape.StrokeBrush
            sprite.StrokeBrush = CreateAnimatedColorBrush(
                context,
                MultiplyAnimatableColorByAnimatableOpacityPercent(
                    context.TrimAnimatable(shapeStroke.Color),
                    context.TrimAnimatable(shapeStroke.OpacityPercent)),
                contextOpacityPercent);

            var strokeThickness = context.TrimAnimatable(shapeStroke.Thickness);
            if (strokeThickness.IsAnimated)
            {
                ApplyScalarKeyFrameAnimation(context, strokeThickness, sprite, nameof(sprite.StrokeThickness));
            }
            else
            {
                sprite.StrokeThickness = (float)strokeThickness.InitialValue;
            }

            sprite.StrokeStartCap = sprite.StrokeEndCap = sprite.StrokeDashCap = StrokeCap(shapeStroke.CapType);

            sprite.StrokeLineJoin = StrokeLineJoin(shapeStroke.JoinType);

            sprite.StrokeMiterLimit = (float)shapeStroke.MiterLimit;

            // NOTE: DashPattern animation (animating dash sizes) are not supported on CompositionSpriteShape.
            foreach (var dash in shapeStroke.DashPattern)
            {
                sprite.StrokeDashArray.Add((float)dash);
            }

            // Set DashOffset
            var strokeDashOffset = context.TrimAnimatable(shapeStroke.DashOffset);
            if (strokeDashOffset.IsAnimated)
            {
                ApplyScalarKeyFrameAnimation(context, strokeDashOffset, sprite, nameof(sprite.StrokeDashOffset));
            }
            else
            {
                sprite.StrokeDashOffset = (float)strokeDashOffset.InitialValue;
            }
        }

        CompositionColorBrush TranslateShapeFill(TranslationContext context, SolidColorFill shapeFill, TrimmedAnimatable<double> opacityPercent)
        {
            if (shapeFill == null)
            {
                return null;
            }

            // A ShapeFill is represented as a CompositionColorBrush.
            return CreateAnimatedColorBrush(
                context,
                MultiplyAnimatableColorByAnimatableOpacityPercent(
                    context.TrimAnimatable(shapeFill.Color),
                    context.TrimAnimatable(shapeFill.OpacityPercent)),
                opacityPercent);
        }

        ShapeOrVisual? TranslateSolidLayer(TranslationContext.For<SolidLayer> context)
        {
            if (context.Layer.IsHidden || context.Layer.Transform.OpacityPercent.AlwaysEquals(0))
            {
                // The layer does not render anything. Nothing to translate. This can happen when someone
                // creates a solid layer to act like a Null layer.
                return null;
            }

            bool layerHasMasks = false;
#if !NoClipping
            layerHasMasks = context.Layer.Masks.Any();
#endif
            ContainerVisual containerVisualRootNode = null;
            ContainerVisual containerVisualContentNode = null;
            CompositionContainerShape containerShapeRootNode = null;
            CompositionContainerShape containerShapeContentNode = null;
            if (layerHasMasks)
            {
                if (!TryCreateContainerVisualTransformChain(context, out containerVisualRootNode, out containerVisualContentNode))
                {
                    // The layer is never visible.
                    return null;
                }

                containerShapeContentNode = CreateContainerShape();
            }
            else
            {
                if (!TryCreateContainerShapeTransformChain(context, out containerShapeRootNode, out containerShapeContentNode))
                {
                    // The layer is never visible.
                    return null;
                }
            }

            var rectangleGeometry = CreateRectangleGeometry();
            rectangleGeometry.Size = Vector2(context.Layer.Width, context.Layer.Height);

            var rectangle = CreateSpriteShape();
            rectangle.Geometry = rectangleGeometry;

            containerShapeContentNode.Shapes.Add(rectangle);

            // If the layer has masks then the opacity is set on a Visual in the chain returned
            // for the layer from TryCreateContainerVisualTransformChain.
            // If that layer has no masks, opacity is implemented via the alpha channel on the brush.
            rectangle.FillBrush = layerHasMasks
                ? CreateColorBrush(context.Layer.Color)
                : CreateAnimatedColorBrush(context, context.Layer.Color, context.TrimAnimatable(context.Layer.Transform.OpacityPercent));

            if (_addDescriptions)
            {
                Describe(rectangle, "SolidLayerRectangle");
                Describe(rectangleGeometry, "SolidLayerRectangle.RectangleGeometry");
            }

            return
#if !NoClipping
            layerHasMasks ? ApplyMaskToTreeWithShapes(context, containerShapeContentNode, containerVisualContentNode, containerVisualRootNode) :
#endif
                 (ShapeOrVisual)containerShapeRootNode;
        }

        Visual TranslateTextLayer(TranslationContext.For<TextLayer> context)
        {
            // Text layers are not yet suported.
            _issues.TextLayerIsNotSupported();
            return null;
        }

        // Returns a chain of ContainerVisual that define the transform for a layer.
        // The top of the chain is the rootTransform, the bottom is the leafTransform.
        void TranslateTransformOnContainerVisualForLayer(
            TranslationContext context,
            Layer layer,
            out ContainerVisual rootTransformNode,
            out ContainerVisual leafTransformNode)
        {
            // Create a ContainerVisual to apply the transform to.
            leafTransformNode = CreateContainerVisual();

            // Apply the transform.
            TranslateAndApplyTransform(context, layer.Transform, leafTransformNode);
            if (_addDescriptions)
            {
                Describe(leafTransformNode, $"Transforms for {layer.Name}");
            }

#if NoTransformInheritance
            rootTransformNode = leafTransformNode;
#else
            // Translate the parent transform, if any.
            if (layer.Parent != null)
            {
                var parentLayer = context.Layers.GetLayerById(layer.Parent.Value);
                TranslateTransformOnContainerVisualForLayer(context, parentLayer, out rootTransformNode, out var parentLeafTransform);
                parentLeafTransform.Children.Add(leafTransformNode);
            }
            else
            {
                rootTransformNode = leafTransformNode;
            }
#endif
        }

        // Returns a chain of CompositionContainerShape that define the transform for a layer.
        // The top of the chain is the rootTransform, the bottom is the leafTransform.
        void TranslateTransformOnContainerShapeForLayer(
            TranslationContext context,
            Layer layer,
            out CompositionContainerShape rootTransformNode,
            out CompositionContainerShape leafTransformNode)
        {
            // Create a ContainerVisual to apply the transform to.
            leafTransformNode = CreateContainerShape();

            // Apply the transform from the layer.
            TranslateAndApplyTransform(context, layer.Transform, leafTransformNode);

#if NoTransformInheritance
            rootTransformNode = leafTransformNode;
#else
            // Translate the parent transform, if any.
            if (layer.Parent != null)
            {
                var parentLayer = context.Layers.GetLayerById(layer.Parent.Value);
                TranslateTransformOnContainerShapeForLayer(context, parentLayer, out rootTransformNode, out var parentLeafTransform);
                parentLeafTransform.Shapes.Add(leafTransformNode);

                if (_addDescriptions)
                {
                    Describe(leafTransformNode, $"Transforms for {layer.Name}", $"Transforms: {layer.Name}");
                }
            }
            else
            {
                rootTransformNode = leafTransformNode;
            }
#endif
        }

        void TranslateAndApplyTransform(
            TranslationContext context,
            Transform transform,
            ContainerShapeOrVisual container)
        {
            TranslateAndApplyAnchorPositionRotationAndScale(
                context,
                transform.Anchor,
                transform.Position,
                context.TrimAnimatable(transform.RotationDegrees),
                context.TrimAnimatable(transform.ScalePercent),
                container);

            // set Skew and Skew Axis
            // TODO: TransformMatrix --> for a Layer, does this clash with Visibility? Should I add an extra Container?
        }

        void TranslateAndApplyAnchorPositionRotationAndScale(
            TranslationContext context,
            IAnimatableVector3 anchor,
            IAnimatableVector3 position,
            in TrimmedAnimatable<double> rotationDegrees,
            in TrimmedAnimatable<Vector3> scalePercent,
            ContainerShapeOrVisual container)
        {
            // There are many different cases to consider in order to do this optimally:
            // * Is the container a CompositionContainerShape (Vector2 properties)
            //    or a ContainerVisual (Vector3 properties)
            // * Is the anchor animated?
            // * Is the anchor expressed as a Vector2 or as X and Y values?
            // * Is the position animated?
            // * Is the position expressed as a Vector2 or as X and Y values?
            // * Is rotation or scale specified? (If they're not and
            //    the anchor is static then the anchor can be expressed
            //    as just an offset)
            //
            // The current implementation doesn't take all cases into consideration yet.
            if (rotationDegrees.IsAnimated)
            {
                ApplyScalarKeyFrameAnimation(context, rotationDegrees, container, nameof(container.RotationAngleInDegrees), "Rotation");
            }
            else
            {
                container.RotationAngleInDegrees = FloatDefaultIsZero(rotationDegrees.InitialValue);
            }

#if !NoScaling
            if (scalePercent.IsAnimated)
            {
                if (container.IsShape)
                {
                    ApplyScaledVector2KeyFrameAnimation(context, scalePercent, 1 / 100.0, container, nameof(container.Scale), "Scale");
                }
                else
                {
                    ApplyScaledVector3KeyFrameAnimation(context, scalePercent, 1 / 100.0, container, nameof(container.Scale), "Scale");
                }
            }
            else
            {
                container.Scale = Vector2DefaultIsOne(scalePercent.InitialValue * (1 / 100.0));
            }
#endif

            var anchorX = default(TrimmedAnimatable<double>);
            var anchorY = default(TrimmedAnimatable<double>);
            var anchor3 = default(TrimmedAnimatable<Vector3>);

            var xyzAnchor = anchor as AnimatableXYZ;
            if (xyzAnchor != null)
            {
                anchorX = context.TrimAnimatable(xyzAnchor.X);
                anchorY = context.TrimAnimatable(xyzAnchor.Y);
            }
            else
            {
                anchor3 = context.TrimAnimatable(anchor);
            }

            var positionX = default(TrimmedAnimatable<double>);
            var positionY = default(TrimmedAnimatable<double>);
            var position3 = default(TrimmedAnimatable<Vector3>);

            var xyzPosition = position as AnimatableXYZ;
            if (xyzPosition != null)
            {
                positionX = context.TrimAnimatable(xyzPosition.X);
                positionY = context.TrimAnimatable(xyzPosition.Y);
            }
            else
            {
                position3 = context.TrimAnimatable(position);
            }

            var anchorIsAnimated = anchorX.IsAnimated || anchorY.IsAnimated || anchor3.IsAnimated;
            var positionIsAnimated = positionX.IsAnimated || positionY.IsAnimated || position3.IsAnimated;

            var initialAnchor = xyzAnchor != null ? Vector2(anchorX.InitialValue, anchorY.InitialValue) : Vector2(anchor3.InitialValue);
            var initialPosition = xyzPosition != null ? Vector2(positionX.InitialValue, positionY.InitialValue) : Vector2(position3.InitialValue);

            // The Lottie Anchor is the centerpoint of the object and is used for rotation and scaling.
            if (anchorIsAnimated)
            {
                container.Properties.InsertVector2("Anchor", initialAnchor);
                var centerPointExpression = CreateExpressionAnimation(container.IsShape ? MyAnchor2 : MyAnchor3);
                centerPointExpression.SetReferenceParameter("my", container);
                StartExpressionAnimation(container, nameof(container.CenterPoint), centerPointExpression);

                if (xyzAnchor != null)
                {
                    if (anchorX.IsAnimated)
                    {
                        ApplyScalarKeyFrameAnimation(context, anchorX, container.Properties, targetPropertyName: "Anchor.X");
                    }

                    if (anchorY.IsAnimated)
                    {
                        ApplyScalarKeyFrameAnimation(context, anchorY, container.Properties, targetPropertyName: "Anchor.Y");
                    }
                }
                else
                {
                    ApplyVector2KeyFrameAnimation(context, anchor3, container.Properties, "Anchor");
                }
            }
            else
            {
                container.CenterPoint = Vector2DefaultIsZero(initialAnchor);
            }

            // If the position or anchor are animated, the offset needs to be calculated via an expression.
            ExpressionAnimation offsetExpression = null;
            if (positionIsAnimated && anchorIsAnimated)
            {
                // Both position and anchor are animated.
                offsetExpression = CreateExpressionAnimation(container.IsShape ? PositionMinusAnchor2 : PositionMinusAnchor3);
            }
            else if (positionIsAnimated)
            {
                // Only position is animated.
                if (initialAnchor == Sn.Vector2.Zero)
                {
                    // Position and Offset are equivalent because the Anchor is not animated and is 0.
                    // We don't need to animate a Position property - we can animate Offset directly.
                    positionIsAnimated = false;

                    if (xyzPosition != null)
                    {
                        if (!positionX.IsAnimated || !positionY.IsAnimated)
                        {
                            container.Offset = Vector2DefaultIsZero(initialPosition - initialAnchor);
                        }

                        if (positionX.IsAnimated)
                        {
                            ApplyScalarKeyFrameAnimation(context, positionX, container, targetPropertyName: "Offset.X");
                        }

                        if (positionY.IsAnimated)
                        {
                            ApplyScalarKeyFrameAnimation(context, positionY, container, targetPropertyName: "Offset.Y");
                        }
                    }
                    else
                    {
                        // TODO - when we support spatial bezier CubicBezierFunction3, we can enable this. For now this
                        //        may result in a CubicBezierFunction2 being applied to the Vector3 Offset property.
                        //ApplyVector3KeyFrameAnimation(context, (AnimatableVector3)position, container, "Offset");
                        offsetExpression = CreateExpressionAnimation(container.IsShape
                            ? (Expr)Expr.Vector2(
                                Expr.Subtract(Expr.Scalar("my.Position.X"), Expr.Scalar(initialAnchor.X)),
                                Expr.Subtract(Expr.Scalar("my.Position.Y"), Expr.Scalar(initialAnchor.Y)))
                            : (Expr)Expr.Vector3(
                                Expr.Subtract(Expr.Scalar("my.Position.X"), Expr.Scalar(initialAnchor.X)),
                                Expr.Subtract(Expr.Scalar("my.Position.Y"), Expr.Scalar(initialAnchor.Y))));

                        positionIsAnimated = true;
                    }
                }
                else
                {
                    offsetExpression = CreateExpressionAnimation(container.IsShape
                        ? (Expr)Expr.Vector2(
                            Expr.Subtract(Expr.Scalar("my.Position.X"), Expr.Scalar(initialAnchor.X)),
                            Expr.Subtract(Expr.Scalar("my.Position.Y"), Expr.Scalar(initialAnchor.Y)))
                        : (Expr)Expr.Vector3(
                            Expr.Subtract(Expr.Scalar("my.Position.X"), Expr.Scalar(initialAnchor.X)),
                            Expr.Subtract(Expr.Scalar("my.Position.Y"), Expr.Scalar(initialAnchor.Y))));
                }
            }
            else if (anchorIsAnimated)
            {
                // Only anchor is animated.
                offsetExpression = CreateExpressionAnimation(container.IsShape
                    ? (Expr)Expr.Vector2(
                        Expr.Subtract(Expr.Scalar(initialPosition.X), Expr.Scalar("my.Anchor.X")),
                        Expr.Subtract(Expr.Scalar(initialPosition.Y), Expr.Scalar("my.Anchor.Y")))
                    : (Expr)Expr.Vector3(
                        Expr.Subtract(Expr.Scalar(initialPosition.X), Expr.Scalar("my.Anchor.X")),
                        Expr.Subtract(Expr.Scalar(initialPosition.Y), Expr.Scalar("my.Anchor.Y"))));
            }

            if (!positionIsAnimated && !anchorIsAnimated)
            {
                // Position and Anchor are static. No expression needed.
                container.Offset = Vector2DefaultIsZero(initialPosition - initialAnchor);
            }

            // Position is a Lottie-only concept. It offsets the object relative to the Anchor.
            if (positionIsAnimated)
            {
                container.Properties.InsertVector2("Position", initialPosition);

                if (xyzPosition != null)
                {
                    if (positionX.IsAnimated)
                    {
                        ApplyScalarKeyFrameAnimation(context, positionX, container.Properties, targetPropertyName: "Position.X");
                    }

                    if (positionY.IsAnimated)
                    {
                        ApplyScalarKeyFrameAnimation(context, positionY, container.Properties, targetPropertyName: "Position.Y");
                    }
                }
                else
                {
                    ApplyVector2KeyFrameAnimation(context, position3, container.Properties, "Position");
                }
            }

            if (offsetExpression != null)
            {
                offsetExpression.SetReferenceParameter("my", container);
                StartExpressionAnimation(container, nameof(container.Offset), offsetExpression);
            }
        }

        void StartExpressionAnimation(CompositionObject compObject, string target, ExpressionAnimation animation)
        {
            // Start the animation.
            compObject.StartAnimation(target, animation);
        }

        void StartKeyframeAnimation(CompositionObject compObject, string target, KeyFrameAnimation_ animation, double scale = 1, double offset = 0)
        {
            Debug.Assert(offset >= 0, "Precondition");
            Debug.Assert(scale <= 1, "Precondition");
            Debug.Assert(animation.KeyFrameCount > 0, "Precondition");

            // Start the animation ...
            compObject.StartAnimation(target, animation);

            // ... but pause it immediately so that it doesn't react to time. Instead, bind
            // its progress to the progress of the composition.
            var controller = compObject.TryGetAnimationController(target);
            controller.Pause();

            // Bind it to the root visual's Progress property, scaling and offsetting if necessary.
            var key = new ScaleAndOffset(scale, offset);
            if (!_progressBindingAnimations.TryGetValue(key, out var bindingAnimation))
            {
                bindingAnimation = CreateExpressionAnimation(ScaledAndOffsetRootProgress(scale, offset));
                bindingAnimation.SetReferenceParameter(RootName, _rootVisual);
                _progressBindingAnimations.Add(key, bindingAnimation);
            }

            // Bind the controller's Progress with a single Progress property on the scene root.
            // The Progress property provides the time reference for the animation.
            controller.StartAnimation("Progress", bindingAnimation);
        }

        void ApplyScalarKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<double> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
            => ApplyScaledScalarKeyFrameAnimation(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        void ApplyPercentKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<double> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
            => ApplyScaledScalarKeyFrameAnimation(context, value, 0.01, targetObject, targetPropertyName, longDescription, shortDescription);

        void ApplyScaledScalarKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<double> value,
            double scale,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription,
            string shortDescription)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                CreateScalarKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, (float)(val * scale), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyColorKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<LottieData.Color> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                CreateColorKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, Color(val), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyPathKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Sequence<BezierSegment>> value,
            SolidColorFill.PathFillType fillType,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                CreatePathKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(
                    progress,
                    CompositionPathFromPathGeometry(
                        val,
                        fillType,

                        // Turn off the optimization that replaces cubic beziers with
                        // segments because it may result in different numbers of
                        // control points in each path in the keyframes.
                        optimizeLines: false),
                    easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyVector2KeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Vector3> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
            => ApplyScaledVector2KeyFrameAnimation(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        void ApplyScaledVector2KeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Vector3> value,
            double scale,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                CreateVector2KeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, Vector2(val * scale), easing),
                (ca, progress, expr, easing) => ca.InsertExpressionKeyFrame(progress, scale != 1 ? Scale(expr, scale) : expr.ToString(), easing),
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyScaledVector3KeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Vector3> value,
            double scale,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                CreateVector3KeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, Vector3(val) * (float)scale, easing),
                (ca, progress, expr, easing) => ca.InsertExpressionKeyFrame(progress, scale != 1 ? Scale(expr, scale).ToString() : expr.ToString(), easing),
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void GenericCreateCompositionKeyFrameAnimation<TCA, T>(
            TranslationContext context,
            in TrimmedAnimatable<T> value,
            Func<TCA> compositionAnimationFactory,
            Action<TCA, float, T, CompositionEasingFunction> insertKeyFrame,
            Action<TCA, float, Expr, CompositionEasingFunction> insertExpressionKeyFrame,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription,
            string shortDescription)
                where TCA : KeyFrameAnimation_
                where T : IEquatable<T>
        {
            Debug.Assert(value.IsAnimated, "Precondition");

            var compositionAnimation = compositionAnimationFactory();

            if (_addDescriptions)
            {
                Describe(compositionAnimation, longDescription ?? targetPropertyName, shortDescription ?? targetPropertyName);
            }

            compositionAnimation.Duration = _lc.Duration;

            var trimmedKeyFrames = value.KeyFrames;

            var firstKeyFrame = trimmedKeyFrames[0];
            var lastKeyFrame = trimmedKeyFrames[trimmedKeyFrames.Length - 1];

            var animationStartTime = firstKeyFrame.Frame;
            var animationEndTime = lastKeyFrame.Frame;

            if (firstKeyFrame.Frame > context.StartTime)
            {
                // The first key frame is after the start of the animation. Create an extra keyframe at 0 to
                // set and hold an initial value until the first specified keyframe.
                // Note that we could set an initial value for the property instead of using a key frame,
                // but seeing as we're creating key frames anyway, it will be fewer operations to
                // just use a first key frame and not set an initial value
                insertKeyFrame(compositionAnimation, 0 /* progress */, firstKeyFrame.Value, CreateStepThenHoldEasingFunction() /*easing*/);

                animationStartTime = context.StartTime;
            }

            if (lastKeyFrame.Frame < context.EndTime)
            {
                // The last key frame is before the end of the animation.
                animationEndTime = context.EndTime;
            }

            var animationDuration = animationEndTime - animationStartTime;

            // The Math.Min is to deal with rounding errors that cause the scale to be slightly more than 1.
            var scale = Math.Min(context.DurationInFrames / animationDuration, 1.0);
            var offset = (context.StartTime - animationStartTime) / animationDuration;

            // Insert the keyframes with the progress adjusted so the first keyframe is at 0 and the remaining
            // progress values are scaled appropriately.
            var previousValue = firstKeyFrame.Value;
            var previousProgress = 0.0 - KeyFrameProgressEpsilon;
            var rootReferenceRequired = false;
            var previousKeyFrameWasExpression = false;
            string progressMappingProperty = null;
            ScalarKeyFrameAnimation progressMappingAnimation = null;

            foreach (var keyFrame in trimmedKeyFrames)
            {
                var adjustedProgress = (keyFrame.Frame - animationStartTime) / animationDuration;

                if (keyFrame.SpatialControlPoint1 != default(Vector3) || keyFrame.SpatialControlPoint2 != default(Vector3))
                {
                    // TODO - should only be on Vector3. In which case, should they be on Animatable, or on something else?
                    if (typeof(T) != typeof(Vector3))
                    {
                        Debug.WriteLine("Spatial control point on non-Vector3 type");
                    }

                    var cp0 = Vector2((Vector3)(object)previousValue);
                    var cp1 = Vector2(keyFrame.SpatialControlPoint1);
                    var cp2 = Vector2(keyFrame.SpatialControlPoint2);
                    var cp3 = Vector2((Vector3)(object)keyFrame.Value);
                    CubicBezierFunction2 cb;

                    switch (keyFrame.Easing.Type)
                    {
                        case Easing.EasingType.Linear:
                        case Easing.EasingType.CubicBezier:
                            if (progressMappingProperty == null)
                            {
                                progressMappingProperty = $"t{_tCounter++}";
                                progressMappingAnimation = CreateScalarKeyFrameAnimation();
                                progressMappingAnimation.Duration = _lc.Duration;
                            }
#if LinearEasingOnSpatialBeziers
                            cb = CubicBezierFunction.Create(cp0, (cp0 + cp1), (cp2 + cp3), cp3, GetRemappedProgress(previousProgress, adjustedProgress));
#else
                            cb = CubicBezierFunction2.Create(
                                cp0,
                                cp0 + cp1,
                                cp2 + cp3,
                                cp3,
                                Expr.Scalar($"{RootName}.{progressMappingProperty}"));
#endif
                            break;
                        case Easing.EasingType.Hold:
                            // Holds should never have interesting cubic beziers, so replace with one that is definitely colinear.
                            cb = CubicBezierFunction2.Zero;
                            break;
                        default:
                            throw new InvalidOperationException();
                    }

                    if (cb.IsEquivalentToLinear || adjustedProgress == 0
#if !SpatialBeziers
                        || true
#endif
                        )
                    {
                        // The cubic bezier function is equivalent to a line, or its value starts at the start of the animation, so no need
                        // for an expression to do spatial beziers on it. Just use a regular key frame.
                        if (previousKeyFrameWasExpression)
                        {
                            // Ensure the previous expression doesn't continue being evaluated during the current keyframe.
                            // This is necessary because the expression is only defined from the previous progress to the current progress.
                            insertKeyFrame(compositionAnimation, (float)previousProgress + KeyFrameProgressEpsilon, previousValue, CreateStepThenHoldEasingFunction());
                        }

                        // The easing for a keyframe at 0 is unimportant, so always use Hold.
                        var easing = adjustedProgress == 0 ? HoldEasing.Instance : keyFrame.Easing;

                        insertKeyFrame(compositionAnimation, (float)adjustedProgress, keyFrame.Value, CreateCompositionEasingFunction(easing));
                        previousKeyFrameWasExpression = false;
                    }
                    else
                    {
                        // Expression key frame needed for a spatial bezier.

                        // Make the progress value just before the requested progress value
                        // so that there is room to add a key frame just after this to hold
                        // the final value. This is necessary so that the expression we're about
                        // to add won't get evaluated during the following segment.
                        if (adjustedProgress > 0)
                        {
                            adjustedProgress -= KeyFrameProgressEpsilon;
                        }

#if !LinearEasingOnSpatialBeziers
                        // Add an animation to map from progress to t over the range of this key frame.
                        if (previousProgress > 0)
                        {
                            progressMappingAnimation.InsertKeyFrame((float)previousProgress + KeyFrameProgressEpsilon, 0, CreateStepThenHoldEasingFunction());
                        }

                        progressMappingAnimation.InsertKeyFrame((float)adjustedProgress, 1, CreateCompositionEasingFunction(keyFrame.Easing));
#endif
                        insertExpressionKeyFrame(
                            compositionAnimation,
                            (float)adjustedProgress,
                            cb,                                 // Expression.
                            CreateStepThenHoldEasingFunction());    // Jump to the final value so the expression is evaluated all the way through.

                        // Note that a reference to the root Visual is required by the animation because it
                        // is used in the expression.
                        rootReferenceRequired = true;
                        previousKeyFrameWasExpression = true;
                    }
                }
                else
                {
                    if (previousKeyFrameWasExpression)
                    {
                        // Ensure the previous expression doesn't continue being evaluated during the current keyframe.
                        insertKeyFrame(compositionAnimation, (float)previousProgress + KeyFrameProgressEpsilon, previousValue, CreateStepThenHoldEasingFunction());
                    }

                    insertKeyFrame(compositionAnimation, (float)adjustedProgress, keyFrame.Value, CreateCompositionEasingFunction(keyFrame.Easing));
                    previousKeyFrameWasExpression = false;
                }

                previousValue = keyFrame.Value;
                previousProgress = adjustedProgress;
            }

            if (previousKeyFrameWasExpression && previousProgress < 1)
            {
                // Add a keyframe to hold the final value. Otherwise the expression on the last keyframe
                // will get evaluated outside the bounds of its keyframe.
                insertKeyFrame(compositionAnimation, (float)previousProgress + KeyFrameProgressEpsilon, (T)(object)previousValue, CreateStepThenHoldEasingFunction());
            }

            // Add a reference to the root Visual if needed (i.e. if an expression keyframe was added).
            if (rootReferenceRequired)
            {
                compositionAnimation.SetReferenceParameter(RootName, _rootVisual);
            }

            // Start the animation scaled and offset.
            StartKeyframeAnimation(targetObject, targetPropertyName, compositionAnimation, scale, offset);

            // Start the animation that maps from the Progress property to a t value for use by the spatial beziers.
            if (progressMappingAnimation != null && progressMappingAnimation.KeyFrameCount > 0)
            {
                _rootVisual.Properties.InsertScalar(progressMappingProperty, 0);
                StartKeyframeAnimation(_rootVisual, progressMappingProperty, progressMappingAnimation, scale, offset);
            }
        }

        float GetInPointProgress(TranslationContext context)
        {
            var result = (context.Layer.InPoint - context.StartTime) / context.DurationInFrames;

            return (float)result;
        }

        float GetOutPointProgress(TranslationContext context)
        {
            var result = (context.Layer.OutPoint - context.StartTime) / context.DurationInFrames;

            return (float)result;
        }

        static string Scale(Expr expression, double scale)
        {
            return Expr.Multiply(Expr.Scalar(scale), expression).ToString();
        }

        sealed class TimeRemap : Expr
        {
            readonly double _tRangeLow;
            readonly double _tRangeHigh;
            readonly Expr _t;

            internal TimeRemap(double tRangeLow, double tRangeHigh, Expr t)
            {
                if (tRangeLow >= tRangeHigh)
                {
                    throw new ArgumentException();
                }

                _tRangeLow = tRangeLow;
                _tRangeHigh = tRangeHigh;
                _t = t;
            }

            protected override Expr Simplify()
            {
                // Adjust t and (1-t) based on the given range. This will make T vary between
                // 0..1 over the duration of the keyframe.
                return Multiply(
                    Scalar(1 / (_tRangeHigh - _tRangeLow)),
                    Subtract(_t, Scalar(_tRangeLow)));
            }

            protected override string CreateExpressionString() => Simplified.ToString();

            public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Scalar);
        }

        // Returns the name of a variable on the root property set that advances linearly from 0 to 1 over the
        // given range of Progress.
        TimeRemap GetRemappedProgress(double tRangeLow, double tRangeHigh) =>
            new TimeRemap(tRangeLow, tRangeHigh, ExpressionFactory.RootProgress);

        int _tCounter = 0;

        struct RemappedProgressParameters
        {
            internal double TRangeLow;
            internal double TRangeHigh;
            internal Vector3 ControlPoint1;
            internal Vector3 ControlPoint2;
        }

        readonly Dictionary<RemappedProgressParameters, Expr> _remappedProgressExpressions = new Dictionary<RemappedProgressParameters, Expr>();

        // Returns the name of a variable on the root property set that advances from 0 to 1 over the
        // given range of Progress, using the given cubic bezier easing.
        Expr GetRemappedProgress(double tRangeLow, double tRangeHigh, Vector3 controlPoint1, Vector3 controlPoint2)
        {
            // Use an existing property if a matching one has already been created.
            var parameters = new RemappedProgressParameters { TRangeLow = tRangeLow, TRangeHigh = tRangeHigh, ControlPoint1 = controlPoint1, ControlPoint2 = controlPoint2 };
            if (!_remappedProgressExpressions.TryGetValue(parameters, out Expr result))
            {
                // Create a property to hold the value.
                var propertyName = $"t{_tCounter++}";
                _rootVisual.Properties.InsertScalar(propertyName, 0);

                // Create the remapping expression.
                var remap = new TimeRemap(tRangeLow, tRangeHigh, ExpressionFactory.RootProgress);

                // Create a cubic bezier function to map the time using the given control points.
                var oneOne = Vector2(1);
                var easing = CubicBezierFunction2.Create(Vector2(0), Vector2(controlPoint1), Vector2(controlPoint2), oneOne, remap);

                var animation = CreateExpressionAnimation(Expr.Scalar($"({easing}).Y"));
                animation.SetReferenceParameter(RootName, _rootVisual);
                StartExpressionAnimation(_rootVisual, propertyName, animation);
                result = Expr.Scalar($"{RootName}.{propertyName}");
                _remappedProgressExpressions.Add(parameters, result);
            }

            return result;
        }

        static SolidColorFill.PathFillType GetPathFillType(SolidColorFill fill) => fill == null ? SolidColorFill.PathFillType.EvenOdd : fill.FillType;

        CompositionPath CompositionPathFromPathGeometry(
            Sequence<BezierSegment> pathGeometry,
            SolidColorFill.PathFillType fillType,
            bool optimizeLines)
        {
            // CompositionPaths can be shared by many SpriteShapes.
            if (!_compositionPaths.TryGetValue((pathGeometry, fillType, optimizeLines), out var result))
            {
                result = new CompositionPath(CreateWin2dPathGeometry(pathGeometry, fillType, Sn.Matrix3x2.Identity, optimizeLines));
                _compositionPaths.Add((pathGeometry, fillType, optimizeLines), result);
            }

            return result;
        }

        TrimmedAnimatable<Color> MultiplyAnimatableColorByAnimatableOpacityPercent(
            in TrimmedAnimatable<Color> color,
            in TrimmedAnimatable<double> opacityPercent)
        {
            if (color.IsAnimated)
            {
                if (opacityPercent.IsAnimated)
                {
                    // TOOD: multiply animations to produce a new set of key frames for the opacity-multiplied color.
                    _issues.OpacityAndColorAnimatedTogetherIsNotSupported();
                    return color;
                }
                else
                {
                    // Multiply the color animation by the single opacity value.
                    var opacityPercentInitialValue = opacityPercent.InitialValue;
                    return new TrimmedAnimatable<Color>(
                        color.Context,
                        initialValue: MultiplyColorByOpacityPercent(color.InitialValue, opacityPercent.InitialValue),
                        keyFrames: color.KeyFrames.SelectToSpan(kf =>
                            new KeyFrame<Color>(
                                kf.Frame,
                                MultiplyColorByOpacityPercent(kf.Value, opacityPercentInitialValue),
                                kf.SpatialControlPoint1,
                                kf.SpatialControlPoint2,
                                kf.Easing)));
                }
            }
            else if (opacityPercent.IsAnimated)
            {
                // Color is not animated.
                return MultiplyColorByAnimatableOpacityPercent(color.InitialValue, opacityPercent);
            }
            else
            {
                // Multiply color by opacity
                var nonAnimatedMultipliedColor = MultiplyColorByOpacityPercent(color.InitialValue, opacityPercent.InitialValue);
                return new TrimmedAnimatable<Color>(color.Context, nonAnimatedMultipliedColor);
            }
        }

        TrimmedAnimatable<Color> MultiplyColorByAnimatableOpacityPercent(
            Color color,
            in TrimmedAnimatable<double> opacityPercent)
        {
            if (!opacityPercent.IsAnimated)
            {
                return new TrimmedAnimatable<Color>(opacityPercent.Context, MultiplyColorByOpacityPercent(color, opacityPercent.InitialValue));
            }
            else
            {
                // Multiply the single color value by the opacity animation.
                return new TrimmedAnimatable<Color>(
                    opacityPercent.Context,
                    initialValue: MultiplyColorByOpacityPercent(color, opacityPercent.InitialValue),
                    keyFrames: opacityPercent.KeyFrames.SelectToSpan(kf =>
                        new KeyFrame<Color>(
                            kf.Frame,
                            MultiplyColorByOpacityPercent(color, kf.Value),
                            kf.SpatialControlPoint1,
                            kf.SpatialControlPoint2,
                            kf.Easing)));
            }
        }

        static Color MultiplyColorByOpacityPercent(Color color, double opacityPercent)
            => opacityPercent == 100 ? color
            : LottieData.Color.FromArgb(color.A * opacityPercent / 100, color.R, color.G, color.B);

        CompositionColorBrush CreateAnimatedColorBrush(TranslationContext context, Color color, in TrimmedAnimatable<double> opacityPercent)
        {
            var multipliedColor = MultiplyColorByAnimatableOpacityPercent(color, opacityPercent);
            return CreateAnimatedColorBrush(context, multipliedColor);
        }

        CompositionColorBrush CreateAnimatedColorBrush(TranslationContext context, in TrimmedAnimatable<Color> color, in TrimmedAnimatable<double> opacityPercent)
        {
            var multipliedColor = MultiplyAnimatableColorByAnimatableOpacityPercent(color, opacityPercent);
            return CreateAnimatedColorBrush(context, multipliedColor);
        }

        CompositionColorBrush CreateAnimatedColorBrush(TranslationContext context, in TrimmedAnimatable<Color> color)
        {
            if (color.IsAnimated)
            {
                var result = CreateColorBrush(color.InitialValue);

                ApplyColorKeyFrameAnimation(
                    context,
                    color,
                    result,
                    nameof(result.Color),
                    "Color",
                    null);
                return result;
            }
            else
            {
                return CreateNonAnimatedColorBrush(color.InitialValue);
            }
        }

        CompositionColorBrush CreateNonAnimatedColorBrush(Color color)
        {
            if (color.A == 0)
            {
                // Transparent brushes that are never animated are all equivalent.
                color = LottieData.Color.TransparentBlack;
            }

            if (!_nonAnimatedColorBrushes.TryGetValue(color, out var result))
            {
                result = CreateColorBrush(color);
                _nonAnimatedColorBrushes.Add(color, result);
            }

            return result;
        }

        // Implements IDisposable.Dispose(). Currently not needed but will be required
        // if this class needs to hold onto any IDisposable objects.
        void IDisposable.Dispose()
        {
        }

        CompositionEllipseGeometry CreateEllipseGeometry()
        {
            return _c.CreateEllipseGeometry();
        }

        CompositionPathGeometry CreatePathGeometry()
        {
            return _c.CreatePathGeometry();
        }

        CompositionPathGeometry CreatePathGeometry(CompositionPath path)
        {
            return _c.CreatePathGeometry(path);
        }

        CompositionRectangleGeometry CreateRectangleGeometry()
        {
            return _c.CreateRectangleGeometry();
        }

        CompositionRoundedRectangleGeometry CreateRoundedRectangleGeometry()
        {
            return _c.CreateRoundedRectangleGeometry();
        }

        CompositionColorBrush CreateColorBrush(Color color)
        {
            return _c.CreateColorBrush(Color(color));
        }

        CompositionEasingFunction CreateCompositionEasingFunction(Easing easingFunction)
        {
            if (easingFunction == null)
            {
                return null;
            }

            switch (easingFunction.Type)
            {
                case Easing.EasingType.Linear:
                    return CreateLinearEasingFunction();
                case Easing.EasingType.CubicBezier:
                    return CreateCubicBezierEasingFunction((CubicBezierEasing)easingFunction);
                case Easing.EasingType.Hold:
                    return CreateHoldThenStepEasingFunction();
                default:
                    throw new InvalidOperationException();
            }
        }

        LinearEasingFunction CreateLinearEasingFunction()
        {
            if (_linearEasingFunction == null)
            {
                _linearEasingFunction = _c.CreateLinearEasingFunction();
            }

            return _linearEasingFunction;
        }

        CubicBezierEasingFunction CreateCubicBezierEasingFunction(CubicBezierEasing cubicBezierEasing)
        {
            if (!_cubicBezierEasingFunctions.TryGetValue(cubicBezierEasing, out var result))
            {
                // WinComp does not support control points with components > 1. Clamp the values to 1.
                var controlPoint1 = ClampedVector2(cubicBezierEasing.ControlPoint1);
                var controlPoint2 = ClampedVector2(cubicBezierEasing.ControlPoint2);

                result = _c.CreateCubicBezierEasingFunction(controlPoint1, controlPoint2);
                _cubicBezierEasingFunctions.Add(cubicBezierEasing, result);
            }

            return result;
        }

        // Returns an easing function that holds its initial value and steps to the final value at the end.
        StepEasingFunction CreateHoldThenStepEasingFunction()
        {
            if (_holdStepEasingFunction == null)
            {
                _holdStepEasingFunction = _c.CreateStepEasingFunction(1);
                _holdStepEasingFunction.IsFinalStepSingleFrame = true;
            }

            return _holdStepEasingFunction;
        }

        // Returns an easing function that steps immediately to its final value.
        StepEasingFunction CreateStepThenHoldEasingFunction()
        {
            if (_jumpStepEasingFunction == null)
            {
                _jumpStepEasingFunction = _c.CreateStepEasingFunction(1);
                _jumpStepEasingFunction.IsInitialStepSingleFrame = true;
            }

            return _jumpStepEasingFunction;
        }

        ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation()
        {
            return _c.CreateScalarKeyFrameAnimation();
        }

        ColorKeyFrameAnimation CreateColorKeyFrameAnimation()
        {
            return _c.CreateColorKeyFrameAnimation();
        }

        PathKeyFrameAnimation CreatePathKeyFrameAnimation()
        {
            return _c.CreatePathKeyFrameAnimation();
        }

        Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation()
        {
            return _c.CreateVector2KeyFrameAnimation();
        }

        Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation()
        {
            return _c.CreateVector3KeyFrameAnimation();
        }

        InsetClip CreateInsetClip()
        {
            return _c.CreateInsetClip();
        }

        CompositionGeometricClip CreateCompositionGeometricClip()
        {
            return _c.CreateCompositionGeometricClip();
        }

        CompositionContainerShape CreateContainerShape()
        {
            return _c.CreateContainerShape();
        }

        ContainerVisual CreateContainerVisual()
        {
            return _c.CreateContainerVisual();
        }

        SpriteVisual CreateSpriteVisual()
        {
            return _c.CreateSpriteVisual();
        }

        ShapeVisual CreateShapeVisual()
        {
            return _c.CreateShapeVisual();
        }

        CompositionSpriteShape CreateSpriteShape()
        {
            return _c.CreateSpriteShape();
        }

        ExpressionAnimation CreateExpressionAnimation(Expr expression)
        {
            return _c.CreateExpressionAnimation(expression);
        }

        CompositionSurfaceBrush CreateSurfaceBrush(ICompositionSurface surface)
        {
            return _c.CreateSurfaceBrush(surface);
        }

        static CompositionStrokeCap StrokeCap(SolidColorStroke.LineCapType lineCapType)
        {
            switch (lineCapType)
            {
                case SolidColorStroke.LineCapType.Butt:
                    return CompositionStrokeCap.Flat;
                case SolidColorStroke.LineCapType.Round:
                    return CompositionStrokeCap.Round;
                case SolidColorStroke.LineCapType.Projected:
                    return CompositionStrokeCap.Square;
                default:
                    throw new InvalidOperationException();
            }
        }

        static CompositionStrokeLineJoin StrokeLineJoin(SolidColorStroke.LineJoinType lineJoinType)
        {
            switch (lineJoinType)
            {
                case SolidColorStroke.LineJoinType.Bevel:
                    return CompositionStrokeLineJoin.Bevel;
                case SolidColorStroke.LineJoinType.Miter:
                    return CompositionStrokeLineJoin.Miter;
                case SolidColorStroke.LineJoinType.Round:
                default:
                    return CompositionStrokeLineJoin.Round;
            }
        }

        static CanvasFilledRegionDetermination FilledRegionDetermination(SolidColorFill.PathFillType fillType)
        {
            return (fillType == SolidColorFill.PathFillType.Winding) ? CanvasFilledRegionDetermination.Winding : CanvasFilledRegionDetermination.Alternate;
        }

        static CanvasGeometryCombine GeometryCombine(MergePaths.MergeMode mergeMode)
        {
            switch (mergeMode)
            {
                case LottieData.MergePaths.MergeMode.Add: return CanvasGeometryCombine.Union;
                case LottieData.MergePaths.MergeMode.Subtract: return CanvasGeometryCombine.Exclude;
                case LottieData.MergePaths.MergeMode.Intersect: return CanvasGeometryCombine.Intersect;

                // TODO - find out what merge should be - maybe should be a Union.
                case LottieData.MergePaths.MergeMode.Merge:
                case LottieData.MergePaths.MergeMode.ExcludeIntersections: return CanvasGeometryCombine.Xor;
                default:
                    throw new InvalidOperationException();
            }
        }

        // Sets a description on an object.
        void Describe(IDescribable obj, string longDescription, string shortDescription = null)
        {
            Debug.Assert(_addDescriptions, "This method should only be called if the user wanted descriptions");
            Debug.Assert(obj.ShortDescription == null, "Descriptions should never get set more than once");
            Debug.Assert(obj.LongDescription == null, "Descriptions should never get set more than once");

            obj.ShortDescription = shortDescription ?? longDescription;
            obj.LongDescription = longDescription;
        }

        static WinCompData.Wui.Color Color(LottieData.Color color) =>
            WinCompData.Wui.Color.FromArgb((byte)(255 * color.A), (byte)(255 * color.R), (byte)(255 * color.G), (byte)(255 * color.B));

        static float Float(double value) => (float)value;

        static float? FloatDefaultIsZero(double value) => value == 0 ? null : (float?)value;

        static float? FloatDefaultIsOne(double value) => value == 1 ? null : (float?)value;

        static float PercentF(double value) => (float)value / 100F;

        static Sn.Vector2 Vector2(LottieData.Vector3 vector3) => Vector2(vector3.X, vector3.Y);

        static Sn.Vector2 Vector2(LottieData.Vector2 vector2) => Vector2(vector2.X, vector2.Y);

        static Sn.Vector2 Vector2(double x, double y) => new Sn.Vector2((float)x, (float)y);

        static Sn.Vector2 Vector2(float x, float y) => new Sn.Vector2(x, y);

        static Sn.Vector2 Vector2(float x) => new Sn.Vector2(x, x);

        static Sn.Vector2? Vector2DefaultIsOne(LottieData.Vector3 vector2) =>
            vector2.X == 1 && vector2.Y == 1 ? null : (Sn.Vector2?)Vector2(vector2);

        static Sn.Vector2? Vector2DefaultIsZero(Sn.Vector2 vector2) =>
            vector2.X == 0 && vector2.Y == 0 ? null : (Sn.Vector2?)vector2;

        static Sn.Vector2 ClampedVector2(LottieData.Vector3 vector3) => ClampedVector2((float)vector3.X, (float)vector3.Y);

        static Sn.Vector2 ClampedVector2(float x, float y) => Vector2(Clamp(x, 0, 1), Clamp(y, 0, 1));

        static Sn.Vector3 Vector3(double x, double y, double z) => new Sn.Vector3((float)x, (float)y, (float)z);

        static Sn.Vector3 Vector3(LottieData.Vector3 vector3) => new Sn.Vector3((float)vector3.X, (float)vector3.Y, (float)vector3.Z);

        static Sn.Vector3? Vector3DefaultIsZero(Sn.Vector2 vector2) =>
                    vector2.X == 0 && vector2.Y == 0 ? null : (Sn.Vector3?)Vector3(vector2);

        static Sn.Vector3? Vector3DefaultIsOne(Sn.Vector3 vector3) =>
                    vector3.X == 1 && vector3.Y == 1 && vector3.Z == 1 ? null : (Sn.Vector3?)vector3;

        static Sn.Vector3? Vector3DefaultIsOne(LottieData.Vector3 vector3) =>
                    Vector3DefaultIsOne(new Sn.Vector3((float)vector3.X, (float)vector3.Y, (float)vector3.Z));

        static Sn.Vector3 Vector3(Sn.Vector2 vector2) => Vector3(vector2.X, vector2.Y, 0);

        static float Clamp(float value, float min, float max)
        {
            Debug.Assert(min <= max, "Precondition");
            return Math.Min(Math.Max(min, value), max);
        }

        // A pair of doubles used as a key in a dictionary.
        sealed class ScaleAndOffset
        {
            readonly double _scale;
            readonly double _offset;

            internal ScaleAndOffset(double scale, double offset)
            {
                _scale = scale;
                _offset = offset;
            }

            public override bool Equals(object obj)
            {
                var other = obj as ScaleAndOffset;
                if (other == null)
                {
                    return false;
                }

                return other._scale == _scale && other._offset == _offset;
            }

            public override int GetHashCode() => _scale.GetHashCode() ^ _offset.GetHashCode();
        }

        // A type that is either a CompositionType or a Visual.
        readonly struct ShapeOrVisual
        {
            readonly CompositionObject _shapeOrVisual;

            ShapeOrVisual(CompositionObject shapeOrVisual)
            {
                _shapeOrVisual = shapeOrVisual;
            }

            public static implicit operator ShapeOrVisual(CompositionShape shape) => new ShapeOrVisual(shape);

            public static implicit operator ShapeOrVisual(Visual visual) => new ShapeOrVisual(visual);

            public static implicit operator CompositionObject(ShapeOrVisual shapeOrVisual) => shapeOrVisual._shapeOrVisual;
        }
    }
}
