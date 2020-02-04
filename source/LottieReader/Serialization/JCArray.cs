// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    sealed class JCArray : IList<JToken>
    {
        internal Newtonsoft.Json.Linq.JArray Wrapped { get; }

        internal JCArray(Newtonsoft.Json.Linq.JArray wrapped)
        {
            Wrapped = wrapped;
        }

        internal static JCArray Load(JsonReader reader, JsonLoadSettings settings)
        {
            return new JCArray(Newtonsoft.Json.Linq.JArray.Load(reader, settings));
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

        public static implicit operator JCArray(Newtonsoft.Json.Linq.JArray value)
        {
            return value is null ? null : new JCArray(value);
        }
    }
}