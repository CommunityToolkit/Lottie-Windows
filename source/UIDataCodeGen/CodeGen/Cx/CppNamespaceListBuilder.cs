// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen.Cx
{
    sealed class CppNamespaceListBuilder
    {
        // A sorted set to hold the namespaces that the generated code will use. The set is maintained in sorted order.
        readonly SortedSet<string> _namespaces = new SortedSet<string>();

        public void Add(string nameSpace) => _namespaces.Add(nameSpace);

        public CodeBuilder ToCodeBuilder()
        {
            var builder = new CodeBuilder();

            foreach (var n in _namespaces)
            {
                builder.WriteLine($"using namespace {n};");
            }

            return builder;
        }

        public override string ToString()
        {
            return ToCodeBuilder().ToString();
        }
    }
}
