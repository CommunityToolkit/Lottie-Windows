// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Diagnostics.CodeAnalysis;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.Expression;
using Sn = System.Numerics;

#if DEBUG
// For diagnosing issues, give nothing scale.
//#define NoScaling
#endif

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates Lottie transforms.
    /// </summary>
    static class Transforms
    {
        public static Sn.Matrix3x2 CreateMatrixFromTransform(LayerContext context, Transform? transform)
        {
            if (transform is null)
            {
                return Sn.Matrix3x2.Identity;
            }

            if (transform.IsAnimated)
            {
                // TODO - report an issue. We can't handle an animated transform.
                // TODO - we could handle it if the only thing that is animated is the Opacity.
            }

            var anchor = ConvertTo.Vector2(transform.Anchor.InitialValue);
            var position = ConvertTo.Vector2(transform.Position.InitialValue);
            var scale = ConvertTo.Vector2(transform.ScalePercent.InitialValue / 100.0);
            var rotation = (float)transform.Rotation.InitialValue.Radians;

            // Calculate the matrix that is equivalent to the properties.
            var combinedMatrix =
                Sn.Matrix3x2.CreateScale(scale, anchor) *
                Sn.Matrix3x2.CreateRotation(rotation, anchor) *
                Sn.Matrix3x2.CreateTranslation(position + anchor);

            return combinedMatrix;
        }

        /// <summary>
        /// Returns a chain with a Visual at the top and a CompositionContainerShape at the bottom.
        /// The nodes in between implement the transforms for the layer.
        /// This chain is used when a shape tree needs to be expressed as a visual tree. We take
        /// advantage of this case to do layer opacity and visibility using Visual nodes rather
        /// than pushing the opacity to the leaves and using Scale animations to do visibility.
        /// </summary>
        /// <returns><c>true</c> if the the chain was created.</returns>
        public static bool TryCreateShapeVisualTransformChain(
            LayerContext context,
            [NotNullWhen(true)] out ContainerVisual? rootNode,
            [NotNullWhen(true)] out CompositionContainerShape? contentsNode)
        {
            // Create containers for the contents in the layer.
            // The rootNode is the root for the layer.
            //
            //     +---------------+
            //     |   rootNode    |-- Root node, optionally with opacity animation for the layer.
            //     +---------------+
            //            ^
            //            |
            //     +-----------------+
            //     |  visiblityNode  |-- Optional visiblity node (only used if the visiblity is animated).
            //     +-----------------+
            //            ^
            //            |
            //     +-----------------+
            //     |  opacityNode    |-- Optional opacity node.
            //     +-----------------+
            //            ^
            //            |
            //     +-----------------+
            //     |   ShapeVisual   |-- Start of the shape tree.
            //     +-----------------+
            //            ^
            //            |
            //     +-------------------+
            //     | rootTransformNode |--Transform without opacity (inherited from root ancestor of the transform tree).
            //     +-------------------+
            //            ^
            //            |
            //     + - - - - - - - - - - - - +
            //     | other transforms nodes  |--Transform without opacity (inherited from the transform tree).
            //     + - - - - - - - - - - - - +
            //            ^
            //            |
            //     +-------------------+
            //     | leafTransformNode |--Transform without opacity defined on the layer.
            //     +-------------------+
            //        ^        ^
            //        |        |
            // +---------+ +---------+
            // | content | | content | ...
            // +---------+ +---------+
            //

            // Get the opacity of the layer.
            var layerOpacity = Optimizer.TrimAnimatable(context, context.Layer.Transform.Opacity);

            // Convert the layer's in point and out point into absolute progress (0..1) values.
            var inProgress = context.InPointAsProgress;
            var outProgress = context.OutPointAsProgress;

            if (inProgress > 1 || outProgress <= 0 || inProgress >= outProgress || layerOpacity.IsAlways(Animatables.Opacity.Transparent))
            {
                // The layer is never visible. Don't create anything.
                rootNode = null;
                contentsNode = null;
                return false;
            }

            rootNode = context.ObjectFactory.CreateContainerVisual();
            ContainerVisual contentsVisual = rootNode;

            // Implement opacity for the layer.
            InsertOpacityVisualIntoTransformChain(context, layerOpacity, ref rootNode);

            // Implement visibility for the layer.
            InsertVisibilityVisualIntoTransformChain(context, inProgress, outProgress, ref rootNode);

            // Create the transforms chain.
            TranslateTransformOnContainerShapeForLayer(context, context.Layer, out var transformsRoot, out contentsNode);

            // Create the shape visual.
            var shapeVisual = context.ObjectFactory.CreateShapeVisualWithChild(transformsRoot, context.CompositionContext.Size);

            shapeVisual.SetDescription(context, () => $"Shape tree root for layer: {context.Layer.Name}");

            contentsVisual.Children.Add(shapeVisual);

            return true;
        }

        /// <summary>
        /// Returns a chain of ContainerShape that define the transforms for a layer.
        /// The top of the chain is the rootTransform, the bottom is the contentsNode.
        /// </summary>
        /// <returns><c>true</c> if the the chain was created.</returns>
        public static bool TryCreateContainerShapeTransformChain(
            LayerContext context,
            [NotNullWhen(true)] out CompositionContainerShape? rootNode,
            [NotNullWhen(true)] out CompositionContainerShape? contentsNode)
        {
            // Create containers for the contents in the layer.
            // The rootNode is the root for the layer. It may be the same object
            // as the contentsNode if there are no inherited transforms and no visibility animation.
            //
            //     +---------------+
            //     |      ...      |
            //     +---------------+
            //            ^
            //            |
            //     +-----------------+
            //     |  visiblityNode  |-- Optional visiblity node (only used if the visiblity is animated)
            //     +-----------------+
            //            ^
            //            |
            //     +-------------------+
            //     | rootTransformNode |--Transform (values are inherited from root ancestor of the transform tree)
            //     +-------------------+
            //            ^
            //            |
            //     + - - - - - - - - - - - - +
            //     | other transforms nodes  |--Transform (values inherited from the transform tree)
            //     + - - - - - - - - - - - - +
            //            ^
            //            |
            //     +-------------------+
            //     | leafTransformNode |--Transform defined on the layer
            //     +-------------------+
            //        ^        ^
            //        |        |
            // +---------+ +---------+
            // | content | | content | ...
            // +---------+ +---------+
            //

            // Get the opacity of the layer.
            var layerOpacity = Optimizer.TrimAnimatable(context, context.Layer.Transform.Opacity);

            // Convert the layer's in point and out point into absolute progress (0..1) values.
            var inProgress = context.InPointAsProgress;
            var outProgress = context.OutPointAsProgress;

            if (inProgress > 1 || outProgress <= 0 || inProgress >= outProgress || layerOpacity.IsAlways(Animatables.Opacity.Transparent))
            {
                // The layer is never visible. Don't create anything.
                rootNode = null;
                contentsNode = null;
                return false;
            }

            // Create the transforms chain.
            TranslateTransformOnContainerShapeForLayer(context, context.Layer, out var transformsRoot, out contentsNode);

            // Implement the Visibility for the layer. Only needed if the layer becomes visible after
            // the LottieComposition's in point, or it becomes invisible before the LottieComposition's out point.
            if (inProgress > 0 || outProgress < 1)
            {
                // Create a node to control visibility.
                var visibilityNode = context.ObjectFactory.CreateContainerShape();
                visibilityNode.Shapes.Add(transformsRoot);
                rootNode = visibilityNode;

                visibilityNode.SetDescription(context, () => $"Layer: {context.Layer.Name}");

                // Animate between Scale(0,0) and Scale(1,1).
                var visibilityAnimation = context.ObjectFactory.CreateVector2KeyFrameAnimation();

                visibilityAnimation.SetName("ShapeVisibilityAnimation");

                if (inProgress > 0)
                {
                    // Set initial value to be non-visible (default is visible).
                    visibilityNode.Scale = Sn.Vector2.Zero;
                    visibilityAnimation.InsertKeyFrame(inProgress, Sn.Vector2.One, context.ObjectFactory.CreateHoldThenStepEasingFunction());
                }

                if (outProgress < 1)
                {
                    visibilityAnimation.InsertKeyFrame(outProgress, Sn.Vector2.Zero, context.ObjectFactory.CreateHoldThenStepEasingFunction());
                }

                visibilityAnimation.Duration = context.Translation.LottieComposition.Duration;
                Animate.WithKeyFrame(context, visibilityNode, nameof(visibilityNode.Scale), visibilityAnimation);
            }
            else
            {
                rootNode = transformsRoot;
            }

            return true;
        }

        /// <summary>
        /// Returns a chain of ContainerVisual that define the transforms for a layer.
        /// The top of the chain is the rootTransform, the bottom is the leafTransform.
        /// Returns false if the layer is never visible.
        /// </summary>
        /// <returns><c>true</c> if the the chain was created.</returns>
        public static bool TryCreateContainerVisualTransformChain(
            LayerContext context,
            [NotNullWhen(true)] out ContainerVisual? rootNode,
            [NotNullWhen(true)] out ContainerVisual? contentsNode)
        {
            // Create containers for the contents in the layer.
            // The rootTransformNode is the root for the layer. It may be the same object
            // as the contentsNode if there are no inherited transforms.
            //
            //     +---------------+
            //     |      ...      |
            //     +---------------+
            //            ^
            //            |
            //     +-----------------+
            //     |  visiblityNode  |-- Optional visiblity node (only used if the visiblity is animated)
            //     +-----------------+
            //            ^
            //            |
            //     +-------------------+
            //     | rootTransformNode |--Transform (values are inherited from root ancestor of the transform tree)
            //     +-------------------+
            //            ^
            //            |
            //     + - - - - - - - - - - - - +
            //     | other transforms nodes  |--Transform (values inherited from the transform tree)
            //     + - - - - - - - - - - - - +
            //            ^
            //            |
            //     +---------------+
            //     | contentsNode  |--Transform defined on the layer
            //     +---------------+
            //        ^        ^
            //        |        |
            // +---------+ +---------+
            // | content | | content | ...
            // +---------+ +---------+
            //

            // Get the opacity of the layer.
            var layerOpacity = Optimizer.TrimAnimatable(context, context.Layer.Transform.Opacity);

            // Convert the layer's in point and out point into absolute progress (0..1) values.
            var inProgress = context.InPointAsProgress;
            var outProgress = context.OutPointAsProgress;

            if (inProgress > 1 || outProgress <= 0 || inProgress >= outProgress || layerOpacity.IsAlways(Animatables.Opacity.Transparent))
            {
                // The layer is never visible. Don't create anything.
                rootNode = null;
                contentsNode = null;
                return false;
            }

            // Create the transforms chain.
            TranslateTransformOnContainerVisualForLayer(context, context.Layer, out rootNode, out contentsNode);

            // Implement opacity for the layer.
            InsertOpacityVisualIntoTransformChain(context, layerOpacity, ref rootNode);

            // Implement visibility for the layer.
            InsertVisibilityVisualIntoTransformChain(context, inProgress, outProgress, ref rootNode);

            return true;
        }

        static void InsertVisibilityVisualIntoTransformChain(
            TranslationContext context,
            float inProgress,
            float outProgress,
            ref ContainerVisual root)
        {
            // Implement the Visibility for the layer. Only needed if the layer becomes visible after
            // the LottieComposition's in point, or it becomes invisible before the LottieComposition's out point.
            if (inProgress > 0 || outProgress < 1)
            {
                // Insert a new node to control visibility at the top of the chain.
                var visibilityNode = context.ObjectFactory.CreateContainerVisual();
                visibilityNode.Children.Add(root);
                root = visibilityNode;

                var visibilityAnimation = context.ObjectFactory.CreateBooleanKeyFrameAnimation();
                if (inProgress > 0)
                {
                    // Set initial value to be non-visible.
                    visibilityNode.IsVisible = false;
                    visibilityAnimation.InsertKeyFrame(inProgress, true);
                }

                if (outProgress < 1)
                {
                    visibilityAnimation.InsertKeyFrame(outProgress, false);
                }

                visibilityAnimation.Duration = context.LottieComposition.Duration;
                Animate.WithKeyFrame(context, visibilityNode, "IsVisible", visibilityAnimation);
            }
        }

        static void InsertOpacityVisualIntoTransformChain(
            LayerContext context,
            in TrimmedAnimatable<Opacity> opacity,
            ref ContainerVisual root)
        {
            // Implement opacity for the layer.
            if (opacity.IsAnimated || opacity.InitialValue < Animatables.Opacity.Opaque)
            {
                // Insert a new node to control opacity at the top of the chain.
                var opacityNode = context.ObjectFactory.CreateContainerVisual();

                opacityNode.SetDescription(context, () => $"Opacity for layer: {context.Layer.Name}");

                opacityNode.Children.Add(root);
                root = opacityNode;

                if (opacity.IsAnimated)
                {
                    Animate.Opacity(context, opacity, opacityNode, "Opacity", "Layer opacity animation");
                }
                else
                {
                    opacityNode.Opacity = ConvertTo.Opacity(opacity.InitialValue);
                }
            }
        }

        // Returns a chain of ContainerVisual that define the transform for a layer.
        // The top of the chain is the rootTransform, the bottom is the leafTransform.
        static void TranslateTransformOnContainerVisualForLayer(
            LayerContext context,
            Layer layer,
            out ContainerVisual rootTransformNode,
            out ContainerVisual leafTransformNode)
        {
            // Create a ContainerVisual to apply the transform to.
            leafTransformNode = context.ObjectFactory.CreateContainerVisual();

            // Apply the transform.
            TranslateAndApplyTransform(context, layer.Transform, leafTransformNode);
            leafTransformNode.SetDescription(context, () => $"Transforms for {layer.Name}");

            // Translate the parent transform, if any.
            if (layer.Parent is not null)
            {
                var parentLayer = context.CompositionContext.Layers.GetLayerById(layer.Parent.Value);
                TranslateTransformOnContainerVisualForLayer(context, parentLayer!, out rootTransformNode, out var parentLeafTransform);
                parentLeafTransform.Children.Add(leafTransformNode);
            }
            else
            {
                rootTransformNode = leafTransformNode;
            }
        }

        // Returns a chain of CompositionContainerShape that define the transform for a layer.
        // The top of the chain is the rootTransform, the bottom is the leafTransform.
        static void TranslateTransformOnContainerShapeForLayer(
            LayerContext context,
            Layer layer,
            out CompositionContainerShape rootTransformNode,
            out CompositionContainerShape leafTransformNode)
        {
            // Create a ContainerVisual to apply the transform to.
            leafTransformNode = context.ObjectFactory.CreateContainerShape();

            // Apply the transform from the layer.
            TranslateAndApplyTransform(context, layer.Transform, leafTransformNode);

            // Recurse to translate the parent transform, if any.
            if (layer.Parent is not null)
            {
                var parentLayer = context.CompositionContext.Layers.GetLayerById(layer.Parent.Value);
                TranslateTransformOnContainerShapeForLayer(context, parentLayer!, out rootTransformNode, out var parentLeafTransform);
                parentLeafTransform.Shapes.Add(leafTransformNode);
                leafTransformNode.SetDescription(context, () => ($"Transforms for {layer.Name}", $"Transforms: {layer.Name}"));
            }
            else
            {
                rootTransformNode = leafTransformNode;
            }
        }

        public static void TranslateAndApplyTransform(
            LayerContext context,
            Transform transform,
            ContainerShapeOrVisual container)
        {
            TranslateAndApplyAnchorPositionRotationAndScale(
                context,
                transform.Anchor,
                transform.Position,
                Optimizer.TrimAnimatable(context, transform.Rotation),
                transform.ScalePercent,
                container);

            // TODO: set Skew and Skew Axis
        }

        static void TranslateAndApplyAnchorPositionRotationAndScale(
            LayerContext context,
            IAnimatableVector3 anchor,
            IAnimatableVector3 position,
            in TrimmedAnimatable<Rotation> rotation,
            IAnimatableVector3 scalePercent,
            ContainerShapeOrVisual container)
        {
            // There are many different cases to consider in order to do this optimally:
            // * Is the container a CompositionContainerShape (Vector2 properties)
            //    or a ContainerVisual (Vector3 properties)
            // * Is the anchor animated?
            // * Is the anchor expressed as a Vector2 or as X and Y values?
            // * Is the position animated?
            // * Is the position expressed as a Vector2 or as X and Y values?
            // * Is rotation or scale specified? (If they're not and
            //    the anchor is static then the anchor can be expressed
            //    as just an offset)
            //
            // The current implementation doesn't take all cases into consideration yet.
            if (rotation.IsAnimated)
            {
                Animate.Rotation(context, rotation, container, nameof(container.RotationAngleInDegrees), "Rotation");
            }
            else
            {
                container.RotationAngleInDegrees = ConvertTo.Float(rotation.InitialValue.Degrees);
            }

#if !NoScaling
            // If the channels have separate easings, convert to an AnimatableXYZ.
            var scale = AnimatableVector3Rewriter.EnsureOneEasingPerChannel(scalePercent);

            if (scale is AnimatableXYZ scaleXYZ)
            {
                var trimmedX = Optimizer.TrimAnimatable(context, scaleXYZ.X);
                var trimmedY = Optimizer.TrimAnimatable(context, scaleXYZ.Y);

                if (trimmedX.IsAnimated)
                {
                    Animate.ScaledScalar(context, trimmedX, 1 / 100.0, container, $"{nameof(container.Scale)}.X", nameof(container.Scale));
                }

                if (trimmedY.IsAnimated)
                {
                    Animate.ScaledScalar(context, trimmedY, 1 / 100.0, container, $"{nameof(container.Scale)}.Y", nameof(container.Scale));
                }

                if (!trimmedX.IsAnimated || !trimmedY.IsAnimated)
                {
                    container.Scale = ConvertTo.Vector2(new Vector3(trimmedX.InitialValue, trimmedY.InitialValue, 0) * (1 / 100.0));
                }
            }
            else
            {
                var trimmedScale = Optimizer.TrimAnimatable<Vector3>(context, (AnimatableVector3)scale);

                if (trimmedScale.IsAnimated)
                {
                    if (container.IsShape)
                    {
                        Animate.ScaledVector2(context, trimmedScale, 1 / 100.0, container, nameof(container.Scale), nameof(container.Scale));
                    }
                    else
                    {
                        Animate.ScaledVector3(context, trimmedScale, 1 / 100.0, container, nameof(container.Scale), nameof(container.Scale));
                    }
                }
                else
                {
                    container.Scale = ConvertTo.Vector2(trimmedScale.InitialValue * (1 / 100.0));
                }
            }
#endif

            var anchorX = default(TrimmedAnimatable<double>);
            var anchorY = default(TrimmedAnimatable<double>);
            var anchor3 = default(TrimmedAnimatable<Vector3>);

            var xyzAnchor = anchor as AnimatableXYZ;
            if (xyzAnchor is not null)
            {
                anchorX = Optimizer.TrimAnimatable(context, xyzAnchor.X);
                anchorY = Optimizer.TrimAnimatable(context, xyzAnchor.Y);
            }
            else
            {
                anchor3 = Optimizer.TrimAnimatable(context, anchor);
            }

            var positionX = default(TrimmedAnimatable<double>);
            var positionY = default(TrimmedAnimatable<double>);
            var position3 = default(TrimmedAnimatable<Vector3>);
            var positionWithSeparateEasings = AnimatableVector3Rewriter.EnsureOneEasingPerChannel(position);

            var xyzPosition = positionWithSeparateEasings as AnimatableXYZ;
            if (xyzPosition is not null)
            {
                positionX = Optimizer.TrimAnimatable(context, xyzPosition.X);
                positionY = Optimizer.TrimAnimatable(context, xyzPosition.Y);
            }
            else
            {
                position3 = Optimizer.TrimAnimatable<Vector3>(context, (AnimatableVector3)positionWithSeparateEasings);
            }

            var anchorIsAnimated = anchorX.IsAnimated || anchorY.IsAnimated || anchor3.IsAnimated;
            var positionIsAnimated = positionX.IsAnimated || positionY.IsAnimated || position3.IsAnimated;

            var initialAnchor = xyzAnchor is not null ? ConvertTo.Vector2(anchorX.InitialValue, anchorY.InitialValue) : ConvertTo.Vector2(anchor3.InitialValue);
            var initialPosition = xyzPosition is not null ? ConvertTo.Vector2(positionX.InitialValue, positionY.InitialValue) : ConvertTo.Vector2(position3.InitialValue);

            // The Lottie Anchor is the centerpoint of the object and is used for rotation and scaling.
            if (anchorIsAnimated)
            {
                container.Properties.InsertVector2("Anchor", initialAnchor);
                var centerPointExpression = context.ObjectFactory.CreateExpressionAnimation(container.IsShape ? (Expr)ExpressionFactory.MyAnchor : (Expr)ExpressionFactory.MyAnchor3);
                centerPointExpression.SetReferenceParameter("my", container);
                Animate.WithExpression(container, centerPointExpression, nameof(container.CenterPoint));

                if (xyzAnchor is not null)
                {
                    if (anchorX.IsAnimated)
                    {
                        Animate.Scalar(context, anchorX, container.Properties, targetPropertyName: "Anchor.X");
                    }

                    if (anchorY.IsAnimated)
                    {
                        Animate.Scalar(context, anchorY, container.Properties, targetPropertyName: "Anchor.Y");
                    }
                }
                else
                {
                    Animate.Vector2(context, anchor3, container.Properties, "Anchor");
                }
            }
            else
            {
                container.CenterPoint = ConvertTo.Vector2(initialAnchor);
            }

            // If the position or anchor are animated, the offset needs to be calculated via an expression.
            ExpressionAnimation? offsetExpression = null;
            if (positionIsAnimated && anchorIsAnimated)
            {
                // Both position and anchor are animated.
                offsetExpression = context.ObjectFactory.CreateExpressionAnimation(container.IsShape ? (Expr)ExpressionFactory.PositionMinusAnchor2 : (Expr)ExpressionFactory.PositionMinusAnchor3);
            }
            else if (positionIsAnimated)
            {
                // Only position is animated.
                if (initialAnchor == Sn.Vector2.Zero)
                {
                    // Position and Offset are equivalent because the Anchor is not animated and is 0.
                    // We don't need to animate a Position property - we can animate Offset directly.
                    positionIsAnimated = false;

                    if (xyzPosition is not null)
                    {
                        if (!positionX.IsAnimated || !positionY.IsAnimated)
                        {
                            container.Offset = ConvertTo.Vector2(initialPosition - initialAnchor);
                        }

                        if (positionX.IsAnimated)
                        {
                            Animate.Scalar(context, positionX, container, targetPropertyName: "Offset.X");
                        }

                        if (positionY.IsAnimated)
                        {
                            Animate.Scalar(context, positionY, container, targetPropertyName: "Offset.Y");
                        }
                    }
                    else
                    {
                        // TODO - when we support spatial Bezier CubicBezierFunction3, we can enable this. For now this
                        //        may result in a CubicBezierFunction2 being applied to the Vector3 Offset property.
                        //ApplyVector3KeyFrameAnimation(context, (AnimatableVector3)position, container, "Offset");
                        offsetExpression = context.ObjectFactory.CreateExpressionAnimation(container.IsShape
                            ? (Expr)Expr.Vector2(
                                ExpressionFactory.MyPosition.X - initialAnchor.X,
                                ExpressionFactory.MyPosition.Y - initialAnchor.Y)
                            : (Expr)Expr.Vector3(
                                ExpressionFactory.MyPosition.X - initialAnchor.X,
                                ExpressionFactory.MyPosition.Y - initialAnchor.Y,
                                0));

                        positionIsAnimated = true;
                    }
                }
                else
                {
                    // Non-zero non-animated anchor. Subtract the anchor.
                    offsetExpression = context.ObjectFactory.CreateExpressionAnimation(container.IsShape
                        ? (Expr)Expr.Vector2(
                            ExpressionFactory.MyPosition.X - initialAnchor.X,
                            ExpressionFactory.MyPosition.Y - initialAnchor.Y)
                        : (Expr)Expr.Vector3(
                            ExpressionFactory.MyPosition.X - initialAnchor.X,
                            ExpressionFactory.MyPosition.Y - initialAnchor.Y,
                            0));
                }
            }
            else if (anchorIsAnimated)
            {
                // Only anchor is animated.
                offsetExpression = context.ObjectFactory.CreateExpressionAnimation(container.IsShape
                    ? (Expr)Expr.Vector2(
                        initialPosition.X - ExpressionFactory.MyAnchor.X,
                        initialPosition.Y - ExpressionFactory.MyAnchor.Y)
                    : (Expr)Expr.Vector3(
                        initialPosition.X - ExpressionFactory.MyAnchor.X,
                        initialPosition.Y - ExpressionFactory.MyAnchor.Y,
                        0));
            }

            if (!positionIsAnimated && !anchorIsAnimated)
            {
                // Position and Anchor are static. No expression needed.
                container.Offset = ConvertTo.Vector2(initialPosition - initialAnchor);
            }

            // Position is a Lottie-only concept. It offsets the object relative to the Anchor.
            if (positionIsAnimated)
            {
                if (!anchorIsAnimated && xyzPosition is null)
                {
                    // The anchor isn't animated and the position is an animated Vector3. This is a very
                    // common case, and can be simplified to an Offset animation by subtracting the Anchor from the Position.
                    offsetExpression = null;
                    var anchoredPosition = PositionAndAnchorToOffset(context, position3, anchor.InitialValue);
                    if (container.IsShape)
                    {
                        Animate.Vector2(context, anchoredPosition, container, "Offset");
                    }
                    else
                    {
                        Animate.Vector3(context, anchoredPosition, container, "Offset");
                    }
                }
                else
                {
                    // Anchor and Position are both animated.
                    container.Properties.InsertVector2("Position", initialPosition);

                    if (xyzPosition is not null)
                    {
                        if (positionX.IsAnimated)
                        {
                            Animate.Scalar(context, positionX, container.Properties, targetPropertyName: "Position.X");
                        }

                        if (positionY.IsAnimated)
                        {
                            Animate.Scalar(context, positionY, container.Properties, targetPropertyName: "Position.Y");
                        }
                    }
                    else
                    {
                        Animate.Vector2(context, position3, container.Properties, "Position");
                    }
                }
            }

            if (offsetExpression is not null)
            {
                offsetExpression.SetReferenceParameter("my", container);
                Animate.WithExpression(container, offsetExpression, nameof(container.Offset));
            }
        }

        static TrimmedAnimatable<Vector3> PositionAndAnchorToOffset(LayerContext context, in TrimmedAnimatable<Vector3> animation, Vector3 anchor)
        {
            var keyframes = new KeyFrame<Vector3>[animation.KeyFrames.Count];

            for (var i = 0; i < animation.KeyFrames.Count; i++)
            {
                var kf = animation.KeyFrames[i];
                keyframes[i] = kf.CloneWithNewValue(kf.Value - anchor);
            }

            return new TrimmedAnimatable<Vector3>(context, keyframes[0].Value, keyframes);
        }
    }
}
