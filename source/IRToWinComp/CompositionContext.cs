// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeful;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    /// <summary>
    /// The context in which the top-level layers of a <see cref="IRComposition"/> or the layers
    /// of a <see cref="PreCompLayer"/> are translated.
    /// </summary>
    sealed class CompositionContext
    {
        internal CompositionContext(
            TranslationContext context,
            CompositionContext? parentComposition,
            string compositionName,
            LayerCollection layers,
            Sn.Vector2 size,
            double startTime,
            double durationInFrames)
        {
            if (durationInFrames < 0)
            {
                throw new ArgumentOutOfRangeException("durationInFrames");
            }

            Translation = context;
            ParentComposition = parentComposition;
            Name = string.IsNullOrWhiteSpace(compositionName) ? "<precomp>" : compositionName;
            Layers = layers;
            Size = size;
            StartTime = startTime;
            DurationInFrames = durationInFrames;
            ObjectFactory = Translation.ObjectFactory;
            Issues = Translation.Issues;
        }

        internal CompositionContext(
            TranslationContext context,
            IRComposition lottieComposition)
            : this(
                  context,
                  null,
                  string.IsNullOrWhiteSpace(lottieComposition.Name) ? "<root>" : lottieComposition.Name,
                  lottieComposition.Layers,
                  new Sn.Vector2((float)lottieComposition.Width, (float)lottieComposition.Height),
                  startTime: lottieComposition.InPoint,
                  durationInFrames: lottieComposition.OutPoint - lottieComposition.InPoint)
        {
        }

        /// <summary>
        /// The name of the composition. <seealso cref="Path"/>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The path to this <see cref="CompositionContext"/>. Used for issue messages.
        /// <seealso cref="Name"/>.
        /// </summary>
        public string Path => $"{ParentComposition?.Path}/{Name}";

        /// <summary>
        /// The parent of this composition, or null if this is the root composition.
        /// </summary>
        public CompositionContext? ParentComposition { get; }

        /// <summary>
        /// The <see cref="Translation"/> in which the contents are being translated.
        /// </summary>
        public TranslationContext Translation { get; }

        public CompositionObjectFactory ObjectFactory { get; }

        public TranslationIssues Issues { get; }

        internal Sn.Vector2 Size { get; }

        internal double StartTime { get; }

        internal double EndTime => StartTime + DurationInFrames;

        internal double DurationInFrames { get; }

        /// <summary>
        /// The layers in this context.
        /// </summary>
        public LayerCollection Layers { get; }

        public ImageLayerContext CreateLayerContext(ImageLayer layer) => new ImageLayerContext(this, layer);

        public PreCompLayerContext CreateLayerContext(PreCompLayer layer) => new PreCompLayerContext(this, layer);

        public ShapeLayerContext CreateLayerContext(ShapeLayer layer) => new ShapeLayerContext(this, layer);

        public SolidLayerContext CreateLayerContext(SolidLayer layer) => new SolidLayerContext(this, layer);

        public TextLayerContext CreateLayerContext(TextLayer layer) => new TextLayerContext(this, layer);

        /// <summary>
        /// Allow a <see cref="CompositionContext"/> to be used wherever a <see cref="Translation"/> is required.
        /// </summary>
        public static implicit operator TranslationContext(CompositionContext obj) => obj.Translation;
    }
}
