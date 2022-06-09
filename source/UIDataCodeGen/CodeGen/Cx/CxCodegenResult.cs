// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen.Cx
{
    /// <summary>
    /// The result produced by the <see cref="CxInstantiatorGenerator"/>.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class CxCodegenResult
    {
        internal CxCodegenResult(
            string cppFilename,
            string cppText,
            string hFilename,
            string hText,
            IReadOnlyList<Uri> assets)
        {
            CppFilename = cppFilename;
            CppText = cppText;
            HFilename = hFilename;
            HText = hText;
            Assets = assets;
        }

        /// <summary>
        /// The name of the .cpp file.
        /// </summary>
        public string CppFilename { get; }

        /// <summary>
        /// The text of the .cpp file.
        /// </summary>
        public string CppText { get; }

        /// <summary>
        /// The name of the .h file.
        /// </summary>
        public string HFilename { get; }

        /// <summary>
        /// The text of the .h file.
        /// </summary>
        public string HText { get; }

        /// <summary>
        /// The assets that the generated code depends on.
        /// </summary>
        public IReadOnlyList<Uri> Assets { get; }
    }
}
