// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    sealed class JCObject
    {
        readonly (bool, string)[] _propertyNames;

        readonly JObject _wrapped;

        JCObject(JObject wrapped)
        {
            _wrapped = wrapped;

            // Get the list of property names.
            _propertyNames = ((IEnumerable<KeyValuePair<string, JToken>>)wrapped).Select(s => (true, s.Key)).ToArray();

            // Sort the names so they can be binary searched.
            Array.Sort(_propertyNames, Comparer.Instance);
        }

        // Marks the given property name as being read.
        void MarkPropertyAsRead(string propertyName)
        {
            int min = 0;
            int max = _propertyNames.Length - 1;
            while (min <= max)
            {
                int mid = (min + max) / 2;
                var current = _propertyNames[mid].Item2;
                if (propertyName == current)
                {
                    _propertyNames[mid].Item1 = false;
                    return;
                }
                else if (string.CompareOrdinal(propertyName, _propertyNames[mid].Item2) < 0)
                {
                    // Look left.
                    max = mid - 1;
                }
                else
                {
                    // Look right.
                    min = mid + 1;
                }
            }

            // Not found.
        }

        internal void AddIgnoredProperty(string propertyName)
            => MarkPropertyAsRead(propertyName);

        internal bool ContainsKey(string key)
            => TryGetProperty(key, out _);

        internal bool TryGetProperty(string propertyName, out JToken value)
        {
            if (_wrapped.TryGetValue(propertyName, out value))
            {
                MarkPropertyAsRead(propertyName);
                return true;
            }

            return false;
        }

        internal static JCObject Load(ref Reader reader, JsonLoadSettings settings)
        {
            return new JCObject(JObject.Load(reader.NewtonsoftReader, settings));
        }

        internal void AssertAllFieldsRead(Action<string> unreadFieldCallback, [CallerMemberName]string memberName = "")
        {
            foreach (var pair in _propertyNames)
            {
                if (pair.Item1)
                {
                    unreadFieldCallback(pair.Item2);
                }
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(_wrapped);

        public static implicit operator JCObject(JObject value)
        {
            return value is null ? null : new JCObject(value);
        }

        sealed class Comparer : IComparer<(bool, string)>
        {
            internal static readonly Comparer Instance = new Comparer();

            int IComparer<(bool, string)>.Compare((bool, string) x, (bool, string) y)
                => string.CompareOrdinal(x.Item2, y.Item2);
        }

        public struct Enumerator
        {
            readonly IEnumerator<KeyValuePair<string, JToken>> _wrapped;

            internal Enumerator(JObject wrapped)
            {
                _wrapped = wrapped.GetEnumerator();
            }

            public KeyValuePair<string, JToken> Current => _wrapped.Current;

            public void Dispose() => _wrapped.Dispose();

            public bool MoveNext() => _wrapped.MoveNext();
        }
    }
}