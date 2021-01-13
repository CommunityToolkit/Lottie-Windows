// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class MetadataRenderingContext : RenderingContext
    {
        internal MetadataRenderingContext(string name) => Name = name;

        public object? Source { get; set; }

        public string Name { get; }

        public override string ToString() => $"Name: {Name}";
    }
}
