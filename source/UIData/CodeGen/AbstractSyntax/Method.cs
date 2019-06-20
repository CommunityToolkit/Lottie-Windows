// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax
{
    /// <summary>
    /// Defines a method.
    /// </summary>
    sealed class Method
    {
        internal Method(IEnumerable<TypeReference> parameterTypes, TypeReference returnType, StatementTree statements)
        {
            ReturnType = returnType;
            ParameterTypes = parameterTypes;
            Statements = statements;
        }

        /// <summary>
        /// The return type of the method, or null if the method does not return a value.
        /// </summary>
        TypeReference ReturnType { get; }

        IEnumerable<TypeReference> ParameterTypes { get; }

        StatementTree Statements { get; }
    }
}
