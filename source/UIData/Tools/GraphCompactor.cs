// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using static CommunityToolkit.WinUI.Lottie.UIData.Tools.Properties;
using Expr = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;

namespace CommunityToolkit.WinUI.Lottie.UIData.Tools
{
    /// <summary>
    /// Optimizes a <see cref="Visual"/> tree by combining and removing containers.
    /// </summary>
    sealed class GraphCompactor
    {
        readonly Visual _root;
        bool _madeProgress;

        GraphCompactor(Visual root)
        {
            _root = root;
        }

        internal static Visual Compact(Visual root)
        {
            // Running the optimization multiple times can improve the results.
            // Keep iterating as long as we are making progress.
            var compactor = new GraphCompactor(root);
            while (compactor.CompactOnce())
            {
                // Keep compacting as long as it makes progress.
            }

            return root;
        }

        bool CompactOnce()
        {
            _madeProgress = false;
            var graph = ObjectGraph<Node>.FromCompositionObject(_root, includeVertices: true);
            Compact(graph);
            return _madeProgress;
        }

        // Do not include this code as it requires a reference to code that is not available in every
        // configuration in which this class is included.
#if false
        // For debugging purposes, dump the current graph.
        void DumpToDgml(string qualifier)
        {
            var dgml = CommunityToolkit.WinUI.Lottie.UIData.CodeGen.CompositionObjectDgmlSerializer.ToXml(_root).ToString();
            var fileNameBase = $"Graph_{qualifier}";
            var counter = 0;
            while (System.IO.File.Exists($"{fileNameBase}_{counter}.dgml"))
            {
                counter++;
            }

            System.IO.File.WriteAllText($"{fileNameBase}_{counter}.dgml", dgml);
        }
#endif

        void GraphHasChanged() => _madeProgress = true;

        void Compact(ObjectGraph<Node> graph)
        {
            // Discover the parents of each container.
            foreach (var node in graph.CompositionObjectNodes)
            {
                switch (node.Object.Type)
                {
                    case CompositionObjectType.CompositionContainerShape:
                        foreach (var child in ((IContainShapes)node.Object).Shapes)
                        {
                            graph[child].Parent = node.Object;
                        }

                        break;

                    case CompositionObjectType.ShapeVisual:
                        foreach (var child in ((IContainShapes)node.Object).Shapes)
                        {
                            graph[child].Parent = node.Object;
                        }

                        // ShapeVisual is also a ContainerVisual.
                        goto case CompositionObjectType.ContainerVisual;

                    case CompositionObjectType.ContainerVisual:
                    case CompositionObjectType.LayerVisual:
                    case CompositionObjectType.SpriteVisual:
                        foreach (var child in ((ContainerVisual)node.Object).Children)
                        {
                            graph[child].Parent = node.Object;
                        }

                        break;
                    case CompositionObjectType.CompositionVisualSurface:
                        Visual? source = ((CompositionVisualSurface)node.Object).SourceVisual;
                        if (source is not null)
                        {
                            graph[source].AllowCoalesing = false;
                        }

                        break;
                }
            }

            OptimizeShapes(graph);
            OptimizeVisuals(graph);
        }

        void OptimizeVisuals(ObjectGraph<Node> graph)
        {
            PushVisualVisibilityUp(graph);
            PushPropertiesDownToShapeVisual(graph);
            CoalesceContainerVisuals(graph);
            CoalesceOrthogonalVisuals(graph);
            CoalesceOrthogonalContainerVisuals(graph);
            RemoveRedundantInsetClipVisuals(graph);
        }

        void OptimizeShapes(ObjectGraph<Node> graph)
        {
            ElideTransparentSpriteShapes(graph);
            OptimizeContainerShapes(graph);
            PushShapeTreeVisibilityIntoVisualTree(graph);
        }

        void OptimizeContainerShapes(ObjectGraph<Node> graph)
        {
            var containerShapes =
                (from pair in graph.CompositionObjectNodes
                 where pair.Object.Type == CompositionObjectType.CompositionContainerShape
                 let parent = (IContainShapes?)pair.Node.Parent
                 select (node: pair.Node, container: (CompositionContainerShape)pair.Object, parent)).ToArray();

            CoalesceSiblingContainerShapes(graph);
            ElideEmptyContainerShapes(graph, containerShapes);
            ElideStructuralContainerShapes(graph, containerShapes);
            PushContainerShapeTransformsDown(graph, containerShapes);
            CoalesceContainerShapes2(graph, containerShapes);
            PushPropertiesDownToSpriteShape(graph, containerShapes);
            PushShapeVisbilityDown(graph, containerShapes);
        }

        // Finds sibling shape containers that have the same properties and combines them.
        void CoalesceSiblingContainerShapes(ObjectGraph<Node> graph)
        {
            // Find the IContainShapes that have 1 or more children.
            var containersWith1OrMoreChildren = graph.CompositionObjectNodes.Where(n =>
                n.Object is IContainShapes shapeContainer &&
                shapeContainer.Shapes.Count > 1
            ).ToArray();

            foreach (var ch in containersWith1OrMoreChildren)
            {
                var container = (IContainShapes)ch.Object;
                var grouped = GroupSimilarChildContainers(container).ToArray();

                if (grouped.Any(g => g.Length > 1))
                {
                    // There was some grouping. Clear out the children and replace them.
                    container.Shapes.Clear();
                    foreach (var group in grouped)
                    {
                        // Add the first item from the group.
                        container.Shapes.Add(group[0]);
                        graph[group[0]].Parent = (CompositionObject)container;

                        if (group.Length > 1)
                        {
                            // If there is more than 1 item in the group then they are all containers
                            // and they are all equivalent.
                            // Add the contents of the other containers into the first container.
                            var first = (CompositionContainerShape)group[0];

                            // All of the items in the group will share the first container.
                            for (var i = 1; i < group.Length; i++)
                            {
                                // Move the children of each of the other containers into this container.
                                var groupI = (CompositionContainerShape)group[i];

                                foreach (var shape in groupI.Shapes)
                                {
                                    first.Shapes.Add(shape);
                                    graph[shape].Parent = first;
                                }

                                groupI.Shapes.Clear();
                            }
                        }
                    }
                }
            }
        }

