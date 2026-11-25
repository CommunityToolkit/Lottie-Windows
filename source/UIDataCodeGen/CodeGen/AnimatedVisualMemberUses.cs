// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// The fields and methods of an AnimatedVisual class that some generated code uses, which the
    /// generator records when it writes their names.
    /// </summary>
    sealed class AnimatedVisualMemberUses
    {
        readonly HashSet<string> _fields = new HashSet<string>();
        readonly HashSet<string> _methods = new HashSet<string>();

        /// <summary>
        /// Gets the names of the fields that are used.
        /// </summary>
        internal IReadOnlyCollection<string> Fields => _fields;

        /// <summary>
        /// Gets the names of the methods that are used. Overloads have the same name, so a
        /// use of the name is a use of all of the overloads.
        /// </summary>
        internal IReadOnlyCollection<string> Methods => _methods;

        internal void UseField(string name) => _fields.Add(name);

        internal void UseMethod(string name) => _methods.Add(name);

        /// <summary>
        /// Records the uses that were recorded by another instance.
        /// </summary>
        /// <param name="other">The uses to add.</param>
        internal void Add(AnimatedVisualMemberUses other)
        {
            _fields.UnionWith(other._fields);
            _methods.UnionWith(other._methods);
        }
    }
}
