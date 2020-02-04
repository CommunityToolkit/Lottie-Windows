// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Newtonsoft.Json.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    static class JTokenExtensions
    {
        internal static JCObject AsObject(this JToken token)
        {
            try
            {
                return (JCObject)token;
            }
            catch (InvalidCastException ex)
            {
                var exceptionString = ex.Message;
                if (!string.IsNullOrWhiteSpace(token.Path))
                {
                    exceptionString += $" Failed to cast to correct type for token in path: {token.Path}.";
                }

                throw new LottieCompositionReaderException(exceptionString, ex);
            }
        }

        internal static JCArray AsArray(this JToken token)
        {
            try
            {
                return (JCArray)token;
            }
            catch (InvalidCastException ex)
            {
                var exceptionString = ex.Message;
                if (!string.IsNullOrWhiteSpace(token.Path))
                {
                    exceptionString += $" Failed to cast to correct type for token in path: {token.Path}.";
                }

                throw new LottieCompositionReaderException(exceptionString, ex);
            }
        }
    }
}
