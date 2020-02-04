// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    static class JCObjectExtensions
    {
        internal static JToken GetNamedValue(this JCObject obj, string name, JToken defaultValue = null)
        {
            return obj.TryGetValue(name, out JToken value) ? value : defaultValue;
        }

        internal static string GetNamedString(this JCObject obj, string name, string defaultValue = "")
        {
            return obj.TryGetValue(name, out JToken value) ? (string)value : defaultValue;
        }

        internal static double GetNamedNumber(this JCObject obj, string name, double defaultValue = double.NaN)
        {
            return obj.TryGetValue(name, out JToken value) ? (double)value : defaultValue;
        }

        internal static JCArray GetNamedArray(this JCObject obj, string name, JCArray defaultValue = null)
        {
            return obj.TryGetValue(name, out JToken value) ? value.AsArray() : defaultValue;
        }

        internal static JCObject GetNamedObject(this JCObject obj, string name, JCObject defaultValue = null)
        {
            return obj.TryGetValue(name, out JToken value) ? value.AsObject() : defaultValue;
        }

        internal static bool GetNamedBoolean(this JCObject obj, string name, bool defaultValue = false)
        {
            return obj.TryGetValue(name, out JToken value) ? (bool)value : defaultValue;
        }
    }
}
