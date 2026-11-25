// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// A field of an AnimatedVisual class.
    /// </summary>
    sealed class AnimatedVisualField
    {
        internal AnimatedVisualField(
            string name,
            string declaration,
            string? mutableDeclaration = null,
            bool isAssignedByConstructor = false,
            bool isCommon = false)
        {
            Name = name;
            Declaration = declaration;
            MutableDeclaration = mutableDeclaration ?? declaration;
            IsAssignedByConstructor = isAssignedByConstructor;
            IsCommon = isCommon;
        }

        internal string Name { get; }

        /// <summary>
        /// Gets the declaration of the field, as written in the class.
        /// </summary>
        internal string Declaration { get; }

        /// <summary>
        /// Gets the declaration of the field without the modifier that makes it read-only. This
        /// is the same as <see cref="Declaration"/> if the field is not read-only.
        /// </summary>
        internal string MutableDeclaration { get; }

        /// <summary>
        /// Gets a value indicating whether the field is read-only.
        /// </summary>
        internal bool IsReadonly => Declaration != MutableDeclaration;

        /// <summary>
        /// Gets a value indicating whether the field is read-only only because a constructor of the class
        /// assigns it, so that it can be declared without the read-only modifier in a base class.
        /// </summary>
        internal bool IsAssignedByConstructor { get; }

        /// <summary>
        /// Gets a value indicating whether the field is one that every class has, and that any of its
        /// methods can use without recording it, like the Compositor. Common fields move to the base
        /// class together with the methods that are shared.
        /// </summary>
        internal bool IsCommon { get; }
    }
}
