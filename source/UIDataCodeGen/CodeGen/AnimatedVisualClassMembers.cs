// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// The fields and methods of an AnimatedVisual class.
    /// </summary>
    sealed class AnimatedVisualClassMembers
    {
        internal AnimatedVisualClassMembers(IReadOnlyList<AnimatedVisualField> fields, IReadOnlyList<AnimatedVisualMethod> methods)
        {
            Fields = fields;
            Methods = methods;
        }

        internal IReadOnlyList<AnimatedVisualField> Fields { get; }

        internal IReadOnlyList<AnimatedVisualMethod> Methods { get; }
    }
}
