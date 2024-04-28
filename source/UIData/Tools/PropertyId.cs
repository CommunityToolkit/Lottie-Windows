// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace CommunityToolkit.WinUI.Lottie.UIData.Tools
{
    /// <summary>
    /// Identifies a property of a CompositionObject.
    /// </summary>
    [Flags]
    enum PropertyId
    {
        None = 0,
        BorderMode = 1,
        CenterPoint = BorderMode << 1,
        Children = CenterPoint << 1,
        Clip = Children << 1,
        Color = Clip << 1,
        Comment = Color << 1,
        IsVisible = Comment << 1,
        Offset = IsVisible << 1,
        Opacity = Offset << 1,
        Path = Opacity << 1,
        Position = Path << 1,
        Progress = Position << 1,
        Properties = Progress << 1,
        RotationAngleInDegrees = Properties << 1,
        RotationAxis = RotationAngleInDegrees << 1,
        Scale = RotationAxis << 1,
        Size = Scale << 1,
        StrokeEndCap = Size << 1,
        StrokeDashCap = StrokeEndCap << 1,
        StrokeLineJoin = StrokeDashCap << 1,
        StrokeMiterLimit = StrokeLineJoin << 1,
        StrokeStartCap = StrokeMiterLimit << 1,
        TransformMatrix = StrokeStartCap << 1,
        TrimEnd = TransformMatrix << 1,
        TrimOffset = TrimEnd << 1,
        TrimStart = TrimOffset << 1,

        // Any new value should also be added to UIData/Tools/Properties.cs
        // This is needed to omit Enum.GetValues usage to be AOT compatible.
    }
}
