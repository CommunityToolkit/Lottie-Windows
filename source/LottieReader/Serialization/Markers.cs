// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable // Temporary while enabling nullable everywhere.

using System.Text.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        Marker ParseMarker(ref Reader reader)
        {
            reader.ExpectToken(JsonTokenType.StartObject);

            string name = null;
            double durationInFrames = 0;
            double frame = 0;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var currentProperty = reader.GetString();

                        reader.ConsumeToken();

                        switch (currentProperty)
                        {
                            case "cm":
                                name = reader.GetString();
                                break;
                            case "dr":
                                durationInFrames = reader.ParseDouble();
                                break;
                            case "tm":
                                frame = reader.ParseDouble();
                                break;
                            default:
                                _issues.UnexpectedField(currentProperty);
                                reader.Skip();
                                break;
                        }

                        break;
                    case JsonTokenType.EndObject:
                        return new Marker(name: name, frame: frame, durationInFrames: durationInFrames);
                    default:
                        throw reader.ThrowUnexpectedToken();
                }
            }

            throw EofException;
        }
    }
}