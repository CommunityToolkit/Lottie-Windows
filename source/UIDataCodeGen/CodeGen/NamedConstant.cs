﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// A named constant.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class NamedConstant
    {
        internal NamedConstant(string name, string description, ConstantType type, object value)
        {
            Name = name;
            Description = description;
            Type = type;
            Value = value;
        }

        internal string Name { get; set; }

        internal string Description { get; set; }

        internal ConstantType Type { get; set; }

        internal object Value { get; set; }
    }
}
