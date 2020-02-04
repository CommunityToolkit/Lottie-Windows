// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// If defined, an issue will be reported for each field that is discovered
// but not parsed. This is used to help test that parsing is complete.
#define CheckForUnparsedFields

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#if CheckForUnparsedFields
using JArray = Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.CheckedJsonArray;
using JObject = Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.CheckedJsonObject;
#endif

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#if CheckForUnparsedFields
    sealed class CheckedJsonArray : IList<JToken>
    {
        internal Newtonsoft.Json.Linq.JArray Wrapped { get; }

        internal CheckedJsonArray(Newtonsoft.Json.Linq.JArray wrapped)
        {
            Wrapped = wrapped;
        }

        internal static CheckedJsonArray Load(JsonReader reader, JsonLoadSettings settings)
        {
            return new CheckedJsonArray(Newtonsoft.Json.Linq.JArray.Load(reader, settings));
        }

        public JToken this[int index] { get => Wrapped[index]; set => throw new NotImplementedException(); }

        public int Count => Wrapped.Count;

        public bool IsReadOnly => throw new NotImplementedException();

        public void Add(JToken item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(JToken item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(JToken[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<JToken> GetEnumerator()
        {
            foreach (var value in Wrapped)
            {
                yield return value;
            }
        }

        public int IndexOf(JToken item) => throw new NotImplementedException();

        public void Insert(int index, JToken item) => throw new NotImplementedException();

        public bool Remove(JToken item) => throw new NotImplementedException();

        public void RemoveAt(int index) => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static implicit operator CheckedJsonArray(Newtonsoft.Json.Linq.JArray value)
        {
            return value is null ? null : new CheckedJsonArray(value);
        }
    }
#endif
}