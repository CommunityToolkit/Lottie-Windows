// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Stringifiers for C++/WinRT syntax.
    /// </summary>
    sealed class CppwinrtStringifier : CppStringifier
    {
        internal CppwinrtStringifier()
        {
        }

        public override string PropertySet(string target, string propertyName, string value)
            => $"{target}.{propertyName}({value})";

        public override string ReferenceTypeName(string value)
            => value == "CanvasGeometry"
                    ? "CanvasGeometry" // CanvasGeometry is a typedef for ComPtr<GeoSource>, thus no hat pointer.
                    : $"{value}";

        public override string DefaultInitialize => "{ nullptr }";

        public override string Deref => ".";

        public override string String(string value) => $"L\"{value}\"";

        public override string PropertyGet(string target, string propertyName) => $"{target}{Deref}{propertyName}()";

        public override string StringType => "winrt::hstring";

        public override string New(string typeName) => $"winrt::make<{typeName}>";

        public override string TimeSpan(string ticks) => $"TimeSpan{{ {ticks} }}";
    }
}
