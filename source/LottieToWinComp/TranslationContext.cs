// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates a <see cref="LottieData.LottieComposition"/> to an equivalent <see cref="Visual"/>.
    /// </summary>
    /// <remarks>See https://helpx.adobe.com/pdf/after_effects_reference.pdf"/> for the
    /// After Effects semantics.</remarks>
#pragma warning disable SA1205 // Partial elements should declare access
    sealed partial class TranslationContext
    {
        // Identifies the Lottie metadata in TranslationResult.SourceMetadata.
        static readonly Guid s_lottieMetadataKey = new Guid("EA3D6538-361A-4B1C-960D-50A6C35563A5");

        // Stores state on behalf of static classes. This allows static classes to
        // associate state they need for their methods with a particular TranslationContext instance.
        readonly Dictionary<Type, object> _stateCache = new Dictionary<Type, object>();

        TranslationContext(
            LottieComposition lottieComposition,
            Compositor compositor,
            in TranslatorConfiguration configuration)
        {
            LottieComposition = lottieComposition;
            ObjectFactory = new CompositionObjectFactory(this, compositor, configuration.TargetUapVersion);
            Issues = new TranslationIssues(configuration.StrictTranslation);
            AddDescriptions = configuration.AddCodegenDescriptions;
            TranslatePropertyBindings = configuration.TranslatePropertyBindings;

            if (configuration.GenerateColorBindings)
            {
                ColorPalette = new Dictionary<Color, string>();
            }
        }

        /// <summary>
        /// Attempts to translates the given <see cref="LottieData.LottieComposition"/>.
        /// </summary>
        /// <param name="lottieComposition">The <see cref="LottieData.LottieComposition"/> to translate.</param>
        /// <param name="configuration">Controls the configuration of the translator.</param>
        /// <returns>The result of the translation.</returns>
        internal static TranslationResult TryTranslateLottieComposition(
            LottieComposition lottieComposition,
            in TranslatorConfiguration configuration)
        {
            // Set up the translator.
            var translator = new TranslationContext(
                lottieComposition,
                new Compositor(),
                configuration: configuration);

            // Translate the Lottie content to a Composition graph.
            translator.Translate();

            var rootVisual = translator.RootVisual;

            var resultRequiredUapVersion = translator.ObjectFactory.HighestUapVersionUsed;

            // See if the version is compatible with what the caller requested.
            if (configuration.TargetUapVersion < resultRequiredUapVersion)
            {
                // We couldn't translate it and meet the requirement for the requested minimum version.
                rootVisual = null;
            }

            // Add the metadata.
            var sourceMetadata = new Dictionary<Guid, object>();

            // Metadata from the source.
            sourceMetadata.Add(
                s_lottieMetadataKey,
                new LottieCompositionMetadata(
                    lottieComposition.Name,
                    lottieComposition.FramesPerSecond,
                    lottieComposition.InPoint,
                    lottieComposition.OutPoint,
                    lottieComposition.Markers.Select(m => (m.Name, m.Frame, m.DurationInFrames))));

            // The list of property binding names.
            translator.PropertyBindings.AddToSourceMetadata(sourceMetadata);

            return new TranslationResult(
                rootVisual: rootVisual,
                translationIssues: translator.Issues.GetIssues().Select(i =>
                    new TranslationIssue(code: i.Code, description: i.Description)),
                minimumRequiredUapVersion: resultRequiredUapVersion,
                sourceMetadata);
        }

        /// <summary>
        /// If true, descriptions are added to the generated objects for use in generated source code.
        /// </summary>
        public bool AddDescriptions { get; }

        /// <summary>
        /// The palette of colors in fills and strokes. Null if color bindings are not enabled.
        /// </summary>
        public Dictionary<Color, string> ColorPalette { get; }

        /// <summary>
        /// Factory for creating composition objects.
        /// </summary>
        public CompositionObjectFactory ObjectFactory { get; }

        /// <summary>
        /// Manages the collection of issues that have been seen during the translation.
        /// </summary>
        public TranslationIssues Issues { get; }

        /// <summary>
        /// The <see cref="LottieData.LottieComposition"/> being translated.
        /// </summary>
        public LottieComposition LottieComposition { get; }

        /// <summary>
        /// Factory used for creating CompositionPropertySet properties
        /// that map from the Progress value of the animated visual
        /// to another value. These are used to create properties
        /// that are required by cubic Bezier expressions used for
        /// spatial Beziers.
        /// </summary>
        public ProgressMapFactory ProgressMapFactory { get; } = new ProgressMapFactory();

        /// <summary>
        /// The name of the property on the resulting <see cref="Visual"/> that controls the progress
        /// of the animation. Setting this property (directly or with an animation)
        /// between 0 and 1 controls the position of the animation.
        /// </summary>
        public static string ProgressPropertyName => "Progress";

        /// <summary>
        /// The names that are bound to properties (such as the Color of a SolidColorFill).
        /// Keep track of them here so that codegen can generate properties for them.
        /// </summary>
        public PropertyBindings PropertyBindings { get; } = new PropertyBindings();

        /// <summary>
        /// The root Visual of the resulting translation.
        /// </summary>
        public ContainerVisual RootVisual { get; private set; }

        /// <summary>
        /// True iff theme property bindings are enabled.
        /// </summary>
        public bool TranslatePropertyBindings { get; }

        /// <summary>
        /// Returns the state cache of the given type, creating it if it doesn't
        /// already exist.
        /// </summary>
        /// <typeparam name="T">The type of the cache.</typeparam>
        /// <returns>A state cache.</returns>
        /// <remarks>This is used to allow static classes to store and retrieve state
        /// for a translation.</remarks>
        public T GetStateCache<T>()
            where T : class, new()
        {
            // Look up the cache.
            if (_stateCache.TryGetValue(typeof(T), out var cached))
            {
                // The object has already been created. Return it.
                return (T)cached;
            }

            // The object has not been created yet - create it now and cache
            // it for next time.
            var result = new T();
            _stateCache.Add(typeof(T), result);
            return result;
        }

        /// <summary>
        /// Returns the asset with the given ID and type, or null if no such asset exists.
        /// </summary>
        /// <returns>The asset or null.</returns>
        public Asset GetAssetById(LayerContext context, string assetId, Asset.AssetType expectedAssetType)
        {
            var referencedAsset = LottieComposition.Assets.GetAssetById(assetId);
            if (referencedAsset is null)
            {
                Issues.ReferencedAssetDoesNotExist(assetId);
            }
            else if (referencedAsset.Type != expectedAssetType)
            {
                Issues.InvalidAssetReferenceFromLayer(context.Layer.Type.ToString(), assetId, referencedAsset.Type.ToString(), expectedAssetType.ToString());
                referencedAsset = null;
            }

            return referencedAsset;
        }

        // Translates the LottieComposition. This must only be called once.
        void Translate()
        {
            Debug.Assert(RootVisual is null, "Translate() must only be called once.");

            if (LottieComposition.Is3d)
            {
                // We don't yet support 3d, so report that as an issue for this Lottie.
                Issues.ThreeDIsNotSupported();
            }

            // Create the root context.
            var context = new CompositionContext(this, LottieComposition);

            // Create the root Visual.
            RootVisual = ObjectFactory.CreateContainerVisual();

            RootVisual.SetDescription(this, () => ("The root of the composition.", string.Empty));
            RootVisual.SetName("Root");

            // Add the master progress property to the visual.
            RootVisual.Properties.InsertScalar(ProgressPropertyName, 0);

            // Add the translations of each layer to the root visual. This will recursively
            // add the tranlation of the layers in precomps.
            var contentsChildren = RootVisual.Children;
            foreach (var visual in Layers.TranslateLayersToVisuals(context))
            {
                contentsChildren.Add(visual);
            }

            // Add and animate the properties that are used to create modified (scaled, eased)
            // versions of the Progress clock. These are necessary for the implementation of
            // spatial beziers and time remapping.
            foreach (var (name, scale, offset, ranges) in ProgressMapFactory.GetVariables())
            {
                RootVisual.Properties.InsertScalar(name, 0);
                var animation = ObjectFactory.CreateScalarKeyFrameAnimation();
                animation.Duration = LottieComposition.Duration;
                animation.SetReferenceParameter(ExpressionFactory.RootName, RootVisual);
                foreach (var keyframe in ranges)
                {
                    animation.InsertKeyFrame(keyframe.Start, 0, ObjectFactory.CreateStepThenHoldEasingFunction());
                    animation.InsertKeyFrame(keyframe.End, 1, ObjectFactory.CreateCompositionEasingFunction(keyframe.Easing));
                }

                Animate.StartKeyframeAnimation(this, RootVisual.Properties, name, animation, scale, offset);
            }
        }
   }
}