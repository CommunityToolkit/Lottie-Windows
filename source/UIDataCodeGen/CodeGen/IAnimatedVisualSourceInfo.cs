// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Numerics;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Holds information about code being generated for an IAnimatedVisualSource.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    interface IAnimatedVisualSourceInfo
    {
        /// <summary>
        /// Gets the name of the IAnimatedVisualSource class that will be exposed to users.
        /// </summary>
        string ClassName { get; }

        /// <summary>
        /// Gets the namespace for the generated code.
        /// </summary>
        string Namespace { get; }

        /// <summary>
        /// 0 or more additional interfaces that the class will claim to implement.
        /// </summary>
        IReadOnlyList<TypeName> AdditionalInterfaces { get; }

        /// <summary>
        /// Gets the name of the field in the instantiator class that holds the reusable ExpressionAnimation.
        /// </summary>
        string ReusableExpressionAnimationFieldName { get; }

        /// <summary>
        /// Gets the constant holding the duration of the composition in ticks.
        /// </summary>
        string DurationTicksFieldName { get; }

        /// <summary>
        /// <c>true</c> if the IAnimatedVisualSource should be a DependencyObject.
        /// </summary>
        bool GenerateDependencyObject { get; }

        /// <summary>
        /// Gets the name of the field holding the theme properties CompositionPropertySet.
        /// </summary>
        string ThemePropertiesFieldName { get; }

        /// <summary>
        /// Gets a value indicating whether or not the animated visual is themed.
        /// </summary>
        bool IsThemed { get; }

        /// <summary>
        /// Gets the declared size of the composition.
        /// </summary>
        Vector2 CompositionDeclaredSize { get; }

        /// <summary>
        /// <c>true</c> if the IAnimatedVisualSource should be public.
        /// </summary>
        bool Public { get; }

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
        /// Gets the <see cref="IAnimatedVisualInfo"/> objects that describe each IAnimatedVisual
        /// class that can be returned from the generated code.
        /// </summary>
        IReadOnlyList<IAnimatedVisualInfo> AnimatedVisualInfos { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on a composite effect.
        /// </summary>
        bool UsesCompositeEffect { get; }

        /// <summary>
        /// Gets a value indicating whether the composition depends on a Gaussian blur effect.
        /// </summary>
        bool UsesGaussianBlurEffect { get; }

        /// <summary>
        /// Gets the XAML LoadedImageSurface nodes of the composition.
        /// </summary>
        IReadOnlyList<LoadedImageSurfaceInfo> LoadedImageSurfaces { get; }

        /// <summary>
        /// Internal constants. These are constants that are expected to be useful
        /// in the assembly in which the codegen output is used, but are not
        /// part of a public interface.
        /// </summary>
        IReadOnlyList<NamedConstant> InternalConstants { get; }

        /// <summary>
        /// The markers. These are names for progress values.
        /// </summary>
        IReadOnlyList<MarkerInfo> Markers { get; }

        /// <summary>
        /// Accesses metadata associated with the source of the composition. This may contain
        /// information such as the frame rate and theme bindings from the source. The contents of
        /// this data is source specific.
        /// </summary>
        SourceMetadata SourceMetadata { get; }

        /// <summary>
        /// The version of WinUI to generate code for.
        /// </summary>
        Version WinUIVersion { get; }
    }
}
