// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// Represents directed acyclic graph of layer groups.
    /// Node1 is child of Node2 iff they have time ranges that intersect and Node1 goes after Node2 in z-order.
    ///
    ///                         +Z
    /// |---------------------------------------------------|
    ///      |--Node1--|
    ///             |---Node2---|                              Time -->
    ///                                |----Node3----|
    /// |---------------------------------------------------|
    ///                         -Z
    ///
    /// In this example Node1 is a child of Node2, but not of Node3.
    /// Nodes can have multiple parents. Optimizations can be made to graphs that don't overlap in
    /// time, which is often the case when a single Lottie file contains multiple animations.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif

    class LayersGraph
    {
        List<GraphNode> nodes;

        class GraphNode
        {
            public LayerGroup Group { get; }

            public List<GraphNode> Parents { get; }

            /// <summary>
            /// If not null then this node was merged with another node and wee should consider them as one node now.
            /// </summary>
            public GraphNode? MergedWithNode { get; set; }

            /// <summary>
            /// If this node was merged with another node then this field will contain result of merging
            /// their layer groups together. Note: MergedGroup and MergedWithNode.MergedGroup are always equal.
            /// </summary>
            public LayerGroup? MergedGroup { get; set; }

            public GraphNode(LayerGroup group)
            {
                Group = group;
                Parents = new List<GraphNode>();
                MergedWithNode = null;
                MergedGroup = null;
            }

            public void AddParent(GraphNode parent)
            {
                Parents.Add(parent);
            }

            public bool IsChildOf(GraphNode node)
            {
                return IsChildOf(node, new HashSet<GraphNode>());
            }

            private bool IsChildOf(GraphNode node, HashSet<GraphNode> visited)
            {
                if (visited.Contains(this))
                {
                    return false;
                }

                visited.Add(this);

                foreach (var parent in Parents)
                {
                    if (parent.Equals(node) || parent.IsChildOf(node, visited))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public LayersGraph(List<LayerGroup> layerGroups)
        {
            nodes = new List<GraphNode>();

            foreach (var group in layerGroups)
            {
                var node = new GraphNode(group);
                var range = TimeRange.GetForLayer(group.MainLayer);

                // Loop over already existing nodes in reverse. It helps reduce number of
                // parent links that we generate, otherwise it will produce transitive closure of this graph
                // (each node will have links to all direct and undirect parents)
                foreach (var other in nodes.Select(v => v).Reverse().ToList())
                {
                    // TODO: Probably we should also check MatteLayer ranges here too.
                    if (!node.IsChildOf(other) && TimeRange.GetForLayer(other.Group.MainLayer).Intersect(range))
                    {
                        node.AddParent(other);
                    }
                }

                nodes.Add(node);
            }
        }

        List<Tuple<GraphNode, GraphNode>> GetCandidatesForMerging()
        {
            var candidates = new List<Tuple<GraphNode, GraphNode>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!nodes[i].Group.CanBeMerged)
                {
                    continue;
                }

                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (!nodes[j].Group.CanBeMerged)
                    {
                        continue;
                    }

                    if (!nodes[i].IsChildOf(nodes[j]) && !nodes[j].IsChildOf(nodes[i]))
                    {
                        candidates.Add(new Tuple<GraphNode, GraphNode>(nodes[i], nodes[j]));
                    }
                }
            }

            return candidates;
        }

        /// <summary>
        /// Simple depth-first search for topological sorting.
        /// </summary>
        void WalkGraph(GraphNode node, HashSet<GraphNode> visited, List<LayerGroup> layerGroups, LayersIndexMapper mapping, LayersIndexMapper.IndexGenerator generator)
        {
            if (visited.Contains(node))
            {
                return;
            }

            visited.Add(node);

            // If this node is merged with another node then we mark it as visited too.
            if (node.MergedWithNode is not null)
            {
                visited.Add(node.MergedWithNode!);
            }

            foreach (var parent in node.Parents)
            {
                WalkGraph(parent, visited, layerGroups, mapping, generator);
            }

            if (node.MergedWithNode is not null)
            {
                foreach (var parent in ((GraphNode)node.MergedWithNode!).Parents)
                {
                    WalkGraph(parent, visited, layerGroups, mapping, generator);
                }
            }

            if (node.MergedGroup is not null)
            {
                // Remap indexes for matte layers.
                if (node.Group.MatteLayer is not null)
                {
                    int matteIndex = generator.GenerateIndex();
                    mapping.SetMapping(node.Group.MatteLayer!.Index, matteIndex);
                    mapping.SetMapping(node.MergedWithNode!.Group.MatteLayer!.Index, matteIndex);
                }

                // Remap indexes for main layers.
                int index = generator.GenerateIndex();
                mapping.SetMapping(node.Group.MainLayer.Index, index);
                mapping.SetMapping(node.MergedWithNode!.Group.MainLayer.Index, index);

                // Add merged layer group of current node to the result.
                layerGroups.Add(node.MergedGroup!);
            }
            else
            {
                // Remap indexes for matte layers.
                if (node.Group.MatteLayer is not null)
                {
                    mapping.SetMapping(node.Group.MatteLayer!.Index, generator.GenerateIndex());
                }

                // Remap indexes for main layers.
                mapping.SetMapping(node.Group.MainLayer.Index, generator.GenerateIndex());

                // Add layer group of current node to the result.
                layerGroups.Add(node.Group);
            }
        }

        /// <summary>
        /// Merge all layer groups that we can merge while preserving the z-order of layers that overlap in time.
        /// </summary>
        public void MergeAllPossibleLayerGroups(MergeHelper mergeHelper)
        {
            foreach (var pair in GetCandidatesForMerging())
            {
                var a = pair.Item1;
                var b = pair.Item2;

                if (a.MergedWithNode is not null || b.MergedWithNode is not null)
                {
                    continue;
                }

                var mergeRes = mergeHelper.MergeLayerGroups(a.Group, b.Group);

                if (!mergeRes.Success)
                {
                    continue;
                }

                a.MergedGroup = b.MergedGroup = mergeRes.Value!;

                a.MergedWithNode = b;
                b.MergedWithNode = a;
            }
        }

        /// <summary>Returns list of all layer groups in correct z-order (Takes into account the fact that some of them are merged).</summary>
        /// <returns>List of all layer groups in correct z-order.</returns>
        public List<LayerGroup> GetLayerGroups()
        {
            var visited = new HashSet<GraphNode>();
            var layerGroups = new List<LayerGroup>();

            var mapping = new LayersIndexMapper();
            var generator = new LayersIndexMapper.IndexGenerator();

            // Topological sorting.
            foreach (var node in nodes)
            {
                WalkGraph(node, visited, layerGroups, mapping, generator);
            }

            layerGroups = mapping.RemapLayerGroups(layerGroups);

            return layerGroups;
        }
    }
}
