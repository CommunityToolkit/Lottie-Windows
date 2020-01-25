// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.YamlData
{
    /// <summary>
    /// Common base class for <see cref="YamlScalar"/>, <see cref="YamlMap"/>, and <see cref="YamlSequence"/>.
    /// </summary>
#if PUBLIC_YamlData
    public
#endif
    abstract class YamlObject
    {
        internal abstract YamlObjectKind Kind { get; }

        /// <summary>
        /// A comment. Comments should be a single line.
        /// </summary>
        public string Comment { get; set; }
    }
}