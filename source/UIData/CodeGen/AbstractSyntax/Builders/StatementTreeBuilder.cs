// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax.Builders
{
    sealed class StatementTreeBuilder
    {
        readonly List<StatementTree> _children = new List<StatementTree>();
        readonly StatementTreeBuilder _parent;
        StatementTreeBuilder _currentScope;

        StatementTreeBuilder(StatementTreeBuilder parent)
        {
            _parent = parent;
            _currentScope = this;
        }

        internal StatementTreeBuilder()
            : this(null)
        {
        }

        public void AddStatement(Statement statement)
        {
            _currentScope._children.Add(statement);
        }

        public void OpenScope()
        {
            _currentScope = new StatementTreeBuilder();
        }

        public void CloseScope()
        {
            if (_currentScope._parent == null)
            {
                throw new InvalidOperationException();
            }

            var childScopeTree = _currentScope.ToStatementTree();
            _currentScope = _currentScope._parent;
            _currentScope._children.Add(childScopeTree);
        }

        public StatementTree ToStatementTree()
        {
            return new StatementTree(_children);
        }
    }
}
