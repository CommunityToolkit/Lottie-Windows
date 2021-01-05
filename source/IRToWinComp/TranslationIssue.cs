// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    /// <summary>
    /// Describes an issue discovered during translation of a Lottie composition.
    /// The <see cref="Code"/> is an alphanumeric code that identifies the type
    /// of issue and can be used to help search online for more information.
    /// The <see cref="Description"/> gives more detail about the particular
    /// instance of the issue.
    /// </summary>
#if PUBLIC
    public
#endif
    struct TranslationIssue
    {
        internal TranslationIssue(string code, string description)
        {
            Code = code;
            Description = description;
        }

        /// <summary>
        /// A code that is used to identify the issue.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// A description of the issue.
        /// </summary>
        public string Description { get; }

        public override string ToString() => $"{Code}: {Description}";
    }
}
