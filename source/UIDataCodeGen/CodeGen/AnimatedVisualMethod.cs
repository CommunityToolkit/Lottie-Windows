// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// A method of an AnimatedVisual class: a factory that creates an object, or a helper
    /// that the factories call. These are the methods that the AnimatedVisual classes of the
    /// different versions of an IAnimatedVisualSource may share.
    /// </summary>
    sealed class AnimatedVisualMethod
    {
        string? _key;
        string? _signature;
        IReadOnlyList<string>? _headerLines;

        internal AnimatedVisualMethod(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the name of the method.
        /// </summary>
        internal string Name { get; }

        /// <summary>
        /// Gets the comments before the method. They are not part of what makes methods equal.
        /// </summary>
        internal CodeBuilder Comment { get; } = new CodeBuilder();

        /// <summary>
        /// Gets or sets the attribute of the method, or null.
        /// </summary>
        internal string? Attribute { get; set; }

        /// <summary>
        /// Gets the signature of the method, which can have several lines, for example if each
        /// parameter is on its own line. The first line has the modifiers and the return type.
        /// </summary>
        internal CodeBuilder Header { get; } = new CodeBuilder();

        /// <summary>
        /// Gets the body of the method, including its braces.
        /// </summary>
        internal CodeBuilder Body { get; } = new CodeBuilder();

        /// <summary>
        /// Gets the fields and methods of the class that the method uses.
        /// </summary>
        internal AnimatedVisualMemberUses Uses { get; } = new AnimatedVisualMemberUses();

        /// <summary>
        /// Gets or sets a value indicating whether the method is static.
        /// </summary>
        internal bool IsStatic { get; set; }

        /// <summary>
        /// Gets a value indicating whether the signature has not been written. This is the case
        /// for a helper method that has not been needed.
        /// </summary>
        internal bool IsEmpty => Header.IsEmpty;

        /// <summary>
        /// Gets the lines of the signature. Overloads have different signatures.
        /// </summary>
        internal IReadOnlyList<string> HeaderLines => _headerLines ??= Header.ToLines(0).ToArray();

        /// <summary>
        /// Gets the text that identifies the method among those of its class.
        /// </summary>
        internal string Signature => _signature ??= string.Join("\n", HeaderLines);

        /// <summary>
        /// Gets the text that is the same for two methods if and only if they have the same
        /// attribute, signature and code, ignoring comments.
        /// </summary>
        internal string Key
            => _key ??= string.Join(
                "\n",
                new[] { Attribute ?? string.Empty }
                    .Concat(Header.ToLines(0, includeComments: false))
                    .Concat(Body.ToLines(0, includeComments: false)));

        /// <summary>
        /// Writes the method.
        /// </summary>
        /// <param name="builder">The <see cref="CodeBuilder"/> to write to.</param>
        /// <param name="modifiers">Text to write before the signature, or null.</param>
        internal void Write(CodeBuilder builder, string? modifiers = null)
        {
            builder.WriteCodeBuilder(Comment);
            if (Attribute is not null)
            {
                builder.WriteLine(Attribute);
            }

            if (modifiers is null)
            {
                builder.WriteCodeBuilder(Header);
            }
            else
            {
                builder.WritePreindentedLines(HeaderLines.Select((line, i) => i == 0 ? modifiers + line : line));
            }

            builder.WriteCodeBuilder(Body);
        }

        /// <summary>
        /// Writes the declaration of the method as abstract, which is what the shared base class
        /// has for a method that the classes implement differently.
        /// </summary>
        /// <param name="builder">The <see cref="CodeBuilder"/> to write to.</param>
        internal void WriteAbstractDeclaration(CodeBuilder builder)
            => builder.WriteLine($"internal abstract {HeaderLines.Single()};");
    }
}
