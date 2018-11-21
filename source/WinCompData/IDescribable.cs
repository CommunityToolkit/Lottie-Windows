// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    /// <summary>
    /// Interface implemented by objects to expose a description in plain language.
    /// The descriptions are typically used by comments in generated code.
    /// </summary>
#if !WINDOWS_UWP
    public
#endif
    interface IDescribable
    {
        /// <summary>
        /// Gets or sets a long desription of the object, suitable for
        /// use in a multi-line comment.
        /// </summary>
        string LongDescription { get; set; }

        /// <summary>
        /// Gets or sets a short description of the object, suitable for
        /// use in a single line comment.
        /// </summary>
        string ShortDescription { get; set; }
    }
}
