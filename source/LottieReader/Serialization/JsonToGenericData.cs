// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;
using Newtonsoft.Json.Linq;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    /// <summary>
    /// Reads the contents of the given <see cref="JToken"/> into a <see cref="GenericDataObject"/>.
    /// </summary>
    static class JsonToGenericData
    {
        internal static GenericDataObject JTokenToGenericData(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var jobject = (IEnumerable<KeyValuePair<string, JToken>>)token;
                    return jobject.ToDictionary(field => field.Key, field => JTokenToGenericData(field.Value));
                case JTokenType.Array:
                    return GenericDataList.Create(((JArray)token).Select(item => JTokenToGenericData(item)));
                case JTokenType.Integer:
                    return (long)token;
                case JTokenType.Float:
                    return (double)token;
                case JTokenType.String:
                    return (string)token;
                case JTokenType.Boolean:
                    return (bool)token;
                case JTokenType.Null:
                    return null;

                // Currently unsupported types.
                case JTokenType.None:
                case JTokenType.Constructor:
                case JTokenType.Property:
                case JTokenType.Comment:
                case JTokenType.Undefined:
                case JTokenType.Date:
                case JTokenType.Raw:
                case JTokenType.Bytes:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                    throw new InvalidOperationException($"Unsupported JSON token type: {token.Type}");

                default:
                    throw Unreachable;
            }
        }
    }
}