// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Lottie.WinCompData.MetaData;

namespace CommunityToolkit.WinUI.Lottie.CompMetadata
{
    /// <summary>
    /// Describes a name bound to a value in a CompositionPropertySet.
    /// </summary>
#if PUBLIC_CompMetadata
    public
#endif
    sealed class PropertyBinding
    {
        public PropertyBinding(
            string bindingName,
            string displayName,
            PropertySetValueType actualType,
            PropertySetValueType exposedType,
            object defaultValue)
        {
            BindingName = bindingName;
            DisplayName = displayName;
            ActualType = actualType;
            ExposedType = exposedType;
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// The name used to identify the value in the CompositionPropertySet.
        /// </summary>
        public string BindingName { get; }

        /// <summary>
        /// A name for the binding for display in tools.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// The type of data stored in the CompositionPropertySet under this name.
        /// </summary>
        public PropertySetValueType ActualType { get; }

        /// <summary>
        /// The type that should be used when making this binding available via an API.
        /// Typically this is the same as the <see cref="ActualType"/>, however some types
        /// are not supported by animations expressions and must be stored using a different type,
        /// for example, colors are stored as Vector4.
        /// </summary>
        public PropertySetValueType ExposedType { get; }

        /// <summary>
        /// The default value of the binding.
        /// </summary>
        public object DefaultValue { get; }
    }
}
