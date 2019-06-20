// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax
{
    /// <summary>
    /// Defines a method.
    /// </summary>
    sealed class ImportedMethodCallTargetReference
    {
        internal ImportedMethodCallTargetReference(string fullyQualifiedName)
        {
            FullyQualifiedName = fullyQualifiedName;
        }

        public string FullyQualifiedName { get; }
    }
}
