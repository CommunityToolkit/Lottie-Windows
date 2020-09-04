// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Cx
{
    sealed class HeaderBuilder
    {
        internal CodeBuilder Preamble { get; } = new CodeBuilder();

        internal CodeBuilder Private { get; } = new CodeBuilder();

        internal CodeBuilder Internal { get; } = new CodeBuilder();

        internal CodeBuilder Public { get; } = new CodeBuilder();

        internal CodeBuilder Postamble { get; } = new CodeBuilder();

        public override string ToString()
        {
            var result = new CodeBuilder();
            result.WriteCodeBuilder(Preamble);
            result.Indent();
            result.OpenScope();
            result.UnIndent();

            result.WriteCodeBuilder(Private);
            result.WriteCodeBuilder(Internal);
            result.WriteCodeBuilder(Public);
            result.WriteCodeBuilder(Postamble);

            return result.ToString();
        }
    }
}
