// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    static class Properties
    {
        static IReadOnlyDictionary<string, PropertyId>? s_propertyIdFromNameMap;

        internal static PropertyId PropertyIdFromName(string value)
        {
            if (s_propertyIdFromNameMap is null)
            {
                s_propertyIdFromNameMap =
                    Enum.GetValues(typeof(PropertyId)).Cast<PropertyId>()
                        .Where(p => p != PropertyId.None)
                        .ToDictionary(p => Enum.GetName(typeof(PropertyId), p)!);
            }

            return s_propertyIdFromNameMap.TryGetValue(value, out var result)
                ? result
                : PropertyId.None;
        }

        internal static PropertyId GetNonDefaultCompositionObjectProperties(CompositionObject obj)
        {
            var result = PropertyId.None;

            foreach (var animator in obj.Animators)
            {
                var animatedPropertyName = animator.AnimatedProperty;

                // The property name may contain subchannels. Trim those off.
                var dotIndex = animatedPropertyName.IndexOf('.');
                if (dotIndex > 0)
                {
                    animatedPropertyName = animatedPropertyName.Substring(0, dotIndex);
                }

                result |= PropertyIdFromName(animatedPropertyName);
            }

            return result;
        }

        internal static PropertyId GetNonDefaultContainerVisualProperties(ContainerVisual obj)
        {
            var result = PropertyId.None;
            if (obj.Children.Count != 0)
            {
                result |= PropertyId.Children;
            }

            return result | GetNonDefaultVisualProperties(obj);
        }

        internal static PropertyId GetNonDefaultGeometryProperties(CompositionGeometry? obj)
        {
            var result = PropertyId.None;

            if (obj is null)
            {
                return result;
            }

            if (obj.TrimStart.HasValue)
            {
                result |= PropertyId.TrimStart;
            }

            if (obj.TrimEnd.HasValue)
            {
                result |= PropertyId.TrimEnd;
            }

            if (obj.TrimOffset.HasValue)
            {
                result |= PropertyId.TrimOffset;
            }

            return result | GetNonDefaultCompositionObjectProperties(obj);
        }

        internal static PropertyId GetNonDefaultShapeProperties(CompositionShape obj)
        {
            var result = PropertyId.None;
            if (obj.CenterPoint.HasValue)
            {
                result |= PropertyId.CenterPoint;
            }

            if (obj.Comment != null)
            {
                result |= PropertyId.Comment;
            }

            if (obj.Offset.HasValue)
            {
                result |= PropertyId.Offset;
            }

            if (obj.Properties.Names.Count != 0)
            {
                result |= PropertyId.Properties;
            }

            if (obj.RotationAngleInDegrees.HasValue)
            {
                result |= PropertyId.RotationAngleInDegrees;
            }

            if (obj.Scale.HasValue)
            {
                result |= PropertyId.Scale;
            }

            if (obj.TransformMatrix.HasValue)
            {
                result |= PropertyId.TransformMatrix;
            }

            return result | GetNonDefaultCompositionObjectProperties(obj);
        }

        internal static PropertyId GetNonDefaultSpriteShapeProperties(CompositionSpriteShape obj)
        {
            var result = PropertyId.None;

            if (obj.StrokeDashCap.HasValue)
            {
                result |= PropertyId.StrokeDashCap;
            }

            if (obj.StrokeEndCap.HasValue)
            {
                result |= PropertyId.StrokeEndCap;
            }

            if (obj.StrokeLineJoin.HasValue)
            {
                result |= PropertyId.StrokeLineJoin;
            }

            if (obj.StrokeMiterLimit.HasValue)
            {
                result |= PropertyId.StrokeMiterLimit;
            }

            if (obj.StrokeStartCap.HasValue)
            {
                result |= PropertyId.StrokeStartCap;
            }

            return result | GetNonDefaultShapeProperties(obj);
        }

        internal static PropertyId GetNonDefaultVisualProperties(Visual obj)
        {
            var result = PropertyId.None;
            if (obj.BorderMode.HasValue)
            {
                result |= PropertyId.BorderMode;
            }

            if (obj.CenterPoint.HasValue)
            {
                result |= PropertyId.CenterPoint;
            }

            if (obj.Clip != null)
            {
                result |= PropertyId.Clip;
            }

            if (obj.Comment != null)
            {
                result |= PropertyId.Comment;
            }

            if (obj.IsVisible != null)
            {
                result |= PropertyId.IsVisible;
            }

            if (obj.Offset.HasValue)
            {
                result |= PropertyId.Offset;
            }

            if (obj.Opacity.HasValue)
            {
                result |= PropertyId.Opacity;
            }

            if (obj.Properties.Names.Count != 0)
            {
                result |= PropertyId.Properties;
            }

            if (obj.RotationAngleInDegrees.HasValue)
            {
                result |= PropertyId.RotationAngleInDegrees;
            }

            if (obj.RotationAxis.HasValue)
            {
                result |= PropertyId.RotationAxis;
            }

            if (obj.Scale.HasValue)
            {
                result |= PropertyId.Scale;
            }

            if (obj.Size.HasValue)
            {
                result |= PropertyId.Size;
            }

            if (obj.TransformMatrix.HasValue)
            {
                result |= PropertyId.TransformMatrix;
            }

            return result | GetNonDefaultCompositionObjectProperties(obj);
        }
    }
}