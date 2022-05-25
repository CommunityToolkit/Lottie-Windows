// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace CommunityToolkit.WinUI.Lottie.WinCompData.MetaData
{
    /// <summary>
    /// Indicates that a class or member was introduced at a particular version of
    /// the Windows.Foundation.UniversalApiContract contract.
    /// </summary>
    [Conditional("DEBUG")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Method | AttributeTargets.Property)]
    sealed class UapVersionAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UapVersionAttribute"/> class.
        /// This attribute indicates which version of the Windows.Foundation.UniversalApiContract introduced the
        /// class or member.
        /// </summary>
        /// <param name="version">The version of the contract.</param>
        public UapVersionAttribute(int version)
        {
            // For reference, the friendly names used for the versions:
            //  1 = 1507  / 10.0.10240 / TH1
            //  2 = 1511  / 10.0.10586 / TH2  / November Update
            //  3 = 1607  / 10.0.14393 / RS1  / Anniversary Update
            //  4 = 1703  / 10.0.15063 / RS2  / Creators Update
            //  5 = 1709  / 10.0.16299 / RS3  / Fall Creators Update
            //  6 = 1803  / 10.0.17134 / RS4  / April 2018 Update
            //  7 = 1809  / 10.0.17763 / RS5  / October 2018 Update
            //  8 = 1903  / 10.0.18362 / 19H1 / May 2019 Update
            //  9 = 1909  / 10.0.18363 / 19H2 / November 2019 Update
            //  10 = 2004 / 10.0.19041 / 20H1 / May 2020 Update
            //  10 = 20H2 / 10.0.19042 / 20H2 / October 2020 Update (NB: 20H2 and 2004 are the same UAP version)
            //  ...
        }
    }
}
