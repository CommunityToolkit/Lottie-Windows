// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.GenericData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class GenericDataBool : GenericDataObject
    {
        GenericDataBool()
        {
        }

        public static GenericDataBool Create(bool value) => value ? True : False;

        public static GenericDataBool True { get; } = new TrueImpl();

        public static GenericDataBool False { get; } = new FalseImpl();

        public abstract bool Value { get; }

        public override GenericDataObjectType Type => GenericDataObjectType.Bool;

        public static implicit operator GenericDataBool(bool value) => Create(value);

        sealed class TrueImpl : GenericDataBool
        {
            public override bool Value => true;

            public override string ToString() => "true";
        }

        sealed class FalseImpl : GenericDataBool
        {
            public override bool Value => false;

            public override string ToString() => "false";
        }
    }
}