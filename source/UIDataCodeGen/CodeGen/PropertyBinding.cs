// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    sealed class PropertyBinding
    {
        internal PropertyBinding(
            string bindingName,
            PropertySetValueType actualType,
            PropertySetValueType exposedType,
            object defaultValue)
        {
            Name = bindingName;
            ActualType = actualType;
            ExposedType = exposedType;
            DefaultValue = defaultValue;
        }

        internal string Name { get; }

        internal PropertySetValueType ActualType { get; }

        internal PropertySetValueType ExposedType { get; }

        internal object DefaultValue { get; }
    }
}
