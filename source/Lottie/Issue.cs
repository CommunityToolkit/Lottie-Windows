// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    /// <summary>
    /// An issue that was discovered while reading or translating a Lottie JSON file.
    /// </summary>
    sealed class Issue
    {
        internal Issue(string code, string description)
        {
            Code = code;
            Description = description;
        }

        /// <summary>
        /// Gets or sets a code that identifies the issue.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets or sets a string that describes the issue.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets a URL that may give more information about the issue.
        /// </summary>
        public string Url => $"https://github.com/windows-toolkit/Lottie-Windows/blob/master/source/Issues/{Code}.md";

        /// <summary>
        /// Returns a string representation of the issue.
        /// </summary>
        /// <returns>A string representation of the issue.</returns>
        public override string ToString() => $"{Code}: {Description}";
    }
}
