// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.GenericData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class GenericDataNumber : GenericDataObject
    {
        readonly double _value;

        GenericDataNumber(double value)
        {
            _value = value;
        }

        public static GenericDataNumber Create(double value) => new GenericDataNumber(value);

        public double Value => _value;

        public override GenericDataObjectType Type => GenericDataObjectType.Number;

        public override string ToString()
            => Math.Floor(_value) == _value
            ? _value.ToString("0", CultureInfo.InvariantCulture)
            : _value.ToString("G9", CultureInfo.InvariantCulture) + "F";
    }
}