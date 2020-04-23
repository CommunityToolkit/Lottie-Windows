// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Use the simple algorithm for combining trim paths. We're not sure of the correct semantics
// for multiple trim paths, so it's possible this is actually the most correct.
#define SimpleTrimPathCombining

// NOTE: The AnimationController.Progress value is used one frame later than expressions,
// so to keep everything in sync if one animation is using a controller tied
// to the uber Progress property, then no animation can be tied to the Progress
// property without going through a controller.

// Use Win2D to create paths from geometry combines when merging shape layers.
//#define PreCombineGeometries
#if DEBUG
// For diagnosing issues, give nothing a clip.
//#define NoClipping
// For diagnosing issues, give nothing scale.
//#define NoScaling
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgc;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp.ExpressionFactory;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.Expression;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates a <see cref="LottieComposition"/> to an equivalent <see cref="Visual"/>.
    /// </summary>
    /// <remarks>See https://helpx.adobe.com/pdf/after_effects_reference.pdf"/> for the
    /// After Effects semantics.</remarks>
#if PUBLIC
    public
#endif
    sealed class LottieToWinCompTranslator : IDisposable
    {
        // Identifies the Lottie metadata in TranslationResult.SourceMetadata.
        static readonly Guid s_lottieMetadataKey = new Guid("EA3D6538-361A-4B1C-960D-50A6C35563A5");

        // Generates a sequence of ints from 0..int.MaxValue. Used to attach indexes to sequences using Zip.
        static readonly IEnumerable<int> PositiveInts = Enumerable.Range(0, int.MaxValue);

        readonly LottieComposition _lc;
        readonly TranslationIssues _issues;
        readonly bool _addDescriptions;
        readonly bool _translatePropertyBindings;
        readonly CompositionObjectFactory _c;
        readonly ContainerVisual _rootVisual;
        readonly Dictionary<ScaleAndOffset, ExpressionAnimation> _progressBindingAnimations = new Dictionary<ScaleAndOffset, ExpressionAnimation>();
        readonly Optimizer _lottieDataOptimizer = new Optimizer();

        // Holds CubicBezierEasingFunctions for reuse when they have the same parameters.
        readonly Dictionary<CubicBezierEasing, CubicBezierEasingFunction> _cubicBezierEasingFunctions = new Dictionary<CubicBezierEasing, CubicBezierEasingFunction>();

        // Holds ColorBrushes that are not animated and can therefore be reused.
        readonly Dictionary<Color, CompositionColorBrush> _nonAnimatedColorBrushes = new Dictionary<Color, CompositionColorBrush>();

        // Paths are shareable.
        readonly Dictionary<(Sequence<BezierSegment>, ShapeFill.PathFillType, bool), CompositionPath> _compositionPaths = new Dictionary<(Sequence<BezierSegment>, ShapeFill.PathFillType, bool), CompositionPath>();

        // The names that are bound to properties (such as the Color of a SolidColorFill).
        // Keep track of them here so that codegen can generate properties for them.
        readonly PropertyBindings _propertyBindings = new PropertyBindings();

        readonly uint _targetUapVersion;

        // Factory used for creating properties that map from the Progress value of the animated visual
        // to another value. These are used to create properties that are required by cubic bezier
        // expressions used for spatial beziers.
        readonly ProgressMapFactory _progressMapFactory = new ProgressMapFactory();

        // Property set used for property bindings for themed Lotties.
        CompositionPropertySet _themePropertySet;

        /// <summary>
        /// The lowest UAP version for which the translator can produce code. Code from the translator
        /// will never be compatible with UAP versions less than this.
        /// </summary>
        // 7 (1809 10.0.17763.0) because that is the version in which Shapes became usable enough for Lottie.
        public static uint MinimumTargetUapVersion { get; } = 7;

        /// <summary>
        /// Gets the name of the property on the resulting <see cref="Visual"/> that controls the progress
        /// of the animation. Setting this property (directly or with an animation)
        /// between 0 and 1 controls the position of the animation.
        /// </summary>
        public static string ProgressPropertyName => "Progress";

        LottieToWinCompTranslator(
            LottieComposition lottieComposition,
            Compositor compositor,
            bool strictTranslation,
            bool addDescriptions,
            bool translatePropertyBindings,
            uint targetUapVersion)
        {
            _lc = lottieComposition;
            _targetUapVersion = targetUapVersion;
            _c = new CompositionObjectFactory(compositor, targetUapVersion);
            _issues = new TranslationIssues(strictTranslation);
            _addDescriptions = addDescriptions;
            _translatePropertyBindings = translatePropertyBindings;

            // Create the root.
            _rootVisual = _c.CreateContainerVisual();
            if (_addDescriptions)
            {
                Describe(_rootVisual, "The root of the composition.", string.Empty);
                Name(_rootVisual, "Root");
            }

            // Add the master progress property to the visual.
            _rootVisual.Properties.InsertScalar(ProgressPropertyName, 0);
        }

        /// <summary>
        /// Attempts to translates the given <see cref="LottieComposition"/>.
        /// </summary>
        /// <param name="lottieComposition">The <see cref="LottieComposition"/> to translate.</param>
        /// <param name="targetUapVersion">The version of UAP that the translator will ensure compatibility with. Must be >= 7.</param>
        /// <param name="strictTranslation">If true, throw an exception if translation issues are found.</param>
        /// <param name="addCodegenDescriptions">Add descriptions to objects for comments on generated code.</param>
        /// <param name="translatePropertyBindings">Translate the special property binding language in Lottie object
        /// names and create bindings to <see cref="CompositionPropertySet"/> values.</param>
        /// <returns>The result of the translation.</returns>
        public static TranslationResult TryTranslateLottieComposition(
            LottieComposition lottieComposition,
            uint targetUapVersion,
            bool strictTranslation,
            bool addCodegenDescriptions,
            bool translatePropertyBindings)
        {
            // Set up the translator.
            using (var translator = new LottieToWinCompTranslator(
                lottieComposition,
                new Compositor(),
                strictTranslation: strictTranslation,
                addDescriptions: addCodegenDescriptions,
                translatePropertyBindings: translatePropertyBindings,
                targetUapVersion))
            {
                // Translate the Lottie content to a Composition graph.
                translator.Translate();

                var rootVisual = translator._rootVisual;

                var resultRequiredUapVersion = translator._c.HighestUapVersionUsed;

                // See if the version is compatible with what the caller requested.
                if (targetUapVersion < resultRequiredUapVersion)
                {
                    // We couldn't translate it and meet the requirement for the requested minimum version.
                    rootVisual = null;
                }

                // Add the metadata.
                var sourceMetadata = new Dictionary<Guid, object>();

                // Metadata from the source.
                sourceMetadata.Add(s_lottieMetadataKey, GetSourceMetadata(lottieComposition));

                // The list of property binding names.
                translator._propertyBindings.AddToSourceMetadata(sourceMetadata);

                return new TranslationResult(
                    rootVisual: rootVisual,
                    translationIssues: translator._issues.GetIssues().Select(i =>
                        new TranslationIssue(code: i.Code, description: i.Description)),
                    minimumRequiredUapVersion: resultRequiredUapVersion,
                    sourceMetadata);
            }
        }

        static LottieCompositionMetadata GetSourceMetadata(LottieComposition lottieComposition)
            => new LottieCompositionMetadata(
                lottieComposition.Name,
                lottieComposition.FramesPerSecond,
                lottieComposition.InPoint,
                lottieComposition.OutPoint,
                lottieComposition.Markers.Select(m => (m.Name, m.Frame, m.DurationInFrames)));

        void Translate()
        {
            var context = new TranslationContext.Root(_lc);
            AddTranslatedLayersToContainerVisual(_rootVisual, context, compositionDescription: "Root");

            AddRemappedProgressAnimations();

            if (_lc.Is3d)
            {
                _issues.ThreeDIsNotSupported();
            }
        }

        // Adds the progress remapping variables and animations that are needed for spatial beziers.
        void AddRemappedProgressAnimations()
        {
            foreach (var (name, scale, offset, keyframes) in _progressMapFactory.GetVariables())
            {
                _rootVisual.Properties.InsertScalar(name, 0);
                var animation = _c.CreateScalarKeyFrameAnimation();
                animation.Duration = _lc.Duration;
                animation.SetReferenceParameter(RootName, _rootVisual);
                foreach (var keyframe in keyframes)
                {
                    animation.InsertKeyFrame(keyframe.rangeStart, 0, _c.CreateStepThenHoldEasingFunction());
                    animation.InsertKeyFrame(keyframe.rangeEnd, 1, _c.CreateCompositionEasingFunction(keyframe.easing));
                }

                StartKeyframeAnimation(_rootVisual.Properties, name, animation, scale, offset);
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
                        Describe(translatedLayer, $"{layer.Type} layer: {layer.Name}");
                    }
                }
            }

            // Go through the layers and compose matte layer and layer to be matted into
            // the resulting visuals. Any layer that is not a matte or matted layer is
            // simply returned unmodified.
            var compositionGraphs = ComposeMattedLayers(context, translatedLayers).ToArray();

            // Layers are translated into either a Visual tree or a Shape tree. Convert the list of Visual and
            // Shape roots to a list of Visual roots by wrapping the Shape trees in ShapeVisuals.
            var translatedAsVisuals = VisualsAndShapesToVisuals(context, compositionGraphs);

            var containerChildren = container.Children;
            foreach (var translatedVisual in translatedAsVisuals)
            {
                containerChildren.Add(translatedVisual);
            }
        }

        // Combines 1 or more CompositionSubGraphs as ComositionShape subgraphs under a ShapeVisual.
        Visual GetVisualForCompositionSubGraphs(TranslationContext context, IReadOnlyList<CompositionSubGraph> shapes)
        {
            Debug.Assert(shapes.All(s => s.IsShape), "Precondition");

            var compositionShapes = shapes.Select(s => (shape: s.GetShapeRoot(), subgraph: s)).Where(s => s.shape != null).ToArray();

            switch (compositionShapes.Length)
            {
                case 0: return null;
                case 1:
                    // There's only 1 shape. Get it to translate directly to a Visual.
                    return compositionShapes[0].subgraph.GetVisualRoot(context.Size);
                default:
                    // There are multiple contiguous shapes. Group them under a ShapeVisual.
                    // The ShapeVisual has to have a size (it clips to its size).
                    // TODO - if the shape graphs share the same opacity and/or visiblity, get them
                    //        to translate without opacity/visiblity and we'll pull those
                    //        into the Visual.
                    var shapeVisual = _c.CreateShapeVisualWithChild(compositionShapes[0].shape, context.Size);

                    if (_addDescriptions)
                    {
                        Describe(shapeVisual, "Layer aggregator");
                    }

                    for (var i = 1; i < compositionShapes.Length; i++)
                    {
                        shapeVisual.Shapes.Add(compositionShapes[i].shape);
                    }

                    return shapeVisual;
            }
        }

        // Takes a list of Visuals and Shapes and returns a list of Visuals by combining all direct
        // sibling shapes together into a ShapeVisual.
        IEnumerable<Visual> VisualsAndShapesToVisuals(TranslationContext context, IEnumerable<CompositionSubGraph> items)
        {
            var shapeSubGraphs = new List<CompositionSubGraph>();

            foreach (var item in items)
            {
                if (item.IsShape)
                {
                    shapeSubGraphs.Add(item);
                }
                else
                {
                    if (shapeSubGraphs.Count > 0)
                    {
                        var visual = GetVisualForCompositionSubGraphs(context, shapeSubGraphs);

                        if (visual != null)
                        {
                            yield return visual;
                        }

                        shapeSubGraphs.Clear();
                    }

                    var visualRoot = item.GetVisualRoot(context.Size);
                    if (visualRoot != null)
                    {
                        yield return visualRoot;
                    }
                }
            }

            if (shapeSubGraphs.Count > 0)
            {
                var visual = GetVisualForCompositionSubGraphs(context, shapeSubGraphs);
                if (visual != null)
                {
                    yield return visual;
                }
            }
        }

        // Walk the collection of layer data and for each pair of matte layer and matted layer, compose them and return a visual
        // with the composed result. All other items are not touched.
        IEnumerable<CompositionSubGraph> ComposeMattedLayers(TranslationContext context, IEnumerable<(CompositionSubGraph translatedLayer, Layer layer)> items)
        {
            // Save off the visual for the layer to be matted when we encounter it. The very next
            // layer is the matte layer.
            Visual mattedVisual = null;
            Layer.MatteType matteType = Layer.MatteType.None;

            // NOTE: The items appear in reverse order from how they appear in the original Lottie file.
            // This means that the layer to be matted appears right before the layer that is the matte.
            foreach (var item in items)
            {
                var layerIsMattedLayer = false;
                layerIsMattedLayer = item.layer.LayerMatteType != Layer.MatteType.None;

                Visual visual = null;

                if (item.translatedLayer.IsShape)
                {
                    // If the layer is a shape then we need to wrap it
                    // in a shape visual so that it can be used for matte
                    // composition.
                    if (layerIsMattedLayer || mattedVisual != null)
                    {
                        visual = item.translatedLayer.GetVisualRoot(context.Size);
                    }
                }
                else
                {
                    visual = item.translatedLayer.GetVisualRoot(context.Size);
                }

                if (visual != null)
                {
                    // The layer to be matted comes first. The matte layer is the very next layer.
                    if (layerIsMattedLayer)
                    {
                        mattedVisual = visual;
                        matteType = item.layer.LayerMatteType;
                    }
                    else if (mattedVisual != null)
                    {
                        var compositedMatteVisual = TranslateMatteLayer(context, visual, mattedVisual, matteType == Layer.MatteType.Invert);
                        mattedVisual = null;
                        matteType = Layer.MatteType.None;
                        yield return compositedMatteVisual;
                    }
                    else
                    {
                        // Return the visual that was not a matte layer or a layer to be matted.
                        yield return new CompositionSubGraph.FromVisual(this, visual);
                    }
                }
                else
                {
                    // Return the shape which does not participate in mattes.
                    yield return item.translatedLayer;
                }
            }
        }

        // Takes the paths for the given masks and adds them as shapes on the maskContainerShape.
        // Requires at least one Mask.
        void TranslateAndAddMaskPaths(
            TranslationContext context,
            ReadOnlySpan<Mask> masks,
            CompositionContainerShape resultContainer)
        {
            Debug.Assert(masks.Length > 0, "Precondition");

            var maskMode = masks[0].Mode;

            // Translate the mask paths
            foreach (var mask in masks)
            {
                if (mask.Inverted)
                {
                    _issues.MaskWithInvertIsNotSupported();

                    // Mask inverted is not yet supported. Skip this mask.
                    continue;
                }

                if (mask.Opacity.IsAnimated ||
                    !mask.Opacity.InitialValue.IsOpaque)
                {
                    _issues.MaskWithAlphaIsNotSupported();

                    // Opacity on masks is not supported. Skip this mask.
                    continue;
                }

                switch (mask.Mode)
                {
                    case Mask.MaskMode.None:
                        // Ignore None masks. They are just a way to disable a Mask in After Effects.
                        continue;
                    default:
                        if (mask.Mode != maskMode)
                        {
                            // Every mask must have the same mode.
                            throw new InvalidOperationException();
                        }

                        break;
                }

                var compositionPathGeometry = _c.CreatePathGeometry();

                ApplyPath(context, compositionPathGeometry, mask.Points, ShapeFill.PathFillType.EvenOdd);

                var maskSpriteShape = _c.CreateSpriteShape();
                maskSpriteShape.Geometry = compositionPathGeometry;

                // The mask geometry needs to be colored with something so that it can be used
                // as a mask.
                maskSpriteShape.FillBrush = CreateNonAnimatedColorBrush(LottieData.Color.Black);

                resultContainer.Shapes.Add(maskSpriteShape);
            }
        }

        // Translate a mask into shapes for a shape visual. The mask is applied to the visual to be masked
        // using the VisualSurface. The VisualSurface can take the rendered contents of a visual tree and
        // use it as a brush. The final masked result is achieved by taking the visual to be masked, putting
        // it into a VisualSurface, then taking the mask and putting that in a VisualSurface and then combining
        // the result with a composite effect.
        Visual TranslateAndApplyMasksForLayer(
            TranslationContext context,
            Visual visualToMask)
        {
            var result = visualToMask;
            var layer = context.Layer;

            if (layer.Masks.Length > 0)
            {
                if (layer.Masks.Length == 1)
                {
                    // Common case for masks: exactly one mask.
                    var masks = layer.Masks.Slice(0, 1);

                    switch (masks[0].Mode)
                    {
                        // If there's only 1 mask, Difference and Intersect act the same as Add.
                        case Mask.MaskMode.Add:
                        case Mask.MaskMode.Difference:
                        case Mask.MaskMode.Intersect:
                        case Mask.MaskMode.None:
                            // Composite using the mask.
                            result = TranslateAndApplyMasks(context, masks, result, CanvasComposite.DestinationIn);
                            break;

                        case Mask.MaskMode.Subtract:
                            // Composite using the mask.
                            result = TranslateAndApplyMasks(context, masks, result, CanvasComposite.DestinationOut);
                            break;

                        default:
                            _issues.MaskWithUnsupportedMode(masks[0].Mode.ToString());
                            break;
                    }
                }
                else
                {
                    // Uncommon case for masks: multiple masks.
                    // Get the contiguous segments of masks that have the same mode, create a shape tree for each
                    // segment, and composite the shape trees.
                    // The goal here is to use the smallest possible number of composites.
                    // 1) Get the masks that have the same mode and are next to each other in the list of masks.
                    // 2) Translate the masks to a ShapeVisual.
                    // 3) Composite each ShapeVisual with the previous ShapeVisual.
                    foreach (var maskSegmentWithSameMode in EnumerateMaskListSegments(layer.Masks.ToArray()))
                    {
                        // Every mask in the segment has the same mode or None. The first mask is never None.
                        var masksWithSameMode = layer.Masks.Slice(maskSegmentWithSameMode.index, maskSegmentWithSameMode.count);
                        switch (masksWithSameMode[0].Mode)
                        {
                            case Mask.MaskMode.Add:
                                // Composite using the mask, and apply to what has been already masked.
                                result = TranslateAndApplyMasks(context, masksWithSameMode, result, CanvasComposite.DestinationIn);
                                break;
                            case Mask.MaskMode.Subtract:
                                // Composite using the mask, and apply to what has been already masked.
                                result = TranslateAndApplyMasks(context, masksWithSameMode, result, CanvasComposite.DestinationOut);
                                break;
                            default:
                                // Only Add, Subtract, and None modes are currently supported.
                                _issues.MaskWithUnsupportedMode(masksWithSameMode[0].Mode.ToString());
                                break;
                        }
                    }
                }
            }

            return result;
        }

        // Enumerates the segments of Masks with the same MaskMode.
        static IEnumerable<(int index, int count)> EnumerateMaskListSegments(Mask[] masks)
        {
            int i;

            // Find the first non-None mask.
            for (i = 0; i < masks.Length && masks[i].Mode == Mask.MaskMode.None; i++)
            {
                continue;
            }

            if (i == masks.Length)
            {
                // There were only None masks in the list.
                yield break;
            }

            var currentMode = masks[i].Mode;
            var segmentIndex = i;

            for (; i < masks.Length; i++)
            {
                var mode = masks[i].Mode;
                if (mode != currentMode && mode != Mask.MaskMode.None)
                {
                    // Switching to a new mask mode. Output the segment for the previous mode.
                    yield return (segmentIndex, i - segmentIndex);

                    currentMode = mode;
                    segmentIndex = i;
                }
            }

            // Output the last segment it's not empty.
            if (segmentIndex < i)
            {
                yield return (segmentIndex, i - segmentIndex);
            }
        }

        // Translates a list of masks to a Visual which can be used to mask another Visual.
        Visual TranslateMasks(TranslationContext context, ReadOnlySpan<Mask> masks)
        {
            Debug.Assert(!masks.IsEmpty, "Precondition");

            // Duplicate the transform chain used on the Layer being masked so
            // that the mask correctly overlays the Layer.
            if (!TryCreateContainerShapeTransformChain(
                context,
                out var containerShapeMaskRootNode,
                out var containerShapeMaskContentNode))
            {
                // The layer is never visible. This should have been discovered already.
                throw new InvalidOperationException();
            }

            // Create the mask tree from the masks.
            TranslateAndAddMaskPaths(context, masks, containerShapeMaskContentNode);

            var result = _c.CreateShapeVisualWithChild(containerShapeMaskRootNode, context.Size);

            if (_addDescriptions)
            {
                Describe(result, "Masks");
            }

            return result;
        }

        Visual TranslateAndApplyMasks(TranslationContext context, ReadOnlySpan<Mask> masks, Visual visualToMask, CanvasComposite compositeMode)
        {
            Debug.Assert(!masks.IsEmpty, "Precondition");

            if (IsUapApiAvailable(nameof(CompositionVisualSurface), versionDependentFeatureDescription: "Mask"))
            {
                var maskShapeVisual = TranslateMasks(context, masks);

                return CompositeVisuals(
                                    source: maskShapeVisual,
                                    destination: visualToMask,
                                    size: context.Size,
                                    offset: Sn.Vector2.Zero,
                                    compositeMode: compositeMode);
            }
            else
            {
                // We can't mask, so just return the unmasked visual as a compromise.
                return visualToMask;
            }
        }

        // Translate a matte layer and the layer to be matted into the composited resulting brush.
        // This brush will be used to paint a sprite visual. The brush is created by using a mask brush
        // which will use the matted layer as a source and the matte layer as an alpha mask.
        // A visual tree is turned into a brush by using the CompositionVisualSurface.
        CompositionSubGraph TranslateMatteLayer(
            TranslationContext context,
            Visual matteLayer,
            Visual mattedLayer,
            bool invert)
        {
            // Calculate the context size which we will use as the size of the images we want to use
            // for the matte content and the content to be matted.
            var contextSize = context.Size;

            if (IsUapApiAvailable(nameof(CompositionVisualSurface), versionDependentFeatureDescription: "Matte"))
            {
                var matteLayerVisualSurface = _c.CreateVisualSurface();
                matteLayerVisualSurface.SourceVisual = matteLayer;
                matteLayerVisualSurface.SourceSize = contextSize;
                var matteSurfaceBrush = _c.CreateSurfaceBrush(matteLayerVisualSurface);

                var mattedLayerVisualSurface = _c.CreateVisualSurface();
                mattedLayerVisualSurface.SourceVisual = mattedLayer;
                mattedLayerVisualSurface.SourceSize = contextSize;
                var mattedSurfaceBrush = _c.CreateSurfaceBrush(mattedLayerVisualSurface);

                return new CompositionSubGraph.FromVisual(this, CompositeVisuals(
                            matteLayer,
                            mattedLayer,
                            contextSize,
                            Sn.Vector2.Zero,
                            invert ? CanvasComposite.DestinationOut : CanvasComposite.DestinationIn));
            }
            else
            {
                // We can't translate the matteing. Just return the layer that needed to be matted as a compromise.
                return new CompositionSubGraph.FromVisual(this, mattedLayer);
            }
        }

        // Combines two visual trees using a CompositeEffect. This is used for Masks and Mattes.
        // The way that the trees are combined is determined by the composite mode. The composition works as follows:
        // +--------------+
        // | SpriteVisual | -- Has the final composited result.
        // +--------------+
        //     ^
        //     |
        // +--------------+
        // | EffectBrush  | -- Composition effect brush allows the composite effect result to be used as a brush.
        // +--------------+
        //     ^
        //     *
        //     *
        //     *
        // +-----------------+
        // | CompositeEffect | -- Composite effect does the work to combine the contents
        // +-----------------+    of the visual surfaces.
        //     |
        //     |  +---------+
        //     -> | Sources |
        //        +---------+
        //         ^   ^
        //         |   |
        //         |   |
        //         |   +----------------------+
        //         |   | Source Surface Brush | -- Surface brush that will paint with the output of the visual surface
        //         |   +----------------------+    that has the source visual assigned to it.
        //         |               |
        //         |               |  +-----------------------+
        //         |               -> | Source VisualSurface  | -- The visual surface captures the renderable contents of its source visual.
        //         |                  +-----------------------+
        //         |                               |
        //         |                               |  +------------------------+
        //         |                               -> | Source Contents Visual | -- The source visual.
        //         |                                  +------------------------+
        //         |
        //         |
        //         |
        //         +--------------------------+
        //         | Destination SurfaceBrush | -- Surface brush that will paint with the output of the visual surface
        //         +--------------------------+    that has the destination visual assigned to it.
        //                         |
        //                         |  +---------------------------+
        //                         -> | Destination VisualSurface | -- The visual surface captures the renderable contents of its source visual.
        //                            +---------------------------+
        //                                         |
        //                                         |  +-----------------------------+
        //                                         -> | Destination Contents Visual | -- The source visual.
        //                                            +-----------------------------+
        SpriteVisual CompositeVisuals(
            Visual source,
            Visual destination,
            Sn.Vector2 size,
            Sn.Vector2 offset,
            CanvasComposite compositeMode)
        {
            // The visual surface captures the contents of a visual and displays it in a brush.
            // If the visual has an offset, it will not be captured by the visual surface.
            // To capture any offsets we add an intermediate parent container visual so that
            // the visual we want captured by the visual surface has a parent to use as the
            // origin of its offsets.
            var sourceIntermediateParent = _c.CreateContainerVisual();

            // Because this is the root of a tree, the inherited BorderMode is Hard.
            // We want it to be Soft in order to enable anti-aliasing.
            // Note that the border mode for trees that are attached to the desktop do not
            // need to have their BorderMode set as they inherit Soft from the desktop.
            sourceIntermediateParent.BorderMode = CompositionBorderMode.Soft;
            sourceIntermediateParent.Children.Add(source);

            var destinationIntermediateParent = _c.CreateContainerVisual();

            // Because this is the root of a tree, the inherited BorderMode is Hard.
            // We want it to be Soft in order to enable anti-aliasing.
            // Note that the border mode for trees that are attached to the desktop do not
            // need to have their BorderMode set as they inherit Soft from the desktop.
            destinationIntermediateParent.BorderMode = CompositionBorderMode.Soft;
            destinationIntermediateParent.Children.Add(destination);

            var sourceVisualSurface = _c.CreateVisualSurface();
            sourceVisualSurface.SourceVisual = sourceIntermediateParent;
            sourceVisualSurface.SourceSize = Vector2DefaultIsZero(size);
            sourceVisualSurface.SourceOffset = Vector2DefaultIsZero(offset);
            var sourceVisualSurfaceBrush = _c.CreateSurfaceBrush(sourceVisualSurface);

            var destinationVisualSurface = _c.CreateVisualSurface();
            destinationVisualSurface.SourceVisual = destinationIntermediateParent;
            destinationVisualSurface.SourceSize = Vector2DefaultIsZero(size);
            destinationVisualSurface.SourceOffset = Vector2DefaultIsZero(offset);
            var destinationVisualSurfaceBrush = _c.CreateSurfaceBrush(destinationVisualSurface);

            var compositeEffect = new CompositeEffect();
            compositeEffect.Mode = compositeMode;

            compositeEffect.Sources.Add(new CompositionEffectSourceParameter("destination"));
            compositeEffect.Sources.Add(new CompositionEffectSourceParameter("source"));

            var compositionEffectFactory = _c.CreateEffectFactory(compositeEffect);
            var effectBrush = compositionEffectFactory.CreateBrush();

            effectBrush.SetSourceParameter("destination", destinationVisualSurfaceBrush);
            effectBrush.SetSourceParameter("source", sourceVisualSurfaceBrush);

            var compositedVisual = _c.CreateSpriteVisual();
            compositedVisual.Brush = effectBrush;
            compositedVisual.Size = size;
            compositedVisual.Offset = Vector3(offset.X, offset.Y, 0);

            return compositedVisual;
        }

        // Translates a Lottie layer into null a Visual or a Shape.
        // Note that ShapeVisual clips to its size.
        CompositionSubGraph TranslateLayer(TranslationContext parentContext, Layer layer)
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

        // Returns a chain with a Visual at the top and a CompositionContainerShape at the bottom.
        // The nodes in between implement the transforms for the layer.
        // This chain is used when a Shape tree needs to be expressed as a Visual tree. We take
        // advantage of this case to do layer opacity and visibility using Visual nodes rather
        // than pushing the opacity to the leaves and using Scale animations to do visibility.
        bool TryCreateShapeVisualTransformChain(
            TranslationContext context,
            out ContainerVisual rootNode,
            out CompositionContainerShape contentsNode)
        {
            // Create containers for the contents in the layer.
            // The rootNode is the root for the layer.
            //
            //     +---------------+
            //     |   rootNode    |-- Root node, optionally with opacity animation for the layer.
            //     +---------------+
            //            ^
            //            |
            //     +-----------------+
            //     |  visiblityNode  |-- Optional visiblity node (only used if the visiblity is animated).
            //     +-----------------+
            //            ^
            //            |
            //     +-----------------+
            //     |  opacityNode    |-- Optional opacity node.
            //     +-----------------+
            //            ^
            //            |
            //     +-----------------+
            //     |   ShapeVisual   |-- Start of the Shape tree.
            //     +-----------------+
            //            ^
            //            |
            //     +-------------------+
            //     | rootTransformNode |--Transform without opacity (inherited from root ancestor of the transform tree).
            //     +-------------------+
            //            ^
            //            |
            //     + - - - - - - - - - - - - +
            //     | other transforms nodes  |--Transform without opacity (inherited from the transform tree)
            //     + - - - - - - - - - - - - +
            //            ^
            //            |
            //     +-------------------+
            //     | leafTransformNode |--Transform without opacity defined on the layer.
            //     +-------------------+
            //        ^        ^
            //        |        |
            // +---------+ +---------+
            // | content | | content | ...
            // +---------+ +---------+
            //

            // Get the opacity of the layer.
            var layerOpacity = context.TrimAnimatable(context.Layer.Transform.Opacity);

            // Convert the layer's in point and out point into absolute progress (0..1) values.
            var inProgress = GetInPointProgress(context);
            var outProgress = GetOutPointProgress(context);

            if (inProgress > 1 || outProgress <= 0 || inProgress >= outProgress || layerOpacity.AlwaysEquals(LottieData.Opacity.Transparent))
            {
                // The layer is never visible. Don't create anything.
                rootNode = null;
                contentsNode = null;
                return false;
            }

            rootNode = _c.CreateContainerVisual();
            ContainerVisual contentsVisual = rootNode;

            // Implement opacity for the layer.
            InsertOpacityVisualIntoTransformChain(context, layerOpacity, ref rootNode);

            // Implement visibility for the layer.
            InsertVisibilityVisualIntoTransformChain(context, inProgress, outProgress, ref rootNode);

            // Create the transforms chain.
            TranslateTransformOnContainerShapeForLayer(context, context.Layer, out var transformsRoot, out contentsNode);

            // Create the shape visual.
            var shapeVisual = _c.CreateShapeVisualWithChild(transformsRoot, context.Size);

            if (_addDescriptions)
            {
                Describe(shapeVisual, $"Shape tree root for layer: {context.Layer.Name}");
            }

            contentsVisual.Children.Add(shapeVisual);

            return true;
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

            // Get the opacity of the layer.
            var layerOpacity = context.TrimAnimatable(context.Layer.Transform.Opacity);

            // Convert the layer's in point and out point into absolute progress (0..1) values.
            var inProgress = GetInPointProgress(context);
            var outProgress = GetOutPointProgress(context);

            if (inProgress > 1 || outProgress <= 0 || inProgress >= outProgress || layerOpacity.AlwaysEquals(LottieData.Opacity.Transparent))
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
                var visibilityNode = _c.CreateContainerShape();
                visibilityNode.Shapes.Add(transformsRoot);
                rootNode = visibilityNode;

                if (_addDescriptions)
                {
                    Describe(visibilityNode, $"Layer: {context.Layer.Name}");
                }

                // Animate between Scale(0,0) and Scale(1,1).
                var visibilityAnimation = _c.CreateVector2KeyFrameAnimation();

                Name(visibilityAnimation, "ShapeVisibilityAnimation");

                if (inProgress > 0)
                {
                    // Set initial value to be non-visible (default is visible).
                    visibilityNode.Scale = Sn.Vector2.Zero;
                    visibilityAnimation.InsertKeyFrame(inProgress, Sn.Vector2.One, _c.CreateHoldThenStepEasingFunction());
                }

                if (outProgress < 1)
                {
                    visibilityAnimation.InsertKeyFrame(outProgress, Sn.Vector2.Zero, _c.CreateHoldThenStepEasingFunction());
                }

                visibilityAnimation.Duration = _lc.Duration;
                StartKeyframeAnimation(visibilityNode, nameof(visibilityNode.Scale), visibilityAnimation);
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
            var layerOpacity = context.TrimAnimatable(context.Layer.Transform.Opacity);

            // Convert the layer's in point and out point into absolute progress (0..1) values.
            var inProgress = GetInPointProgress(context);
            var outProgress = GetOutPointProgress(context);

            if (inProgress > 1 || outProgress <= 0 || inProgress >= outProgress || layerOpacity.AlwaysEquals(LottieData.Opacity.Transparent))
            {
                // The layer is never visible. Don't create anything.
                rootNode = null;
                contentsNode = null;
                return false;
            }

            // Create the transforms chain.
            TranslateTransformOnContainerVisualForLayer(context, context.Layer, out rootNode, out contentsNode);

            // Implement opacity for the layer.
            InsertOpacityVisualIntoTransformChain(context, layerOpacity, ref rootNode);

            // Implement visibility for the layer.
            InsertVisibilityVisualIntoTransformChain(context, inProgress, outProgress, ref rootNode);

            return true;
        }

        void InsertVisibilityVisualIntoTransformChain(
            TranslationContext context,
            float inProgress,
            float outProgress,
            ref ContainerVisual root)
        {
            // Implement the Visibility for the layer. Only needed if the layer becomes visible after
            // the LottieComposition's in point, or it becomes invisible before the LottieComposition's out point.
            if (inProgress > 0 || outProgress < 1)
            {
                // Insert a new node to control visibility at the top of the chain.
                var visibilityNode = _c.CreateContainerVisual();
                visibilityNode.Children.Add(root);
                root = visibilityNode;

                var visibilityAnimation = _c.CreateBooleanKeyFrameAnimation();
                if (inProgress > 0)
                {
                    // Set initial value to be non-visible.
                    visibilityNode.IsVisible = false;
                    visibilityAnimation.InsertKeyFrame(inProgress, true);
                }

                if (outProgress < 1)
                {
                    visibilityAnimation.InsertKeyFrame(outProgress, false);
                }

                visibilityAnimation.Duration = _lc.Duration;
                StartKeyframeAnimation(visibilityNode, "IsVisible", visibilityAnimation);
            }
        }

        void InsertOpacityVisualIntoTransformChain(
            TranslationContext context,
            in TrimmedAnimatable<Opacity> opacity,
            ref ContainerVisual root)
        {
            // Implement opacity for the layer.
            if (opacity.IsAnimated || opacity.InitialValue < LottieData.Opacity.Opaque)
            {
                // Insert a new node to control opacity at the top of the chain.
                var opacityNode = _c.CreateContainerVisual();

                if (_addDescriptions)
                {
                    Describe(opacityNode, $"Opacity for layer: {context.Layer.Name}");
                }

                opacityNode.Children.Add(root);
                root = opacityNode;

                if (opacity.IsAnimated)
                {
                    ApplyOpacityKeyFrameAnimation(context, opacity, opacityNode, "Opacity", "Layer opacity animation");
                }
                else
                {
                    opacityNode.Opacity = Opacity(opacity.InitialValue);
                }
            }
        }

        CompositionSubGraph TranslateImageLayer(TranslationContext.For<ImageLayer> context)
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

            var content = _c.CreateSpriteVisual();
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

            var imageBrush = _c.CreateSurfaceBrush(surface);
            content.Brush = imageBrush;

            if (_addDescriptions)
            {
                Describe(surface, $"{context.Layer.Name}, {imageAssetWidth}x{imageAssetHeight}");
            }

            return new CompositionSubGraph.FromVisual(this, containerVisualRootNode);
        }

        CompositionSubGraph TranslatePreCompLayerToVisual(TranslationContext.For<PreCompLayer> context)
        {
            // Create the transform chain.
            if (!TryCreateContainerVisualTransformChain(context, out var rootNode, out var contentsNode))
            {
                // The layer is never visible.
                return null;
            }

            var result = _c.CreateContainerVisual();

#if !NoClipping
            // PreComps must clip to their size.
            // Create another ContainerVisual to apply clipping to.
            var clippingNode = _c.CreateContainerVisual();
            contentsNode.Children.Add(clippingNode);
            contentsNode = clippingNode;
            contentsNode.Clip = _c.CreateInsetClip();
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

            // Add mask if the layer has masks.
            // This must be done after all children are added to the content node.
            bool layerHasMasks = false;
#if !NoClipping
            layerHasMasks = context.Layer.Masks.Any();
#endif
            if (layerHasMasks)
            {
                var compositedVisual = TranslateAndApplyMasksForLayer(context, rootNode);

                result.Children.Add(compositedVisual);
            }
            else
            {
                result.Children.Add(rootNode);
            }

            return new CompositionSubGraph.FromVisual(this, result);
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
            static readonly Animatable<Opacity> s_Opaque = new Animatable<Opacity>(LottieData.Opacity.Opaque, null);

            readonly LottieToWinCompTranslator _owner;

            internal ShapeStroke Stroke { get; private set; }

            internal ShapeFill Fill { get; private set; }

            internal TrimPath TrimPath { get; private set; }

            internal RoundedCorner RoundedCorner { get; private set; }

            internal Transform Transform { get; private set; }

            // Opacity is not part of the Lottie context for shapes. But because WinComp
            // doesn't support opacity on shapes, the opacity is inherited from
            // the Transform and passed through to the brushes here.
            internal CompositeOpacity Opacity { get; private set; } = CompositeOpacity.Opaque;

            internal ShapeContentContext(LottieToWinCompTranslator owner)
            {
                _owner = owner;
            }

            internal void UpdateFromStack(Stack<ShapeLayerContent> stack)
            {
                while (stack.Count > 0)
                {
                    var popped = stack.Peek();
                    switch (popped.ContentType)
                    {
                        case ShapeContentType.LinearGradientFill:
                        case ShapeContentType.RadialGradientFill:
                        case ShapeContentType.SolidColorFill:
                            Fill = ComposeFills(Fill, (ShapeFill)popped);
                            break;

                        case ShapeContentType.LinearGradientStroke:
                        case ShapeContentType.RadialGradientStroke:
                        case ShapeContentType.SolidColorStroke:
                            Stroke = ComposeStrokes(Stroke, (ShapeStroke)popped);
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
                    Opacity = Opacity,
                    Transform = Transform,
                };
            }

            internal void UpdateOpacityFromTransform(TranslationContext context, Transform transform)
            {
                if (transform == null)
                {
                    return;
                }

                Opacity = Opacity.ComposedWith(context.TrimAnimatable(transform.Opacity));
            }

            // Only used when translating geometries. Layers use an extra Shape or Visual to
            // apply the transform, but geometries need to take the transform into account when
            // they're created.
            internal void SetTransform(Transform transform)
            {
                Transform = transform;
            }

            ShapeFill ComposeFills(ShapeFill a, ShapeFill b)
            {
                if (a == null)
                {
                    return b;
                }
                else if (b == null)
                {
                    return a;
                }

                if (a.FillKind != b.FillKind)
                {
                    _owner._issues.MultipleFillsIsNotSupported();
                    return b;
                }

                switch (a.FillKind)
                {
                    case ShapeFill.ShapeFillKind.SolidColor:
                        return ComposeSolidColorFills((SolidColorFill)a, (SolidColorFill)b);
                }

                _owner._issues.MultipleFillsIsNotSupported();
                return b;
            }

            SolidColorFill ComposeSolidColorFills(SolidColorFill a, SolidColorFill b)
            {
                if (!b.Color.IsAnimated && !b.Opacity.IsAnimated)
                {
                    if (b.Opacity.InitialValue == LottieData.Opacity.Opaque &&
                        b.Color.InitialValue.A == 1)
                    {
                        // b overrides a.
                        return b;
                    }
                    else if (b.Opacity.InitialValue.IsTransparent || b.Color.InitialValue.A == 0)
                    {
                        // b is transparent, so a wins.
                        return a;
                    }
                }

                _owner._issues.MultipleFillsIsNotSupported();
                return b;
            }

            ShapeStroke ComposeStrokes(ShapeStroke a, ShapeStroke b)
            {
                if (a == null)
                {
                    return b;
                }
                else if (b == null)
                {
                    return a;
                }

                if (a.StrokeKind != b.StrokeKind)
                {
                    _owner._issues.MultipleStrokesIsNotSupported();
                    return b;
                }

                switch (a.StrokeKind)
                {
                    case ShapeStroke.ShapeStrokeKind.SolidColor:
                        return ComposeSolidColorStrokes((SolidColorStroke)a, (SolidColorStroke)b);
                    case ShapeStroke.ShapeStrokeKind.LinearGradient:
                        return ComposeLinearGradientStrokes((LinearGradientStroke)a, (LinearGradientStroke)b);
                    case ShapeStroke.ShapeStrokeKind.RadialGradient:
                        return ComposeRadialGradientStrokes((RadialGradientStroke)a, (RadialGradientStroke)b);
                    default:
                        throw new InvalidOperationException();
                }
            }

            LinearGradientStroke ComposeLinearGradientStrokes(LinearGradientStroke a, LinearGradientStroke b)
            {
                Debug.Assert(a != null && b != null, "Precondition");

                if (!a.StrokeWidth.IsAnimated && !b.StrokeWidth.IsAnimated &&
                    a.Opacity.AlwaysEquals(LottieData.Opacity.Opaque) && b.Opacity.AlwaysEquals(LottieData.Opacity.Opaque))
                {
                    if (a.StrokeWidth.InitialValue >= b.StrokeWidth.InitialValue)
                    {
                        // a occludes b, so b can be ignored.
                        return a;
                    }
                }

                _owner._issues.MultipleStrokesIsNotSupported();
                return a;
            }

            RadialGradientStroke ComposeRadialGradientStrokes(RadialGradientStroke a, RadialGradientStroke b)
            {
                Debug.Assert(a != null && b != null, "Precondition");

                if (!a.StrokeWidth.IsAnimated && !b.StrokeWidth.IsAnimated &&
                    a.Opacity.AlwaysEquals(LottieData.Opacity.Opaque) && b.Opacity.AlwaysEquals(LottieData.Opacity.Opaque))
                {
                    if (a.StrokeWidth.InitialValue >= b.StrokeWidth.InitialValue)
                    {
                        // a occludes b, so b can be ignored.
                        return a;
                    }
                }

                _owner._issues.MultipleStrokesIsNotSupported();
                return a;
            }

            SolidColorStroke ComposeSolidColorStrokes(SolidColorStroke a, SolidColorStroke b)
            {
                Debug.Assert(a != null && b != null, "Precondition");

                if (!a.StrokeWidth.IsAnimated && !b.StrokeWidth.IsAnimated &&
                    !a.DashPattern.Any() && !b.DashPattern.Any() &&
                    a.Opacity.AlwaysEquals(LottieData.Opacity.Opaque) && b.Opacity.AlwaysEquals(LottieData.Opacity.Opaque))
                {
                    if (a.StrokeWidth.InitialValue >= b.StrokeWidth.InitialValue)
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

                if (!a.StartTrim.IsAnimated && !a.StartTrim.IsAnimated && !a.Offset.IsAnimated)
                {
                    // a is not animated.
                    if (!b.StartTrim.IsAnimated && !b.StartTrim.IsAnimated && !b.Offset.IsAnimated)
                    {
                        // Both are not animated.
                        if (a.StartTrim.InitialValue == b.EndTrim.InitialValue)
                        {
                            // a trims out everything. b is unnecessary.
                            return a;
                        }
                        else if (b.StartTrim.InitialValue == b.EndTrim.InitialValue)
                        {
                            // b trims out everything. a is unnecessary.
                            return b;
                        }
                        else if (a.StartTrim.InitialValue.Value == 0 && a.EndTrim.InitialValue.Value == 1 && a.Offset.InitialValue.Degrees == 0)
                        {
                            // a is trimming nothing. a is unnecessary.
                            return b;
                        }
                        else if (b.StartTrim.InitialValue.Value == 0 && b.EndTrim.InitialValue.Value == 1 && b.Offset.InitialValue.Degrees == 0)
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
        CompositionSubGraph TranslateShapeLayer(TranslationContext.For<ShapeLayer> context)
        {
            return new CompositionSubGraph.FromShapeLayer(this, context);
        }

        CompositionShape TranslateGroupShapeContent(TranslationContext.For<ShapeLayer> context, ShapeContentContext shapeContext, ShapeGroup group)
        {
            var result = TranslateShapeLayerContents(context, shapeContext, group.Contents);

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
            var container = _c.CreateContainerShape();

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
                        // TODO - currently ignoring the StartOpacity and EndOpacity - should generate a new transform
                        //        that interpolates that.
                        var generatedItems = itemsBeforeRepeater.Concat(Enumerable.Repeat(repeater.Transform, i + 1)).Concat(itemsAfterRepeater).ToArray();

                        // Recurse to translate the synthesized items.
                        container.Shapes.Add(TranslateShapeLayerContents(context, shapeContext, generatedItems));
                    }

                    return result;
                }
            }

            CheckForUnsupportedShapeGroup(in contents);

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
                            shapeContext.UpdateOpacityFromTransform(context, transform);

                            // Insert a new container at the top. The transform will be applied to it.
                            var newContainer = _c.CreateContainerShape();
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
                var result = _c.CreateSpriteShape();
                result.Geometry = _c.CreatePathGeometry(new CompositionPath(mergedGeometry));
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
            var pathFillType = shapeContext.Fill == null ? ShapeFill.PathFillType.EvenOdd : shapeContext.Fill.FillType;
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
            ShapeFill.PathFillType pathFillType)
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
                            var groupedGeometries = CreateCanvasGeometries(context, shapeContext.Clone(), new Stack<ShapeLayerContent>(group.Contents.ToArray()), pathFillType).ToArray();
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

        CanvasGeometry CreateWin2dPathGeometry(Path path, ShapeFill.PathFillType fillType)
        {
            var result = CreateWin2dPathGeometry(path.Data.InitialValue, fillType, transformMatrix: Sn.Matrix3x2.Identity, optimizeLines: true);

            if (_addDescriptions)
            {
                Describe(result, path.Name);
            }

            return result;
        }

        CanvasGeometry CreateWin2dPathGeometry(
            Sequence<BezierSegment> figure,
            ShapeFill.PathFillType fillType,
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
            var rotation = (float)transform.Rotation.InitialValue.Radians;

            // Calculate the matrix that is equivalent to the properties.
            var combinedMatrix =
                Sn.Matrix3x2.CreateScale(scale, anchor) *
                Sn.Matrix3x2.CreateRotation(rotation, anchor) *
                Sn.Matrix3x2.CreateTranslation(position + anchor);

            return combinedMatrix;
        }

        CanvasGeometry CreateWin2dPathGeometryFromShape(
            TranslationContext context,
            ShapeContentContext shapeContext,
            Path path,
            ShapeFill.PathFillType fillType,
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
            var compositionSpriteShape = _c.CreateSpriteShape();

            var compositionEllipseGeometry = _c.CreateEllipseGeometry();
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
            var compositionRectangle = _c.CreateSpriteShape();
            var position = context.TrimAnimatable(shapeContent.Position);
            var size = context.TrimAnimatable(shapeContent.Size);

            if (shapeContent.CornerRadius.AlwaysEquals(0) && shapeContext.RoundedCorner == null)
            {
                CompositionGeometry geometry;

                // Use a non-rounded rectangle geometry.
                if (_targetUapVersion <= 7)
                {
                    // V7 did not reliably draw non-rounded rectangles.
                    // Work around the problem by using a rounded rectangle with a tiny corner radius.
                    var roundedRectangleGeometry = _c.CreateRoundedRectangleGeometry();
                    geometry = roundedRectangleGeometry;

                    // NOTE: magic tiny corner radius number - do not change!
                    roundedRectangleGeometry.CornerRadius = new Sn.Vector2(0.000001F);

                    // Convert size and position into offset. This is necessary because a geometry's offset is for
                    // its top left corner, wherease a Lottie position is for its centerpoint.
                    roundedRectangleGeometry.Offset = Vector2(position.InitialValue - (size.InitialValue / 2));

                    if (!size.IsAnimated)
                    {
                        roundedRectangleGeometry.Size = Vector2(size.InitialValue);
                    }
                }
                else
                {
                    // V8 and beyond doesn't need the rounded rectangle workaround.
                    var rectangleGeometry = _c.CreateRectangleGeometry();
                    geometry = rectangleGeometry;

                    // Convert size and position into offset. This is necessary because a geometry's offset is for
                    // its top left corner, wherease a Lottie position is for its centerpoint.
                    rectangleGeometry.Offset = Vector2(position.InitialValue - (size.InitialValue / 2));

                    if (!size.IsAnimated)
                    {
                        rectangleGeometry.Size = Vector2(size.InitialValue);
                    }
                }

                compositionRectangle.Geometry = geometry;

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

                    var offsetExpressionAnimation = _c.CreateExpressionAnimation(offsetExpression);
                    offsetExpressionAnimation.SetReferenceParameter("my", geometry);
                    StartExpressionAnimation(geometry, "Offset", offsetExpressionAnimation);
                }
            }
            else
            {
                // Use a rounded rectangle geometry.
                var geometry = _c.CreateRoundedRectangleGeometry();
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

                    var offsetExpressionAnimation = _c.CreateExpressionAnimation(offsetExpression);
                    offsetExpressionAnimation.SetReferenceParameter("my", geometry);
                    StartExpressionAnimation(geometry, nameof(geometry.Offset), offsetExpressionAnimation);
                }
            }

            // Lottie rectangles have 0,0 at top right. That causes problems for TrimPath which expects 0,0 to be top left.
            // Add an offset to the trim path.

            // TODO - this only works correctly if Size and TrimOffset are not animated. A complete solution requires
            //        adding another property.
            var isPartialTrimPath = shapeContext.TrimPath != null &&
                (shapeContext.TrimPath.StartTrim.IsAnimated || shapeContext.TrimPath.EndTrim.IsAnimated || shapeContext.TrimPath.Offset.IsAnimated ||
                shapeContext.TrimPath.StartTrim.InitialValue.Value != 0 || shapeContext.TrimPath.EndTrim.InitialValue.Value != 1);

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
            var compositionPathGeometry = _c.CreatePathGeometry();

            var compositionPath = new CompositionPath(
                CanvasGeometry.CreateGroup(
                    null,
                    paths.Select(p => CreateWin2dPathGeometry(p, fillType)).ToArray(),
                    FilledRegionDetermination(fillType)));

            compositionPathGeometry.Path = compositionPath;

            var compositionSpriteShape = _c.CreateSpriteShape();
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

            // A path is represented as a SpriteShape with a CompositionPathGeometry.
            var geometry = _c.CreatePathGeometry();

            var compositionSpriteShape = _c.CreateSpriteShape();
            compositionSpriteShape.Geometry = geometry;

            ApplyPath(context, geometry, path.Data, GetPathFillType(shapeContext.Fill));

            if (_addDescriptions)
            {
                Describe(compositionSpriteShape, path.Name);
                Describe(geometry, $"{path.Name}.PathGeometry");
            }

            TranslateAndApplyShapeContentContext(context, shapeContext, compositionSpriteShape, 0);

            return compositionSpriteShape;
        }

        void TranslateAndApplyShapeContentContext(TranslationContext context, ShapeContentContext shapeContext, CompositionSpriteShape shape, double trimOffsetDegrees = 0)
        {
            shape.FillBrush = TranslateShapeFill(context, shapeContext.Fill, shapeContext.Opacity);
            TranslateAndApplyStroke(context, shapeContext.Stroke, shape, shapeContext.Opacity);
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

        static AnimatableOrder GetAnimatableOrder(in TrimmedAnimatable<Trim> a, in TrimmedAnimatable<Trim> b)
        {
            var initialA = a.InitialValue.Value;
            var initialB = b.InitialValue.Value;

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
                aMin = Math.Min(a.KeyFrames.Min(kf => kf.Value.Value), initialA);
                aMax = Math.Max(a.KeyFrames.Max(kf => kf.Value.Value), initialA);
            }

            var bMin = initialB;
            var bMax = initialB;
            if (b.IsAnimated)
            {
                bMin = Math.Min(b.KeyFrames.Min(kf => kf.Value.Value), initialB);
                bMax = Math.Max(b.KeyFrames.Max(kf => kf.Value.Value), initialB);
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

            var startTrim = context.TrimAnimatable(trimPath.StartTrim);
            var endTrim = context.TrimAnimatable(trimPath.EndTrim);
            var trimPathOffset = context.TrimAnimatable(trimPath.Offset);

            if (!startTrim.IsAnimated && !endTrim.IsAnimated)
            {
                // Handle some well-known static cases.
                if (startTrim.InitialValue.Value == 0 && endTrim.InitialValue.Value == 1)
                {
                    // The trim does nothing.
                    return;
                }
                else if (startTrim.InitialValue == endTrim.InitialValue)
                {
                    // TODO - the trim trims away all of the path.
                }
            }

            var order = GetAnimatableOrder(in startTrim, in endTrim);

            switch (order)
            {
                case AnimatableOrder.Before:
                case AnimatableOrder.Equal:
                    break;
                case AnimatableOrder.After:
                    {
                        // Swap is necessary to match the WinComp semantics.
                        var temp = startTrim;
                        startTrim = endTrim;
                        endTrim = temp;
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
                geometry.Properties.InsertScalar("TStart", Float(startTrim.InitialValue));
                if (startTrim.IsAnimated)
                {
                    ApplyTrimKeyFrameAnimation(context, startTrim, geometry.Properties, "TStart", "TStart", null);
                }

                var trimStartExpression = _c.CreateExpressionAnimation(ExpressionFactory.MinTStartTEnd);
                trimStartExpression.SetReferenceParameter("my", geometry);
                StartExpressionAnimation(geometry, nameof(geometry.TrimStart), trimStartExpression);

                geometry.Properties.InsertScalar("TEnd", Float(endTrim.InitialValue));
                if (endTrim.IsAnimated)
                {
                    ApplyTrimKeyFrameAnimation(context, endTrim, geometry.Properties, "TEnd", "TEnd", null);
                }

                var trimEndExpression = _c.CreateExpressionAnimation(ExpressionFactory.MaxTStartTEnd);
                trimEndExpression.SetReferenceParameter("my", geometry);
                StartExpressionAnimation(geometry, nameof(geometry.TrimEnd), trimEndExpression);
            }
            else
            {
                // Directly animate the TrimStart and TrimEnd properties.
                if (startTrim.IsAnimated)
                {
                    ApplyTrimKeyFrameAnimation(context, startTrim, geometry, nameof(geometry.TrimStart), "TrimStart", null);
                }
                else
                {
                    geometry.TrimStart = Float(startTrim.InitialValue);
                }

                if (endTrim.IsAnimated)
                {
                    ApplyTrimKeyFrameAnimation(context, endTrim, geometry, nameof(geometry.TrimEnd), "TrimEnd", null);
                }
                else
                {
                    geometry.TrimEnd = Float(endTrim.InitialValue);
                }
            }

            if (trimOffsetDegrees != 0 && !trimPathOffset.IsAnimated)
            {
                // Rectangle shapes are treated specially here to account for Lottie rectangle 0,0 being
                // top right and WinComp rectangle 0,0 being top left. As long as the TrimOffset isn't
                // being animated we can simply add an offset to the trim path.
                geometry.TrimOffset = (float)((trimPathOffset.InitialValue.Degrees + trimOffsetDegrees) / 360);
            }
            else
            {
                if (trimOffsetDegrees != 0)
                {
                    // TODO - can be handled with another property.
                    _issues.AnimatedTrimOffsetWithStaticTrimOffsetIsNotSupported();
                }

                if (trimPathOffset.IsAnimated)
                {
                    ApplyScaledRotationKeyFrameAnimation(context, trimPathOffset, 1 / 360.0, geometry, nameof(geometry.TrimOffset), "TrimOffset", null);
                }
                else
                {
                    geometry.TrimOffset = Float(trimPathOffset.InitialValue.Degrees / 360);
                }
            }
        }

        void TranslateAndApplyStroke(
            TranslationContext context,
            ShapeStroke shapeStroke,
            CompositionSpriteShape sprite,
            CompositeOpacity contextOpacity)
        {
            if (shapeStroke == null)
            {
                return;
            }

            if (shapeStroke.StrokeWidth.AlwaysEquals(0))
            {
                return;
            }

            switch (shapeStroke.StrokeKind)
            {
                case ShapeStroke.ShapeStrokeKind.SolidColor:
                    TranslateAndApplySolidColorStroke(context, (SolidColorStroke)shapeStroke, sprite, contextOpacity);
                    break;
                case ShapeStroke.ShapeStrokeKind.LinearGradient:
                    TranslateAndApplyLinearGradientStroke(context, (LinearGradientStroke)shapeStroke, sprite, contextOpacity);
                    break;
                case ShapeStroke.ShapeStrokeKind.RadialGradient:
                    TranslateAndApplyRadialGradientStroke(context, (RadialGradientStroke)shapeStroke, sprite, contextOpacity);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        void TranslateAndApplyLinearGradientStroke(
            TranslationContext context,
            LinearGradientStroke shapeStroke,
            CompositionSpriteShape sprite,
            CompositeOpacity contextOpacity)
        {
            ApplyCommonStrokeProperties(
                context,
                shapeStroke,
                TranslateLinearGradient(context, shapeStroke, contextOpacity),
                sprite);
        }

        void TranslateAndApplyRadialGradientStroke(
            TranslationContext context,
            RadialGradientStroke shapeStroke,
            CompositionSpriteShape sprite,
            CompositeOpacity contextOpacity)
        {
            ApplyCommonStrokeProperties(
                context,
                shapeStroke,
                TranslateRadialGradient(context, shapeStroke, contextOpacity),
                sprite);
        }

        void TranslateAndApplySolidColorStroke(
            TranslationContext context,
            SolidColorStroke shapeStroke,
            CompositionSpriteShape sprite,
            CompositeOpacity contextOpacity)
        {
            ApplyCommonStrokeProperties(
                context,
                shapeStroke,
                TranslateSolidColorStrokeColor(context, shapeStroke, contextOpacity),
                sprite);

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

        // Applies the properties that are common to all Lottie ShapeStrokes to a CompositionSpriteShape.
        void ApplyCommonStrokeProperties(
            TranslationContext context,
            ShapeStroke shapeStroke,
            CompositionBrush brush,
            CompositionSpriteShape sprite)
        {
            var strokeThickness = context.TrimAnimatable(shapeStroke.StrokeWidth);

            if (!TryBindScalarPropertyToTheme(
                target: sprite,
                bindingSpec: shapeStroke.Name,
                lottiePropertyName: nameof(shapeStroke.StrokeWidth),
                compositionPropertyName: nameof(sprite.StrokeThickness),
                defaultValue: strokeThickness.InitialValue))
            {
                if (strokeThickness.IsAnimated)
                {
                    ApplyScalarKeyFrameAnimation(context, strokeThickness, sprite, nameof(sprite.StrokeThickness));
                }
                else
                {
                    sprite.StrokeThickness = FloatDefaultIsOne(strokeThickness.InitialValue);
                }
            }

            sprite.StrokeStartCap = sprite.StrokeEndCap = sprite.StrokeDashCap = StrokeCapDefaultIsFlat(shapeStroke.CapType);

            sprite.StrokeLineJoin = StrokeLineJoinDefaultIsMiter(shapeStroke.JoinType);

            // Lottie (and SVG/CSS) defines miter limit as (miter_length / stroke_thickness).
            // WUC defines miter limit as (miter_length / (2*stroke_thickness).
            // WUC requires the value not be < 1.
            sprite.StrokeMiterLimit = FloatDefaultIsOne(Math.Max(shapeStroke.MiterLimit / 2, 1));

            sprite.StrokeBrush = brush;
        }

        CompositionBrush TranslateShapeFill(TranslationContext context, ShapeFill shapeFill, CompositeOpacity opacity)
        {
            if (shapeFill == null)
            {
                return null;
            }

            switch (shapeFill.FillKind)
            {
                case ShapeFill.ShapeFillKind.SolidColor:
                    return TranslateSolidColorFill(context, (SolidColorFill)shapeFill, opacity);
                case ShapeFill.ShapeFillKind.LinearGradient:
                    return TranslateLinearGradient(context, (LinearGradientFill)shapeFill, opacity);
                case ShapeFill.ShapeFillKind.RadialGradient:
                    return TranslateRadialGradient(context, (RadialGradientFill)shapeFill, opacity);
                default:
                    throw new InvalidOperationException();
            }
        }

        CompositionColorBrush CreateAnimatedColorBrush(
            TranslationContext context,
            in TrimmedAnimatable<Color> color,
            CompositeOpacity opacity)
        {
            // Opacity is pushed to the alpha channel of the brush. Translate this in the simplest
            // way depending on whether the color or the opacities are animated.
            if (!opacity.IsAnimated)
            {
                // The opacity isn't animated, so it can be simply multiplied into the color.
                var nonAnimatedOpacity = opacity.NonAnimatedValue;
                return color.IsAnimated
                    ? CreateAnimatedColorBrush(context, MultiplyAnimatableColorByOpacity(color, nonAnimatedOpacity))
                    : CreateNonAnimatedColorBrush(color.InitialValue * nonAnimatedOpacity);
            }

            // The opacity has animation. If it's a simple animation (i.e. not composed) and the color
            // is not animated then the color can simply be multiplied by the animation. Otherwise we
            // need to create an expression to relate the opacity value to the color value.
            var animatableOpacities =
                (from a in opacity.GetAnimatables().Zip(PositiveInts, (first, second) => (First: first, Second: second))
                 select (animatable: a.First, name: $"Opacity{a.Second}")).ToArray();

            if (animatableOpacities.Length == 1 && !color.IsAnimated)
            {
                // The color is not animated, so the opacity can be multiplied into the alpha channel.
                return CreateAnimatedColorBrush(
                    context,
                    MultiplyColorByAnimatableOpacity(color.InitialValue, context.TrimAnimatable(animatableOpacities[0].animatable)));
            }

            // We can't simply multiply the opacity into the alpha channel because the opacity animation is not simple
            // or the color is animated. Create properties for the opacities and color and multiply them into a
            // color expression.
            var result = _c.CreateColorBrush();

            // Add a property for each opacity.
            foreach (var a in animatableOpacities)
            {
                var trimmed = context.TrimAnimatable(a.animatable);
                var propertyName = a.name;
                result.Properties.InsertScalar(propertyName, Opacity(trimmed.InitialValue));
                ApplyOpacityKeyFrameAnimation(context, trimmed, result.Properties, propertyName, propertyName, null);
            }

            result.Properties.InsertVector4("Color", Vector4(Color(color.InitialValue)));
            if (color.IsAnimated)
            {
                ApplyColorKeyFrameAnimationAsVector4(context, color, result.Properties, "Color", "Color", null);
            }

            var opacityScalarExpressions = animatableOpacities.Select(a => Expr.Scalar($"my.{a.name}")).ToArray();
            var anim = _c.CreateExpressionAnimation(ExpressionFactory.MyColorAsVector4MultipliedByOpacity(opacityScalarExpressions));
            anim.SetReferenceParameter("my", result.Properties);
            StartExpressionAnimation(result, nameof(result.Color), anim);
            return result;
        }

        CompositionColorBrush TranslateSolidColorStrokeColor(
            TranslationContext context,
            SolidColorStroke shapeStroke,
            CompositeOpacity inheritedOpacity)
            => TranslateSolidColorWithBindings(
                context,
                shapeStroke.Color,
                inheritedOpacity.ComposedWith(context.TrimAnimatable(shapeStroke.Opacity)),
                bindingSpec: shapeStroke.Name);

        CompositionColorBrush TranslateSolidColorFill(
            TranslationContext context,
            SolidColorFill shapeFill,
            CompositeOpacity inheritedOpacity)
            => TranslateSolidColorWithBindings(
                context,
                shapeFill.Color,
                inheritedOpacity.ComposedWith(context.TrimAnimatable(shapeFill.Opacity)),
                bindingSpec: shapeFill.Name);

        // Returns a single color that can be used to represent the given animatable color.
        // This is used as the default color for property bindings. If the animatable color is
        // not animated then we return its value. If it's animated we return the value of the
        // keyframe with the highest alpha, so that it's likely to be visible.
        // The actual color we return here isn't all that important since it is expected to be set
        // to some other value at runtime via property binding, but it is handy to have a visible
        // color when testing, and even better if the color looks like what the designer saw.
        static Color DefaultValueOf(Animatable<Color> animatableColor)
            => animatableColor.IsAnimated
                ? animatableColor.KeyFrames.ToArray().OrderByDescending(kf => kf.Value.A).First().Value
                : animatableColor.InitialValue;

        CompositionColorBrush TranslateSolidColorWithBindings(
            TranslationContext context,
            Animatable<Color> color,
            CompositeOpacity opacity,
            string bindingSpec)
        {
            // Read property bindings embedded into the name of the fill.
            var bindingName = GetThemeBindingNameForLottieProperty(bindingSpec, "Color");

            return bindingName is null
                ? CreateAnimatedColorBrush(context, context.TrimAnimatable(color), opacity)
                : TranslateBoundSolidColor(context, opacity, bindingName, DefaultValueOf(color));
        }

        // Translates a SolidColorFill that gets its color value from a property set value with the given name.
        CompositionColorBrush TranslateBoundSolidColor(
                TranslationContext context,
                CompositeOpacity opacity,
                string bindingName,
                Color defaultColor)
        {
            // Ensure there is a property added to the theme property set.
            var themeDefaultValue = EnsureColorThemePropertyExists(context, bindingName, defaultColor);

            // Use whatever value has already been set for the theme property.
            // This is to deal with theme properties that have been inconsistently set.
            defaultColor = themeDefaultValue;

            var result = _c.CreateColorBrush();

            if (_addDescriptions)
            {
                Describe(result, $"Color bound to theme property value: {bindingName}", bindingName);

                // Name the brush with a name that includes the binding name. This will allow the code generator to
                // give its factory a more meaningful name.
                Name(result, $"ThemeColor_{bindingName}");
            }

            if (opacity.IsAnimated)
            {
                // The opacity has animation. Create an expression to relate the opacity value to the color value.
                var animatableOpacities =
                    (from a in opacity.GetAnimatables().Zip(PositiveInts, (first, second) => (First: first, Second: second))
                     select (animatable: a.First, name: $"Opacity{a.Second}")).ToArray();

                // Add a property for each opacity.
                foreach (var a in animatableOpacities)
                {
                    var propertyName = a.name;
                    result.Properties.InsertScalar(propertyName, Opacity(a.animatable.InitialValue));
                    ApplyOpacityKeyFrameAnimation(context, context.TrimAnimatable(a.animatable), result.Properties, propertyName, propertyName, null);
                }

                var opacityScalarExpressions = animatableOpacities.Select(a => Expr.Scalar($"my.{a.name}")).ToArray();
                var anim = _c.CreateExpressionAnimation(ThemedColorAsVector4MultipliedByOpacities(bindingName, opacityScalarExpressions));
                anim.SetReferenceParameter("my", result.Properties);
                anim.SetReferenceParameter(ThemePropertiesName, _themePropertySet);

                StartExpressionAnimation(result, nameof(result.Color), anim);
                return result;
            }
            else
            {
                // Opacity isn't animated. Multiply the alpha channel of the color by the non-animated opacity value.
                var anim = _c.CreateExpressionAnimation(ThemedColorMultipliedByOpacity(bindingName, opacity.NonAnimatedValue));
                anim.SetReferenceParameter(ThemePropertiesName, _themePropertySet);
                StartExpressionAnimation(result, nameof(result.Color), anim);
                return result;
            }
        }

        CompositionLinearGradientBrush TranslateLinearGradient(
            TranslationContext context,
            IGradient linearGradient,
            CompositeOpacity opacity)
        {
            var result = _c.CreateLinearGradientBrush();

            // BodyMovin specifies start and end points in absolute values.
            result.MappingMode = CompositionMappingMode.Absolute;

            var startPoint = context.TrimAnimatable(linearGradient.StartPoint);
            var endPoint = context.TrimAnimatable(linearGradient.EndPoint);

            if (startPoint.IsAnimated)
            {
                ApplyVector2KeyFrameAnimation(context, startPoint, result, nameof(result.StartPoint));
            }
            else
            {
                result.StartPoint = Vector2(startPoint.InitialValue);
            }

            if (endPoint.IsAnimated)
            {
                ApplyVector2KeyFrameAnimation(context, endPoint, result, nameof(result.EndPoint));
            }
            else
            {
                result.EndPoint = Vector2(endPoint.InitialValue);
            }

            var gradientStops = context.TrimAnimatable(linearGradient.GradientStops);

            if (gradientStops.InitialValue.Items.IsEmpty)
            {
                // If there are no gradient stops then we can't create a brush.
                return null;
            }

            TranslateAndApplyGradientStops(context, result, in gradientStops, opacity);

            return result;
        }

        CompositionGradientBrush TranslateRadialGradient(
            TranslationContext context,
            IRadialGradient gradient,
            CompositeOpacity opacity)
        {
            if (!IsUapApiAvailable(nameof(CompositionRadialGradientBrush), versionDependentFeatureDescription: "Radial gradient fill"))
            {
                // CompositionRadialGradientBrush didn't exist until UAP v8. If the target OS doesn't support
                // UAP v8 then fall back to linear gradients as a compromise.
                return TranslateLinearGradient(context, gradient, opacity);
            }

            var result = _c.CreateRadialGradientBrush();

            // BodyMovin specifies start and end points in absolute values.
            result.MappingMode = CompositionMappingMode.Absolute;

            var startPoint = context.TrimAnimatable(gradient.StartPoint);
            var endPoint = context.TrimAnimatable(gradient.EndPoint);

            if (startPoint.IsAnimated)
            {
                ApplyVector2KeyFrameAnimation(context, startPoint, result, nameof(result.EllipseCenter));
            }
            else
            {
                result.EllipseCenter = Vector2(startPoint.InitialValue);
            }

            if (endPoint.IsAnimated)
            {
                // We don't yet support animated EndPoint.
                _issues.GradientFillIsNotSupported("Radial", "animated end point");
            }

            result.EllipseRadius = new Sn.Vector2(Sn.Vector2.Distance(Vector2(startPoint.InitialValue), Vector2(endPoint.InitialValue)));

            if (gradient.HighlightLength != null &&
                (gradient.HighlightLength.InitialValue != 0 || gradient.HighlightLength.IsAnimated))
            {
                // We don't yet support animated HighlightLength.
                _issues.GradientFillIsNotSupported("Radial", "animated highlight length");
            }

            var gradientStops = context.TrimAnimatable(gradient.GradientStops);

            if (gradientStops.InitialValue.Items.IsEmpty)
            {
                // If there are no gradient stops then we can't create a brush.
                return null;
            }

            TranslateAndApplyGradientStops(context, result, in gradientStops, opacity);

            return result;
        }

        void TranslateAndApplyGradientStops(
            TranslationContext context,
            CompositionGradientBrush brush,
            in TrimmedAnimatable<Sequence<GradientStop>> gradientStops,
            CompositeOpacity opacity)
        {
            if (gradientStops.IsAnimated)
            {
                TranslateAndApplyAnimatedGradientStops(context, brush, gradientStops, opacity);
            }
            else
            {
                TranslateAndApplyNonAnimatedGradientStops(context, brush, gradientStops.InitialValue, opacity);
            }
        }

        void TranslateAndApplyAnimatedGradientStops(
            TranslationContext context,
            CompositionGradientBrush brush,
            in TrimmedAnimatable<Sequence<GradientStop>> gradientStops,
            CompositeOpacity opacity)
        {
            if (opacity.IsAnimated)
            {
                TranslateAndApplyAnimatedGradientStopsWithAnimatedOpacity(context, brush, in gradientStops, opacity);
            }
            else
            {
                TranslateAndApplyAnimatedColorGradientStopsWithStaticOpacity(context, brush, in gradientStops, opacity.NonAnimatedValue);
            }
        }

        void TranslateAndApplyAnimatedGradientStopsWithAnimatedOpacity(
            TranslationContext context,
            CompositionGradientBrush brush,
            in TrimmedAnimatable<Sequence<GradientStop>> gradientStops,
            CompositeOpacity opacity)
        {
            // Lottie represents animation of stops as a sequence of lists of stops.
            // WinComp uses a single list of stops where each stop is animated.

            // Lottie represents stops as either color or opacity stops. Convert them all to color stops.
            var colorStopKeyFrames = gradientStops.KeyFrames.SelectToArray(kf => GradientStopOptimizer.Optimize(kf));
            colorStopKeyFrames = GradientStopOptimizer.RemoveRedundantStops(colorStopKeyFrames).ToArray();
            var stopsCount = colorStopKeyFrames[0].Value.Count();
            var keyframesCount = colorStopKeyFrames.Length;

            // The opacity has animation. Create an expression to relate the opacity value to the color value.
            var animatableOpacities =
                (from a in opacity.GetAnimatables().Zip(PositiveInts, (first, second) => (First: first, Second: second))
                 select (animatable: a.First, name: $"Opacity{a.Second}")).ToArray();

            // Add a property for each opacity.
            foreach (var a in animatableOpacities)
            {
                var trimmedOpacity = context.TrimAnimatable(a.animatable);
                var propertyName = a.name;
                brush.Properties.InsertScalar(propertyName, Opacity(trimmedOpacity.InitialValue * 255));

                // Pre-multiply the opacities by 255 so we can use the simpler
                // expression for multiplying color by opacity.
                ApplyScaledOpacityKeyFrameAnimation(context, trimmedOpacity, 255, brush.Properties, propertyName, propertyName, null);
            }

            var opacityExpressions = animatableOpacities.Select(ao => Expr.Scalar($"my.{ao.name}")).ToArray();

            // Create the Composition stops and animate them.
            for (var i = 0; i < stopsCount; i++)
            {
                var gradientStop = _c.CreateColorGradientStop();

                if (_addDescriptions)
                {
                    Describe(gradientStop, $"Stop {i}");
                }

                brush.ColorStops.Add(gradientStop);

                // Extract the color key frames for this stop.
                var colorKeyFrames = ExtractKeyFramesFromColorStopKeyFrames(
                    colorStopKeyFrames,
                    i,
                    gs => ExpressionFactory.ColorMultipliedByPreMultipliedOpacities(Color(gs.Color), opacityExpressions)).ToArray();

                // Bind the color to the opacities multiplied by the colors.
                ApplyExpressionColorKeyFrameAnimation(
                    context,
                    new TrimmedAnimatable<WinCompData.Expressions.Color>(context, colorKeyFrames[0].Value, colorKeyFrames),
                    gradientStop,
                    nameof(gradientStop.Color),
                    anim => anim.SetReferenceParameter("my", brush.Properties));

                // Extract the offset key frames for this stop.
                var offsetKeyFrames = ExtractKeyFramesFromColorStopKeyFrames(colorStopKeyFrames, i, gs => gs.Offset).ToArray();
                ApplyScalarKeyFrameAnimation(
                    context,
                    new TrimmedAnimatable<double>(context, offsetKeyFrames[0].Value, offsetKeyFrames),
                    gradientStop,
                    nameof(gradientStop.Offset));
            }
        }

        void TranslateAndApplyAnimatedColorGradientStopsWithStaticOpacity(
            TranslationContext context,
            CompositionGradientBrush brush,
            in TrimmedAnimatable<Sequence<GradientStop>> gradientStops,
            Opacity opacity)
        {
            // Lottie represents animation of stops as a sequence of lists of stops.
            // WinComp uses a single list of stops where each stop is animated.

            // Lottie represents stops as either color or opacity stops. Convert them all to color stops.
            var colorStopKeyFrames = gradientStops.KeyFrames.SelectToArray(kf => GradientStopOptimizer.Optimize(kf));
            colorStopKeyFrames = GradientStopOptimizer.RemoveRedundantStops(colorStopKeyFrames).ToArray();
            var stopsCount = colorStopKeyFrames[0].Value.Count();
            var keyframesCount = colorStopKeyFrames.Length;

            // Create the Composition stops and animate them.
            for (var i = 0; i < stopsCount; i++)
            {
                var gradientStop = _c.CreateColorGradientStop();

                if (_addDescriptions)
                {
                    Describe(gradientStop, $"Stop {i}");
                }

                brush.ColorStops.Add(gradientStop);

                // Extract the color key frames for this stop.
                var colorKeyFrames = ExtractKeyFramesFromColorStopKeyFrames(
                    colorStopKeyFrames,
                    i,
                    gs => gs.Color * opacity).ToArray();

                ApplyColorKeyFrameAnimation(
                    context,
                    new TrimmedAnimatable<Color>(context, colorKeyFrames[0].Value, colorKeyFrames),
                    gradientStop,
                    nameof(gradientStop.Color));

                // Extract the offset key frames for this stop.
                var offsetKeyFrames = ExtractKeyFramesFromColorStopKeyFrames(colorStopKeyFrames, i, gs => gs.Offset).ToArray();
                ApplyScalarKeyFrameAnimation(
                    context,
                    new TrimmedAnimatable<double>(context, offsetKeyFrames[0].Value, offsetKeyFrames),
                    gradientStop,
                    nameof(gradientStop.Offset));
            }
        }

        void TranslateAndApplyNonAnimatedGradientStops(
            TranslationContext context,
            CompositionGradientBrush brush,
            Sequence<GradientStop> gradientStops,
            CompositeOpacity opacity)
        {
            var optimizedGradientStops = GradientStopOptimizer.OptimizeColorStops(GradientStopOptimizer.Optimize(gradientStops.Items.ToArray()));

            if (opacity.IsAnimated)
            {
                TranslateAndApplyNonAnimatedColorGradientStopsWithAnimatedOpacity(context, brush, optimizedGradientStops, opacity);
            }
            else
            {
                TranslateAndApplyNonAnimatedColorGradientStopsWithStaticOpacity(context, brush, optimizedGradientStops, opacity.NonAnimatedValue);
            }
        }

        void TranslateAndApplyNonAnimatedColorGradientStopsWithStaticOpacity(
            TranslationContext context,
            CompositionGradientBrush brush,
            IEnumerable<ColorGradientStop> gradientStops,
            Opacity opacity)
        {
            var i = 0;
            foreach (var stop in gradientStops)
            {
                var color = stop.Color * opacity;

                var gradientStop = _c.CreateColorGradientStop(Float(stop.Offset), color);

                if (_addDescriptions)
                {
                    Describe(gradientStop, $"Stop {i}");
                }

                brush.ColorStops.Add(gradientStop);
                i++;
            }
        }

        void TranslateAndApplyNonAnimatedColorGradientStopsWithAnimatedOpacity(
            TranslationContext context,
            CompositionGradientBrush brush,
            IEnumerable<ColorGradientStop> gradientStops,
            CompositeOpacity opacity)
        {
            // The opacity has animation. Create an expression to relate the opacity value to the color value.
            var animatableOpacities =
                (from a in opacity.GetAnimatables().Zip(PositiveInts, (first, second) => (First: first, Second: second))
                 select (animatable: a.First, name: $"Opacity{a.Second}")).ToArray();

            // Add a property for each opacity.
            foreach (var a in animatableOpacities)
            {
                var trimmedOpacity = context.TrimAnimatable(a.animatable);
                var propertyName = a.name;
                brush.Properties.InsertScalar(propertyName, Opacity(trimmedOpacity.InitialValue * 255));

                // Pre-multiply the opacities by 255 so we can use the simpler
                // expression for multiplying color by opacity.
                ApplyScaledOpacityKeyFrameAnimation(context, trimmedOpacity, 255, brush.Properties, propertyName, propertyName, null);
            }

            var opacityExpressions = animatableOpacities.Select(ao => Expr.Scalar($"my.{ao.name}")).ToArray();

            var i = 0;
            foreach (var stop in gradientStops)
            {
                var gradientStop = _c.CreateColorGradientStop();

                if (_addDescriptions)
                {
                    Describe(gradientStop, $"Stop {i}");
                }

                gradientStop.Offset = Float(stop.Offset);

                if (stop.Color.A == 0)
                {
                    // The stop has 0 alpha, so no point multiplying it by opacity.
                    gradientStop.Color = Color(stop.Color);
                }
                else
                {
                    // Bind the color to the opacity multiplied by the color.
                    var anim = _c.CreateExpressionAnimation(ColorMultipliedByPreMultipliedOpacities(Color(stop.Color), opacityExpressions));
                    anim.SetReferenceParameter("my", brush.Properties);
                    StartExpressionAnimation(gradientStop, "Color", anim);
                }

                brush.ColorStops.Add(gradientStop);
                i++;
            }
        }

        static IEnumerable<KeyFrame<TKeyFrame>> ExtractKeyFramesFromColorStopKeyFrames<TKeyFrame>(
            KeyFrame<Sequence<ColorGradientStop>>[] stops,
            int stopIndex,
            Func<ColorGradientStop, TKeyFrame> selector)
            where TKeyFrame : IEquatable<TKeyFrame>
        {
            for (var i = 0; i < stops.Length; i++)
            {
                var kf = stops[i];
                var value = kf.Value.Items[stopIndex];
                var selected = selector(value);

                yield return new KeyFrame<TKeyFrame>(kf.Frame, selected, kf.SpatialControlPoint1, kf.SpatialControlPoint2, kf.Easing);
            }
        }

        CompositionSubGraph TranslateSolidLayer(TranslationContext.For<SolidLayer> context)
             => new CompositionSubGraph.FromSolidLayer(this, context);

        CompositionSubGraph TranslateTextLayer(TranslationContext.For<TextLayer> context)
        {
            // Text layers are not yet suported.
            _issues.TextLayerIsNotSupported();
            return null;
        }

        // Returns a chain of ContainerVisual that define the opacity for a layer.
        // The top of the chain is the rootTransform, the bottom is the leafTransform.
        void TranslateOpacityTransformOnContainerVisualForLayer(
            TranslationContext context,
            Layer layer,
            out ContainerVisual rootTransformNode,
            out ContainerVisual leafTransformNode)
        {
            // Create a ContainerVisual to apply the transform to.
            leafTransformNode = _c.CreateContainerVisual();

            // Apply the transform.
            TranslateAndApplyTransform(context, layer.Transform, leafTransformNode);
            if (_addDescriptions)
            {
                Describe(leafTransformNode, $"Transforms for {layer.Name}");
            }

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
            leafTransformNode = _c.CreateContainerVisual();

            // Apply the transform.
            TranslateAndApplyTransform(context, layer.Transform, leafTransformNode);
            if (_addDescriptions)
            {
                Describe(leafTransformNode, $"Transforms for {layer.Name}");
            }

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
            leafTransformNode = _c.CreateContainerShape();

            // Apply the transform from the layer.
            TranslateAndApplyTransform(context, layer.Transform, leafTransformNode);

            // Recurse to translate the parent transform, if any.
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
                context.TrimAnimatable(transform.Rotation),
                context.TrimAnimatable(transform.ScalePercent),
                container);

            // TOTO: set Skew and Skew Axis
        }

        void TranslateAndApplyAnchorPositionRotationAndScale(
            TranslationContext context,
            IAnimatableVector3 anchor,
            IAnimatableVector3 position,
            in TrimmedAnimatable<Rotation> rotation,
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
            if (rotation.IsAnimated)
            {
                ApplyRotationKeyFrameAnimation(context, rotation, container, nameof(container.RotationAngleInDegrees), "Rotation");
            }
            else
            {
                container.RotationAngleInDegrees = FloatDefaultIsZero(rotation.InitialValue.Degrees);
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
                var centerPointExpression = _c.CreateExpressionAnimation(container.IsShape ? (Expr)MyAnchor : (Expr)MyAnchor3);
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
                offsetExpression = _c.CreateExpressionAnimation(container.IsShape ? (Expr)PositionMinusAnchor2 : (Expr)PositionMinusAnchor3);
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
                        offsetExpression = _c.CreateExpressionAnimation(container.IsShape
                            ? (Expr)Expr.Vector2(
                                MyPosition.X - initialAnchor.X,
                                MyPosition.Y - initialAnchor.Y)
                            : (Expr)Expr.Vector3(
                                MyPosition.X - initialAnchor.X,
                                MyPosition.Y - initialAnchor.Y,
                                0));

                        positionIsAnimated = true;
                    }
                }
                else
                {
                    // Non-zero non-animated anchor. Subtract the anchor.
                    offsetExpression = _c.CreateExpressionAnimation(container.IsShape
                        ? (Expr)Expr.Vector2(
                            MyPosition.X - initialAnchor.X,
                            MyPosition.Y - initialAnchor.Y)
                        : (Expr)Expr.Vector3(
                            MyPosition.X - initialAnchor.X,
                            MyPosition.Y - initialAnchor.Y,
                            0));
                }
            }
            else if (anchorIsAnimated)
            {
                // Only anchor is animated.
                offsetExpression = _c.CreateExpressionAnimation(container.IsShape
                    ? (Expr)Expr.Vector2(
                        initialPosition.X - MyAnchor.X,
                        initialPosition.Y - MyAnchor.Y)
                    : (Expr)Expr.Vector3(
                        initialPosition.X - MyAnchor.X,
                        initialPosition.Y - MyAnchor.Y,
                        0));
            }

            if (!positionIsAnimated && !anchorIsAnimated)
            {
                // Position and Anchor are static. No expression needed.
                container.Offset = Vector2DefaultIsZero(initialPosition - initialAnchor);
            }

            // Position is a Lottie-only concept. It offsets the object relative to the Anchor.
            if (positionIsAnimated)
            {
                if (!anchorIsAnimated && xyzPosition is null)
                {
                    // The anchor isn't animated and the position is an animated Vector3. This is a very
                    // common case, and can be simplified to an Offset animation by subtracting the Anchor from the Position.
                    offsetExpression = null;
                    var anchoredPosition = PositionAndAnchorToOffset(context, position3, anchor.InitialValue);
                    if (container.IsShape)
                    {
                        ApplyVector2KeyFrameAnimation(context, anchoredPosition, container, "Offset");
                    }
                    else
                    {
                        ApplyVector3KeyFrameAnimation(context, anchoredPosition, container, "Offset");
                    }
                }
                else
                {
                    // Anchor and Position are both animated.
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
            }

            if (offsetExpression != null)
            {
                offsetExpression.SetReferenceParameter("my", container);
                StartExpressionAnimation(container, nameof(container.Offset), offsetExpression);
            }
        }

        static TrimmedAnimatable<Vector3> PositionAndAnchorToOffset(TranslationContext context, in TrimmedAnimatable<Vector3> animation, Vector3 anchor)
        {
            var keyframes = new KeyFrame<Vector3>[animation.KeyFrames.Length];

            for (var i = 0; i < animation.KeyFrames.Length; i++)
            {
                var kf = animation.KeyFrames[i];
                keyframes[i] = new KeyFrame<Vector3>(kf.Frame, kf.Value - anchor, kf.SpatialControlPoint1, kf.SpatialControlPoint2, kf.Easing);
            }

            return new TrimmedAnimatable<Vector3>(context, keyframes[0].Value, keyframes);
        }

        static bool ContainsSpatialControlPoints(in TrimmedAnimatable<Vector3> animation)
             => animation.KeyFrames.Any(kf => kf.SpatialControlPoint1 != LottieData.Vector3.Zero || kf.SpatialControlPoint2 != LottieData.Vector3.Zero);

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
                bindingAnimation = _c.CreateExpressionAnimation(ScaledAndOffsetRootProgress(scale, offset));
                bindingAnimation.SetReferenceParameter(RootName, _rootVisual);
                if (_addDescriptions)
                {
                    // Give the animation a nice readable name in codegen.
                    var name = key.Offset != 0 || key.Scale != 1
                        ? "RootProgressRemapped"
                        : "RootProgress";

                    Name(bindingAnimation, name);
                }

                _progressBindingAnimations.Add(key, bindingAnimation);
            }

            // Bind the controller's Progress with a single Progress property on the scene root.
            // The Progress property provides the time reference for the animation.
            controller.StartAnimation("Progress", bindingAnimation);
        }

        void ApplyPath(
            TranslationContext context,
            CompositionPathGeometry targetGeometry,
            Animatable<Sequence<BezierSegment>> path,
            ShapeFill.PathFillType fillType)
        {
            var optimizedPathAnimatable = context.TrimAnimatable(_lottieDataOptimizer.GetOptimized(path));

            // PathKeyFrameAnimation was introduced in 6 but was unreliable until 11.
            if (optimizedPathAnimatable.IsAnimated && IsUapApiAvailable(nameof(PathKeyFrameAnimation), versionDependentFeatureDescription: "Path animation"))
            {
                ApplyPathKeyFrameAnimation(context, optimizedPathAnimatable, fillType, targetGeometry, nameof(targetGeometry.Path), nameof(targetGeometry.Path));
            }
            else
            {
                targetGeometry.Path = CompositionPathFromPathGeometry(
                    optimizedPathAnimatable.InitialValue,
                    fillType,
                    optimizeLines: true);
            }
        }

        void ApplyRotationKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Rotation> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
            => ApplyScaledRotationKeyFrameAnimation(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

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

        void ApplyOpacityKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Opacity> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
            => ApplyScaledOpacityKeyFrameAnimation(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        void ApplyTrimKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Trim> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
            => ApplyScaledTrimKeyFrameAnimation(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

        void ApplyScaledRotationKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Rotation> value,
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
                _c.CreateScalarKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, (float)(val.Degrees * scale), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyScaledOpacityKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Opacity> value,
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
                _c.CreateScalarKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, (float)(val.Value * scale), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

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
                _c.CreateScalarKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, (float)(val * scale), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyScaledTrimKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Trim> value,
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
                _c.CreateScalarKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, (float)(val.Value * scale), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyColorKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Color> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                _c.CreateColorKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, Color(val), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyExpressionColorKeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<WinCompData.Expressions.Color> value,
            CompositionObject targetObject,
            string targetPropertyName,
            Action<ColorKeyFrameAnimation> beforeStartCallback,
            string longDescription = null,
            string shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                _c.CreateColorKeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertExpressionKeyFrame(progress, val, easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription,
                beforeStartCallback);
        }

        void ApplyColorKeyFrameAnimationAsVector4(
            TranslationContext context,
            in TrimmedAnimatable<Color> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");
            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                _c.CreateVector4KeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, Vector4(Color(val)), easing),
                null,
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyPathKeyFrameAnimation(
                TranslationContext context,
                in TrimmedAnimatable<Sequence<BezierSegment>> value,
                ShapeFill.PathFillType fillType,
                CompositionObject targetObject,
                string targetPropertyName,
                string longDescription = null,
                string shortDescription = null)
        {
            Debug.Assert(value.IsAnimated, "Precondition");

            GenericCreateCompositionKeyFrameAnimation(
                context,
                value,
                _c.CreatePathKeyFrameAnimation,
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
                _c.CreateVector2KeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, Vector2(val * scale), easing),
                (ca, progress, expr, easing) => ca.InsertExpressionKeyFrame(progress, scale * expr, easing),
                targetObject,
                targetPropertyName,
                longDescription,
                shortDescription);
        }

        void ApplyVector3KeyFrameAnimation(
            TranslationContext context,
            in TrimmedAnimatable<Vector3> value,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription = null,
            string shortDescription = null)
            => ApplyScaledVector3KeyFrameAnimation(context, value, 1, targetObject, targetPropertyName, longDescription, shortDescription);

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
                _c.CreateVector3KeyFrameAnimation,
                (ca, progress, val, easing) => ca.InsertKeyFrame(progress, Vector3(val) * (float)scale, easing),
                (ca, progress, expr, easing) => ca.InsertExpressionKeyFrame(progress, scale * expr.AsVector3(), easing),
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
            Action<TCA, float, CubicBezierFunction2, CompositionEasingFunction> insertExpressionKeyFrame,
            CompositionObject targetObject,
            string targetPropertyName,
            string longDescription,
            string shortDescription,
            Action<TCA> beforeStartCallback = null)
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
                insertKeyFrame(compositionAnimation, 0 /* progress */, firstKeyFrame.Value, _c.CreateStepThenHoldEasingFunction() /*easing*/);

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
            var previousProgress = Float32.PreviousSmallerThan(0);
            var rootReferenceRequired = false;
            var previousKeyFrameWasExpression = false;

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
                            cb = CubicBezierFunction2.Create(
                                cp0,
                                cp0 + cp1,
                                cp2 + cp3,
                                cp3,
                                Expr.Scalar("dummy"));
                            break;
                        case Easing.EasingType.Hold:
                            // Holds should never have interesting cubic beziers, so replace with one that is definitely colinear.
                            cb = CubicBezierFunction2.ZeroBezier;
                            break;
                        default:
                            throw new InvalidOperationException();
                    }

                    if (cb.IsEquivalentToLinear || adjustedProgress == 0)
                    {
                        // The cubic bezier function is equivalent to a line, or its value starts at the start of the animation, so no need
                        // for an expression to do spatial beziers on it. Just use a regular key frame.
                        if (previousKeyFrameWasExpression)
                        {
                            // Ensure the previous expression doesn't continue being evaluated during the current keyframe.
                            // This is necessary because the expression is only defined from the previous progress to the current progress.
                            insertKeyFrame(compositionAnimation, Float32.NextLargerThan(previousProgress), previousValue, _c.CreateStepThenHoldEasingFunction());
                        }

                        // The easing for a keyframe at 0 is unimportant, so always use Hold.
                        var easing = adjustedProgress == 0 ? HoldEasing.Instance : keyFrame.Easing;

                        insertKeyFrame(compositionAnimation, (float)adjustedProgress, keyFrame.Value, _c.CreateCompositionEasingFunction(easing));
                        previousKeyFrameWasExpression = false;
                    }
                    else
                    {
                        // Expression key frame needed for a spatial bezier.

                        // Make the progress value just before the requested progress value
                        // so that there is room to add a key frame just after this to hold
                        // the final value. This is necessary so that the expression we're about
                        // to add won't get evaluated during the following segment.
                        if ((float)adjustedProgress > 0)
                        {
                            adjustedProgress = Float32.PreviousSmallerThan((float)adjustedProgress);
                        }

                        if ((float)previousProgress > 0)
                        {
                            previousProgress = Float32.NextLargerThan((float)previousProgress);
                        }

                        // Re-create the cubic bezier using the real variable name (it was created previously just to
                        // see if it was linear).
                        cb = CubicBezierFunction2.Create(
                            cp0,
                            cp0 + cp1,
                            cp2 + cp3,
                            cp3,
                            RootScalar(_progressMapFactory.GetVariableForProgressMapping((float)previousProgress, (float)adjustedProgress, keyFrame.Easing, scale, offset)));

                        // Insert the cubic bezier expression. The easing has to be a StepThenHold because otherwise
                        // the value will be interpolated between the result of the expression, and the previous
                        // key frame value. The StepThenHold will make it just evaluate the expression.
                        insertExpressionKeyFrame(
                            compositionAnimation,
                            (float)adjustedProgress,
                            cb,                                 // Expression.
                            _c.CreateStepThenHoldEasingFunction());    // Jump to the final value so the expression is evaluated all the way through.

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
                        insertKeyFrame(compositionAnimation, Float32.NextLargerThan(previousProgress), previousValue, _c.CreateStepThenHoldEasingFunction());
                    }

                    insertKeyFrame(compositionAnimation, (float)adjustedProgress, keyFrame.Value, _c.CreateCompositionEasingFunction(keyFrame.Easing));
                    previousKeyFrameWasExpression = false;
                }

                previousValue = keyFrame.Value;
                previousProgress = (float)adjustedProgress;
            }

            if (previousKeyFrameWasExpression && previousProgress < 1)
            {
                // Add a keyframe to hold the final value. Otherwise the expression on the last keyframe
                // will get evaluated outside the bounds of its keyframe.
                insertKeyFrame(compositionAnimation, Float32.NextLargerThan(previousProgress), (T)(object)previousValue, _c.CreateStepThenHoldEasingFunction());
            }

            // Add a reference to the root Visual if needed (i.e. if an expression keyframe was added).
            if (rootReferenceRequired)
            {
                compositionAnimation.SetReferenceParameter(RootName, _rootVisual);
            }

            beforeStartCallback?.Invoke(compositionAnimation);

            // Start the animation scaled and offset.
            StartKeyframeAnimation(targetObject, targetPropertyName, compositionAnimation, scale, offset);
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

        static ShapeFill.PathFillType GetPathFillType(ShapeFill fill) => fill == null ? ShapeFill.PathFillType.EvenOdd : fill.FillType;

        CompositionPath CompositionPathFromPathGeometry(
            Sequence<BezierSegment> pathGeometry,
            ShapeFill.PathFillType fillType,
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

        TrimmedAnimatable<Color> MultiplyAnimatableColorByOpacity(
            in TrimmedAnimatable<Color> color,
            Opacity opacity)
        {
            var initialColorValue = color.InitialValue * opacity;

            if (color.IsAnimated)
            {
                // Multiply the color animation by the opacity.
                return new TrimmedAnimatable<Color>(
                    color.Context,
                    initialValue: initialColorValue,
                    keyFrames: color.KeyFrames.SelectToSpan(kf =>
                        new KeyFrame<Color>(
                            kf.Frame,
                            kf.Value * opacity,
                            kf.SpatialControlPoint1,
                            kf.SpatialControlPoint2,
                            kf.Easing)));
            }
            else
            {
                return new TrimmedAnimatable<Color>(color.Context, initialColorValue);
            }
        }

        TrimmedAnimatable<Color> MultiplyColorByAnimatableOpacity(
            Color color,
            in TrimmedAnimatable<Opacity> opacity)
        {
            if (!opacity.IsAnimated)
            {
                return new TrimmedAnimatable<Color>(opacity.Context, color * opacity.InitialValue);
            }
            else
            {
                // Multiply the single color value by the opacity animation.
                return new TrimmedAnimatable<Color>(
                    opacity.Context,
                    initialValue: color * opacity.InitialValue,
                    keyFrames: opacity.KeyFrames.SelectToSpan(kf =>
                        new KeyFrame<Color>(
                            kf.Frame,
                            color * kf.Value,
                            kf.SpatialControlPoint1,
                            kf.SpatialControlPoint2,
                            kf.Easing)));
            }
        }

        CompositionColorBrush CreateAnimatedColorBrush(TranslationContext context, Color color, in TrimmedAnimatable<Opacity> opacity)
        {
            var multipliedColor = MultiplyColorByAnimatableOpacity(color, in opacity);
            return CreateAnimatedColorBrush(context, multipliedColor);
        }

        CompositionColorBrush CreateAnimatedColorBrush(TranslationContext context, in TrimmedAnimatable<Color> color)
        {
            if (color.IsAnimated)
            {
                var result = _c.CreateColorBrush();

                ApplyColorKeyFrameAnimation(
                    context,
                    color,
                    result,
                    targetPropertyName: nameof(result.Color),
                    longDescription: "Color",
                    shortDescription: null);
                return result;
            }
            else
            {
                return CreateNonAnimatedColorBrush(color.InitialValue);
            }
        }

        CompositionColorBrush CreateNonAnimatedColorBrush(Color color)
        {
            if (!_nonAnimatedColorBrushes.TryGetValue(color, out var result))
            {
                result = _c.CreateNonAnimatedColorBrush(color);
                _nonAnimatedColorBrushes.Add(color, result);
            }

            return result;
        }

        bool TryBindScalarPropertyToTheme(
            CompositionObject target,
            string bindingSpec,
            string lottiePropertyName,
            string compositionPropertyName,
            double defaultValue)
        {
            var bindingName = GetThemeBindingNameForLottieProperty(bindingSpec, lottiePropertyName);

            if (bindingName is null)
            {
                return false;
            }
            else
            {
                // Ensure there is a property in the theme property set for this binding name.
                var themeDefaultValue = EnsureScalarThemePropertyExists(bindingName, defaultValue);

                // Use whatever value has already been set for the theme property.
                // This is to deal with theme properties that have been inconsistently set.
                defaultValue = themeDefaultValue;

                // Create an expression that binds property to the theme property set.
                var anim = _c.CreateExpressionAnimation(ThemedScalar(bindingName));
                anim.SetReferenceParameter(ThemePropertiesName, _themePropertySet);
                target.StartAnimation(compositionPropertyName, anim);
                return true;
            }
        }

        // Ensures there is a property in the theme property set with the given name and default value.
        Color EnsureColorThemePropertyExists(TranslationContext context, string bindingName, Color defaultValue)
        {
            // Create a theme property set if one hasn't been created yet.
            var themeProperties = _themePropertySet ?? (_themePropertySet = _c.CreatePropertySet());

            var defaultValueAsWinUIColor = Color(defaultValue);
            var defaultValueAsVector4 = Vector4(defaultValueAsWinUIColor);

            // Insert a property set value for the scalar if one hasn't yet been added.
            switch (themeProperties.TryGetVector4(bindingName, out var existingColorAsVector4))
            {
                case CompositionGetValueStatus.NotFound:
                    // The property hasn't been added yet. Add it.
                    themeProperties.InsertVector4(bindingName, Vector4(defaultValueAsWinUIColor));
                    _propertyBindings.AddPropertyBinding(
                        bindingName,
                        actualType: PropertySetValueType.Vector4,
                        exposedType: PropertySetValueType.Color,
                        defaultValue: defaultValueAsWinUIColor);
                    return defaultValue;

                case CompositionGetValueStatus.Succeeded:
                    // The property has already been added.
                    var existingValue = Color(Color(existingColorAsVector4));

                    if (defaultValueAsVector4 != existingColorAsVector4)
                    {
                        _issues.ThemePropertyValuesAreInconsistent(bindingName, existingValue.ToString(), Color(Color(defaultValueAsVector4)).ToString());
                    }

                    // Return the value we have.
                    return existingValue;

                case CompositionGetValueStatus.TypeMismatch:
                default:
                    throw new InvalidOperationException();
            }
        }

        // Ensures there is a property in the theme property set with the given name and default value.
        float EnsureScalarThemePropertyExists(string bindingName, double defaultValue)
        {
            // Create a theme property set if one hasn't been created yet.
            var themeProperties = _themePropertySet ?? (_themePropertySet = _c.CreatePropertySet());

            var defaultValueAsFloat = Float(defaultValue);

            // Insert a property set value for the scalar if one hasn't yet been added.
            switch (themeProperties.TryGetScalar(bindingName, out var existingValueAsFloat))
            {
                case CompositionGetValueStatus.NotFound:
                    // The property hasn't been added yet. Add it.
                    themeProperties.InsertScalar(bindingName, defaultValueAsFloat);
                    _propertyBindings.AddPropertyBinding(
                        bindingName,
                        actualType: PropertySetValueType.Scalar,
                        exposedType: PropertySetValueType.Scalar,
                        defaultValue: Float(defaultValue));
                    return defaultValueAsFloat;

                case CompositionGetValueStatus.Succeeded:
                    // The property has already been added.
                    if (existingValueAsFloat != defaultValueAsFloat)
                    {
                        _issues.ThemePropertyValuesAreInconsistent(bindingName, existingValueAsFloat.ToString(), defaultValueAsFloat.ToString());
                    }

                    // Return the value we have.
                    return existingValueAsFloat;

                case CompositionGetValueStatus.TypeMismatch:
                default:
                    throw new InvalidOperationException();
            }
        }

        // Parses the given bindingSpec, and returns the name of the property in the theme
        // propertyset that should be used for binding to, or null if the property bindings
        // are currently disabled, or the bindingSpec doesn't mention the given property name.
        string GetThemeBindingNameForLottieProperty(string bindingSpec, string propertyName)
            => _translatePropertyBindings
                ? PropertyBindings.FindFirstBindingNameForProperty(bindingSpec, propertyName)
                : null;

        // Implements IDisposable.Dispose(). Currently not needed but will be required
        // if this class needs to hold onto any IDisposable objects.
        void IDisposable.Dispose()
        {
        }

        static CompositionStrokeCap? StrokeCapDefaultIsFlat(ShapeStroke.LineCapType lineCapType)
        {
            switch (lineCapType)
            {
                case ShapeStroke.LineCapType.Butt:
                    return null;
                case ShapeStroke.LineCapType.Round:
                    return CompositionStrokeCap.Round;
                case ShapeStroke.LineCapType.Projected:
                    return CompositionStrokeCap.Square;
                default:
                    throw new InvalidOperationException();
            }
        }

        static CompositionStrokeLineJoin? StrokeLineJoinDefaultIsMiter(ShapeStroke.LineJoinType lineJoinType)
        {
            switch (lineJoinType)
            {
                case ShapeStroke.LineJoinType.Bevel:
                    return CompositionStrokeLineJoin.Bevel;
                case ShapeStroke.LineJoinType.Miter:
                    return null;
                case ShapeStroke.LineJoinType.Round:
                default:
                    return CompositionStrokeLineJoin.Round;
            }
        }

        static CanvasFilledRegionDetermination FilledRegionDetermination(ShapeFill.PathFillType fillType)
        {
            return (fillType == ShapeFill.PathFillType.Winding) ? CanvasFilledRegionDetermination.Winding : CanvasFilledRegionDetermination.Alternate;
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
            Debug.Assert(_addDescriptions, "Descriptions should only be set when requested.");
            Debug.Assert(obj.ShortDescription is null, "Descriptions should never get set more than once.");
            Debug.Assert(obj.LongDescription is null, "Descriptions should never get set more than once.");

            obj.ShortDescription = shortDescription ?? longDescription;
            obj.LongDescription = longDescription;
        }

        // Sets a name on an object. This allows the code generator to give the object
        // a more meaningful name.
        static void Name(IDescribable obj, string name)
        {
            Debug.Assert(obj.Name is null, "Names should never get set more than once.");
            obj.Name = name;
        }

        static WinCompData.Wui.Color Color(Color color)
            => WinCompData.Wui.Color.FromArgb((byte)(255 * color.A), (byte)(255 * color.R), (byte)(255 * color.G), (byte)(255 * color.B));

        static Color Color(WinCompData.Wui.Color color)
            => LottieData.Color.FromArgb(color.A / 255.0, color.R / 255.0, color.G / 255.0, color.B / 255.0);

        static float Float(double value) => (float)value;

        static float Float(Trim value) => (float)value.Value;

        static float? FloatDefaultIsZero(double value) => value == 0 ? null : (float?)value;

        static float? FloatDefaultIsOne(double value) => value == 1 ? null : (float?)value;

        static float Opacity(Opacity value) => (float)value.Value;

        static float PercentF(double value) => (float)value / 100F;

        static Sn.Vector2 Vector2(Vector3 vector3) => Vector2(vector3.X, vector3.Y);

        static Sn.Vector2 Vector2(Vector2 vector2) => Vector2(vector2.X, vector2.Y);

        static Sn.Vector2 Vector2(double x, double y) => new Sn.Vector2((float)x, (float)y);

        static Sn.Vector2 Vector2(float x, float y) => new Sn.Vector2(x, y);

        static Sn.Vector2 Vector2(float x) => new Sn.Vector2(x, x);

        static Sn.Vector2? Vector2DefaultIsOne(Vector3 vector2)
            => vector2.X == 1 && vector2.Y == 1 ? null : (Sn.Vector2?)Vector2(vector2);

        static Sn.Vector2? Vector2DefaultIsZero(Sn.Vector2 vector2)
            => vector2.X == 0 && vector2.Y == 0 ? null : (Sn.Vector2?)vector2;

        static Sn.Vector2 ClampedVector2(Vector3 vector3) => ClampedVector2((float)vector3.X, (float)vector3.Y);

        static Sn.Vector2 ClampedVector2(float x, float y) => Vector2(Clamp(x, 0, 1), Clamp(y, 0, 1));

        static Sn.Vector3 Vector3(double x, double y, double z) => new Sn.Vector3((float)x, (float)y, (float)z);

        static Sn.Vector3 Vector3(Vector3 vector3) => new Sn.Vector3((float)vector3.X, (float)vector3.Y, (float)vector3.Z);

        static Sn.Vector3? Vector3DefaultIsZero(Sn.Vector2 vector2)
            => vector2.X == 0 && vector2.Y == 0 ? null : (Sn.Vector3?)Vector3(vector2);

        static Sn.Vector3? Vector3DefaultIsOne(Sn.Vector3 vector3)
            => vector3.X == 1 && vector3.Y == 1 && vector3.Z == 1 ? null : (Sn.Vector3?)vector3;

        static Sn.Vector3? Vector3DefaultIsOne(Vector3 vector3)
            => Vector3DefaultIsOne(new Sn.Vector3((float)vector3.X, (float)vector3.Y, (float)vector3.Z));

        static Sn.Vector3 Vector3(Sn.Vector2 vector2) => Vector3(vector2.X, vector2.Y, 0);

        static Sn.Vector4 Vector4(WinCompData.Wui.Color color) => new Sn.Vector4(color.R, color.G, color.B, color.A);

        static WinCompData.Wui.Color Color(Sn.Vector4 color) => WinCompData.Wui.Color.FromArgb((byte)color.W, (byte)color.X, (byte)color.Y, (byte)color.Z);

        static float Clamp(float value, float min, float max)
        {
            Debug.Assert(min <= max, "Precondition");
            return Math.Min(Math.Max(min, value), max);
        }

        // Checks whether the given API is available for the current translation.
        bool IsUapApiAvailable(string apiName, string versionDependentFeatureDescription)
        {
            if (!_c.IsUapApiAvailable(apiName))
            {
                _issues.UapVersionNotSupported(versionDependentFeatureDescription, _c.GetUapVersionForApi(apiName).ToString());
                return false;
            }

            return true;
        }

        // A pair of doubles used as a key in a dictionary.
        sealed class ScaleAndOffset
        {
            internal ScaleAndOffset(double scale, double offset)
            {
                Scale = scale;
                Offset = offset;
            }

            internal double Scale { get; }

            internal double Offset { get; }

            public override bool Equals(object obj)
                => obj is ScaleAndOffset other &&
                   other.Scale == Scale &&
                   other.Offset == Offset;

            public override int GetHashCode() => Scale.GetHashCode() ^ Offset.GetHashCode();
        }

        /// <summary>
        /// A Composition graph that is the result of translating a Lottie subtree.
        /// The graph may be represented as a Visual or as a Shape.
        /// </summary>
        /// <remarks>We try to keep as much of the overall translation as CompositionShapes as
        /// that should be the most efficient. However sometimes we have to use Visuals. A Shape
        /// graph can always be turned into a Visual (by wrapping it in a ShapeVisual) but a
        /// Visual cannot be turned into a Shape graph.</remarks>
        abstract class CompositionSubGraph : IDescribable
        {
            readonly LottieToWinCompTranslator _owner;

            CompositionSubGraph(LottieToWinCompTranslator owner)
            {
                _owner = owner;
            }

            internal virtual CompositionShape GetShapeRoot()
            {
                throw new InvalidOperationException();
            }

            // The size is passed in here because it will be needed if a CompositionShape tree
            // needs to be converted to a ShapeVisual. Shape trees need to know their maximum
            // size.
            internal abstract Visual GetVisualRoot(Sn.Vector2 maximumSize);

            /// <summary>
            /// True if the graph can be represented by a root Shape.
            /// Otherwise the graph can only be represented by a root Visual.
            /// Note that all graphs can be represented by a root Visual.
            /// </summary>
            internal virtual bool IsShape => false;

            public string LongDescription { get; set; }

            public string ShortDescription { get; set; }

            public string Name { get; set; }

            void Describe(IDescribable obj)
            {
                if (_owner._addDescriptions && obj.LongDescription is null && obj.ShortDescription is null && !(string.IsNullOrWhiteSpace(LongDescription) || string.IsNullOrWhiteSpace(ShortDescription)))
                {
                    _owner.Describe(obj, LongDescription, ShortDescription);
                }

                if (_owner._addDescriptions && obj.Name is null && !string.IsNullOrWhiteSpace(Name))
                {
                    Name(obj, Name);
                }
            }

            internal sealed class FromVisual : CompositionSubGraph
            {
                readonly Visual _root;

                internal FromVisual(LottieToWinCompTranslator owner, Visual root)
                    : base(owner)
                {
                    _root = root;
                }

                internal override Visual GetVisualRoot(Sn.Vector2 maximumSize)
                {
                    Describe(_root);
                    return _root;
                }
            }

            internal sealed class FromShape : CompositionSubGraph
            {
                readonly CompositionShape _root;

                internal FromShape(LottieToWinCompTranslator owner, CompositionShape root)
                    : base(owner)
                {
                    _root = root;
                }

                internal override CompositionShape GetShapeRoot()
                {
                    Describe(_root);
                    return _root;
                }

                internal override Visual GetVisualRoot(Sn.Vector2 maximumSize)
                {
                    // Create a ShapeVisual to hold the CompositionShape.
                    var result = _owner._c.CreateShapeVisualWithChild(_root, maximumSize);
                    Describe(result);
                    return result;
                }

                internal override bool IsShape => true;
            }

            internal sealed class FromSolidLayer : CompositionSubGraph
            {
                readonly TranslationContext.For<SolidLayer> _context;

                internal FromSolidLayer(LottieToWinCompTranslator owner, TranslationContext.For<SolidLayer> context)
                    : base(owner)
                {
                    _context = context;
                }

                internal override bool IsShape =>
                    !_context.Layer.Masks.Any() || _context.Layer.IsHidden || _context.Layer.Transform.Opacity.AlwaysEquals(LottieData.Opacity.Transparent);

                internal override CompositionShape GetShapeRoot()
                {
                    if (_context.Layer.IsHidden || _context.Layer.Transform.Opacity.AlwaysEquals(LottieData.Opacity.Transparent))
                    {
                        // The layer does not render anything. Nothing to translate. This can happen when someone
                        // creates a solid layer to act like a Null layer.
                        return null;
                    }

                    if (!_owner.TryCreateContainerShapeTransformChain(_context, out var containerRootNode, out var containerContentNode))
                    {
                        // The layer is never visible.
                        return null;
                    }

                    var rectangle = _owner._c.CreateSpriteShape();

                    if (_owner._targetUapVersion <= 7)
                    {
                        // V7 did not reliably draw non-rounded rectangles.
                        // Work around the problem by using a rounded rectangle with a tiny corner radius.
                        var roundedRectangleGeometry = _owner._c.CreateRoundedRectangleGeometry();

                        // NOTE: magic tiny corner radius number - do not change!
                        roundedRectangleGeometry.CornerRadius = new Sn.Vector2(0.000001F);
                        roundedRectangleGeometry.Size = Vector2(_context.Layer.Width, _context.Layer.Height);
                        rectangle.Geometry = roundedRectangleGeometry;
                    }
                    else
                    {
                        // V8 and beyond doesn't need the rounded rectangle workaround.
                        var rectangleGeometry = _owner._c.CreateRectangleGeometry();
                        rectangleGeometry.Size = Vector2(_context.Layer.Width, _context.Layer.Height);
                        rectangle.Geometry = rectangleGeometry;
                    }

                    containerContentNode.Shapes.Add(rectangle);

                    // Opacity is implemented via the alpha channel on the brush.
                    rectangle.FillBrush = _owner.CreateAnimatedColorBrush(_context, _context.Layer.Color, _context.TrimAnimatable(_context.Layer.Transform.Opacity));

                    if (_owner._addDescriptions)
                    {
                        _owner.Describe(rectangle, "SolidLayerRectangle");
                        _owner.Describe(rectangle.Geometry, "SolidLayerRectangle.RectangleGeometry");
                    }

                    Describe(containerRootNode);

                    return containerRootNode;
                }

                internal override Visual GetVisualRoot(Sn.Vector2 maximumSize)
                {
                    // Translate the SolidLayer to a Visual.
                    if (_context.Layer.IsHidden || _context.Layer.Transform.Opacity.AlwaysEquals(LottieData.Opacity.Transparent))
                    {
                        // The layer does not render anything. Nothing to translate. This can happen when someone
                        // creates a solid layer to act like a Null layer.
                        return null;
                    }

                    if (!_owner.TryCreateContainerVisualTransformChain(_context, out var containerRootNode, out var containerContentNode))
                    {
                        // The layer is never visible.
                        return null;
                    }

                    var rectangle = _owner._c.CreateSpriteVisual();
                    rectangle.Size = Vector2(_context.Layer.Width, _context.Layer.Height);

                    containerContentNode.Children.Add(rectangle);

                    var layerHasMasks = false;
#if !NoClipping
                    layerHasMasks = _context.Layer.Masks.Any();
#endif
                    rectangle.Brush = _owner.CreateNonAnimatedColorBrush(_context.Layer.Color);

                    if (_owner._addDescriptions)
                    {
                        _owner.Describe(rectangle, "SolidLayerRectangle");
                    }

                    var result = layerHasMasks
                        ? _owner.TranslateAndApplyMasksForLayer(_context, containerRootNode)
                        : containerRootNode;

                    Describe(result);

                    return result;
                }
            }

            internal sealed class FromShapeLayer : CompositionSubGraph
            {
                readonly TranslationContext.For<ShapeLayer> _context;

                internal FromShapeLayer(LottieToWinCompTranslator owner, TranslationContext.For<ShapeLayer> context)
                    : base(owner)
                {
                    _context = context;
                }

                internal override bool IsShape => !_context.Layer.Masks.Any();

                internal override CompositionShape GetShapeRoot()
                {
                    bool layerHasMasks = false;
#if !NoClipping
                    layerHasMasks = _context.Layer.Masks.Any();
#endif
                    if (layerHasMasks)
                    {
                        throw new InvalidOperationException();
                    }

                    if (!_owner.TryCreateContainerShapeTransformChain(_context, out var rootNode, out var contentsNode))
                    {
                        // The layer is never visible.
                        return null;
                    }

                    var shapeContext = new ShapeContentContext(_owner);

                    // Update the opacity from the transform. This is necessary to push the opacity
                    // to the leafs (because CompositionShape does not support opacity).
                    shapeContext.UpdateOpacityFromTransform(_context, _context.Layer.Transform);
                    contentsNode.Shapes.Add(_owner.TranslateShapeLayerContents(_context, shapeContext, _context.Layer.Contents));

                    return rootNode;
                }

                internal override Visual GetVisualRoot(Sn.Vector2 maximumSize)
                {
                    bool layerHasMasks = false;
#if !NoClipping
                    layerHasMasks = _context.Layer.Masks.Any();
#endif

                    if (!_owner.TryCreateShapeVisualTransformChain(_context, out var rootNode, out var contentsNode))
                    {
                        // The layer is never visible.
                        return null;
                    }

                    var shapeContext = new ShapeContentContext(_owner);

                    contentsNode.Shapes.Add(_owner.TranslateShapeLayerContents(_context, shapeContext, _context.Layer.Contents));

                    return layerHasMasks
                        ? _owner.TranslateAndApplyMasksForLayer(_context, rootNode)
                        : rootNode;
                }
            }
        }
    }
}