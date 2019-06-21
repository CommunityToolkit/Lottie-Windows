// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax
{
    /// <summary>
    /// A reference to a type; either a built-in type or an imported type.
    /// </summary>
    internal abstract class TypeReference
    {
        TypeReference()
        {
        }

        public static IEnumerable<TypeReference> EmptyList { get; } = new TypeReference[0];

        /// <summary>
        /// A reference to a type that is not one of the built-in types.
        /// </summary>
        public sealed class ImportedTypeReference : TypeReference
        {
            public ImportedTypeReference(string fullyQualifiedName)
            {
                FullyQualifiedName = fullyQualifiedName;
            }

            public string FullyQualifiedName { get; }

            public override string ToString() => FullyQualifiedName;
        }

        /// <summary>
        /// A reference to a type that is known by the system.
        /// </summary>
        public sealed class BuiltIn : TypeReference
        {
            readonly string _name;

            BuiltIn(string name)
            {
                _name = name;
            }

            public static BuiltIn Boolean { get; } = new BuiltIn("bool");

            public static BuiltIn Float { get; } = new BuiltIn("float");

            public static BuiltIn Int32 { get; } = new BuiltIn("int");

            public static BuiltIn Matrix3x2 { get; } = new BuiltIn("Matrix3x2");

            public static BuiltIn Matrix4x4 { get; } = new BuiltIn("Matrix4x4");

            public static BuiltIn String { get; } = new BuiltIn("string");

            public static BuiltIn TimeSpan { get; } = new BuiltIn("TimeSpan");

            public static BuiltIn Uri { get; } = new BuiltIn("Uri");

            public static BuiltIn Vector2 { get; } = new BuiltIn("Vector2");

            public static BuiltIn Vector3 { get; } = new BuiltIn("Vector3");

            public override string ToString() => _name;
        }
    }
}
