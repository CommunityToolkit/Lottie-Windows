// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax.Builders
{
    sealed class MethodBuilder
    {
        readonly List<TypeReference> _parameterTypes = new List<TypeReference>();
        readonly StatementTreeBuilder _statements = new StatementTreeBuilder();
        string _name;
        string _comment;
        TypeReference _returnType;

        internal MethodBuilder()
        {
        }

        public void SetReturnType(TypeReference returnType)
        {
            _returnType = returnType;
        }

        public void AddParameter(TypeReference parameterType)
        {
            _parameterTypes.Add(parameterType);
        }

        public void SetMethodName(string name)
        {
            _name = name;
        }

        public void SetMethodComment(string comment)
        {
            _comment = comment;
        }

        public void AddStatement(Statement statement)
        {
            _statements.AddStatement(statement);
        }

        public void OpenScope() => _statements.OpenScope();

        public void CloseScope() => _statements.CloseScope();

        public Method ToMethod()
        {
            return new Method(_name, _parameterTypes, _returnType, _statements.ToStatementTree(), _comment);
        }
    }
}
