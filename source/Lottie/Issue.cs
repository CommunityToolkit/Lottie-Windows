// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    /// <summary>
    /// An issue that was discovered while reading or translating a Lottie JSON file.
    /// </summary>
    public sealed class Issue
    {
        /// <summary>
        /// Gets or sets a code that identifies the issue.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets a string that describes the issue.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets a URL that may give more information about the issue.
        /// </summary>
        public string Url => $"https://airbnb.design/lottie/#{Code}";

        /// <summary>
        /// Returns a string representation of the issue.
        /// </summary>
        /// <returns>A string representation of the issue.</returns>
        public override string ToString() => $"{Code}: {Description}";
    }
}
