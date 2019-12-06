// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    abstract class CanonicalizedNode<T> : Graph.Node<T>
        where T : CanonicalizedNode<T>, new()
    {
        Vertex[] _canonicalInRefs;

        public CanonicalizedNode()
        {
            Canonical = (T)this;
            NodesInGroup = new T[] { (T)this };
        }

        /// <summary>
        /// Gets or sets the node that is equivalent to this node. Initially set to this.
        /// </summary>
        public T Canonical { get; set; }

        /// <summary>
        /// Gets or sets the nodes that are canonicalized to the canonical node.
        /// </summary>
        public IEnumerable<T> NodesInGroup { get; set; }

        public bool IsCanonical => Canonical == this;

        public IEnumerable<Vertex> CanonicalInRefs
        {
            get
            {
                if (_canonicalInRefs == null)
                {
                    // Get the references from all canonical nodes
                    // that reference all versions of this node.
                    _canonicalInRefs =
                        (from n in NodesInGroup
                         from r in n.InReferences
                         where r.Node.IsCanonical
                         orderby r.Position
                         select r).ToArray();
                }

                return _canonicalInRefs;
            }
        }
    }
}