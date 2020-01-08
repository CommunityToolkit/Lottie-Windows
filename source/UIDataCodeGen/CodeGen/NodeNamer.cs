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
            var nodesByTypeName = new Dictionary<string, List<TNode>>();
            foreach (var node in nodes)
            {
                string baseName;

                // Generate descriptive name for each node. The name is generated based on its type
                // and properties to give as much information about the node as possible, so that
                // a specific node can be identified in the composition.
                switch (node.Type)
                {
                    case Graph.NodeType.CompositionObject:
                        baseName = DescribeCompositionObject(node, (CompositionObject)node.Object);
                        break;
                    case Graph.NodeType.CompositionPath:
                        baseName = "CompositionPath";
                        break;
                    case Graph.NodeType.CanvasGeometry:
                        baseName = "Geometry";
                        break;
                    case Graph.NodeType.LoadedImageSurface:
                        baseName = DescribeLoadedImageSurface(node, (LoadedImageSurface)node.Object);
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                if (!nodesByTypeName.TryGetValue(baseName, out var nodeList))
                {
                    nodeList = new List<TNode>();
                    nodesByTypeName.Add(baseName, nodeList);
                }

                nodeList.Add(node);
            }

            // Set the names on each node.
            foreach (var entry in nodesByTypeName)
            {
                var baseName = entry.Key;
                var nodeList = entry.Value;

                // Append a counter suffix.
                // NOTE: For C# there is no need for a suffix if there is only one node with this name,
                //       however this can break C++ which cannot distinguish between a method name and
                //       a type name. For example, if a single CompositionPath node produced a method
                //       called CompositionPath() and then a call was made to "new CompositionPath(...)"
                //       the C++ compiler will complain that CompositionPath is not a type.
                //       So to ensure we don't hit that case, always append a counter suffix.

                // Use only as many digits as necessary to express the largest count.
                var digitsRequired = (int)Math.Ceiling(Math.Log10(nodeList.Count + 1));
                var counterFormat = new string('0', digitsRequired);

                for (var i = 0; i < nodeList.Count; i++)
                {
                    yield return (nodeList[i], $"{baseName}_{i.ToString(counterFormat)}");
                }
            }
        }

        static string AppendDescription(string baseName, string description)
            => baseName + (string.IsNullOrWhiteSpace(description) ? string.Empty : $"_{description}");

        // Returns the value from the given keyframe, or null.
        static T? ValueFromKeyFrame<T>(KeyFrameAnimation<T>.KeyFrame kf)
            where T : struct
        {
            return kf is KeyFrameAnimation<T>.ValueKeyFrame valueKf ? (T?)valueKf.Value : null;
        }

        static (T? First, T? Last) FirstAndLastValuesFromKeyFrame<T>(KeyFrameAnimation<T> animation)
            where T : struct
        {
            // If there's only one keyframe, return it as the last value and leave the first value null.
            var first = animation.KeyFrameCount > 1 ? ValueFromKeyFrame(animation.KeyFrames.First()) : null;
            var last = ValueFromKeyFrame(animation.KeyFrames.Last());
            return (first, last);
        }

        // Returns a string for use in an identifier that describes a ColorKeyFrameAnimation, or null
        // if the animation cannot be described.
        static string DescribeAnimationRange(ColorKeyFrameAnimation animation) => DescribeAnimationRange(animation, v => v.Name);

        // Returns a string for use in an identifier that describes a ScalarKeyFrameAnimation, or null
        // if the animation cannot be described.
        static string DescribeAnimationRange(ScalarKeyFrameAnimation animation) => DescribeAnimationRange(animation, FloatAsId);

        // Returns a string for use in an identifier that describes a KeyFrameAnimation, or null
        // if the animation cannot be described.
        static string DescribeAnimationRange<T>(KeyFrameAnimation<T> animation, Func<T, string> valueFormatter)
            where T : struct
        {
            (var firstValue, var lastValue) = FirstAndLastValuesFromKeyFrame(animation);
            return lastValue.HasValue
                ? firstValue.HasValue
                    ? $"{valueFormatter(firstValue.Value)}_to_{valueFormatter(lastValue.Value)}"
                    : $"to_{valueFormatter(lastValue.Value)}"
                : null;
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

            return animators.Length == 1 ? animators[0] : null;
        }

        static string SanitizePropertyName(string propertyName)
        {
            return propertyName == null ? null : propertyName.Replace(".", string.Empty);
        }

        static string DescribeCompositionObject(TNode node, CompositionObject obj)
        {
            string result = null;
            switch (obj.Type)
            {
                case CompositionObjectType.ColorKeyFrameAnimation:
                    result = AppendDescription("ColorAnimation", DescribeAnimationRange((ColorKeyFrameAnimation)obj));
                    break;
                case CompositionObjectType.ScalarKeyFrameAnimation:
                    result = AppendDescription($"{TryGetAnimatedPropertyName(node)}ScalarAnimation", DescribeAnimationRange((ScalarKeyFrameAnimation)obj));
                    break;
                case CompositionObjectType.Vector2KeyFrameAnimation:
                    result = $"{SanitizePropertyName(TryGetAnimatedPropertyName(node))}Vector2Animation";
                    break;
                case CompositionObjectType.Vector3KeyFrameAnimation:
                    result = $"{SanitizePropertyName(TryGetAnimatedPropertyName(node))}Vector3Animation";
                    break;
                case CompositionObjectType.Vector4KeyFrameAnimation:
                    result = $"{SanitizePropertyName(TryGetAnimatedPropertyName(node))}Vector4Animation";
                    break;
                case CompositionObjectType.CompositionColorBrush:
                    // Color brushes that are not animated get names describing their color.
                    // Optimization ensures there will only be one brush for any one non-animated color.
                    var brush = (CompositionColorBrush)obj;
                    if (brush.Animators.Count > 0)
                    {
                        // Brush is animated. Give it a name based on the colors in the animation.
                        var colorAnimation = brush.Animators.Where(a => a.AnimatedProperty == "Color").First().Animation;
                        if (colorAnimation.Type == CompositionObjectType.ColorKeyFrameAnimation)
                        {
                            result = AppendDescription("AnimatedColorBrush", DescribeAnimationRange((ColorKeyFrameAnimation)colorAnimation));
                        }
                        else
                        {
                            // The color is bound to a property set.
                            var objectName = ((IDescribable)brush).Name;

                            result = string.IsNullOrWhiteSpace(objectName)
                                ? "BoundColorBrush"
                                : $"{objectName}ColorBrush";
                        }
                    }
                    else
                    {
                        // Brush is not animated. Give it a name based on the color.
                        result = AppendDescription("ColorBrush", brush.Color?.Name);
                    }

                    break;
                case CompositionObjectType.CompositionColorGradientStop:
                    var stop = (CompositionColorGradientStop)obj;
                    if (stop.Animators.Count > 0)
                    {
                        // Brush is animated. Give it a name based on the colors in the animation.
                        var colorAnimation = stop.Animators.Where(a => a.Animation is ColorKeyFrameAnimation).First().Animation;
                        if (colorAnimation.Type == CompositionObjectType.ColorKeyFrameAnimation)
                        {
                            result = AppendDescription("AnimatedGradientStop", DescribeAnimationRange((ColorKeyFrameAnimation)colorAnimation));
                        }
                        else
                        {
                            // The color is bound to an expression.
                            result = "BoundColorStop";
                        }
                    }
                    else
                    {
                        // Brush is not animated. Give it a name based on the color.
                        result = AppendDescription("GradientStop", stop.Color.Name);
                    }

                    break;
                case CompositionObjectType.CompositionRectangleGeometry:
                    var rectangle = (CompositionRectangleGeometry)obj;
                    result = AppendDescription("Rectangle", Vector2AsId(rectangle.Size));
                    break;
                case CompositionObjectType.CompositionRoundedRectangleGeometry:
                    var roundedRectangle = (CompositionRoundedRectangleGeometry)obj;
                    result = AppendDescription("RoundedRectangle", Vector2AsId(roundedRectangle.Size));
                    break;
                case CompositionObjectType.CompositionEllipseGeometry:
                    var ellipse = (CompositionEllipseGeometry)obj;
                    result = AppendDescription("Ellipse", Vector2AsId(ellipse.Radius));
                    break;
                case CompositionObjectType.ExpressionAnimation:
                    var expressionAnimation = (ExpressionAnimation)obj;
                    var expression = expressionAnimation.Expression;
                    var expressionType = expression.InferredType;
                    if (expressionType.IsValid && !expressionType.IsGeneric)
                    {
                        result = $"{expressionType.Constraints.ToString()}ExpressionAnimation";
                    }
                    else
                    {
                        result = "ExpressionAnimation";
                    }

                    break;
                case CompositionObjectType.StepEasingFunction:
                    // Recognize 2 common patterns: HoldThenStep and StepThenHold
                    var stepEasingFunction = (StepEasingFunction)obj;
                    if (stepEasingFunction.StepCount == 1 && stepEasingFunction.IsFinalStepSingleFrame && !stepEasingFunction.IsInitialStepSingleFrame)
                    {
                        result = "HoldThenStepEasingFunction";
                    }
                    else if (stepEasingFunction.StepCount == 1 && stepEasingFunction.IsInitialStepSingleFrame && !stepEasingFunction.IsFinalStepSingleFrame)
                    {
                        result = "StepThenHoldEasingFunction";
                    }
                    else
                    {
                        // Didn't recognize the pattern.
                        goto default;
                    }

                    break;
                default:
                    result = obj.Type.ToString();
                    break;
            }

            // Remove the "Composition" prefix so the name is easier to read.
            const string compositionPrefix = "Composition";
            if (result.StartsWith(compositionPrefix))
            {
                result = result.Substring(compositionPrefix.Length);
            }

            return result;
        }

        static string DescribeLoadedImageSurface(TNode node, LoadedImageSurface obj)
        {
            string result = null;
            var loadedImageSurface = (LoadedImageSurface)node.Object;

            switch (loadedImageSurface.Type)
            {
                case LoadedImageSurface.LoadedImageSurfaceType.FromStream:
                    result = "ImageFromStream";
                    break;
                case LoadedImageSurface.LoadedImageSurfaceType.FromUri:
                    var loadedImageSurfaceFromUri = (LoadedImageSurfaceFromUri)loadedImageSurface;

                    // Get the image file name only.
                    var imageFileName = loadedImageSurfaceFromUri.Uri.Segments.Last();
                    var imageFileNameWithoutExtension = imageFileName.Substring(0, imageFileName.LastIndexOf('.'));

                    // Replace any disallowed character with underscores.
                    var cleanedImageName = new string((from ch in imageFileNameWithoutExtension
                                                       select char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

                    // Remove any duplicated underscores.
                    cleanedImageName = cleanedImageName.Replace("__", "_");
                    result = AppendDescription("Image", cleanedImageName);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return result;
        }

        // A float for use in an id.
        static string FloatAsId(float value) => value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', 'p').Replace('-', 'm');

        // A Vector2 for use in an id.
        static string Vector2AsId(Vector2 size)
        {
            return size.X == size.Y
                ? FloatAsId(size.X)
                : $"{FloatAsId(size.X)}x{FloatAsId(size.Y)}";
        }
    }
}
