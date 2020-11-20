// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Text;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using static Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.Properties;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    /// <summary>
    /// Un-sets properties that have been initialized with their default value. Initializing
    /// a property with its default value is a redundant operation. This optimizer finds
    /// such cases and sets the property value to null.
    /// Also replaces scalar properties with TransformMatrix properties where
    /// possible.
    /// </summary>
    static class PropertyValueOptimizer
    {
        internal static Visual OptimizePropertyValues(Visual root)
        {
            var graph = ObjectGraph<Graph.Node>.FromCompositionObject(root, includeVertices: false);

            foreach (var (_, obj) in graph.CompositionObjectNodes)
            {
                switch (obj.Type)
                {
                    case CompositionObjectType.ContainerVisual:
                    case CompositionObjectType.LayerVisual:
                    case CompositionObjectType.SpriteVisual:
                    case CompositionObjectType.ShapeVisual:
                        OptimizeVisualProperties((Visual)obj);
                        break;

                    case CompositionObjectType.CompositionSpriteShape:
                        OptimizeSpriteShapeProperties((CompositionSpriteShape)obj);
                        break;

                    case CompositionObjectType.CompositionContainerShape:
                        OptimizeShapeProperties((CompositionShape)obj);
                        break;

                    case CompositionObjectType.StepEasingFunction:
                        OptimizeStepEasingFunctionProperties((StepEasingFunction)obj);
                        break;

                    case CompositionObjectType.CompositionLinearGradientBrush:
                    case CompositionObjectType.CompositionRadialGradientBrush:
                        OptimizeGradientBrush((CompositionGradientBrush)obj);
                        break;
                }
            }

            return root;
        }

        static void OptimizeGradientBrush(CompositionGradientBrush obj)
        {
            if (obj.AnchorPoint == Vector2.Zero)
            {
                obj.AnchorPoint = null;
            }

            if (obj.CenterPoint == Vector2.Zero)
            {
                obj.CenterPoint = null;
            }

            if (obj.ExtendMode == CompositionGradientExtendMode.Clamp)
            {
                obj.ExtendMode = null;
            }

            if (obj.InterpolationSpace == CompositionColorSpace.Rgb)
            {
                obj.InterpolationSpace = null;
            }

            if (obj.MappingMode == CompositionMappingMode.Relative)
            {
                obj.MappingMode = null;
            }

            if (obj.Offset == Vector2.Zero)
            {
                obj.Offset = null;
            }

            if (obj.RotationAngleInDegrees == 0)
            {
                obj.RotationAngleInDegrees = null;
            }

            if (obj.Scale == Vector2.One)
            {
                obj.Scale = null;
            }

            if (obj.TransformMatrix == Matrix3x2.Identity)
            {
                obj.TransformMatrix = null;
            }
        }

        static void OptimizeStepEasingFunctionProperties(StepEasingFunction obj)
        {
            if (obj.StepCount == 1)
            {
                obj.StepCount = null;
            }

            if (obj.IsInitialStepSingleFrame == false)
            {
                obj.IsInitialStepSingleFrame = null;
            }

            if (obj.InitialStep == 0)
            {
                obj.InitialStep = null;
            }

            if (obj.FinalStep == 1)
            {
                obj.FinalStep = null;
            }

            if (obj.IsFinalStepSingleFrame == false)
            {
                obj.IsFinalStepSingleFrame = null;
            }
        }

        // Remove the centerpoint property if it's redundant, and convert properties to TransformMatrix if possible.
        static void OptimizeShapeProperties(CompositionShape obj)
        {
            // Remove the centerpoint if it's not used by Scale or Rotation.
            var nonDefaultProperties = GetNonDefaultShapeProperties(obj);
            if (obj.CenterPoint.HasValue &&
                ((nonDefaultProperties & (PropertyId.RotationAngleInDegrees | PropertyId.Scale)) == PropertyId.None))
            {
                // Centerpoint is not needed if Scale or Rotation are not used.
                obj.CenterPoint = null;
            }

            // Convert the properties to a transform matrix. This can reduce the
            // number of calls needed to initialize the object, and makes finding
            // and removing redundant containers easier.
            if (obj.Animators.Count == 0)
            {
                // Get the values for the properties, and the defaults for the properties that are not set.
                var centerPoint = obj.CenterPoint ?? Vector2.Zero;
                var scale = obj.Scale ?? Vector2.One;
                var rotation = obj.RotationAngleInDegrees ?? 0;
                var offset = obj.Offset ?? Vector2.Zero;
                var transform = obj.TransformMatrix ?? Matrix3x2.Identity;

                // Clear out the properties.
                obj.CenterPoint = null;
                obj.Scale = null;
                obj.RotationAngleInDegrees = null;
                obj.Offset = null;
                obj.TransformMatrix = null;

                // Calculate the matrix that is equivalent to the properties.
                var combinedMatrix =
                    Matrix3x2.CreateScale(scale, centerPoint) *
                    Matrix3x2.CreateRotation(DegreesToRadians(rotation), centerPoint) *
                    Matrix3x2.CreateTranslation(offset) *
                    transform;

                // If the matrix actually does something, set it.
                if (!combinedMatrix.IsIdentity)
                {
                    if (combinedMatrix != transform)
                    {
                        var transformDescription = DescribeTransform(scale, rotation, offset);
                        AppendShortDescription(obj, transformDescription);
                        AppendLongDescription(obj, transformDescription);
                    }

                    obj.TransformMatrix = combinedMatrix;
                }
            }
        }

        static void OptimizeSpriteShapeProperties(CompositionSpriteShape sprite)
        {
            OptimizeShapeProperties(sprite);

            // Unset properties that are set to their default values.
            if (sprite.IsStrokeNonScaling == false)
            {
                sprite.IsStrokeNonScaling = null;
            }

            if (sprite.StrokeStartCap == CompositionStrokeCap.Flat)
            {
                sprite.StrokeStartCap = null;
            }

            if (sprite.StrokeDashCap == CompositionStrokeCap.Flat)
            {
                sprite.StrokeDashCap = null;
            }

            if (sprite.StrokeDashOffset == 0)
            {
                sprite.StrokeDashOffset = null;
            }

            if (sprite.StrokeEndCap == CompositionStrokeCap.Flat)
            {
                sprite.StrokeEndCap = null;
            }

            if (sprite.StrokeLineJoin == CompositionStrokeLineJoin.Miter)
            {
                sprite.StrokeLineJoin = null;
            }

            if (sprite.StrokeMiterLimit == 1)
            {
                sprite.StrokeMiterLimit = null;
            }

            if (sprite.StrokeThickness == 1)
            {
                sprite.StrokeThickness = null;
            }

            if (sprite.Geometry != null)
            {
                OptimizeGeometryProperties(sprite.Geometry);

                var nonDefaultProperties = GetNonDefaultSpriteShapeProperties(sprite);

                var nonDefaultGeometryProperties = GetNonDefaultGeometryProperties(sprite.Geometry);

                var isTrimmed = (nonDefaultGeometryProperties & (PropertyId.TrimEnd | PropertyId.TrimStart)) != PropertyId.None;

                if (sprite.Geometry.Type == CompositionObjectType.CompositionEllipseGeometry)
                {
                    // Remove the StrokeMiterLimit and StrokeLineJoin properties. These properties
                    // only apply to changes of direction in a path, and never to an ellipse.
                    if ((nonDefaultProperties & PropertyId.StrokeMiterLimit) != PropertyId.None)
                    {
                        sprite.StrokeMiterLimit = null;
                        sprite.StopAnimation(nameof(sprite.StrokeMiterLimit));
                    }

                    if ((nonDefaultProperties & PropertyId.StrokeLineJoin) != PropertyId.None)
                    {
                        sprite.StrokeLineJoin = null;
                        sprite.StopAnimation(nameof(sprite.StrokeLineJoin));
                    }
                }

                if (sprite.Geometry.Type == CompositionObjectType.CompositionRectangleGeometry ||
                    sprite.Geometry.Type == CompositionObjectType.CompositionRoundedRectangleGeometry ||
                    sprite.Geometry.Type == CompositionObjectType.CompositionEllipseGeometry)
                {
                    // TODO - this can also be enabled for path geometries that are closed paths.
                    // The geometry is closed. If it's not trimmed then the caps are irrelavent.
                    if (!isTrimmed)
                    {
                        if ((nonDefaultProperties & PropertyId.StrokeStartCap) != PropertyId.None)
                        {
                            sprite.StrokeStartCap = null;
                            sprite.StopAnimation(nameof(sprite.StrokeStartCap));
                        }

                        if ((nonDefaultProperties & PropertyId.StrokeEndCap) != PropertyId.None)
                        {
                            sprite.StrokeEndCap = null;
                            sprite.StopAnimation(nameof(sprite.StrokeEndCap));
                        }
                    }
                }
            }
        }

        // Remove redundant TrimEnd, TrimOffset, and TrimStart properties.
        static void OptimizeGeometryProperties(CompositionGeometry obj)
        {
            // Unset properties that are set to their default values.
            if (obj.TrimEnd == 1)
            {
                obj.TrimEnd = null;
            }

            if (obj.TrimOffset == 0)
            {
                obj.TrimOffset = null;
            }

            if (obj.TrimStart == 0)
            {
                obj.TrimStart = null;
            }
        }

        // Remove the IsVisible, CenterPoint, and RotationAxis properties if they're redundant,
        // and convert properties to TransformMatrix if possible.
        static void OptimizeVisualProperties(Visual obj)
        {
            // Unset properties that are set to their default values.
            if (obj.CenterPoint == Vector3.Zero)
            {
                obj.CenterPoint = null;
            }

            if (obj.IsVisible == true)
            {
                obj.IsVisible = null;
            }

            if (obj.Offset == Vector3.Zero)
            {
                obj.Offset = null;
            }

            if (obj.Opacity == 1)
            {
                obj.Opacity = null;
            }

            if (obj.RotationAngleInDegrees == 0)
            {
                obj.RotationAngleInDegrees = null;
            }

            if (obj.RotationAxis == Vector3.UnitZ)
            {
                obj.RotationAxis = null;
            }

            if (obj.Scale == Vector3.One)
            {
                obj.Scale = null;
            }

            if (obj.Size == Vector2.Zero)
            {
                obj.Size = null;
            }

            if (obj.TransformMatrix == Matrix4x4.Identity)
            {
                obj.TransformMatrix = null;
            }

            var nonDefaultProperties = GetNonDefaultVisualProperties(obj);

            if (obj.CenterPoint.HasValue &&
                ((nonDefaultProperties & (PropertyId.RotationAngleInDegrees | PropertyId.Scale)) == PropertyId.None))
            {
                // Centerpoint and RotationAxis is not needed if Scale or Rotation are not used.
                obj.CenterPoint = null;
                obj.RotationAxis = null;
            }

            // Convert the properties to a transform matrix. This can reduce the
            // number of calls needed to initialize the object, and makes finding
            // and removing redundant containers easier.

            // We currently only support rotation around the Z axis here. Check for that.
            var hasNonStandardRotation =
                obj.RotationAngleInDegrees.HasValue && obj.RotationAngleInDegrees.Value != 0 &&
                obj.RotationAxis.HasValue && obj.RotationAxis != Vector3.UnitZ;

            if (obj.Animators.Count == 0 && !hasNonStandardRotation)
            {
                // Get the values of the properties, and the defaults for properties that are not set.
                var centerPoint = obj.CenterPoint ?? Vector3.Zero;
                var scale = obj.Scale ?? Vector3.One;
                var rotation = obj.RotationAngleInDegrees ?? 0;
                var offset = obj.Offset ?? Vector3.Zero;
                var transform = obj.TransformMatrix ?? Matrix4x4.Identity;

                // Clear out the properties.
                obj.CenterPoint = null;
                obj.Scale = null;
                obj.RotationAngleInDegrees = null;
                obj.Offset = null;
                obj.TransformMatrix = null;

                // Calculate the matrix that is equivalent to the properties.
                var combinedMatrix =
                    Matrix4x4.CreateScale(scale, centerPoint) *
                    Matrix4x4.CreateRotationZ(DegreesToRadians(rotation), centerPoint) *
                    Matrix4x4.CreateTranslation(offset) *
                    transform;

                // If the matrix actually does something, set it.
                if (!combinedMatrix.IsIdentity)
                {
                    if (combinedMatrix != transform)
                    {
                        var transformDescription = DescribeTransform(scale, rotation, offset);
                        AppendShortDescription(obj, transformDescription);
                        AppendLongDescription(obj, transformDescription);
                    }

                    obj.TransformMatrix = combinedMatrix;
                }
            }
        }

        static string DescribeTransform(Vector2 scale, double rotationDegrees, Vector2 offset)
        {
            var sb = new StringBuilder();
            if (scale != Vector2.One)
            {
                sb.Append($"Scale:{scale.X},{scale.Y}");
            }

            if (rotationDegrees != 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"RotationDegrees:{rotationDegrees}");
            }

            if (offset != Vector2.Zero)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"Offset:{offset}");
            }

            return sb.ToString();
        }

        static string DescribeTransform(Vector3 scale, double rotationDegrees, Vector3 offset)
        {
            var sb = new StringBuilder();
            if (scale != Vector3.One)
            {
                sb.Append($"Scale({scale.X},{scale.Y},{scale.Z})");
            }

            if (rotationDegrees != 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"RotationDegrees({rotationDegrees})");
            }

            if (offset != Vector3.Zero)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"Offset({offset.X},{offset.Y},{offset.Z})");
            }

            return sb.ToString();
        }

        static void AppendShortDescription(IDescribable obj, string description)
        {
            obj.ShortDescription = $"{obj.ShortDescription} {description}";
        }

        static void AppendLongDescription(IDescribable obj, string description)
        {
            obj.LongDescription = $"{obj.LongDescription} {description}";
        }

        static float DegreesToRadians(float angle) => (float)(Math.PI * angle / 180.0);
    }
}
