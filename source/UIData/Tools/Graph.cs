// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.WinUI.Lottie.WinCompData;

namespace CommunityToolkit.WinUI.Lottie.UIData.Tools
{
#if PUBLIC_UIData
    public
#endif
    abstract class Graph
    {
        int _vertexCounter;

        private protected Graph()
        {
        }

        /// <summary>
        /// Returns the graph of nodes reachable from the given <see cref="CompositionObject"/> root.
        /// </summary>
        /// <returns>A <see cref="Graph"/> from the given Composition tree.</returns>
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
            object? _obj;

            List<Vertex>? _inReferences;

            // _obj should be non-null by the time this is called. We require that
            // SetObject(...) is set as part of initialization of the object.
            public object Object
            {
                get
                {
                    Debug.Assert(_obj is not null, "Precondition");
                    return _obj!;
                }
            }

            /// <summary>
            /// Sets the Object property. This should be called as part of intialization of the object.
            /// </summary>
            public void SetObject(object obj)
            {
                if (_obj is not null)
                {
                    throw new InvalidOperationException();
                }

                _obj = obj;
            }

            public Vertex[] InReferences => _inReferences is null ? Array.Empty<Vertex>() : _inReferences.ToArray();

            public int ReferenceCount => InReferences.Length;

            public NodeType Type { get; private set; }

            /// <summary>
            /// The position of this node in a traversal of the graph.
            /// </summary>
            public int Position { get; private set; }

            /// <summary>
            /// Returns <c>True</c> iff this node is reachable from the given node.
            /// </summary>
            /// <param name="node">The node to test.</param>
            /// <returns><c>True</c> if this node is reachable from the given node.</returns>
            public bool IsReachableFrom(Node<T>? node) => !(node is null) && IsReachableFrom(node, new HashSet<Node<T>>());

            bool IsReachableFrom(Node<T> targetNode, HashSet<Node<T>> alreadyVisited)
            {
                // Walk the tree of references to this node, ignoring any that have already
                // been visited.
                foreach (var vertex in targetNode.InReferences)
                {
                    // inRef is a node that directly references this node.
                    var inRef = vertex.Node;

                    if (alreadyVisited.Add(inRef))
                    {
                        // We haven't examined the inRef node yet.
                        if (inRef == targetNode || inRef.IsReachableFrom(targetNode, alreadyVisited))
                        {
                            // inRef is the targetNode, or it's reachable from targetNode.
                            return true;
                        }

                        // This node is not reachable from the targetNode.
                    }
                }

                // Not reachable from targetNode.
                return false;
            }

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

                // For debugging purposes only.
                public override string ToString() => $"{Node}--{Position}-->";
            }

            List<Vertex> INodePrivate<T>.InReferences
            {
                get
                {
                    if (_inReferences is null)
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
            LoadedImageSurface,
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
