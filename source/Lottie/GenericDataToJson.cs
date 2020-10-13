// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;
using Windows.Data.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    static class GenericDataToJson
    {
        internal static IJsonValue GenericDataObjectToJsonValue(GenericDataObject? obj)
        {
            if (obj is null)
            {
                return JsonValue.CreateNullValue();
            }

            switch (obj.Type)
            {
                case GenericDataObjectType.Bool:
                    return JsonValue.CreateBooleanValue(((GenericDataBool)obj).Value);

                case GenericDataObjectType.List:
                    {
                        var result = new JsonArray();
                        foreach (var value in (GenericDataList)obj)
                        {
                            result.Add(GenericDataObjectToJsonValue(value));
                        }

                        return result;
                    }

                case GenericDataObjectType.Map:
                    {
                        var result = new JsonObject();
                        foreach ((var key, var value) in (GenericDataMap)obj)
                        {
                            result.Add(key, GenericDataObjectToJsonValue(value));
                        }

                        return result;
                    }

                case GenericDataObjectType.Number:
                    return JsonValue.CreateNumberValue(((GenericDataNumber)obj).Value);

                case GenericDataObjectType.String:
                    return JsonValue.CreateStringValue(((GenericDataString)obj).Value);

                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
