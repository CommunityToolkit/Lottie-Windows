// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// The name of a type.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class TypeName : IEquatable<TypeName>
    {
        internal TypeName(string namespaceName, string unqualifiedName)
        {
            if (namespaceName.Contains(':'))
            {
                throw new ArgumentException();
            }

            NormalizedNamespace = namespaceName;
            UnqualifiedName = unqualifiedName;
        }

        internal TypeName(string qualifiedName)
            => (NormalizedNamespace, UnqualifiedName) = ParseQualifiedName(qualifiedName);

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
            => string.IsNullOrWhiteSpace(NormalizedNamespace)
                ? UnqualifiedName
                : stringifier.Namespace($"{NormalizedNamespace}.{UnqualifiedName}");

        public string GetNamespace(Stringifier stringifier)
            => stringifier.Namespace(NormalizedNamespace);

        /// <summary>
        /// A non-language-specific name that can be used for display
        /// and comparison.
        /// </summary>
        public string NormalizedQualifiedName
            => string.IsNullOrWhiteSpace(NormalizedNamespace)
                ? UnqualifiedName
                : $"{NormalizedNamespace}.{UnqualifiedName}";

        /// <summary>
        /// A non-language-specific namespace name that can be used for display
        /// and comparison.
        /// </summary>
        public string NormalizedNamespace { get; }

        public override bool Equals(object? obj)
            => obj is TypeName other && Equals(other);

        public override int GetHashCode() => UnqualifiedName.GetHashCode();

        public override string ToString() => NormalizedQualifiedName;

        public bool Equals([AllowNull] TypeName other)
            => !(other is null) &&
                other.NormalizedNamespace == NormalizedNamespace &&
                other.UnqualifiedName == UnqualifiedName;
    }
}