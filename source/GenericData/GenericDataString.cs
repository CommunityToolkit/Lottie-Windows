// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.GenericData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class GenericDataString : GenericDataObject
    {
        readonly string _value;

        GenericDataString(string value)
        {
            _value = value;
        }

        public static GenericDataString Create(string value) => new GenericDataString(value);

        public string Value => _value;

        public override GenericDataObjectType Type => GenericDataObjectType.String;

        public override string ToString() => "\"" + _value + "\"";
    }
}