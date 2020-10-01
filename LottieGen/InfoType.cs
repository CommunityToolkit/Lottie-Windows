// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

/// <summary>
/// Categories of information that are displayed to the user on the console. These
/// categories are used to color the output.
/// </summary>
enum InfoType
{
    Default = 0,

    /// <summary>
    /// Advice to the user about a follow up action they should take.
    /// </summary>
    Advice,

    /// <summary>
    /// A file path or file name.
    /// </summary>
    FilePath,

    /// <summary>
    /// An issue that may prevent the result from working as expected.
    /// </summary>
    Issue,

    /// <summary>
    /// An announcement about the tool name and version.
    /// </summary>
    Signon,
}
