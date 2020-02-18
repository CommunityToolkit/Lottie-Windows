// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            IList<Font> list = EmptyList<Font>.Singleton;

            var fontsObject = LottieJsonObjectElement.Load(this, ref reader, s_jsonLoadSettings);
            foreach (var item in fontsObject.AsArrayProperty("list").Value)
            {
                var element = item.AsObject();
                if (!element.HasValue)
                {
                    continue;
                }

                var obj = element.Value;
                var fName = obj.StringOrNullProperty("fName") ?? string.Empty;
                var fFamily = obj.StringOrNullProperty("fFamily") ?? string.Empty;
                var fStyle = obj.StringOrNullProperty("fStyle") ?? string.Empty;
                var ascent = obj.DoubleOrNullProperty("ascent") ?? 0;
                obj.AssertAllPropertiesRead();
                if (list == EmptyList<Font>.Singleton)
                {
                    list = new List<Font>();
                }

                list.Add(new Font(fName, fFamily, fStyle, ascent));
            }

            fontsObject.AssertAllPropertiesRead();

            return list.ToArray();
        }
    }
}