        static IEnumerable<CompositionShape[]> GroupSimilarChildContainers(IContainShapes container)
        {
            var grouped = new List<CompositionContainerShape>();

            foreach (var child in container.Shapes)
            {
                if (!(child is CompositionContainerShape childContainer))
                {
                    if (grouped.Count > 0)
                    {
                        // Output the group so far.
                        yield return grouped.ToArray();
                        grouped.Clear();
                    }

                    // Output a group with only one item - the shape that is not a container.
                    yield return new[] { child };
                }
                else
                {
                    // The shape is a container.
                    if (grouped.Count == 0)
                    {
                        // Start a new group.
                        grouped.Add(childContainer);
                    }
                    else
                    {
                        // See if this container belongs in the current group. It does if it is the same as
                        // the first item in the group except for having different children.
                        if (IsEquivalentContainer(grouped[0], childContainer))
                        {
                            grouped.Add(childContainer);
                        }
                        else
                        {
                            yield return grouped.ToArray();
                            grouped.Clear();
                            grouped.Add(childContainer);
                        }
                    }
                }
            }

            if (grouped.Count > 0)
            {
                // Output the final group.
                yield return grouped.ToArray();
            }
        }

        static bool IsEquivalentContainer(CompositionContainerShape a, CompositionContainerShape b)
        {
            if (a.TransformMatrix != b.TransformMatrix ||
                a.CenterPoint != b.CenterPoint ||
                a.Offset != b.Offset ||
                a.RotationAngleInDegrees != b.RotationAngleInDegrees ||
                a.Scale != b.Scale ||
                a.Properties.Names.Count > 0 || b.Properties.Names.Count > 0)
            {
                return false;
            }

            return AreAnimatorsEquivalent(a.Animators, b.Animators);
        }

        static bool AreAnimatorsEquivalent(IReadOnlyList<CompositionObject.Animator> a, IReadOnlyList<CompositionObject.Animator> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                var animatorA = a[i];
                var animatorB = b[i];

                if (animatorA.AnimatedProperty != animatorB.AnimatedProperty)
                {
                    return false;
                }

                // Identity comparison is sufficient here as long as the Canonicalizer has already run, because
                // that will ensure that equivalent animations have the same identity.
                if (animatorA.Animation != animatorB.Animation)
                {
                    return false;
                }

                // NOTE: we do not compare the controllers here. For Lottie this is usually sufficient.
            }

            return true;
        }

        // Finds ContainerVisual with a single ShapeVisual child where the ContainerVisual
        // only exists to set an InsetClip. In this case the ContainerVisual can be removed
        // because the ShapeVisual has an implicit InsetClip.
        void RemoveRedundantInsetClipVisuals(ObjectGraph<Node> graph)
        {
            var containersClippingShapeVisuals = graph.CompositionObjectNodes.Where(n =>

                    // Find the ContainerVisuals that have only a Clip and Size set and have one
                    // child that is a ShapeVisual.
                    n.Object is ContainerVisual container &&
                    (GetNonDefaultContainerVisualProperties(container) & (PropertyId.Clip | PropertyId.Size | PropertyId.Children))
                        == (PropertyId.Clip | PropertyId.Size | PropertyId.Children) &&
                    container.Clip?.Type == CompositionObjectType.InsetClip &&
                    container.Animators.Count == 0 &&
                    container.Properties.Names.Count == 0 &&
                    container.Children.Count == 1 &&
                    container.Children[0].Type == CompositionObjectType.ShapeVisual
            ).ToArray();

            foreach (var (node, obj) in containersClippingShapeVisuals)
            {
                var container = (ContainerVisual)obj;
                var shapeVisual = (ShapeVisual)container.Children[0];

                // Check that the clip and size on the container is the same
                // as the size on the shape visual.
                // The Clip is definitely an InsetClip as we have already filtered
                // the list to remove any non-InsetClip clips.
                var containerClip = (InsetClip)container.Clip!;

                var childClip = shapeVisual.Clip as InsetClip;

                if (childClip is null)
                {
                    continue;
                }

                // NOTE: we rely on the optimizer to have already removed default-valued properties.
                if ((containerClip.TopInset != childClip.TopInset) ||
                    (containerClip.RightInset != childClip.TopInset) ||
                    (containerClip.LeftInset != childClip.LeftInset) ||
                    (containerClip.BottomInset != childClip.BottomInset) ||
                    (containerClip.Scale != childClip.Scale) ||
                    (containerClip.CenterPoint != childClip.CenterPoint))
                {
                    continue;
                }

                if (container.Size != shapeVisual.Size)
                {
                    continue;
                }

                // The container is redundant.
                var parent = node.Parent;
                if (parent is ContainerVisual parentContainer)
                {
                    GraphHasChanged();

                    // Replace the container with the ShapeVisual.
                    var indexOfRedundantContainer = parentContainer.Children.IndexOf(container);

                    // The container may have been already removed (this can happen if one of the
                    // coalescing methods here doesn't update the graph).
                    if (indexOfRedundantContainer >= 0)
                    {
                        parentContainer.Children.RemoveAt(indexOfRedundantContainer);
                        parentContainer.Children.Insert(indexOfRedundantContainer, shapeVisual);

                        CopyDescriptions(container, shapeVisual);
                    }
                }
            }
        }

        static bool IsBrushTransparent(CompositionBrush? brush)
        {
            return brush is null || (!brush.Animators.Any() && (brush as CompositionColorBrush)?.Color?.A == 0);
        }

        void ElideTransparentSpriteShapes(ObjectGraph<Node> graph)
        {
            var transparentShapes =
                (from pair in graph.CompositionObjectNodes
                 where pair.Object.Type == CompositionObjectType.CompositionSpriteShape
                 let shape = (CompositionSpriteShape)pair.Object
                 where IsBrushTransparent(shape.FillBrush) && IsBrushTransparent(shape.StrokeBrush)
                 select (Shape: shape, Parent: (IContainShapes?)pair.Node.Parent)).ToArray();

            foreach (var (shape, parent) in transparentShapes)
            {
                GraphHasChanged();

                parent.Shapes.Remove(shape);
            }
        }

