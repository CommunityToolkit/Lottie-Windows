// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax
{
    /// <summary>
    /// Defines a method.
    /// </summary>
    sealed class Method
    {
        internal Method(
            string name,
            IEnumerable<TypeReference> parameterTypes,
            TypeReference returnType,
            StatementTree statements,
            string comment)
        {
            Name = name;
            ReturnType = returnType;
            ParameterTypes = parameterTypes;
            Statements = statements;
            Comment = comment;
        }

        public string Name { get; }

        /// <summary>
        /// The return type of the method, or null if the method does not return a value.
        /// </summary>
        public TypeReference ReturnType { get; }

        public IEnumerable<TypeReference> ParameterTypes { get; }

        public StatementTree Statements { get; }

        public string Comment { get; }

        public override string ToString()
        {
            var parameterList = string.Join(", ", ParameterTypes.Select(p => p.ToString()));
            return $"{(ReturnType == null ? "void" : ReturnType.ToString())} {Name}({parameterList})\r\n{Statements}";
        }
    }
}
