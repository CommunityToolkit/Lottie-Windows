using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
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

            public GraphNode? MergedWithNode { get; set; }

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

            public bool IsChildOf(GraphNode node, HashSet<GraphNode>? visited = null)
            {
                if (visited is null)
                {
                    visited = new HashSet<GraphNode>();
                }

                if (visited.Contains(node))
                {
                    return false;
                }

                visited.Add(node);

                foreach (var parent in Parents)
                {
                    if (parent.Equals(node) || parent.IsChildOf(node))
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
                var range = Range.GetForLayer(group.MainLayer);

                var reversedNodes = nodes.Select(v => v).ToList();
                reversedNodes.Reverse();
                foreach (var other in reversedNodes)
                {
                    if (!node.IsChildOf(other) && Range.GetForLayer(other.Group.MainLayer).Intersect(range))
                    {
                        node.AddParent(other);
                    }
                }

                nodes.Add(node);
            }
        }

        struct MergableNodePair
        {
            public GraphNode First { get; set; }

            public GraphNode Second { get; set; }
        }

        List<MergableNodePair> GetMergableNodes()
        {
            var res = new List<MergableNodePair>();
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
                        res.Add(new MergableNodePair { First = nodes[i], Second = nodes[j] });
                    }
                }
            }

            return res;
        }

        void WalkGraph(GraphNode node, HashSet<GraphNode> visited, List<LayerGroup> layerGroups, LayersIndexMapper mapping, LayersIndexMapper.IndexGenerator generator)
        {
            if (visited.Contains(node))
            {
                return;
            }

            visited.Add(node);

            if (node.MergedWithNode is not null)
            {
                visited.Add((GraphNode)node.MergedWithNode!);
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
                if (node.Group.MatteLayer is not null)
                {
                    int matteIndex = generator.GenerateIndex();
                    mapping.SetMapping(node.Group.MatteLayer!.Index, matteIndex);
                    mapping.SetMapping(node.MergedWithNode!.Group.MatteLayer!.Index, matteIndex);
                }

                int index = generator.GenerateIndex();
                mapping.SetMapping(node.Group.MainLayer.Index, index);
                mapping.SetMapping(node.MergedWithNode!.Group.MainLayer.Index, index);

                // todo: add matte layer
                layerGroups.Add(node.MergedGroup!);
            }
            else
            {
                if (node.Group.MatteLayer is not null)
                {
                    mapping.SetMapping(node.Group.MatteLayer!.Index, generator.GenerateIndex());
                }

                mapping.SetMapping(node.Group.MainLayer.Index, generator.GenerateIndex());

                // todo: add matte layer
                layerGroups.Add(node.Group);
            }
        }

        public void MergeAllPossibleLayerGroups(MergeHelper mergeHelper)
        {
            foreach (var pair in GetMergableNodes())
            {
                var a = pair.First;
                var b = pair.Second;

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

        public List<LayerGroup> GetLayerGroups()
        {
            var visited = new HashSet<GraphNode>();
            var layers = new List<LayerGroup>();

            var mapping = new LayersIndexMapper();
            var generator = new LayersIndexMapper.IndexGenerator();

            foreach (var node in nodes)
            {
                WalkGraph(node, visited, layers, mapping, generator);
            }

            foreach (var layer in layers)
            {
                mapping.RemapLayer(layer.MainLayer);
                if (layer.MatteLayer is not null)
                {
                    mapping.RemapLayer(layer.MatteLayer!);
                }
            }

            return layers;
        }
    }
}
