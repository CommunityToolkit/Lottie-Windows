﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Issues.
    /// </summary>
    sealed class TranslationIssues
    {
        readonly HashSet<(string Code, string Description)> _issues = new HashSet<(string Code, string Description)>();
        readonly bool _throwOnIssue;

        internal TranslationIssues(bool throwOnIssue)
        {
            _throwOnIssue = throwOnIssue;
        }

        internal (string Code, string Description)[] GetIssues() => _issues.ToArray();

        internal void AnimatedRectangleWithTrimPathIsNotSupported() => Report("LT0001", "Rectangle with animated size and TrimPath is not supported.");

        internal void AnimatedTrimOffsetWithStaticTrimOffsetIsNotSupported() => Report("LT0002", "Animated trim offset with static trim offset is not supported.");

        // LT0003 has been deprecated.
        // Was: Multiplication of two or more animated values is not supported.

        internal void BlendModeNotNormal(string layer, string blendMode) => Report("LT0004", $"{layer} has {blendMode} as blend mode. Only Normal is supported.");

        internal void CombiningAnimatedShapesIsNotSupported() => Report("LT0005", "Combining animated shapes is not supported.");

        internal void GradientFillIsNotSupported(string linearOrRadial, string combination) => Report("LT0006", $"{linearOrRadial} gradient fill with {combination} is not supported.");

        // LT0007 has been deprecated.
        // Was: {linearOrRadial} gradient stroke with {combination} is not supported.

        // LT0009 has been deprecated.
        // Was: Image layers are not supported.

        internal void MergingALargeNumberOfShapesIsNotSupported() => Report("LT0010", "Merging a large number of shape is not supported.");

        internal void MultipleAnimatedRoundCornersIsNotSupported() => Report("LT0011", "Multiple animated round corners is not supported.");

        internal void MultipleFillsIsNotSupported() => Report("LT0012", "Multiple fills is not supported.");

        internal void MultipleStrokesIsNotSupported() => Report("LT0013", "Multiple strokes is not supported.");

        internal void MultipleTrimPathsIsNotSupported() => Report("LT0014", "Multiple trim paths is not supported.");

        // LT0015 has been deprecated.
        // Was: Opacity and color animated at the same time is not supported.

        internal void PathWithRoundCornersIsNotSupported() => Report("LT0016", "Path with round corners is not supported.");

        internal void PolystarIsNotSupported() => Report("LT0017", "Polystar is not supported.");

        internal void RepeaterIsNotSupported() => Report("LT0018", "Repeater is not supported.");

        internal void TextLayerIsNotSupported() => Report("LT0019", "Text layer is not supported.");

        internal void ThreeDIsNotSupported() => Report("LT0020", "3D composition is not supported.");

        internal void ThreeDLayerIsNotSupported() => Report("LT0021", "3D layer is not supported.");

        internal void TimeStretchIsNotSupported() => Report("LT0022", "Time stretch is not supported.");

        internal void MaskWithInvertIsNotSupported() => Report("LT0023", "Mask with invert is not supported.");

        internal void MaskWithUnsupportedMode(string mode) => Report("LT0024", $"Mask mode: {mode} is not supported.");

        internal void MaskWithAlphaIsNotSupported() => Report("LT0025", "Mask with alpha value other than 1 is not supported.");

        // LT0026 has been deprecated.
        // Was: Mask with multiple shapes is not supported.

        internal void CombiningMultipleShapesIsNotSupported() => Report("LT0027", "Combining multiple shapes is not supported.");

        internal void ReferencedAssetDoesNotExist(string RefId) => Report("LT0028", $"Referenced asset {RefId} does not exist.");

        internal void InvalidAssetReferenceFromLayer(string layerType, string assetRefId, string assetType, string expectedAssetType) => Report("LT0029", $"{layerType} referenced asset {assetRefId} of type {assetType} which is invalid. Expected an asset of type {expectedAssetType}.");

        internal void ImageFileRequired(string filePath) => Report("LT0030", $"Image file required at {filePath}.");

        // LT0031 has been deprecated.
        // Was: Mattes are not supported.

        // LT0032 has been deprecated.
        // Was: A multiple shape mask is only supported if the shapes all have the same mode.

        // LT0033 has been deprecated.
        // Was: Masks are not supported.

        internal void UapVersionNotSupported(string versionDependentFeature, string optimalUapVersion) => Report("LT0034", $"{versionDependentFeature} requires a UAP version of at least {optimalUapVersion}.");

        internal void ThemePropertyValuesAreInconsistent(string themePropertyName, string chosenValue, string requestedValue) => Report("LT0035", $"Theme property \"{themePropertyName}\" has more than one value. Using {chosenValue} in place of {requestedValue}.");

        internal void CombiningMultipleAnimatedPathsIsNotSupported() => Report("LT0036", "Combining multiple animated paths is not supported.");

        internal void ConflictingRoundnessAndRadiusIsNotSupported() => Report("LT0037", "Rectangle roundness with round corners is not supported.");

        internal void LayerEffectNotSupportedOnLayer(string type, string layerType) => Report("LT0038", $"Effects of type {type} are not supported on {layerType} layers.");

        internal void RepeatedLayerEffect(string type) => Report("LT0039", $"Layer effect of type {type} is specified more than once.");

        internal void ShadowOnlyShadowEffect() => Report("LT0040", "Shadow-only drop shadow are not supported.");

        internal void AnimatedLayerEffectParameters(string layerEffectType) => Report("LT0041", $"Animated parameters on {layerEffectType} effect are not supported.");

        internal void UnsupportedLayerEffectParameter(string layerEffectType, string parameterName, string value) => Report("LT0042", $"Layer effects of type {layerEffectType} do not support {parameterName} values of {value}.");

        void Report(string code, string description)
        {
            _issues.Add((code, description));

            if (_throwOnIssue)
            {
                throw new NotSupportedException($"{code}: {description}");
            }
        }
    }
}
