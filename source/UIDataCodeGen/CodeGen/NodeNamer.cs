// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Generates names for the nodes in an <see cref="ObjectGraph{T}"/>.
    /// </summary>
    /// <typeparam name="TNode">The type of the data associated with each node.</typeparam>
#if PUBLIC_UIDataCodeGen
    public
#endif
    static class NodeNamer<TNode>
        where TNode : Graph.Node<TNode>, new()
    {
        /// <summary>
        /// Takes a list of nodes and generates unique names for them. Returns a list of node + name pairs.
        /// The names are chosen to be descriptive and usable in code generation.
        /// </summary>
        /// <returns>A lot of node + name pairs usable in code generation.</returns>
        public static IEnumerable<(TNode, string)> GenerateNodeNames(IEnumerable<TNode> nodes)
        {
            var nodesByName = new Dictionary<NodeName, List<TNode>>();
            foreach (var node in nodes)
            {
                // Generate descriptive name for each node. The name is generated based on its type
                // and properties to give as much information about the node as possible, so that
                // a specific node can be identified in the composition.
                var nodeName = node.Type switch
                {
                    Graph.NodeType.CompositionObject => NameCompositionObject(node, (CompositionObject)node.Object),
                    Graph.NodeType.CompositionPath => NodeName.FromNonTypeName("Path"),
                    Graph.NodeType.CanvasGeometry => NodeName.FromNonTypeName("Geometry"),
                    Graph.NodeType.LoadedImageSurface => NameLoadedImageSurface(node, (LoadedImageSurface)node.Object),
                    _ => throw Unreachable,
                };

                if (!nodesByName.TryGetValue(nodeName, out var nodeList))
                {
                    nodeList = new List<TNode>();
                    nodesByName.Add(nodeName, nodeList);
                }

                nodeList.Add(node);
            }

            // Set the names on each node.
            var uniqueNames = new HashSet<string>();

            // First deal with names that we know are unique.
            foreach (var (nodeName, nodeList) in nodesByName)
            {
                // NOTE: For C# there is no need for a suffix if there is only one node with this name,
                //       however this can break C++ which cannot distinguish between a method name and
                //       a type name. For example, if a single CompositionPath node produced a method
                //       called CompositionPath() and then a call was made to "new CompositionPath(...)"
                //       the C++ compiler will complain that CompositionPath is not a type.
                //       So to ensure we don't hit that case, append a counter suffix, unless the name
                //       is known to not be a type name.
                if (nodeList.Count == 1 && nodeName.IsNotATypeName)
                {
                    // The name is unique and is not a type name, so no need for a suffix.
                    var name = nodeName.Name;
                    uniqueNames.Add(name);
                    yield return (nodeList[0], name);
                }
            }

            // Now deal with the names that are not unique by appending a counter suffix.
            foreach (var (nodeName, nodeList) in nodesByName)
            {
                if (nodeList.Count > 1 || !nodeName.IsNotATypeName)
                {
                    // Use only as many digits as necessary to express the largest count.
                    var digitsRequired = (int)Math.Ceiling(Math.Log10(nodeList.Count));
                    var counterFormat = new string('0', digitsRequired);

                    var suffixOffset = 0;
                    for (var i = 0; i < nodeList.Count; i++)
                    {
                        // Create a unique name by appending a suffix.
                        // If the name already exists then increment the suffix until a unique
                        // name is found. This is necessary to deal with collisions with the
                        // names that were known to be unique but that have names that look
                        // like they have counter suffixes, for example Rectangle_15 could
                        // be a 15x15 rectangle, or it could be the 15th rectangle with an
                        // animated size.
                        string name;
                        while (true)
                        {
                            var counter = i + suffixOffset;
                            name = $"{nodeName.Name}_{counter.ToString(counterFormat)}";
                            if (uniqueNames.Add(name))
                            {
                                // The name was unique.
                                break;
                            }

                            // Try the next suffix value.
                            suffixOffset++;
                        }

                        yield return (nodeList[i], name);
                    }
                }
            }
        }

        // Returns a name for the given CompositionObject, suitable for use as an identifier.
        static NodeName NameCompositionObject(TNode node, CompositionObject obj)
        {
            var name = NameOf(obj);

            if (name != null)
            {
                // The object has a name, so use it.
                return NodeName.FromNonTypeName(name);
            }

            return obj.Type switch
            {
                // For some animations, we can include a description of the start and end values
                // to make the names more descriptive.
                CompositionObjectType.ColorKeyFrameAnimation
                    => NodeName.FromNameAndDescription("ColorAnimation", DescribeAnimationRange((ColorKeyFrameAnimation)obj)),
                CompositionObjectType.ScalarKeyFrameAnimation
                    => NodeName.FromNameAndDescription($"{TryGetAnimatedPropertyName(node)}ScalarAnimation", DescribeAnimationRange((ScalarKeyFrameAnimation)obj)),

                // Do not include descriptions of the animation range for vectors - the names
                // end up being very long, complicated, and confusing to the reader.
                CompositionObjectType.Vector2KeyFrameAnimation => NodeName.FromNonTypeName($"{TryGetAnimatedPropertyName(node)}Vector2Animation"),
                CompositionObjectType.Vector3KeyFrameAnimation => NodeName.FromNonTypeName($"{TryGetAnimatedPropertyName(node)}Vector3Animation"),
                CompositionObjectType.Vector4KeyFrameAnimation => NodeName.FromNonTypeName($"{TryGetAnimatedPropertyName(node)}Vector4Animation"),

                // Boolean animations don't have interesting range descriptions, but their property name
                // is helpful to know (it is typically "IsVisible").
                CompositionObjectType.BooleanKeyFrameAnimation => NodeName.FromNonTypeName($"{TryGetAnimatedPropertyName(node)}BooleanAnimation"),

                // Geometries include their size as part of the description.
                CompositionObjectType.CompositionRectangleGeometry
                    => NodeName.FromNameAndDescription("Rectangle", Vector2AsId(((CompositionRectangleGeometry)obj).Size)),
                CompositionObjectType.CompositionRoundedRectangleGeometry
                    => NodeName.FromNameAndDescription("RoundedRectangle", Vector2AsId(((CompositionRoundedRectangleGeometry)obj).Size)),
                CompositionObjectType.CompositionEllipseGeometry
                    => NodeName.FromNameAndDescription("Ellipse", Vector2AsId(((CompositionEllipseGeometry)obj).Radius)),

                CompositionObjectType.ExpressionAnimation => NameExpressionAnimation((ExpressionAnimation)obj),
                CompositionObjectType.CompositionColorBrush => NameCompositionColorBrush((CompositionColorBrush)obj),
                CompositionObjectType.CompositionColorGradientStop => NameCompositionColorGradientStop((CompositionColorGradientStop)obj),
                CompositionObjectType.StepEasingFunction => NameStepEasingFunction((StepEasingFunction)obj),

                _ => NameCompositionObjectType(obj.Type),
            };
        }

        static NodeName NameCompositionColorBrush(CompositionColorBrush obj)
        {
            // Color brushes that are not animated get names describing their color.
            // Optimization ensures there will only be one brush for any one non-animated color.
            if (obj.Animators.Count > 0)
            {
                // Brush is animated. Give it a name based on the colors in the animation.
                var colorAnimation = obj.Animators.Where(a => a.AnimatedProperty == "Color").First().Animation;
                if (colorAnimation is ColorKeyFrameAnimation colorKeyFrameAnimation)
                {
                    return NodeName.FromNameAndDescription("AnimatedColorBrush", DescribeAnimationRange(colorKeyFrameAnimation));
                }
                else
                {
                    return NodeName.FromNonTypeName("AnimatedColorBrush");
                }
            }
            else
            {
                // Brush is not animated. Give it a name based on the color.
                return NodeName.FromNameAndDescription("ColorBrush", obj.Color?.Name);
            }
        }

        static NodeName NameCompositionColorGradientStop(CompositionColorGradientStop obj)
        {
            var offsetId = FloatAsId(obj.Offset);

            if (obj.Animators.Count > 0)
            {
                var baseName = $"AnimatedGradientStop_{offsetId}";

                // Gradient stop is animated. Give it a name based on the colors in the animation.
                var colorAnimation = obj.Animators.Where(a => a.AnimatedProperty == "Color").First().Animation;
                if (colorAnimation is ColorKeyFrameAnimation colorKeyFrameAnimation)
                {
                    return NodeName.FromNameAndDescription(baseName, DescribeAnimationRange(colorKeyFrameAnimation));
                }
                else
                {
                    return NodeName.FromNonTypeName(baseName);
                }
            }
            else
            {
                // Gradient stop is not animated. Give it a name based on the color.
                return NodeName.FromNameAndDescription($"GradientStop_{offsetId}", obj.Color.Name);
            }
        }

        static NodeName NameCompositionObjectType(CompositionObjectType type)
        {
            // ToString() the type name, but strip the "Composition" prefix to make
            // it easier to read. The "Composition" prefix is redundant as far as the
            // reader is concerned because most of the objects have it and it doesn't
            // indicate anything useful to the reader.
            var typeName = type.ToString();
            var strippedTypeName = StripPrefix(typeName, "Composition");
            return strippedTypeName == typeName ? NodeName.FromTypeName(typeName) : NodeName.FromNonTypeName(strippedTypeName);
        }

        static NodeName NameExpressionAnimation(ExpressionAnimation obj)
            => NodeName.FromNonTypeName($"{obj.Expression.Type}ExpressionAnimation");

        static NodeName NameStepEasingFunction(StepEasingFunction obj)
        {
            // Recognize 2 common patterns: HoldThenStep and StepThenHold
            if (obj.StepCount == 1)
            {
                if (obj.IsFinalStepSingleFrame && !obj.IsInitialStepSingleFrame)
                {
                    return NodeName.FromNonTypeName("HoldThenStepEasingFunction");
                }
                else if (obj.IsInitialStepSingleFrame && !obj.IsFinalStepSingleFrame)
                {
                    return NodeName.FromNonTypeName("StepThenHoldEasingFunction");
                }
            }

            // Didn't recognize the pattern.
            return NodeName.FromNonTypeName("EasingFunction");
        }

        static NodeName NameLoadedImageSurface(TNode node, LoadedImageSurface obj)
            => obj.Type switch
            {
                LoadedImageSurface.LoadedImageSurfaceType.FromStream => NodeName.FromNonTypeName("ImageFromStream"),
                LoadedImageSurface.LoadedImageSurfaceType.FromUri => DescribeLoadedImageSurfaceFromUri((LoadedImageSurfaceFromUri)obj),
                _ => throw Unreachable,
            };

        static NodeName DescribeLoadedImageSurfaceFromUri(LoadedImageSurfaceFromUri obj)
        {
            // Get the image file name only.
            var imageFileName = obj.Uri.Segments.Last();
            var imageFileNameWithoutExtension = imageFileName.Substring(0, imageFileName.LastIndexOf('.'));

            // Replace any disallowed character with underscores.
            var cleanedImageName = new string((from ch in imageFileNameWithoutExtension
                                               select char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

            // Remove any duplicated underscores.
            cleanedImageName = cleanedImageName.Replace("__", "_");
            return NodeName.FromNameAndDescription("Image", cleanedImageName);
        }

        // Returns a string for use in an identifier that describes a BooleanKeyFrameAnimation, or null
        // if the animation cannot be described.
        static string DescribeAnimationRange(BooleanKeyFrameAnimation animation) => DescribeAnimationRange(animation, v => v.ToString());

        // Returns a string for use in an identifier that describes a ColorKeyFrameAnimation, or null
        // if the animation cannot be described.
        static string DescribeAnimationRange(ColorKeyFrameAnimation animation) => DescribeAnimationRange(animation, v => v.Name);

        // Returns a string for use in an identifier that describes a ScalarKeyFrameAnimation, or null
        // if the animation cannot be described.
        static string DescribeAnimationRange(ScalarKeyFrameAnimation animation) => DescribeAnimationRange(animation, FloatAsId);

        // Returns a string for use in an identifier that describes a KeyFrameAnimation, or null
        // if the animation cannot be described.
        static string DescribeAnimationRange<T, TExpression>(KeyFrameAnimation<T, TExpression> animation, Func<T, string> valueFormatter)
            where T : struct
            where TExpression : Expr.Expression_<TExpression>
        {
            (var firstValue, var lastValue) = FirstAndLastValuesFromKeyFrame(animation);
            return lastValue.HasValue
                ? firstValue.HasValue
                    ? $"{valueFormatter(firstValue.Value)}_to_{valueFormatter(lastValue.Value)}"
                    : $"to_{valueFormatter(lastValue.Value)}"
                : null;
        }

        // Returns the value from the given keyframe, or null.
        static T? ValueFromKeyFrame<T, TExpression>(KeyFrameAnimation_.KeyFrame kf)
            where TExpression : Expr.Expression_<TExpression>
            where T : struct
                => kf is KeyFrameAnimation<T, TExpression>.ValueKeyFrame valueKf ? (T?)valueKf.Value : null;

        static (T? First, T? Last) FirstAndLastValuesFromKeyFrame<T, TExpression>(KeyFrameAnimation<T, TExpression> animation)
            where T : struct
            where TExpression : Expr.Expression_<TExpression>
        {
            // If there's only one keyframe, return it as the last value and leave the first value null.
            var first = animation.KeyFrameCount > 1 ? ValueFromKeyFrame<T, TExpression>(animation.KeyFrames.First()) : null;
            var last = ValueFromKeyFrame<T, TExpression>(animation.KeyFrames.Last());
            return (first, last);
        }

        static string TryGetAnimatedPropertyName(TNode node)
        {
            // Find the property name that references this animation.
            var animators =
                (from inref in node.InReferences
                 let referrer = (CompositionObject)inref.Node.Object
                 from animator in referrer.Animators
                 where animator.Animation == node.Object
                 select animator.AnimatedProperty).Distinct().ToArray();

            return animators.Length == 1 ? SanitizePropertyName(animators[0]) : null;
        }

        static string SanitizePropertyName(string propertyName)
            => propertyName?.Replace(".", string.Empty);

        // Removes the given prefix from a name.
        static string StripPrefix(string name, string prefix)
            => name.StartsWith(prefix)
                ? name.Substring(prefix.Length)
                : name;

        // A float for use in an id.
        static string FloatAsId(float value)
            => value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', 'p').Replace('-', 'm');

        static string NameOf(IDescribable obj) => obj.Name;

        // A Vector2 for use in an id.
        static string Vector2AsId(Vector2? size)
            => size.HasValue
                ? (size.Value.X == size.Value.Y ? FloatAsId(size.Value.X) : $"{FloatAsId(size.Value.X)}x{FloatAsId(size.Value.Y)}")
                : string.Empty;

        // The code we hit is supposed to be unreachable. This indicates a bug.
        static Exception Unreachable => new InvalidOperationException("Unreachable code executed");

        readonly struct NodeName
        {
            NodeName(string name, bool isNotATypeName)
            {
                Name = name;
                IsNotATypeName = isNotATypeName;
            }

            internal string Name { get; }

            // True iff the name is definitely not the name of a type.
            internal bool IsNotATypeName { get; }

            internal static NodeName FromNameAndDescription(string name, string description)
                => new NodeName(name + (string.IsNullOrWhiteSpace(description) ? string.Empty : $"_{description}"), true);

            internal static NodeName FromNonTypeName(string name) => new NodeName(name, true);

            internal static NodeName FromTypeName(string typeName) => new NodeName(typeName, false);
        }
    }
}
