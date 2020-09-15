﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Cx
{
    /// <summary>
    /// The result produced by the <see cref="CxInstantiatorGenerator"/>.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class CxCodegenResult
    {
        /// <summary>
        /// The name of the .cpp file.
        /// </summary>
        public string CppFilename { get; internal set; }

        /// <summary>
        /// The text of the .cpp file.
        /// </summary>
        public string CppText { get; internal set; }

        /// <summary>
        /// The name of the .h file.
        /// </summary>
        public string HFilename { get; internal set; }

        /// <summary>
        /// The text of the .h file.
        /// </summary>
        public string HText { get; internal set; }

        /// <summary>
        /// The assets that the generated code depends on.
        /// </summary>
        public IReadOnlyList<Uri> Assets { get; internal set; }
    }
}
