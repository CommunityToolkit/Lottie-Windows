// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
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
