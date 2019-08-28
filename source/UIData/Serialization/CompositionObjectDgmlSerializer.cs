// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    /// <summary>
    /// Serializes a <see cref="CompositionObject"/> graph into DGML format.
    /// </summary>
    sealed class CompositionObjectDgmlSerializer
    {
        // The categories of each node in the DGML graph. The category determines the appearance in the graph.
        static readonly Category CategoryRoot = new Category("Root", Colors.MediumVioletRed);
        static readonly Category CategoryContainerVisual = new Category("ContainerVisual", Colors.DarkRed);
        static readonly Category CategoryContainerVisualAnimated = new Category("AnimatedContainerVisual", "Animated ContainerVisual", Colors.Crimson);
        static readonly Category CategorySpriteVisual = new Category("SpriteVisual", Colors.Magenta);
        static readonly Category CategoryShapeVisual = new Category("ShapeVisual", Colors.CornflowerBlue);
        static readonly Category CategoryShapeVisualAnimated = new Category("AnimatedShapeVisual", "Animated ShapeVisual", Colors.RoyalBlue);
        static readonly Category CategoryContainerShape = new Category("ContainerShape", Colors.SeaGreen);
        static readonly Category CategoryContainerShapeAnimated = new Category("AnimatedContainerShape", "Animated ContainerShape", Colors.Teal);
        static readonly Category CategoryShape = new Category("Shape", Colors.Yellow);
        static readonly Category CategoryShapeAnimated = new Category("AnimatedShape", "Animated Shape", Colors.Wheat);
        static readonly Category CategoryEllipse = new Category("Ellipse", Colors.DarkGoldenrod);
        static readonly Category CategoryEllipseAnimated = new Category("AnimatedEllipse", "Animated Ellipse", Colors.Goldenrod);
        static readonly Category CategoryPath = new Category("Path", Colors.DarkOrange);
        static readonly Category CategoryPathAnimated = new Category("AnimatedPath", "Animated Path", Colors.Orange);
        static readonly Category CategoryRectangle = new Category("Rectangle", Colors.SandyBrown);
        static readonly Category CategoryRectangleAnimated = new Category("AnimatedRectangle", "Animated Rectangle", Colors.Yellow);
        static readonly Category CategoryRoundedRectangle = new Category("RoundedRectangle", Colors.Plum);
        static readonly Category CategoryAnimatedAnimatedRoundedRectangle = new Category("AnimatedRoundedRectangle", "Animated RoundedRectangle", Colors.Purple);

        static readonly XNamespace ns = "http://schemas.microsoft.com/vs/2009/dgml";
        ObjectGraph<ObjectData> _objectGraph;
        int _idGenerator;
        int _groupIdGenerator;

        CompositionObjectDgmlSerializer()
        {
        }

        public static XDocument ToXml(CompositionObject compositionObject)
        {
            return new CompositionObjectDgmlSerializer().ToXDocument(compositionObject);
        }

        XDocument ToXDocument(CompositionObject compositionObject)
        {
            // Build the graph of objects.
            _objectGraph = ObjectGraph<ObjectData>.FromCompositionObject(compositionObject, includeVertices: true);

            // Give names to each object.
            foreach ((var node, var name) in CodeGen.NodeNamer<ObjectData>.GenerateNodeNames(_objectGraph.Nodes))
            {
                node.Name = name;
            }

            // Initialize each node.
            foreach (var n in _objectGraph.Nodes)
            {
                n.Initialize(this);
            }

            // Second stage initialization - relies on all nodes having had the first stage of initialization.
            foreach (var n in _objectGraph.Nodes)
            {
                n.Initialize2();
            }

            var rootNode = _objectGraph[compositionObject];

            // Give the root object a special name and category.
            rootNode.Name = $"{rootNode.Name} (Root)";
            rootNode.Category = CategoryRoot;

            // Get the groups.
            var groups = GroupTree(rootNode, null).ToArray();

            // Get the Nodes for the objects that are going to show up in the DGML.
            var objectNodes = _objectGraph.Nodes.Where(n => n.IsDgmlNode).ToArray();

            // Create the DGML nodes.
            var nodes =
                from n in objectNodes
                select CreateNodeXml(id: n.Id, label: n.Name, category: n.Category.Id);

            // Create the DGML nodes for the groups.
            nodes = nodes.Concat(
                from gn in groups
                select CreateNodeXml(id: gn.Id, label: gn.GroupName, @group: "Expanded"));

            // Create the categories used by object nodes.
            var categories =
                (from n in objectNodes
                 select n.Category).Distinct();

            // Create the links between the nodes.
            var links =
                from n in objectNodes
                from otherNode in n.Children
                select new XElement(ns + "Link", new XAttribute("Source", n.Id), new XAttribute("Target", otherNode.Id));

            // Create the "contains" links for the nodes contained in groups.
            var containsLinks =
                (from g in groups
                 from member in g.ItemsInGroup
                 select new XElement(ns + "Link", new XAttribute("Source", g.Id), new XAttribute("Target", member.Id), new XAttribute("Category", "Contains"))).ToArray();

            // Create the "contains" links for the groups contained in groups
            var groupContainsGroupsLinks =
                (from g in groups
                 from member in g.GroupsInGroup
                 select new XElement(ns + "Link", new XAttribute("Source", g.Id), new XAttribute("Target", member.Id), new XAttribute("Category", "Contains"))).ToArray();

            containsLinks = containsLinks.Concat(groupContainsGroupsLinks).ToArray();

            // Create the XML
            return new XDocument(
                new XElement(
                    ns + "DirectedGraph",
                    new XElement(ns + "Nodes", nodes),
                    new XElement(ns + "Links", links.Concat(containsLinks)),
                    new XElement(
                        ns + "Categories",
                        categories.Select(c => c.ToXElement()).Append(
                        new XElement(
                            ns + "Category",
                            new XAttribute("Id", "Contains"),
                            new XAttribute("Label", "Contains"),
                            new XAttribute("Description", "Whether the source of the link contains the target object"),
                            new XAttribute("CanBeDataDriven", "False"),
                            new XAttribute("CanLinkedNodesBeDataDriven", "True"),
                            new XAttribute("IncomingActionLabel", "Contained By"),
                            new XAttribute("IsContainment", "True"),
                            new XAttribute("OutgoingActionLabel", "Contains")))
                            ),
                    new XElement(
                        ns + "Properties",
                        CreatePropertyXml(id: "Bounds", dataType: "System.Windows.Rect"),
                        CreatePropertyXml(id: "CanBeDataDriven", label: "CanBeDataDriven", description: "CanBeDataDriven", dataType: "System.Boolean"),
                        CreatePropertyXml(id: "CanLinkedNodesBeDataDriven", label: "CanLinkedNodesBeDataDriven", description: "CanLinkedNodesBeDataDriven", dataType: "System.Boolean"),
                        CreatePropertyXml(id: "Group", label: "Group", description: "Display the node as a group", dataType: "Microsoft.VisualStudio.GraphModel.GraphGroupStyle"),
                        CreatePropertyXml(id: "IncomingActionLabel", label: "IncomingActionLabel", description: "IncomingActionLabel", dataType: "System.String"),
                        CreatePropertyXml(id: "IsContainment", dataType: "System.Boolean"),
                        CreatePropertyXml(id: "Label", label: "Label", description: "Displayable label of an Annotatable object", dataType: "System.String"),
                        CreatePropertyXml(id: "Layout", dataType: "System.String"),
                        CreatePropertyXml(id: "OutgoingActionLabel", label: "OutgoingActionLabel", description: "OutgoingActionLabel", dataType: "System.String"),
                        CreatePropertyXml(id: "UseManualLocation", dataType: "System.Boolean"),
                        CreatePropertyXml(id: "ZoomLevel", dataType: "System.String")
                        )
                    )
                );
        }

        static XElement CreatePropertyXml(string id, string label = null, string description = null, string dataType = null)
        {
            return new XElement(ns + "Property", CreateAttributes(new[]
            {
                ("Id", id),
                ("Label", label),
                ("Description", description),
                ("DataType", dataType),
            }));
        }

        static XElement CreateNodeXml(string id, string label = null, string name = null, string category = null, string @group = null)
        {
            return new XElement(ns + "Node", CreateAttributes(new[]
            {
                ("Id", id),
                ("Label", label),
                ("Category", category),
                ("Group", @group),
            }));
        }

        static IEnumerable<XAttribute> CreateAttributes(IEnumerable<(string name, string value)> attrs)
        {
            foreach (var (name, value) in attrs)
            {
                if (value != null)
                {
                    yield return new XAttribute(name, value);
                }
            }
        }

        IEnumerable<GroupNode> GroupTree(ObjectData node, GroupNode group)
        {
            if (group != null)
            {
                group.ItemsInGroup.Add(node);
            }

            var childLinks = node.Children.ToArray();

            foreach (var child in childLinks)
            {
                GroupNode childGroup;
                var childObject = child.Object as CompositionObject;
                var childDescription = (childObject != null && !string.IsNullOrWhiteSpace(childObject.ShortDescription))
                    ? childObject.ShortDescription
                    : string.Empty;

                // Start a new group for the child if:
                //   a) There is more than one child and the child is not a leaf
                //  or
                //   b) The child has a ShortDescription starting with "Layer: "
                if ((childLinks.Length > 1 && child.Children.Any()) || childDescription.StartsWith("Layer "))
                {
                    childGroup = new GroupNode(this)
                    {
                        Id = GenerateGroupId(),
                        GroupName = childDescription,
                    };

                    if (group != null)
                    {
                        group.GroupsInGroup.Add(childGroup);
                    }

                    yield return childGroup;
                }
                else
                {
                    childGroup = group;
                }

                // Recurse to group the subtree.
                foreach (var groupNode in GroupTree(child, childGroup))
                {
                    yield return groupNode;
                }
            }
        }

        string GenerateId() => $"id{_idGenerator++}";

        string GenerateGroupId() => $"gid{_groupIdGenerator++}";

        sealed class ObjectData : Graph.Node<ObjectData>
        {
            readonly List<ObjectData> _children = new List<ObjectData>();
            CompositionObjectDgmlSerializer _owner;
            ObjectData _parent;

            internal string Name { get; set; }

            internal Category Category { get; set; }

            internal bool IsDgmlNode { get; private set; }

            internal string Id { get; private set; }

            // The links from this node to its children.
            internal IEnumerable<ObjectData> Children => _children;

            // Called after the graph has been created. Do things here that depend on other nodes
            // in the graph.
            internal void Initialize(CompositionObjectDgmlSerializer owner)
            {
                // Set a category for the node, or leave it null if it's not
                // to be displayed in the graph.
                _owner = owner;
                var obj = Object as CompositionObject;
                if (obj != null)
                {
                    switch (obj.Type)
                    {
                        case CompositionObjectType.AnimationController:
                        case CompositionObjectType.ColorKeyFrameAnimation:
                        case CompositionObjectType.CompositionColorBrush:
                        case CompositionObjectType.CompositionColorGradientStop:
                        case CompositionObjectType.CompositionEllipseGeometry:
                        case CompositionObjectType.CompositionLinearGradientBrush:
                        case CompositionObjectType.CompositionGeometricClip:
                        case CompositionObjectType.CompositionPathGeometry:
                        case CompositionObjectType.CompositionPropertySet:
                        case CompositionObjectType.CompositionRadialGradientBrush:
                        case CompositionObjectType.CompositionRectangleGeometry:
                        case CompositionObjectType.CompositionRoundedRectangleGeometry:
                        case CompositionObjectType.CompositionViewBox:
                        case CompositionObjectType.CubicBezierEasingFunction:
                        case CompositionObjectType.ExpressionAnimation:
                        case CompositionObjectType.InsetClip:
                        case CompositionObjectType.LinearEasingFunction:
                        case CompositionObjectType.PathKeyFrameAnimation:
                        case CompositionObjectType.ScalarKeyFrameAnimation:
                        case CompositionObjectType.StepEasingFunction:
                        case CompositionObjectType.Vector2KeyFrameAnimation:
                        case CompositionObjectType.Vector3KeyFrameAnimation:
                        case CompositionObjectType.CompositionSurfaceBrush:
                            // Do not display in the graph.
                            return;
                        case CompositionObjectType.CompositionContainerShape:
                            Category = IsAnimatedCompositionObject ? CategoryContainerShapeAnimated : CategoryContainerShape;
                            break;
                        case CompositionObjectType.CompositionSpriteShape:
                            Category = GetCategory((CompositionSpriteShape)obj);
                            break;
                        case CompositionObjectType.ContainerVisual:
                            Category = IsAnimatedCompositionObject ? CategoryContainerVisualAnimated : CategoryContainerVisual;
                            break;
                        case CompositionObjectType.ShapeVisual:
                            Category = IsAnimatedCompositionObject ? CategoryShapeVisualAnimated : CategoryShapeVisual;
                            break;
                        case CompositionObjectType.SpriteVisual:
                            Category = CategorySpriteVisual;
                            break;
                        default:
                            throw new InvalidOperationException();
                    }

                    IsDgmlNode = true;
                    Id = _owner.GenerateId();
                }
            }

            internal void Initialize2()
            {
                // Create a link to the parent and from the parent to the child.
                if (IsDgmlNode)
                {
                    _parent = InReferences.Select(v => v.Node).Where(n => n.IsDgmlNode).FirstOrDefault();

                    if (_parent != null)
                    {
                        _parent._children.Add(this);
                    }
                }
            }

            public override string ToString() => Id;

            bool IsAnimatedCompositionObject
            {
                get
                {
                    var obj = (CompositionObject)Object;
                    return obj.Animators.Any() || obj.Properties.Animators.Any();
                }
            }

            bool IsSpriteShapeWithAnimatedGeometory
            {
                get
                {
                    var obj = (CompositionSpriteShape)Object;
                    var geometry = obj.Geometry;
                    return geometry != null && (geometry.Animators.Any() || geometry.Properties.Animators.Any());
                }
            }

            static Category GetCategory(CompositionSpriteShape shape)
            {
                var isAnimated = shape.Animators.Any() || shape.Properties.Animators.Any();
                var geometry = shape.Geometry;
                if (geometry == null)
                {
                    return isAnimated ? CategoryShapeAnimated : CategoryShape;
                }

                var isGeometryAnimated = geometry.Animators.Any() || geometry.Properties.Animators.Any();

                switch (geometry.Type)
                {
                    case CompositionObjectType.CompositionEllipseGeometry:
                        return isGeometryAnimated ? CategoryEllipseAnimated : CategoryEllipse;
                    case CompositionObjectType.CompositionPathGeometry:
                        return isGeometryAnimated ? CategoryPathAnimated : CategoryPath;
                    case CompositionObjectType.CompositionRectangleGeometry:
                        return isGeometryAnimated ? CategoryRectangleAnimated : CategoryRectangle;
                    case CompositionObjectType.CompositionRoundedRectangleGeometry:
                        return isGeometryAnimated ? CategoryAnimatedAnimatedRoundedRectangle : CategoryRoundedRectangle;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        sealed class GroupNode
        {
            readonly CompositionObjectDgmlSerializer _owner;

            internal GroupNode(CompositionObjectDgmlSerializer owner)
            {
                _owner = owner;
            }

            internal HashSet<ObjectData> ItemsInGroup { get; } = new HashSet<ObjectData>();

            internal List<GroupNode> GroupsInGroup { get; } = new List<GroupNode>();

            internal string Id { get; set; }

            internal string GroupName { get; set; }

            public override string ToString() => Id;
        }

        sealed class Category
        {
            readonly string _label;
            readonly Color _backgroundColor;

            internal Category(string id, string label, Color backgroundColor)
            {
                Id = id;
                _label = label;
                _backgroundColor = backgroundColor;
            }

            internal Category(string idAndLabel, Color backgroundColor)
                : this(idAndLabel, idAndLabel, backgroundColor)
            {
            }

            internal string Id { get; private set; }

            internal XElement ToXElement()
            {
                return new XElement(ns + "Category", CreateAttributes(new[]
                {
                    ("Id", Id),
                    ("Label", _label),
                    ("Background", $"#{_backgroundColor.Hex}"),
                    ("IsTag", "True"),
                }));
            }
        }
    }
}