        // Removes any CompositionContainerShapes that have no children.
        void ElideEmptyContainerShapes(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            // Keep track of which containers were removed so we don't consider them again.
            var removed = new HashSet<CompositionContainerShape>();

            // Keep going as long as progress is made.
            for (var madeProgress = true; madeProgress;)
            {
                madeProgress = false;
                foreach (var (_, container, parent) in containerShapes)
                {
                    if (!removed.Contains(container) && container.Shapes.Count == 0)
                    {
                        GraphHasChanged();

                        // Indicate that we successfully removed a container.
                        madeProgress = true;

                        // Remove the empty container.
                        parent.Shapes.Remove(container);

                        // Don't look at the removed object again.
                        removed.Add(container);
                    }
                }
            }
        }

        void PushContainerShapeTransformsDown(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            // If a container is not animated and has no other properties set apart from a transform,
            // and all of its children do not have an animated transform, the transform can be pushed down to
            // each child, and the container can be removed.
            // Note that this is safe because TransformMatrix effectively sits above all transforming
            // properties, so after pushing it down it will still be above all transforming properties.
            var elidableContainers = containerShapes.Where(n =>
            {
                var container = n.container;

                if (container.Shapes.Count == 0)
                {
                    // Ignore empty containers.
                    return false;
                }

                var containerProperties = GetNonDefaultShapeProperties(container);

                if (container.Animators.Count != 0 || (containerProperties & ~PropertyId.TransformMatrix) != PropertyId.None)
                {
                    // Ignore this container if it has animators or anything other than the transform is set.
                    return false;
                }

                foreach (var child in container.Shapes)
                {
                    var childProperties = GetNonDefaultShapeProperties(child);

                    if (TryGetAnimatorByPropertyName(child, nameof(CompositionShape.TransformMatrix)) is not null)
                    {
                        // Ignore this container if any of the children has an animated transform.
                        return false;
                    }
                }

                return true;
            });

            // Push the transform down to each child.
            foreach (var (_, container, _) in elidableContainers)
            {
                foreach (var child in container.Shapes)
                {
                    // Push the transform down to the child.
                    if (container.TransformMatrix.HasValue)
                    {
                        child.TransformMatrix = (child.TransformMatrix ?? Matrix3x2.Identity) * container.TransformMatrix;
                        if (child.TransformMatrix.Value.IsIdentity)
                        {
                            child.TransformMatrix = null;
                        }
                    }
                }

                // Remove the container.
                ElideContainerShape(graph, container);
            }
        }

        void ElideStructuralContainerShapes(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            // If a container is not animated and has no properties set, its children can be inserted into its parent.
            var containersWithNoPropertiesSet = containerShapes.Where(n =>
            {
                var container = n.container;
                var containerProperties = GetNonDefaultShapeProperties(container);

                if (container.Animators.Count != 0 || containerProperties != PropertyId.None)
                {
                    return false;
                }

                // Container has no properties set.
                return true;
            }).ToArray();

            foreach (var (_, container, _) in containersWithNoPropertiesSet)
            {
                ElideContainerShape(graph, container);
            }
        }

        // Removes a container shape, copying its shapes into its parent.
        // Does nothing if the container has no parent.
        void ElideContainerShape(ObjectGraph<Node> graph, CompositionContainerShape container)
        {
            // Insert the children into the parent.
            var parent = (IContainShapes?)graph[container].Parent;
            if (parent is null)
            {
                // The container may have already been removed, or it might be a root.
                return;
            }

            // Find the index in the parent of the container.
            // If childCount is 1, just replace the the container in the parent.
            // If childCount is >1, insert into the parent.
            var index = parent.Shapes.IndexOf(container);

            if (index == -1)
            {
                // Container has already been removed.
                return;
            }

            // Get the children from the container.
            var children = container.Shapes;

            if (children.Count == 0)
            {
                // The container has no children. This is rare but can happen if
                // the container is for a layer type that we don't support.
                return;
            }

            GraphHasChanged();

            // Insert the first child where the container was.
            var child0 = children[0];

            CopyDescriptions(container, child0);

            parent.Shapes[index] = child0;

            // Fix the parent pointer in the graph.
            graph[child0].Parent = (CompositionObject)parent;

            // Insert the rest of the children.
            for (var n = 1; n < children.Count; n++)
            {
                var childN = children[n];

                CopyDescriptions(container, childN);

                parent.Shapes.Insert(index + n, childN);

                // Fix the parent pointer in the graph.
                graph[childN].Parent = (CompositionObject)parent;
            }

            // Remove the children from the container.
            container.Shapes.Clear();
        }

        // Removes a container visual, copying its children into its parent.
        // Does nothing if the container has no parent.
        bool TryElideContainerVisual(ObjectGraph<Node> graph, ContainerVisual container)
        {
            // Insert the children into the parent.
            var parent = (ContainerVisual?)graph[container].Parent;
            if (parent is null)
            {
                // The container may have already been removed, or it might be a root.
                return false;
            }

            // Find the index in the parent of the container.
            // If childCount is 1, just replace the the container in the parent.
            // If childCount is >1, insert into the parent.
            var index = parent.Children.IndexOf(container);

            // Get the children from the container.
            var children = container.Children;

            if (container.Children.Count == 0)
            {
                // The container has no children. This is rare but can happen if
                // the container is for a layer type that we don't support.
                return true;
            }

            GraphHasChanged();

            // Insert the first child where the container was.
            var child0 = children[0];

            CopyDescriptions(container, child0);

            parent.Children[index] = child0;

            // Fix the parent pointer in the graph.
            graph[child0].Parent = parent;

            // Insert the rest of the children.
            for (var n = 1; n < children.Count; n++)
            {
                var childN = children[n];

                CopyDescriptions(container, childN);

                parent.Children.Insert(index + n, childN);

                // Fix the parent pointer in the graph.
                graph[childN].Parent = parent;
            }

            // Remove the children from the container.
            container.Children.Clear();

            return true;
        }

