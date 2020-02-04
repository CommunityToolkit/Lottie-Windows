// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// If defined, an issue will be reported for each field that is discovered
// but not parsed. This is used to help test that parsing is complete.
#define CheckForUnparsedFields

using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#if CheckForUnparsedFields
    sealed class JCObject : IEnumerable<KeyValuePair<string, JToken>>
    {
        internal Newtonsoft.Json.Linq.JObject Wrapped { get; }

        internal HashSet<string> ReadFields { get; } = new HashSet<string>();

        internal JCObject(Newtonsoft.Json.Linq.JObject wrapped)
        {
            Wrapped = wrapped;
        }

        internal static JCObject Parse(string input, JsonLoadSettings loadSettings) => new JCObject(Newtonsoft.Json.Linq.JObject.Parse(input, loadSettings));

        internal bool ContainsKey(string key)
        {
            ReadFields.Add(key);
            return Wrapped.ContainsKey(key);
        }

        internal bool TryGetValue(string propertyName, out JToken value)
        {
            ReadFields.Add(propertyName);
            return Wrapped.TryGetValue(propertyName, out value);
        }

        internal static JCObject Load(JsonReader reader, JsonLoadSettings settings)
        {
            return new JCObject(Newtonsoft.Json.Linq.JObject.Load(reader, settings));
        }

        public IEnumerator<KeyValuePair<string, JToken>> GetEnumerator()
        {
            return Wrapped.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Wrapped.GetEnumerator();
        }

        public static implicit operator JCObject(Newtonsoft.Json.Linq.JObject value)
        {
            return value is null ? null : new JCObject(value);
        }
    }
#endif
}