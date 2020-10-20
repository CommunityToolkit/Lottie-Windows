// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        ref struct LottieJsonDocument
        {
            readonly LottieCompositionReader _owner;
            readonly JsonDocument _wrapped;

            internal LottieJsonDocument(LottieCompositionReader owner, JsonDocument wrapped)
            {
                _owner = owner;
                _wrapped = wrapped;
            }

            internal LottieJsonElement RootElement => new LottieJsonElement(_owner, _wrapped.RootElement);

            public void Dispose()
                => _wrapped.Dispose();
        }
    }
}