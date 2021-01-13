// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    sealed class ContentAndContext
    {
        internal ContentAndContext(RenderingContent content, RenderingContext context)
        {
            Content = content;
            Context = context;
        }

        public RenderingContent Content { get; }

        public RenderingContext Context { get; }

        public ContentAndContext WithContext(RenderingContext replacementContext)
            => new ContentAndContext(Content, replacementContext);

        public void Deconstruct(out RenderingContent content, out RenderingContext context)
        {
            content = Content;
            context = Context;
        }

        public override string ToString() => $"{Content}, {Context}";
    }
}