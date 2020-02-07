// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// The name of a type.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class TypeName
    {
        readonly string _namespaceName;

        internal TypeName(string namespaceName, string unqualifiedName)
        {
            if (namespaceName.Contains(':'))
            {
                throw new ArgumentException();
            }

            _namespaceName = namespaceName;
            UnqualifiedName = unqualifiedName;
        }

        internal TypeName(string qualifiedName)
            => (_namespaceName, UnqualifiedName) = ParseQualifiedName(qualifiedName);

        static (string namespaceName, string unqualifiedName) ParseQualifiedName(string qualifiedName)
        {
            var normalizedInterfaceName = qualifiedName.Replace("::", ".");
            var endOfNamespaceIndex = normalizedInterfaceName.LastIndexOf('.');
            return endOfNamespaceIndex == -1
                ? (string.Empty, qualifiedName)
                : (
                    normalizedInterfaceName.Substring(0, endOfNamespaceIndex),
                    normalizedInterfaceName.Substring(endOfNamespaceIndex + 1));
        }

        public string UnqualifiedName { get; }

        public string GetQualifiedName(Stringifier stringifier)
            => string.IsNullOrWhiteSpace(_namespaceName)
                ? UnqualifiedName
                : stringifier.Namespace($"{_namespaceName}.{UnqualifiedName}");

        public string GetNamespace(Stringifier stringifier)
            => stringifier.Namespace(_namespaceName);

        /// <summary>
        /// A non-language-specific name that can be used for display
        /// and comparison.
        /// </summary>
        public string NormalizedQualifiedName
            => string.IsNullOrWhiteSpace(_namespaceName)
                ? UnqualifiedName
                : $"{_namespaceName}.{UnqualifiedName}";
    }
}