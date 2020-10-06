// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable // Temporary while enabling nullable everywhere.

using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// The context in which to translate a layer. This is used to ensure that
    /// layers are translated in the context of the composition or their containing
    /// PreComp, and to carry around other context-specific state.
    /// </summary>
    abstract class LayerContext
    {
        FrameNumberEqualityComparer _frameNumberEqualityComparer;

        private protected LayerContext(CompositionContext compositionContext, Layer layer)
        {
            CompositionContext = compositionContext;
            Layer = layer;

            // Copy some frequently accessed properties so they can accessed more efficiently.
            Translation = compositionContext.Translation;
            ObjectFactory = Translation.ObjectFactory;
            Issues = Translation.Issues;
        }

        public CompositionContext CompositionContext { get; }

        public CompositionObjectFactory ObjectFactory { get; }

        public TranslationContext Translation { get; }

        public TranslationIssues Issues { get; }

        internal Layer Layer { get; }

        /// <summary>
        /// Returns the <see cref="Layer"/> from which the current layer inherits transforms
        /// or null if there is no transform parent.
        /// </summary>
        public Layer TransformParentLayer =>
            Layer.Parent.HasValue ? CompositionContext.Layers.GetLayerById(Layer.Parent.Value) : null;

        public override string ToString() => $"{GetType().Name} - {Layer.Name}";

        /// <summary>
        /// The <see cref="Layer"/>'s in point as a progress value.
        /// </summary>
        internal float InPointAsProgress =>
            (float)((Layer.InPoint - CompositionContext.StartTime) / CompositionContext.DurationInFrames);

        /// <summary>
        /// The <see cref="Layer"/>'s out point as a progress value.
        /// </summary>
        internal float OutPointAsProgress =>
            (float)((Layer.OutPoint - CompositionContext.StartTime) / CompositionContext.DurationInFrames);

        internal IEqualityComparer<double> FrameNumberComparer =>
            _frameNumberEqualityComparer ??= new FrameNumberEqualityComparer(this);

        /// <summary>
        /// Compares frame numbers for equality. This takes into account the lossiness of the conversion
        /// that is done from <see cref="double"/> frame numbers to <see cref="float"/> progress values.
        /// </summary>
        sealed class FrameNumberEqualityComparer : IEqualityComparer<double>
        {
            readonly LayerContext _context;

            internal FrameNumberEqualityComparer(LayerContext context)
            {
                _context = context;
            }

            public bool Equals(double x, double y) => ProgressOf(x) == ProgressOf(y);

            public int GetHashCode(double obj) => ProgressOf(obj).GetHashCode();

            // Converts a frame number into a progress value.
            float ProgressOf(double value) =>
                (float)((value - _context.CompositionContext.StartTime) / _context.CompositionContext.DurationInFrames);
        }

        /// <summary>
        /// Allow a <see cref="LayerContext"/> to be used wherever a <see cref="LottieToWinComp.CompositionContext"/> is required.
        /// </summary>
        public static implicit operator CompositionContext(LayerContext obj) => obj.CompositionContext;

        /// <summary>
        /// Allow a <see cref="LayerContext"/> to be used wherever a <see cref="TranslationContext"/> is required.
        /// </summary>
        public static implicit operator TranslationContext(LayerContext obj) => obj.CompositionContext.Translation;
    }
}
