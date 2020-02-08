// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        Font[] ParseFonts(ref Reader reader)
        {
            IList<Font> list = EmptyList<Font>.Singleton;

            var fontsObject = JCObject.Load(ref reader, s_jsonLoadSettings);
            foreach (JCObject item in fontsObject.GetNamedArray("list"))
            {
                var fName = item.GetNamedString("fName");
                var fFamily = item.GetNamedString("fFamily");
                var fStyle = item.GetNamedString("fStyle");
                var ascent = ReadFloat(item.GetNamedValue("ascent"));
                AssertAllFieldsRead(item);
                if (list == EmptyList<Font>.Singleton)
                {
                    list = new List<Font>();
                }

                list.Add(new Font(fName, fFamily, fStyle, ascent));
            }

            AssertAllFieldsRead(fontsObject);

            return list.ToArray();
        }
    }
}