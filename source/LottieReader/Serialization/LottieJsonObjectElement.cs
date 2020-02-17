// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        internal readonly struct LottieJsonObjectElement
        {
            readonly LottieCompositionReader _owner;
            readonly JObject _wrapped;
            readonly (bool, string)[] _propertyNames;

            internal LottieJsonObjectElement(LottieCompositionReader owner, JToken wrapped)
            {
                Debug.Assert(wrapped.Type == JTokenType.Object, "Precondition");
                _owner = owner;
                _wrapped = (JObject)wrapped;

                // Get the list of property names.
                _propertyNames = ((IEnumerable<KeyValuePair<string, JToken>>)wrapped).Select(s => (true, s.Key)).ToArray();

                // Sort the names so they can be binary searched.
                Array.Sort(_propertyNames, Comparer.Instance);
            }

            internal LottieJsonArrayElement? ArrayOrNullProperty(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsArray() : null;

            internal bool? BoolOrNullProperty(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsBoolean() : null;

            internal double? DoubleOrNullProperty(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsDouble() : null;

            internal int? Int32OrNullProperty(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsInt32() : null;

            internal LottieJsonObjectElement? ObjectOrNullProperty(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsObject() : null;

            internal string StringOrNullProperty(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsString() : null;

            // Indicates that the given property will not be read because we don't yet support it.
            internal void IgnorePropertyThatIsNotYetSupported(string propertyName)
                => MarkPropertyAsRead(propertyName);

            internal void IgnorePropertyThatIsNotYetSupported(
                string propertyName1,
                string propertyName2)
            {
                MarkPropertyAsRead(propertyName1);
                MarkPropertyAsRead(propertyName2);
            }

            internal void IgnorePropertyThatIsNotYetSupported(
                string propertyName1,
                string propertyName2,
                string propertyName3)
            {
                MarkPropertyAsRead(propertyName1);
                MarkPropertyAsRead(propertyName2);
                MarkPropertyAsRead(propertyName3);
            }

            internal void IgnorePropertyThatIsNotYetSupported(
                string propertyName1,
                string propertyName2,
                string propertyName3,
                string propertyName4)
            {
                MarkPropertyAsRead(propertyName1);
                MarkPropertyAsRead(propertyName2);
                MarkPropertyAsRead(propertyName3);
                MarkPropertyAsRead(propertyName4);
            }

            internal void IgnorePropertyThatIsNotYetSupported(
                string propertyName1,
                string propertyName2,
                string propertyName3,
                string propertyName4,
                string propertyName5)
            {
                MarkPropertyAsRead(propertyName1);
                MarkPropertyAsRead(propertyName2);
                MarkPropertyAsRead(propertyName3);
                MarkPropertyAsRead(propertyName4);
                MarkPropertyAsRead(propertyName5);
            }

            // Indicates that the given property is not read because we don't need to read it.
            internal void IgnorePropertyIntentionally(string propertyName)
                => MarkPropertyAsRead(propertyName);

            internal bool ContainsProperty(string propertyName)
                => TryGetProperty(propertyName, out _);

            internal bool TryGetProperty(string propertyName, out LottieJsonElement value)
            {
                if (_wrapped.TryGetValue(propertyName, out var jtokenValue))
                {
                    MarkPropertyAsRead(propertyName);
                    value = new LottieJsonElement(_owner, jtokenValue);
                    return true;
                }

                value = default(LottieJsonElement);

                return false;
            }

            internal static LottieJsonObjectElement Load(LottieCompositionReader owner, ref Reader reader, JsonLoadSettings settings)
            {
                return new LottieJsonObjectElement(owner, JObject.Load(reader.NewtonsoftReader, settings));
            }

            internal void AssertAllPropertiesRead([CallerMemberName]string memberName = "")
            {
                foreach (var pair in _propertyNames)
                {
                    if (pair.Item1)
                    {
                        _owner._issues.IgnoredField($"{memberName}.{pair.Item2}");
                    }
                }
            }

            public Enumerator GetEnumerator() => new Enumerator(this, _wrapped);

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

            sealed class Comparer : IComparer<(bool, string)>
            {
                internal static readonly Comparer Instance = new Comparer();

                int IComparer<(bool, string)>.Compare((bool, string) x, (bool, string) y)
                    => string.CompareOrdinal(x.Item2, y.Item2);
            }

            public struct Enumerator
            {
                readonly LottieJsonObjectElement _owner;
                readonly IEnumerator<KeyValuePair<string, JToken>> _wrapped;

                internal Enumerator(LottieJsonObjectElement owner, JObject wrapped)
                {
                    _owner = owner;
                    _wrapped = wrapped.GetEnumerator();
                }

                public KeyValuePair<string, LottieJsonElement> Current
                    => new KeyValuePair<string, LottieJsonElement>(
                            _wrapped.Current.Key,
                            new LottieJsonElement(_owner._owner, _wrapped.Current.Value));

                public void Dispose() => _wrapped.Dispose();

                public bool MoveNext() => _wrapped.MoveNext();
            }
        }
    }
}