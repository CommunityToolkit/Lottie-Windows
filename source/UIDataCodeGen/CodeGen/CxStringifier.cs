// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Stringifiers for C++/CX syntax.
    /// </summary>
    sealed class CxStringifier : CppStringifier
    {
        internal CxStringifier()
        {
        }

        public override string ReferenceTypeName(string value)
            => value == "CanvasGeometry"
                    ? "CanvasGeometry" // CanvasGeometry is a typedef for ComPtr<GeoSource>, thus no hat pointer.
                    : $"{value}^";

        public override string StringType => "String^";

        public override string Hatted(string typeName) => $"{typeName}^";
    }
}
