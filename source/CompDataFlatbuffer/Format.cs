// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer
{
    /// <summary>
    /// Constants and helpers that describe the lottie_comp.fbs wire format but
    /// which are not expressible in the schema itself, and so are not produced
    /// by the FlatBuffers compiler.
    /// </summary>
#if PUBLIC_CompDataFlatbuffer
    public
#endif
    static class Format
    {
        /// <summary>
        /// The version of the schema that this code reads and writes. This is stored in
        /// the schema_version field of every buffer. It is incremented for every change
        /// to lottie_comp.fbs. Only additive changes are permitted, so a reader can load
        /// any buffer whose schema_version is less than or equal to its own.
        /// </summary>
        public const ushort Version = 1;

        /// <summary>
        /// The 4 character identifier stored at offset 4 of every buffer. This is the
        /// file_identifier declared in lottie_comp.fbs.
        /// </summary>
        public const string FileIdentifier = "LCMP";

        /// <summary>
        /// The file extension used for serialized compositions. This is the
        /// file_extension declared in lottie_comp.fbs.
        /// </summary>
        public const string FileExtension = ".lcomp";

        /// <summary>
        /// The value of an index or object reference field that refers to nothing.
        /// </summary>
        public const uint NullIndex = 0xFFFFFFFF;

        /// <summary>
        /// The number of low bits of an object reference that store the index. The
        /// remaining high bits store the <see cref="Schema.ObjectCategory"/>, i.e. which
        /// of the root table's node vectors the index refers to.
        /// </summary>
        const int ObjectReferenceIndexBits = 28;

        const uint ObjectReferenceIndexMask = (1u << ObjectReferenceIndexBits) - 1;

        /// <summary>
        /// Combines a category and an index into the single uint used by object
        /// reference fields.
        /// </summary>
        /// <param name="category">The vector that <paramref name="index"/> indexes.</param>
        /// <param name="index">The index of the node within that vector.</param>
        /// <returns>The packed object reference.</returns>
        public static uint PackObjectReference(Schema.ObjectCategory category, int index)
            => ((uint)category << ObjectReferenceIndexBits) | ((uint)index & ObjectReferenceIndexMask);

        /// <summary>
        /// Returns the category part of an object reference.
        /// </summary>
        /// <param name="reference">A packed object reference.</param>
        /// <returns>The category of the referenced node.</returns>
        public static Schema.ObjectCategory UnpackCategory(uint reference)
            => (Schema.ObjectCategory)(reference >> ObjectReferenceIndexBits);

        /// <summary>
        /// Returns the index part of an object reference.
        /// </summary>
        /// <param name="reference">A packed object reference.</param>
        /// <returns>The index of the referenced node within its category's vector.</returns>
        public static int UnpackIndex(uint reference)
            => (int)(reference & ObjectReferenceIndexMask);
    }
}
