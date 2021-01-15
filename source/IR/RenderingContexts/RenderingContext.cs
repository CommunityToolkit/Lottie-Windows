// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    /// <summary>
    /// The context in which a <see cref="RenderingContents.RenderingContent"/> is rendered.
    /// </summary>
    abstract class RenderingContext : IEnumerable<RenderingContext>
    {
        public static RenderingContext Null { get; } = new NullRenderingContext();

        public abstract RenderingContext WithTimeOffset(double timeOffset);

        public virtual RenderingContext WithOffset(Vector3 offset) => this;

        public abstract bool IsAnimated { get; }

        public static RenderingContext Compose(IEnumerable<RenderingContext> renderingContexts)
            => ComposeAlreadyFlattened(Flatten(renderingContexts).ToArray());

        public static RenderingContext Compose(params RenderingContext[] renderingContexts)
            => Compose((IList<RenderingContext>)renderingContexts);

        public static RenderingContext Compose(IList<RenderingContext> renderingContexts)
            => renderingContexts.Count switch
            {
                0 => Null,
                1 => renderingContexts[0],
                _ => ComposeAlreadyFlattened(Flatten(renderingContexts).ToArray()),
            };

        static RenderingContext ComposeAlreadyFlattened(IReadOnlyList<RenderingContext> renderingContexts)
            => renderingContexts.Count switch
            {
                0 => Null,
                1 => renderingContexts[0],
                _ => new CompositeRenderingContext(renderingContexts),
            };

        public static RenderingContext operator +(RenderingContext a, RenderingContext b)
            => Compose(a, b);

        public RenderingContext Where(Func<RenderingContext, bool> predicate)
        {
            if (this is CompositeRenderingContext composite)
            {
                return Compose(composite.Items.Where(predicate));
            }
            else if (predicate(this))
            {
                return this;
            }
            else
            {
                return Null;
            }
        }

        /// <summary>
        /// Returns a context with all items except any of <typeparamref name="T"/> that
        /// return false for the predicate.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="RenderingContext"/> to filter.</typeparam>
        /// <returns>A filtered <see cref="RenderingContext"/>.</returns>
        public RenderingContext Filter<T>(Func<T, bool> predicate)
            where T : RenderingContext
            => Where(item => item is T itemAsT ? predicate(itemAsT) : true);

        public static IEnumerable<RenderingContext> Filter<T>(IEnumerable<RenderingContext> items, Func<T, bool> predicate)
            => items.Where(item => !(item is T itemT) || predicate(itemT));

        static IEnumerable<RenderingContext> Flatten(IEnumerable<RenderingContext> input)
        {
            foreach (var item in input)
            {
                switch (item)
                {
                    case NullRenderingContext nullContext:
                        break;
                    case CompositeRenderingContext compositeContext:
                        foreach (var subItem in compositeContext.Items)
                        {
                            yield return subItem;
                        }

                        break;
                    default:
                        yield return item;
                        break;
                }
            }
        }

        IEnumerator<RenderingContext> IEnumerable<RenderingContext>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        IEnumerator<RenderingContext> GetEnumerator()
            => (this is CompositeRenderingContext composite)
                ? composite.Items.GetEnumerator()
                : (IEnumerator<RenderingContext>)(new RenderingContext[] { this }).GetEnumerator();

        sealed class NullRenderingContext : RenderingContext
        {
            public override bool IsAnimated => false;

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() => "Null";
        }
    }
}
