// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// A factory for a Composition graph that is the result of translating a Lottie subtree.
    /// The factory is used to create a <see cref="Visual"/>, an optionally also a
    /// <see cref="CompositionShape"/>.
    /// </summary>
    /// <remarks>We try to keep as much as possible of the overall translation as
    /// CompositionShapes as that should be the most efficient at runtime. However sometimes
    /// we have to use Visuals. A Shape graph can always be turned into a Visual (by
    /// wrapping it in a ShapeVisual) but a Visual cannot be turned into a Shape graph.
    /// </remarks>
    abstract class LayerTranslator : IDescribable
    {
        /// <summary>
        /// Gets the translation of the layer as a <see cref="CompositionShape"/>.
        /// Only valid to call is <see cref="IsShape"/> is <c>true</c>.
        /// </summary>
        /// <returns>The <see cref="CompositionShape"/> or null if the layer is never visible.</returns>
        internal virtual CompositionShape? GetShapeRoot(TranslationContext context)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Gets the translation of the layer as a <see cref="Visual"/>.
        /// </summary>
        /// <returns>The <see cref="Visual"/> or null if the layer is never visible.</returns>
        /// <remarks>
        /// The size (in the context) is needed in case a CompositionShape tree
        /// needs to be converted to a ShapeVisual. Shape trees need to know their
        /// maximum size.
        /// </remarks>
        internal abstract Visual? GetVisualRoot(CompositionContext context);

        /// <summary>
        /// True if the graph can be represented by a root CompositionShape.
        /// Otherwise the graph can only be represented by a root Visual.
        /// Note that all graphs can be represented by a root Visual but only
        /// some can be represented by a root CompositionShape.
        /// </summary>
        internal virtual bool IsShape => false;

        public string? LongDescription { get; set; }

        public string? ShortDescription { get; set; }

        public string? Name { get; set; }

        private protected void Describe(TranslationContext context, IDescribable obj)
        {
            if (context.AddDescriptions && obj.LongDescription is null && obj.ShortDescription is null && !(string.IsNullOrWhiteSpace(LongDescription) || string.IsNullOrWhiteSpace(ShortDescription)))
            {
                obj.SetDescription(context, LongDescription, ShortDescription);
            }

            if (context.AddDescriptions && obj.Name is null && !string.IsNullOrWhiteSpace(Name))
            {
                obj.SetName(Name);
            }
        }

        /// <summary>
        /// A <see cref="LayerTranslator"/> for an eagerly translated Visual.
        /// </summary>
        internal sealed class FromVisual : LayerTranslator
        {
            readonly Visual _root;

            internal FromVisual(Visual root)
            {
                _root = root;
            }

            internal override Visual GetVisualRoot(CompositionContext context)
            {
                Describe(context, _root);
                return _root;
            }
        }

        /// <summary>
        /// A <see cref="LayerTranslator"/> for an eagerly translated Shape.
        /// </summary>
        internal sealed class FromShape : LayerTranslator
        {
            readonly CompositionShape _root;

            internal FromShape(CompositionShape root)
            {
                _root = root;
            }

            internal override CompositionShape GetShapeRoot(TranslationContext context)
            {
                Describe(context, _root);
                return _root;
            }

            internal override Visual GetVisualRoot(CompositionContext context)
            {
                // Create a ShapeVisual to hold the CompositionShape.
                var result = context.ObjectFactory.CreateShapeVisualWithChild(_root, context.Size);
                Describe(context, result);
                return result;
            }

            internal override bool IsShape => true;
        }
    }
}