        // Finds ContainerShapes that only have their Transform set, with a single child that
        // does not have its Transform set and pulls the child into the parent. This is OK to do
        // because the Transform will still be evaluated as if it is higher in the tree.
        void CoalesceContainerShapes2(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            var containerShapesWith1Container = containerShapes.Where(n =>
                    n.container.Shapes.Count == 1 &&
                    n.container.Shapes[0].Type == CompositionObjectType.CompositionContainerShape
                ).ToArray();

            foreach (var (_, container, _) in containerShapesWith1Container)
            {
                if (!container.Shapes.Any())
                {
                    // The children have already been removed.
                    continue;
                }

                var child = (CompositionContainerShape)container.Shapes[0];

                var parentProperties = GetNonDefaultShapeProperties(container);
                var childProperties = GetNonDefaultShapeProperties(child);

                if (parentProperties == PropertyId.TransformMatrix &&
                    (childProperties & PropertyId.TransformMatrix) == PropertyId.None)
                {
                    if (child.Animators.Any())
                    {
                        // Ignore if the child is animated. We could handle it but it's more complicated.
                        continue;
                    }

                    TransferShapeProperties(child, container);

                    // Move the child's children into the parent.
                    ElideContainerShape(graph, child);
                }
            }
        }

        // Finds chains of Visuals and moves the IsVisible property and
        // animation up to the top of the chain.
        void PushVisualVisibilityUp(ObjectGraph<Node> graph)
        {
            // Find the Visuals that have a single parent. It is safe to combine
            // the visibility of such a Visual with the visibility of its parent,
            // except in the case where the parent is the SourceVisual of a
            // CompositionVisualSurface (CompositionVisualSurface ignores
            // IsVisible on its SourceVisual).
            var visualsWithSingleParents =
                from n in graph.CompositionObjectNodes
                let visual = n.Object as Visual
                where visual is not null
                let parent = n.Node.Parent as ContainerVisual
                where parent is not null &&
                      parent.Children.Count == 1 &&
                      !IsVisualSurfaceSourceVisual(graph, parent)
                select (visual, parent);

            foreach (var (visual, parent) in visualsWithSingleParents)
            {
                var visibilityController = visual.TryGetAnimationController("IsVisible");
                if (visibilityController is not null)
                {
                    var animator = TryGetAnimatorByPropertyName(visibilityController, "Progress");

                    if (visibilityController.IsCustom)
                    {
                        ApplyVisibility(parent, GetVisiblityAnimationDescription(visual), null, visibilityController);
                    }
                    else if (animator is not null)
                    {
                        ApplyVisibility(parent, GetVisiblityAnimationDescription(visual), animator.Animation, null);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }

                    // Clear out the visibility property and animation from the visual.
                    visual.IsVisible = null;
                    visual.StopAnimation("IsVisible");
                }
            }
        }

        // Find ContainerVisuals that have a single ShapeVisual child with orthongonal properties and
        // push the properties down to the ShapeVisual.
        static void PushPropertiesDownToShapeVisual(ObjectGraph<Node> graph)
        {
            var shapeVisualsWithSingleParents =
                (from n in graph.CompositionObjectNodes
                 let parent = n.Node.Parent
                 where n.Object.Type == CompositionObjectType.ShapeVisual
                 where parent is not null
                 let parentContainerVisual = (ContainerVisual)parent
                 where parentContainerVisual.Children.Count == 1
                 select (n.Node, (ShapeVisual)n.Object, parentContainerVisual)).ToArray();

            foreach (var (node, shapeVisual, parent) in shapeVisualsWithSingleParents)
            {
                var parentProperties = GetNonDefaultVisualProperties(parent);

                if (parentProperties == PropertyId.None)
                {
                    // No properties to push down.
                    continue;
                }

                // If the parent has no transforming properties, and a Size that
                // is the same as the Child's size, and a 0 InsetClip and none of
                // these properties is animated, the InsetClip and Size on the Visual
                // are redundant and can be removed.
                if ((parentProperties &
                        (PropertyId.CenterPoint | PropertyId.Offset |
                         PropertyId.RotationAngleInDegrees | PropertyId.Scale |
                         PropertyId.TransformMatrix)) == PropertyId.None &&
                    parent.Clip is InsetClip insetClip &&
                    insetClip.CenterPoint.HasValue &&
                    insetClip.Scale.HasValue &&
                    insetClip.LeftInset.HasValue && insetClip.RightInset.HasValue &&
                    insetClip.TopInset.HasValue && insetClip.BottomInset.HasValue &&
                    insetClip.Animators.Count == 0 &&
                    parent.Size == shapeVisual.Size &&
                    !IsPropertyAnimated(parent, PropertyId.Size) &&
                    !IsPropertyAnimated(shapeVisual, PropertyId.Size))
                {
                    parent.Clip = null;
                    parent.Size = null;
                }
            }
        }

        static bool IsPropertyAnimated(CompositionObject obj, PropertyId property)
        {
            var propertyName = property.ToString();
            return obj.Animators.Any(p => p.AnimatedProperty == propertyName);
        }

        // Finds ShapeVisuals with a single shape that has a visibility animation and
        // move the animation into the ShapeVisual.
        void PushShapeTreeVisibilityIntoVisualTree(ObjectGraph<Node> graph)
        {
            var candidate =
                (from n in graph.CompositionObjectNodes
                 let sv = n.Object as ShapeVisual
                 where sv is not null && sv.Shapes.Count == 1
                 let shape = sv.Shapes[0]
                 where IsScaleUsedForVisibility(shape)
                 select sv).ToArray();

            foreach (var visual in candidate)
            {
                var shape = visual.Shapes[0];

                var visibilityController = shape.TryGetAnimationController("Scale");
                if (visibilityController is not null)
                {
                    var animator = TryGetAnimatorByPropertyName(visibilityController, "Progress");

                    if (visibilityController.IsCustom)
                    {
                        ApplyVisibility(visual, GetVisiblityAnimationDescription(shape), null, visibilityController);
                    }
                    else if (animator is not null)
                    {
                        ApplyVisibility(visual, GetVisiblityAnimationDescription(shape), animator.Animation, null);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }

                    // Clear out the Scale properties and animations from the shape.
                    shape.Scale = null;
                    shape.StopAnimation("Scale");
                }
            }
        }

