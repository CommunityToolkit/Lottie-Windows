// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    /// <summary>
    /// Describes a location in a <see cref="RenderingContext"/>.
    /// </summary>
    sealed class MetadataRenderingContext : RenderingContext
    {
        public MetadataRenderingContext(string name, object? source)
            => (Name, Source) = (name, source);

        public string Name { get; }

        public object? Source { get; }

        /// <summary>
        /// Creates a <see cref="MetadataRenderingContext"/> from a list of
        /// <see cref="MetadataRenderingContext"/>. This it typically used
        /// to represent a path.
        /// </summary>
        /// <returns>A composite <see cref="MetadataRenderingContext"/>.</returns>
        public static MetadataRenderingContext Compose(IEnumerable<MetadataRenderingContext> path)
        {
            var source = path.ToArray();

            if (source.Length == 1)
            {
                // It's already a single MetadataRenderingContext. Just return it.
                return source[0];
            }

            var name = string.Join("/", path.Select(m => m.Name));
            return new MetadataRenderingContext(name, source);
        }

        public override sealed bool DependsOn(RenderingContext other) => true;

        public override bool IsAnimated => false;

        public override sealed RenderingContext WithOffset(Vector2 offset) => this;

        public override RenderingContext WithTimeOffset(double timeOffset) => this;

        public override string ToString() => $"Metadata Name:{Name}";
    }
}
