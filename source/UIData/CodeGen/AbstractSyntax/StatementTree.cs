// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax
{
    /// <summary>
    /// Orders the execution of <see cref="Statement"/>s and provides a scope for local
    /// variables.
    /// </summary>
    class StatementTree
    {
        public IReadOnlyList<StatementTree> Children { get; }
    }
}
