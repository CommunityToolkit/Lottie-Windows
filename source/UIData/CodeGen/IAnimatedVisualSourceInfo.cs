﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Holds information about code being generated for an IAnimatedVisualSource.
    /// </summary>
#if PUBLIC_UIData
    public
#endif
    interface IAnimatedVisualSourceInfo
    {
        /// <summary>
        /// Gets the name of the IAnimatedVisualSource class that will be exposed to users.
        /// </summary>
        string ClassName { get; }

        /// <summary>
        /// Gets the name of the field in the instantiator class that holds the reusable ExpressionAnimation.
        /// </summary>
        string ReusableExpressionAnimationFieldName { get; }

        /// <summary>
        /// Gets the constant holding the duration of the composition in ticks.
        /// </summary>
        string DurationTicksFieldName { get; }

        /// <summary>
        /// Gets the declared size of the composition.
        /// </summary>
        Vector2 CompositionDeclaredSize { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on the Microsoft.Graphics.Canvas namespace.
        /// </summary>
        bool UsesCanvas { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on the Microsoft.Graphics.Canvas.Effects namespace.
        /// </summary>
        bool UsesCanvasEffects { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on the Microsoft.Graphics.Canvas.Geometry namespace.
        /// </summary>
        bool UsesCanvasGeometry { get; }

        /// <summary>
        /// Gets a value indicating whether the composition uses the Windows.UI.Xaml.Media namespace.
        /// </summary>
        bool UsesNamespaceWindowsUIXamlMedia { get; }

        /// <summary>
        /// Gets a value indicating whether the composition uses streams.
        /// </summary>
        bool UsesStreams { get; }

        /// <summary>
        /// Gets a value indicating whether the composition has LoadedImageSurface.
        /// </summary>
        bool HasLoadedImageSurface { get; }

        /// <summary>
        /// Gets the <see cref="IAnimatedVisualInfo"/> objects that describe each IAnimatedVisual
        /// class that can be returned from the generated code.
        /// </summary>
        IReadOnlyList<IAnimatedVisualInfo> AnimatedVisualInfos { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on a composite effect.
        /// </summary>
        bool UsesCompositeEffect { get; }

        /// <summary>
        /// Gets the XAML LoadedImageSurface nodes of the composition.
        /// </summary>
        IReadOnlyList<LoadedImageSurfaceInfo> LoadedImageSurfaceNodes { get; }
    }
}