        // Applies the given visibility to the given Visual, combining it with the
        // visibility it already has.
        void ApplyVisibility(Visual to, VisibilityDescription fromVisibility, CompositionAnimation? progressAnimation, AnimationController? customController)
        {
            Debug.Assert(progressAnimation is not null || customController is not null, "Precondition");

            var toVisibility = GetVisiblityAnimationDescription(to);

            var compositeVisibility = VisibilityDescription.Compose(fromVisibility, toVisibility);

            if (compositeVisibility.Sequence.Length > 0)
            {
                _madeProgress = true;
                var c = new Compositor();
                var animation = c.CreateBooleanKeyFrameAnimation();
                animation.Duration = compositeVisibility.Duration;
                if (compositeVisibility.Sequence[0].Progress == 0)
                {
                    // Set the initial visiblity.
                    to.IsVisible = compositeVisibility.Sequence[0].IsVisible;
                }

                foreach (var (isVisible, progress) in compositeVisibility.Sequence)
                {
                    animation.InsertKeyFrame(progress, isVisible);
                }

                if (progressAnimation is not null)
                {
                    to.StartAnimation("IsVisible", animation);
                    var controller = to.TryGetAnimationController("IsVisible")!;
                    controller.Pause();
                    controller.StartAnimation("Progress", progressAnimation);
                }
                else
                {
                    to.StartAnimation("IsVisible", animation, customController);
                }
            }
        }

        // Returns a description of the visibility over time of the given visual.
        static VisibilityDescription GetVisiblityAnimationDescription(Visual visual)
        {
            // Get the visibility animation.
            // TODO - this needs to take the controller's Progress expression into account.
            var animator = TryGetAnimatorByPropertyName(visual, nameof(visual.IsVisible));

            if (animator is null)
            {
                return new VisibilityDescription(TimeSpan.Zero, Array.Empty<VisibilityAtProgress>());
            }

            var visibilityAnimation = (BooleanKeyFrameAnimation)animator.Animation;

            return new VisibilityDescription(visibilityAnimation.Duration, GetDescription().ToArray());

            IEnumerable<VisibilityAtProgress> GetDescription()
            {
                if (animator is null)
                {
                    // Not animated, or it uses an expression so we can't deal with it.
                    yield break;
                }

                var firstSeen = false;

                foreach (KeyFrameAnimation<bool, Expr.Boolean>.ValueKeyFrame kf in visibilityAnimation.KeyFrames)
                {
                    if (!firstSeen)
                    {
                        firstSeen = true;

                        // If the first keyframe is not at 0, and its target is initially non-visible,
                        // add a non-visible state at 0.
                        if (kf.Progress != 0 && visual.IsVisible == false)
                        {
                            // Output an initial keyframe.
                            yield return new VisibilityAtProgress(false, 0);
                        }
                    }

                    yield return new VisibilityAtProgress(kf.Value, kf.Progress);
                }
            }
        }

        // Returns a description of the visibility over time of the given shape.
        static VisibilityDescription GetVisiblityAnimationDescription(CompositionShape shape)
        {
            var scaleValue = shape.Scale;

            if (scaleValue.HasValue && scaleValue != Vector2.One && scaleValue != Vector2.Zero)
            {
                // The animation is not used for visibility. Precondition.
                throw new InvalidOperationException();
            }

            var scaleAnimator = TryGetAnimatorByPropertyName(shape, nameof(shape.Scale));

            if (scaleAnimator is null)
            {
                // The animation is not used for visibility. Precondition.
                throw new InvalidOperationException();
            }

            var firstSeen = false;
            var scaleAnimation = (Vector2KeyFrameAnimation)scaleAnimator.Animation;

            return new VisibilityDescription(scaleAnimation.Duration, GetDescription().ToArray());

            IEnumerable<VisibilityAtProgress> GetDescription()
            {
                foreach (KeyFrameAnimation<Vector2, Expr.Vector2>.ValueKeyFrame kf in scaleAnimation.KeyFrames)
                {
                    if (kf.Easing?.Type != CompositionObjectType.StepEasingFunction)
                    {
                        // The animation is not used for visibility. Precondition.
                        throw new InvalidOperationException();
                    }

                    if (kf.Value != Vector2.One && kf.Value != Vector2.Zero)
                    {
                        // The animation is not used for visibility. Precondition.
                        throw new InvalidOperationException();
                    }

                    if (!firstSeen)
                    {
                        firstSeen = true;

                        // If the first keyframe is not at 0, and its target is initially non-visible,
                        // add a non-visible state at 0.
                        if (kf.Progress != 0 && shape.Scale == Vector2.Zero)
                        {
                            yield return new VisibilityAtProgress(false, 0);
                        }
                    }

                    yield return new VisibilityAtProgress(kf.Value == Vector2.One, kf.Progress);
                }
            }
        }

