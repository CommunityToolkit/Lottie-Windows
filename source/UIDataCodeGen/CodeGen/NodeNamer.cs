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
                        baseName = "Path";
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

        // Returns a description of the given CompositionObject, suitable for use as an identifier.
        static string DescribeCompositionObject(TNode node, CompositionObject obj)
        {
            var result = obj.Type switch
            {
                // For some animations, we can include a description of the start and end values
                // to make the names more descriptive.
                CompositionObjectType.ColorKeyFrameAnimation
                    => AppendDescription("ColorAnimation", DescribeAnimationRange((ColorKeyFrameAnimation)obj)),
                CompositionObjectType.ScalarKeyFrameAnimation
                    => AppendDescription($"{TryGetAnimatedPropertyName(node)}ScalarAnimation", DescribeAnimationRange((ScalarKeyFrameAnimation)obj)),

                // Do not include descriptions of the animation range for vectors - the names
                // end up being very long, complicated, and confusing to the reader.
                CompositionObjectType.Vector2KeyFrameAnimation => $"{TryGetAnimatedPropertyName(node)}Vector2Animation",
                CompositionObjectType.Vector3KeyFrameAnimation => $"{TryGetAnimatedPropertyName(node)}Vector3Animation",
                CompositionObjectType.Vector4KeyFrameAnimation => $"{TryGetAnimatedPropertyName(node)}Vector4Animation",

                // Geometries include their size as part of the description.
                CompositionObjectType.CompositionRectangleGeometry
                    => AppendDescription("Rectangle", Vector2AsId(((CompositionRectangleGeometry)obj).Size)),
                CompositionObjectType.CompositionRoundedRectangleGeometry
                    => AppendDescription("RoundedRectangle", Vector2AsId(((CompositionRoundedRectangleGeometry)obj).Size)),
                CompositionObjectType.CompositionEllipseGeometry
                    => AppendDescription("Ellipse", Vector2AsId(((CompositionEllipseGeometry)obj).Radius)),

                CompositionObjectType.ExpressionAnimation => DescribeExpressionAnimation((ExpressionAnimation)obj),
                CompositionObjectType.CompositionColorBrush => DescribeCompositionColorBrush((CompositionColorBrush)obj),
                CompositionObjectType.CompositionColorGradientStop => DescribeCompositionColorGradientStop((CompositionColorGradientStop)obj),
                CompositionObjectType.StepEasingFunction => DescribeStepEasingFunction((StepEasingFunction)obj),

                // All other cases, just ToString() the type name.
                _ => obj.Type.ToString(),
            };

            // Remove the "Composition" prefix so the name is easier to read.
            // The prefix is redundant as far as the reader is concerned because most of the
            // objects have it and it doesn't indicate anything useful to the reader.
            return StripPrefix(result, "Composition");
        }

        static string DescribeCompositionColorBrush(CompositionColorBrush obj)
        {
            // Color brushes that are not animated get names describing their color.
            // Optimization ensures there will only be one brush for any one non-animated color.
            if (obj.Animators.Count > 0)
            {
                // Brush is animated. Give it a name based on the colors in the animation.
                var colorAnimation = obj.Animators.Where(a => a.AnimatedProperty == "Color").First().Animation;
                if (colorAnimation is ColorKeyFrameAnimation colorKeyFrameAnimation)
                {
                    return AppendDescription("AnimatedColorBrush", DescribeAnimationRange(colorKeyFrameAnimation));
                }
                else
                {
                    // The color is bound to a property set.
                    var objectName = ((IDescribable)obj).Name;

                    return string.IsNullOrWhiteSpace(objectName)
                        ? "BoundColorBrush"
                        : $"{objectName}ColorBrush";
                }
            }
            else
            {
                // Brush is not animated. Give it a name based on the color.
                return AppendDescription("ColorBrush", obj.Color?.Name);
            }
        }

        static string DescribeCompositionColorGradientStop(CompositionColorGradientStop obj)
        {
            if (obj.Animators.Count > 0)
            {
                // Gradient stop is animated. Give it a name based on the colors in the animation.
                var colorAnimation = obj.Animators.Where(a => a.AnimatedProperty == "Color").First().Animation;
                if (colorAnimation is ColorKeyFrameAnimation colorKeyFrameAnimation)
                {
                    return AppendDescription("AnimatedGradientStop", DescribeAnimationRange(colorKeyFrameAnimation));
                }
                else
                {
                    // The color is bound to an expression.
                    return "BoundColorStop";
                }
            }
            else
            {
                // Gradient stop is not animated. Give it a name based on the color.
                return AppendDescription("GradientStop", obj.Color.Name);
            }
        }

        static string DescribeExpressionAnimation(ExpressionAnimation obj)
        {
            var expression = obj.Expression;
            var expressionType = expression.Type;
            return $"{expressionType}ExpressionAnimation";
        }

        static string DescribeStepEasingFunction(StepEasingFunction obj)
        {
            // Recognize 2 common patterns: HoldThenStep and StepThenHold
            if (obj.StepCount == 1)
            {
                if (obj.IsFinalStepSingleFrame && !obj.IsInitialStepSingleFrame)
                {
                    return "HoldThenStepEasingFunction";
                }
                else if (obj.IsInitialStepSingleFrame && !obj.IsFinalStepSingleFrame)
                {
                    return "StepThenHoldEasingFunction";
                }
            }

            // Didn't recognize the pattern.
            return "EasingFunction";
        }

        static string DescribeLoadedImageSurface(TNode node, LoadedImageSurface obj)
        {
            string result = null;
            switch (obj.Type)
            {
                case LoadedImageSurface.LoadedImageSurfaceType.FromStream:
                    result = "ImageFromStream";
                    break;
                case LoadedImageSurface.LoadedImageSurfaceType.FromUri:
                    var loadedImageSurfaceFromUri = (LoadedImageSurfaceFromUri)obj;

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
        static T? ValueFromKeyFrame<T, TExpression>(KeyFrameAnimation<T, TExpression>.KeyFrame kf)
            where TExpression : Expr.Expression_<TExpression>
            where T : struct
                => kf is KeyFrameAnimation<T, TExpression>.ValueKeyFrame valueKf ? (T?)valueKf.Value : null;

        static (T? First, T? Last) FirstAndLastValuesFromKeyFrame<T, TExpression>(KeyFrameAnimation<T, TExpression> animation)
            where T : struct
            where TExpression : Expr.Expression_<TExpression>
        {
            // If there's only one keyframe, return it as the last value and leave the first value null.
            var first = animation.KeyFrameCount > 1 ? ValueFromKeyFrame(animation.KeyFrames.First()) : null;
            var last = ValueFromKeyFrame(animation.KeyFrames.Last());
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

        static string AppendDescription(string baseName, string description)
            => baseName + (string.IsNullOrWhiteSpace(description) ? string.Empty : $"_{description}");

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

        // A Vector2 for use in an id.
        static string Vector2AsId(Vector2 size)
            => size.X == size.Y ? FloatAsId(size.X) : $"{FloatAsId(size.X)}x{FloatAsId(size.Y)}";
    }
}
