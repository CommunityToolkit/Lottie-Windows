// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.GenericData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class GenericDataString : GenericDataObject
    {
        GenericDataString(string value)
        {
            Value = value;
        }

        public static GenericDataString Create(string value) => new GenericDataString(value);

        public string Value { get; private set; }

        public override GenericDataObjectType Type => GenericDataObjectType.String;

        public override string ToString() => "\"" + Value + "\"";

        public static implicit operator GenericDataString(string value) => Create(value);
    }
}