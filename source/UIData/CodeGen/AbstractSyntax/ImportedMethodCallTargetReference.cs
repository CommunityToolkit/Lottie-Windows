// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax
{
    sealed class ImportedMethodCallTargetReference : CallTargetReference
    {
        internal ImportedMethodCallTargetReference(TypeReference resultType, TypeReference receiverType, string name)
            : this(resultType, receiverType, name, TypeReference.EmptyList)
        {
        }

        internal ImportedMethodCallTargetReference(TypeReference resultType, TypeReference receiverType, string name, IEnumerable<TypeReference> parameterTypes)
            : base(resultType)
        {
            ReceiverType = receiverType;
            Name = name;
            ParameterTypes = parameterTypes;
        }

        public TypeReference ReceiverType { get; }

        public string Name { get; }

        public IEnumerable<TypeReference> ParameterTypes { get; }

        public override string ToString() => Name;
    }
}
