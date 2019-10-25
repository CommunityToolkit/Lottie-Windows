// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Numerics;
using static Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.InstantiatorGeneratorBase;

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
        public string ClassName { get; }

        /// <summary>
        /// Gets the name of the field in the instantiator class that holds the reusable ExpressionAnimation.
        /// </summary>
        public string ReusableExpressionAnimationFieldName { get; }

        /// <summary>
        /// Gets the constant holding the duration of the composition in ticks.
        /// </summary>
        public string DurationTicksFieldName { get; }

        /// <summary>
        /// Gets the declared size of the composition.
        /// </summary>
        public Vector2 CompositionDeclaredSize { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on the Microsoft.Graphics.Canvas namespace.
        /// </summary>
        public bool UsesCanvas { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on the Microsoft.Graphics.Canvas.Effects namespace.
        /// </summary>
        public bool UsesCanvasEffects { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on the Microsoft.Graphics.Canvas.Geometry namespace.
        /// </summary>
        public bool UsesCanvasGeometry { get; }

        /// <summary>
        /// Gets a value indicating whether the composition uses the Windows.UI.Xaml.Media namespace.
        /// </summary>
        public bool UsesNamespaceWindowsUIXamlMedia { get; }

        /// <summary>
        /// Gets a value indicating whether the composition uses streams.
        /// </summary>
        public bool UsesStreams { get; }

        /// <summary>
        /// Gets a value indicating whether the composition has LoadedImageSurface.
        /// </summary>
        public bool HasLoadedImageSurface { get; }

        /// <summary>
        /// Gets the <see cref="IAnimatedVisualInfo"/> objects that describe each IAnimatedVisual
        /// class that can be returned from the generated code.
        /// </summary>
        internal IReadOnlyList<IAnimatedVisualInfo> AnimatedVisualInfos { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on a composite effect.
        /// </summary>
        public bool UsesCompositeEffect { get; }

        /// <summary>
        /// Gets the <see cref="LoadedImageSurface"/> nodes of the composition.
        /// </summary>
        internal IReadOnlyList<LoadedImageSurfaceInfo> LoadedImageSurfaceNodes { get; }
    }
}
