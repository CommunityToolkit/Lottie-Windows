// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        Font[] ParseFonts(ref Reader reader)
        {
            using var subDocument = reader.ParseElement();

            var fontsObject = subDocument.RootElement.AsObject();

            return fontsObject.HasValue
                ? ParseFonts(fontsObject.Value)
                : Array.Empty<Font>();
        }

        Font[] ParseFonts(in LottieJsonObjectElement fontsObject)
        {
            IList<Font> list = EmptyList<Font>.Singleton;

            var listArray = fontsObject.ArrayPropertyOrNull("list");
            if (listArray.HasValue)
            {
                foreach (var item in listArray)
                {
                    var element = item.AsObject();
                    if (!element.HasValue)
                    {
                        continue;
                    }

                    var obj = element.Value;
                    var fName = obj.StringPropertyOrNull("fName") ?? string.Empty;
                    var fFamily = obj.StringPropertyOrNull("fFamily") ?? string.Empty;
                    var fStyle = obj.StringPropertyOrNull("fStyle") ?? string.Empty;
                    var ascent = obj.DoublePropertyOrNull("ascent") ?? 0;
                    obj.AssertAllPropertiesRead();

                    if (list == EmptyList<Font>.Singleton)
                    {
                        list = new List<Font>();
                    }

                    list.Add(new Font(fName, fFamily, fStyle, ascent));
                }
            }

            fontsObject.AssertAllPropertiesRead();

            return list.ToArray();
        }
    }
}