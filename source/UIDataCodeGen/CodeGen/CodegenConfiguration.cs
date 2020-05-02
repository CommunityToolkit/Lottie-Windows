// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Specifies the configuration of a code generator.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class CodegenConfiguration
    {
        /// <summary>
        /// The name for the resulting IAnimatedVisualSource implementation.
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// The namespace for the generated code. If not set, defaults to "AnimatedVisuals".
        /// </summary>
        public string Namespace { get; set; } = "AnimatedVisuals";

        /// <summary>
        /// The width of the animated visual.
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// The height of the animated visual.
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// The duration of the animated visual.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Determines whether the code generator should disable optimizations. Setting
        /// it to <c>true</c> may make the generated code easier to modify, although
        /// less efficient.
        /// </summary>
        public bool DisableOptimization { get; set; }

        /// <summary>
        /// When <c>true</c>, the generated IAnimatedVisualSource implementation will
        /// be a subclass of DependencyObject, and any theme properties will be
        /// implemented as DependencyPropertys.
        /// </summary>
        public bool GenerateDependencyObject { get; set; }

        /// <summary>
        /// Defines the interface used in the generated code. If not set it defaults to
        /// "Microsoft.UI.Xaml.Controls.IAnimatedVisual" which will cause the generation
        /// of code for IAnimatedVisualSource and IAnimatedVisual.
        /// </summary>
        public string InterfaceType { get; set; } = "Microsoft.UI.Xaml.Controls.IAnimatedVisual";

        /// <summary>
        /// The object graphs for which source will be generated.
        /// </summary>
        public IReadOnlyList<(CompositionObject graphRoot, uint requiredUapVersion)> ObjectGraphs { get; set; }

        /// <summary>
        /// Information about the source.
        /// </summary>
        public IReadOnlyDictionary<Guid, object> SourceMetadata { get; set; }

        /// <summary>
        /// Information about the tool that initiated the code generation.
        /// This information will be included in comments in the generated source.
        /// </summary>
        public IReadOnlyList<string> ToolInfo { get; set; }

        /// <summary>
        /// When <c>true</c> the generated class is made public.
        /// </summary>
        public bool Public { get; set; }
    }
}