        // Finds container shapes with a single child and have only Scale properties set for visibility animations
        // and pushes the scale property and animation down.
        void PushShapeVisbilityDown(
                        ObjectGraph<Node> graph,
                        (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            var containerShapesWith1Child = containerShapes.Where(n =>
                    n.container.Shapes.Count == 1
                ).ToArray();

            foreach (var (_, parent, _) in containerShapesWith1Child)
            {
                if (!parent.Shapes.Any())
                {
                    // The children have already been removed.
                    continue;
                }

                var child = parent.Shapes[0];

                var parentProperties = GetNonDefaultShapeProperties(parent);
                var childProperties = GetNonDefaultShapeProperties(child);

                if (parentProperties == PropertyId.Scale && IsScaleUsedForVisibility(parent))
                {
                    // The parent is only used for visibility (via its Scale property).
                    // This can be safely pushed up or down the tree.
                    if ((childProperties & PropertyId.Scale) == PropertyId.None)
                    {
                        // The child does not use Scale. Move the Scale down to the child.
                        TransferShapeProperties(parent, child);

                        // Remove the parent as it's not needed any more.
                        ElideContainerShape(graph, parent);
                    }
                }
            }
        }

        // Find ContainerShapes that have a single SpriteShape with orthongonal properties
        // and remove the ContainerShape.
        void PushPropertiesDownToSpriteShape(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            var containerShapesWith1Sprite = containerShapes.Where(n =>
                    n.container.Shapes.Count == 1 &&
                    n.container.Shapes[0].Type == CompositionObjectType.CompositionSpriteShape
                ).ToArray();

            foreach (var (_, parent, _) in containerShapesWith1Sprite)
            {
                if (!parent.Shapes.Any())
                {
                    // The children have already been removed.
                    continue;
                }

                var child = (CompositionSpriteShape)parent.Shapes[0];

                var parentProperties = GetNonDefaultShapeProperties(parent);
                var childProperties = GetNonDefaultShapeProperties(child);

                if ((parentProperties & PropertyId.Properties) != PropertyId.None)
                {
                    // Ignore if the parent has PropertySet properties. We could handle it but it's more complicated.
                    continue;
                }

                if (ArePropertiesOrthogonal(parentProperties, childProperties))
                {
                    // Copy the parent's properties onto the child and remove the parent.
                    TransferShapeProperties(parent, child);

                    ElideContainerShape(graph, parent);
                }
            }
        }

        // Returns true iff the Scale property on the given shape is used for visibility.
        static bool IsScaleUsedForVisibility(CompositionShape shape)
        {
            var scaleValue = shape.Scale;

            if (scaleValue.HasValue && scaleValue != Vector2.One && scaleValue != Vector2.Zero)
            {
                // Scale has a value that is not invisible (0,0) and it's not identity (1,1).
                return false;
            }

            var scaleAnimator = TryGetAnimatorByPropertyName(shape, nameof(shape.Scale));

            if (scaleAnimator is null)
            {
                return false;
            }

            var scaleAnimation = (Vector2KeyFrameAnimation)scaleAnimator.Animation;
            foreach (var kf in scaleAnimation.KeyFrames)
            {
                if (kf.Easing?.Type != CompositionObjectType.StepEasingFunction)
                {
                    return false;
                }

                if (kf.Type != KeyFrameType.Value)
                {
                    return false;
                }

                var keyFrameValue = ((KeyFrameAnimation<Vector2, Expr.Vector2>.ValueKeyFrame)kf).Value;

                if (keyFrameValue != Vector2.One && keyFrameValue != Vector2.Zero)
                {
                    return false;
                }
            }

            return true;
        }

        void CoalesceContainerVisuals(ObjectGraph<Node> graph)
        {
            // If a container is not animated and has no properties set, its children can be inserted into its parent.
            var containersWithNoPropertiesSet =
                (from n in graph.CompositionObjectNodes
                 where n.Object.Type == CompositionObjectType.ContainerVisual
                 let containerVisual = (ContainerVisual)n.Object

                 // Only if the container has no properties set.
                 where GetNonDefaultVisualProperties(containerVisual) == PropertyId.None

                 // The parent may have been removed already.
                 let parent = n.Node.Parent
                 where parent is not null && n.Node.AllowCoalesing
                 select ((ContainerVisual)parent, containerVisual)).ToArray();

            // Pull the children of the container into the parent of the container. Remove the unnecessary containers.
            foreach (var (parent, container) in containersWithNoPropertiesSet)
            {
                // Find the index in the parent of the container.
                // If childCount is 1, just replace the the container in the parent.
                // If childCount is >1, insert into the parent.
                var index = parent.Children.IndexOf(container);

                if (index == -1)
                {
                    // Container has already been removed.
                    continue;
                }

                var children = container.Children;

                // Get the children from the container.
                if (children.Count == 0)
                {
                    // The container has no children. This is rare but can happen if
                    // the container is for a layer type that we don't support.
                    continue;
                }

                GraphHasChanged();

                // Insert the first child where the container was.
                var child0 = children[0];

                CopyDescriptions(container, child0);

                parent.Children[index] = child0;

                // Fix the parent pointer in the graph.
                graph[child0].Parent = parent;

                // Insert the rest of the children.
                for (var n = 1; n < children.Count; n++)
                {
                    var childN = children[n];

                    CopyDescriptions(container, childN);

                    parent.Children.Insert(index + n, childN);

                    // Fix the parent pointer in the graph.
                    graph[childN].Parent = parent;
                }

                // Remove the children from the container.
                children.Clear();
            }
        }

        // If a ContainerVisual has exactly one child that is a ContainerVisual, and each
        // affects different sets of properties then they can be combined into one.
        void CoalesceOrthogonalContainerVisuals(ObjectGraph<Node> graph)
        {
            // If a container is not animated and has no properties set, its children can be inserted into its parent,
            // except the root node. We should not change the root node since the user can try to change width/height/scale
            // of the root node and in combination with non-null Clip property (or others)
            // this can lead to incorrect animation.
            var containersWithASingleContainer = graph.CompositionObjectNodes.Where(n =>
            {
                // Find the ContainerVisuals that have a single child that is a ContainerVisual.
                return
                        n.Object is ContainerVisual container &&
                        n.Object != graph.Root.Object &&
                        container.Children.Count == 1 &&
                        container.Children[0].Type == CompositionObjectType.ContainerVisual;
            }).ToArray();

            foreach (var (_, obj) in containersWithASingleContainer)
            {
                var parent = (ContainerVisual)obj;
                if (parent.Children.Count != 1)
                {
                    // The previous iteration of the loop modified the Children list.
                    continue;
                }

                var child = (ContainerVisual)parent.Children[0];

                var parentProperties = GetNonDefaultVisualProperties(parent);
                var childProperties = GetNonDefaultVisualProperties(child);

                // If the containers have non-overlapping properties they can be coalesced.
                // If the child has PropertySet values, don't try to coalesce (although we could
                // move the properties, we're not supporting that case for now.).
                if (ArePropertiesOrthogonal(parentProperties, childProperties) &&
                    (childProperties & PropertyId.Properties) == PropertyId.None)
                {
                    if (IsVisualSurfaceSourceVisual(graph, parent))
                    {
                        // VisualSurface roots are special - they ignore their transforming properties
                        // so such properties cannot be hoisted from the child.
                        continue;
                    }

                    // Move the children of the child into the parent, and set the child's
                    // properties and animations on the parent.
                    if (TryElideContainerVisual(graph, child))
                    {
                        TransferContainerVisualProperties(from: child, to: parent);
                    }
                }
            }
        }

        // True iff the given Visual is the SourceVisual of a CompositionVisualSurface.
        // In this case the transforming properties (e.g. offset) and visiblity will be ignored,
        // so it is not safe to hoist any such properties from its child.
        static bool IsVisualSurfaceSourceVisual(ObjectGraph<Node> graph, Visual visual)
         => graph[visual].InReferences.Any(vertex => vertex.Node.Object is CompositionVisualSurface);

        static bool ArePropertiesOrthogonal(PropertyId parent, PropertyId child)
        {
            if ((parent & child) != PropertyId.None)
            {
                // The properties overlap.
                return false;
            }

            // The properties do not overlap. But we have to check for some properties that
            // need to be evaluated in a particular order, which means they cannot be just
            // moved between the child and parent.
            if ((parent & (PropertyId.Color | PropertyId.Opacity | PropertyId.Path)) == parent ||
                (child & (PropertyId.Color | PropertyId.Opacity | PropertyId.Path)) == child)
            {
                // These properties are not order dependent.
                return true;
            }

            // Evaluation order is TransformMatrix, Offset, Rotation, Scale. So if the
            // child has a transform it can not be pulled into the parent if the parent
            // has offset, rotation, scale, clip, or centerpoint because it would cause
            // the transform to be evaluated too early.
            if (((child & PropertyId.TransformMatrix) != PropertyId.None) &&
                ((parent & (PropertyId.Offset | PropertyId.RotationAngleInDegrees | PropertyId.Scale | PropertyId.Clip | PropertyId.CenterPoint)) != PropertyId.None))
            {
                return false;
            }

            // If the child has a centerpoint, it cannot be pulled into the parent if the
            // parent has a transform, offset, rotation, or scale, as that would change the
            // centerpoint context in which the parent's transform, offset, rotation, and scale
            // are performed.
            if (((child & PropertyId.CenterPoint) != PropertyId.None) &&
                ((parent & (PropertyId.TransformMatrix | PropertyId.Offset | PropertyId.RotationAngleInDegrees | PropertyId.Scale)) != PropertyId.None))
            {
                return false;
            }

            if (((parent & PropertyId.RotationAngleInDegrees) != PropertyId.None) &&
                ((child & (PropertyId.Offset | PropertyId.Clip)) != PropertyId.None))
            {
                return false;
            }

            if (((parent & PropertyId.Scale) != PropertyId.None) &&
                ((child & (PropertyId.Offset | PropertyId.RotationAngleInDegrees | PropertyId.Clip)) != PropertyId.None))
            {
                return false;
            }

            return true;
        }

        // If a ContainerVisual has exactly one child that is a SpriteVisual or ShapeVisual, and each
        // affects different sets of properties then properties from the container can be
        // copied into the SpriteVisual and the container can be removed.
        void CoalesceOrthogonalVisuals(ObjectGraph<Node> graph)
        {
            // If a container is not animated and has no properties set, its children can be inserted into its parent.
            var containersWithASingleSprite = graph.CompositionObjectNodes.Where(n =>
            {
                // Find the ContainerVisuals that have a single child that is a ContainerVisual.
                return
                        n.Object is ContainerVisual container &&
                        n.Node.Parent is ContainerVisual &&
                        container.Children.Count == 1 &&
                        (container.Children[0].Type == CompositionObjectType.SpriteVisual ||
                         container.Children[0].Type == CompositionObjectType.ShapeVisual);
            }).ToArray();

            foreach (var (node, obj) in containersWithASingleSprite)
            {
                var parent = (ContainerVisual)obj;
                var child = (ContainerVisual)parent.Children[0];

                var parentProperties = GetNonDefaultVisualProperties(parent);
                var childProperties = GetNonDefaultVisualProperties(child);

                // If the containers have non-overlapping properties they can be coalesced.
                // If the parent has PropertySet values, don't try to coalesce (although we could
                // move the properties, we're not supporting that case for now.).
                if (ArePropertiesOrthogonal(parentProperties, childProperties) &&
                    (parentProperties & PropertyId.Properties) == PropertyId.None)
                {
                    if (IsVisualSurfaceSourceVisual(graph, parent))
                    {
                        // VisualSurface roots are special - they ignore their transforming properties
                        // so such properties cannot be hoisted from the child.
                        continue;
                    }

                    // Copy the values of the non-default properties from the parent to the child.
                    if (TryElideContainerVisual(graph, parent))
                    {
                        TransferContainerVisualProperties(from: parent, to: child);
                    }
                }
            }
        }

        static void TransferShapeProperties(CompositionShape from, CompositionShape to)
        {
            void TransferClassProperty<T>(Func<CompositionShape, T?> get, Action<CompositionShape, T> set)
                where T : class
            {
                var fromValue = get(from);
                if (fromValue is not null)
                {
                    Debug.Assert(get(to) is null, "Precondition");
                    set(to, fromValue);
                }
            }

            void TransferStructProperty<T>(Func<CompositionShape, T?> get, Action<CompositionShape, T> set)
                where T : struct
            {
                var fromValue = get(from);
                if (fromValue is not null)
                {
                    Debug.Assert(get(to) is null, "Precondition");
                    set(to, fromValue.Value);
                }
            }

            TransferStructProperty(cv => cv.CenterPoint, (cv, value) => cv.CenterPoint = value);
            TransferClassProperty(cv => cv.Comment, (cv, value) => cv.Comment = value);
            TransferStructProperty(cv => cv.Offset, (cv, value) => cv.Offset = value);
            TransferStructProperty(cv => cv.RotationAngleInDegrees, (cv, value) => cv.RotationAngleInDegrees = value);
            TransferStructProperty(cv => cv.Scale, (cv, value) => cv.Scale = value);
            TransferStructProperty(cv => cv.TransformMatrix, (cv, value) => cv.TransformMatrix = value);

            // Start the from's animations on the to.
            foreach (var anim in from.Animators)
            {
                if (anim.Controller is null || !anim.Controller.IsCustom)
                {
                    to.StartAnimation(anim.AnimatedProperty, anim.Animation);
                } else
                {
                    to.StartAnimation(anim.AnimatedProperty, anim.Animation, anim.Controller);
                }

                if (anim.Controller is not null && !anim.Controller.IsCustom && (anim.Controller.IsPaused || anim.Controller.Animators.Count > 0))
                {
                    var controller = to.TryGetAnimationController(anim.AnimatedProperty)!;
                    if (anim.Controller.IsPaused)
                    {
                        controller.Pause();
                    }

                    foreach (var controllerAnim in anim.Controller.Animators)
                    {
                        if (controllerAnim.Controller is null || !controllerAnim.Controller.IsCustom)
                        {
                            controller.StartAnimation(controllerAnim.AnimatedProperty, controllerAnim.Animation);
                        }
                        else
                        {
                            controller.StartAnimation(controllerAnim.AnimatedProperty, controllerAnim.Animation, controllerAnim.Controller);
                        }
                    }
                }
            }
        }

        static void TransferContainerVisualProperties(ContainerVisual from, ContainerVisual to)
        {
            void TransferClassProperty<T>(Func<ContainerVisual, T?> get, Action<ContainerVisual, T> set)
                where T : class
            {
                var fromValue = get(from);
                if (fromValue is not null)
                {
                    Debug.Assert(get(to) is null, "Precondition");
                    set(to, fromValue);
                }
            }

            void TransferStructProperty<T>(Func<ContainerVisual, T?> get, Action<ContainerVisual, T> set)
                where T : struct
            {
                var fromValue = get(from);
                if (fromValue is not null)
                {
                    Debug.Assert(get(to) is null, "Precondition");
                    set(to, fromValue.Value);
                }
            }

            TransferStructProperty(cv => cv.BorderMode, (cv, value) => cv.BorderMode = value);
            TransferStructProperty(cv => cv.CenterPoint, (cv, value) => cv.CenterPoint = value);
            TransferClassProperty(cv => cv.Clip, (cv, value) => cv.Clip = value);
            TransferClassProperty(cv => cv.Comment, (cv, value) => cv.Comment = value);
            TransferStructProperty(cv => cv.IsVisible, (cv, value) => cv.IsVisible = value);
            TransferStructProperty(cv => cv.Offset, (cv, value) => cv.Offset = value);
            TransferStructProperty(cv => cv.Opacity, (cv, value) => cv.Opacity = value);
            TransferStructProperty(cv => cv.RotationAngleInDegrees, (cv, value) => cv.RotationAngleInDegrees = value);
            TransferStructProperty(cv => cv.RotationAxis, (cv, value) => cv.RotationAxis = value);
            TransferStructProperty(cv => cv.Scale, (cv, value) => cv.Scale = value);
            TransferStructProperty(cv => cv.Size, (cv, value) => cv.Size = value);
            TransferStructProperty(cv => cv.TransformMatrix, (cv, value) => cv.TransformMatrix = value);

            // Start the from's animations on the to.
            foreach (var anim in from.Animators)
            {
                if (anim.Controller is null || !anim.Controller.IsCustom)
                {
                    to.StartAnimation(anim.AnimatedProperty, anim.Animation);
                }
                else
                {
                    to.StartAnimation(anim.AnimatedProperty, anim.Animation, anim.Controller);
                }

                if (anim.Controller is not null && !anim.Controller.IsCustom && (anim.Controller.IsPaused || anim.Controller.Animators.Count > 0))
                {
                    var controller = to.TryGetAnimationController(anim.AnimatedProperty)!;
                    if (anim.Controller.IsPaused)
                    {
                        controller.Pause();
                    }

                    foreach (var controllerAnim in anim.Controller.Animators)
                    {
                        if (controllerAnim.Controller is null || !controllerAnim.Controller.IsCustom)
                        {
                            controller.StartAnimation(controllerAnim.AnimatedProperty, controllerAnim.Animation);
                        }
                        else
                        {
                            controller.StartAnimation(controllerAnim.AnimatedProperty, controllerAnim.Animation, controllerAnim.Controller);
                        }
                    }
                }
            }
        }

        void CopyDescriptions(IDescribable from, IDescribable to)
        {
            GraphHasChanged();

            // Copy the short description. This may lose some information
            // in the "to" but generally that same information is in the
            // "from" description anyway.
            var fromShortDescription = from.ShortDescription;
            if (!string.IsNullOrWhiteSpace(fromShortDescription))
            {
                to.ShortDescription = fromShortDescription;
            }

            // Do not try to append the long description - it's impossible to do
            // a reasonable job of combining 2 long descriptions. But if the "to"
            // object doesn't already have a long description, copy the long
            // description from the "from" object.
            var toLongDescription = to.LongDescription;
            if (string.IsNullOrWhiteSpace(toLongDescription))
            {
                var fromLongDescription = from.LongDescription;
                if (!string.IsNullOrWhiteSpace(fromLongDescription))
                {
                    to.LongDescription = fromLongDescription;
                }
            }

            // If the "from" object has a name and the "to" object does not,
            // copy the name. For any other case it's not clear what we should
            // do, so just leave the name as it was.
            var fromName = from.Name;
            if (!string.IsNullOrWhiteSpace(fromName))
            {
                if (string.IsNullOrWhiteSpace(to.Name))
                {
                    to.Name = fromName;
                }
            }
        }

        // Gets the animator targeting the given named property, or null if not found.
        static CompositionObject.Animator? TryGetAnimatorByPropertyName(CompositionObject obj, string name) =>
            obj.Animators.Where(anim => anim.AnimatedProperty == name).FirstOrDefault();

        sealed class Node : Graph.Node<Node>
        {
            internal CompositionObject? Parent { get; set; }

            internal bool AllowCoalesing { get; set; } = true;
        }
    }
}
