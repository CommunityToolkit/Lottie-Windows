// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax
{
    /// <summary>
    /// A leaf node in a <see cref="StatementTree"/>.
    /// Executing a <see cref="Statement"/> will mutate the state of the process.
    /// </summary>
    class Statement : StatementTree
    {
        Statement()
        {
        }

        internal class AssignToLocal : Statement
        {
            public AssignToLocal(LocalVariable destination, Expression source)
            {
                Destination = destination;
                Source = source;
            }

            public Expression Source { get; }

            public LocalVariable Destination { get; }

            public override string ToString() => $"{Destination} = {Source}";
        }

        internal class DeclareAndInitializeLocal : Statement
        {
            public DeclareAndInitializeLocal(LocalVariable destination, Expression source)
            {
                Destination = destination;
                Source = source;
            }

            public Expression Source { get; }

            public LocalVariable Destination { get; }

            public override string ToString() => $"var {Destination} = {Source}";
        }
    }
}
