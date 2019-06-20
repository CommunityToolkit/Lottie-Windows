// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        /// <summary>
        /// A reference to a type that is not one of the built-in types.
        /// </summary>
        public sealed class ImportedTypeReference : TypeReference
        {
            ImportedTypeReference(string fullyQualifiedName)
            {
                FullyQualifiedName = fullyQualifiedName;
            }

            public string FullyQualifiedName { get; }
        }

        /// <summary>
        /// A reference to a type that is known by the system.
        /// </summary>
        public sealed class BuiltIn : TypeReference
        {
            BuiltIn(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public static BuiltIn Boolean { get; } = new BuiltIn("Boolean");

            public static BuiltIn Float { get; } = new BuiltIn("Float");

            public static BuiltIn Int32 { get; } = new BuiltIn("Int32");

            public static BuiltIn Matrix3x2 { get; } = new BuiltIn("Matrix3x2");

            public static BuiltIn Matrix4x4 { get; } = new BuiltIn("Matrix4x4");

            public static BuiltIn String { get; } = new BuiltIn("String");

            public static BuiltIn TimeSpan { get; } = new BuiltIn("TimeSpan");

            public static BuiltIn Uri { get; } = new BuiltIn("Uri");

            public static BuiltIn Vector2 { get; } = new BuiltIn("Vector2");

            public static BuiltIn Vector3 { get; } = new BuiltIn("Vector3");
        }

    }
}
