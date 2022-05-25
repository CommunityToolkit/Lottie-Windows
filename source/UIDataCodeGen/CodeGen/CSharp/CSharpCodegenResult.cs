// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen.CSharp
{
    /// <summary>
    /// The result produced by the <see cref="CSharpCodegenResult"/>.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class CSharpCodegenResult
    {
        internal CSharpCodegenResult(string csText, IReadOnlyList<Uri> assets)
        {
            CsText = csText;
            Assets = assets;
        }

        /// <summary>
        /// The text of the .cs file.
        /// </summary>
        public string CsText { get; internal set; }

        /// <summary>
        /// The assets that the generated code depends on.
        /// </summary>
        public IReadOnlyList<Uri> Assets { get; internal set; }
    }
}
