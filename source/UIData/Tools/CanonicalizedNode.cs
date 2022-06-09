// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.UIData.Tools
{
    abstract class CanonicalizedNode<T> : Graph.Node<T>
        where T : CanonicalizedNode<T>, new()
    {
        public CanonicalizedNode()
        {
            Canonical = (T)this;
        }

        /// <summary>
        /// Gets or sets the node that is equivalent to this node. Initially set to this.
        /// </summary>
        public T Canonical { get; set; }
    }
}