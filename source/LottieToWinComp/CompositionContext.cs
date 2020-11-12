﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// The context in which the top-level layers of a <see cref="LottieComposition"/> or the layers
    /// of a <see cref="PreCompLayer"/> are translated.
    /// </summary>
    sealed class CompositionContext
    {
        internal CompositionContext(
            TranslationContext context,
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
            Layers = layers;
            Size = size;
            StartTime = startTime;
            DurationInFrames = durationInFrames;
            ObjectFactory = Translation.ObjectFactory;
            Issues = Translation.Issues;
        }

        internal CompositionContext(
            TranslationContext context,
            LottieComposition lottieComposition)
            : this(
                  context,
                  lottieComposition.Layers,
                  new Sn.Vector2((float)lottieComposition.Width, (float)lottieComposition.Height),
                  startTime: lottieComposition.InPoint,
                  durationInFrames: lottieComposition.OutPoint - lottieComposition.InPoint)
        {
        }

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