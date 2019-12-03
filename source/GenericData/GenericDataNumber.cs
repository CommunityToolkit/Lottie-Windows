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
        GenericDataNumber(double value)
        {
            Value = value;
        }

        public static GenericDataNumber Create(double value) => new GenericDataNumber(value);

        public double Value { get; private set; }

        public override GenericDataObjectType Type => GenericDataObjectType.Number;

        public override string ToString()
            => Math.Floor(Value) == Value
            ? Value.ToString("0", CultureInfo.InvariantCulture)
            : Value.ToString("G9", CultureInfo.InvariantCulture) + "F";

        public static implicit operator GenericDataNumber(double value) => Create(value);
    }
}