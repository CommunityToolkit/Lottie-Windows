// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools
{
#if !WINDOWS_UWP
    public
#endif
    abstract class Graph
    {
        int _vertexCounter;

        protected private Graph()
        {
        }

        /// <summary>
        /// Returns the graph of nodes reachable from the given <see cref="CompositionObject"/> root.
        /// </summary>
        public static ObjectGraph<Node> FromCompositionObject(CompositionObject root, bool includeVertices)
            => ObjectGraph<Node>.FromCompositionObject(root, includeVertices);

        /// <summary>
        /// A simple non-extensible node type.
        /// </summary>
        public sealed class Node : Node<Node>
        {
        }

        public class Node<T> : INodePrivate<T>
            where T : Node<T>, new()
        {
            static readonly Vertex[] EmptyVertexArray = new Vertex[0];

            List<Vertex> _inReferences;

            public object Object { get; set; }

            public Vertex[] InReferences => _inReferences == null ? EmptyVertexArray : _inReferences.ToArray();

            public int ReferenceCount => InReferences.Length;

            public NodeType Type { get; private set; }

            /// <summary>
            /// The position of this node in a traversal of the graph.
            /// </summary>
            public int Position { get; private set; }

            public struct Vertex
            {
                /// <summary>
                /// The position of this vertex in a traversal of the graph.
                /// </summary>
                public int Position { get; internal set; }

                /// <summary>
                /// The node at the other end of the <see cref="Vertex"/>.
                /// </summary>
                public T Node { get; internal set; }
            }

            List<Vertex> INodePrivate<T>.InReferences
            {
                get
                {
                    if (_inReferences == null)
                    {
                        _inReferences = new List<Vertex>();
                    }

                    return _inReferences;
                }
            }

            void INodePrivate<T>.Initialize(NodeType type, int position)
            {
                Type = type;
                Position = position;
            }
        }

        public enum NodeType
        {
            CompositionObject,
            CompositionPath,
            CanvasGeometry,
        }

        protected void InitializeNode<T>(T node, NodeType type, int position)
            where T : Node<T>, new()
            => NodePrivate(node).Initialize(type, position);

        protected void AddVertex<T>(T from, T to)
            where T : Node<T>, new()
        {
            var toNode = NodePrivate(to);
            toNode.InReferences.Add(new Node<T>.Vertex { Position = _vertexCounter++, Node = from });
        }

        static INodePrivate<T> NodePrivate<T>(T node)
            where T : Node<T>, new()
        {
            return node;
        }

        // Private inteface that allows ObjectGraph to modify Nodes.
        interface INodePrivate<T>
            where T : Node<T>, new()
        {
            void Initialize(NodeType type, int position);

            List<Node<T>.Vertex> InReferences { get; }
        }
    }
}
