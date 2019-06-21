// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax
{
    /// <summary>
    /// Orders the execution of <see cref="Statement"/>s and provides a scope for local
    /// variables.
    /// </summary>
    class StatementTree
    {
        static readonly StatementTree[] s_emptyList = new StatementTree[0];

        protected internal StatementTree()
            : this(s_emptyList)
        {
        }

        internal StatementTree(IEnumerable<StatementTree> children)
        {
            Children = children.ToArray();
        }

        public IReadOnlyList<StatementTree> Children { get; }

        public override string ToString()
        {
            return "{" + string.Join("\r\n", Children.Select(c => c.ToString() + ";")) + "}";
        }
    }
}
