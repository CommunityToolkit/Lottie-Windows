// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        internal readonly struct LottieJsonObjectElement
        {
            readonly LottieCompositionReader _owner;
            readonly (bool unread, JsonProperty property)[] _properties;

            internal LottieJsonObjectElement(LottieCompositionReader owner, JsonElement wrapped)
            {
                Debug.Assert(wrapped.ValueKind == JsonValueKind.Object, "Precondition");
                _owner = owner;

                // Get the properties.
                _properties = wrapped.EnumerateObject().Select(jp => (true, jp)).ToArray();

                // Sort the names so they can be binary searched.
                Array.Sort(_properties, Comparer.Instance);
            }

            public Vector2? AsVector2()
            {
                var x = DoublePropertyOrNull("x");
                var y = DoublePropertyOrNull("y");
                return x is null
                    ? (Vector2?)null
                    : new Vector2(x.Value, y ?? 0);
            }

            // An array of Vector2 stored as 2 arrays of equal-length
            // "x" and "y" values.
            public Vector2[] AsVector2Array()
            {
                var xs = ArrayPropertyOrNull("x");
                var ys = ArrayPropertyOrNull("y");

                var length = Math.Min(xs?.Count ?? 0, ys?.Count ?? 0);

                var result = new Vector2[length];

                for (var i = 0; i < length; i++)
                {
                    result[i] = new Vector2(xs.Value[i].AsDouble() ?? 0.0, ys.Value[i].AsDouble() ?? 0.0);
                }

                return result;
            }

            public Vector3? AsVector3()
            {
                var x = DoublePropertyOrNull("x");
                var y = DoublePropertyOrNull("y");
                var z = DoublePropertyOrNull("z");
                return x is null
                    ? (Vector3?)null
                    : new Vector3(x.Value, y ?? 0, z ?? 0);
            }

            internal LottieJsonArrayElement? ArrayPropertyOrNull(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsArray() : null;

            internal bool? BoolPropertyOrNull(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsBoolean() : null;

            internal double? DoublePropertyOrNull(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsDouble() : null;

            internal int? Int32PropertyOrNull(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsInt32() : null;

            internal LottieJsonObjectElement? ObjectPropertyOrNull(string propertyName)
                => TryGetProperty(propertyName, out var value) ? value.AsObject() : null;

            internal string StringPropertyOrNull(string propertyName)
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
                var propertyIndex = FindProperty(propertyName);
                if (propertyIndex is null)
                {
                    value = default(LottieJsonElement);
                    return false;
                }
                else
                {
                    ref var property = ref _properties[propertyIndex.Value];
                    property.unread = false;
                    value = new LottieJsonElement(_owner, property.property.Value);
                    return true;
                }
            }

            internal void AssertAllPropertiesRead([CallerMemberName] string memberName = "")
            {
                foreach (var pair in _properties)
                {
                    if (pair.unread)
                    {
                        _owner._issues.UnexpectedField($"{memberName}.{pair.property.Name}");
                    }
                }
            }

            public Enumerator GetEnumerator() => new Enumerator(this);

            // Marks the given property name as being read.
            void MarkPropertyAsRead(string propertyName)
            {
                var propertyIndex = FindProperty(propertyName);
                if (propertyIndex.HasValue)
                {
                    _properties[propertyIndex.Value].unread = false;
                }
            }

            // Returns the index of the given property, or null if not found.
            int? FindProperty(string propertyName)
            {
                int min = 0;
                int max = _properties.Length - 1;
                while (min <= max)
                {
                    int mid = (min + max) / 2;
                    var current = _properties[mid].property.Name;
                    if (propertyName == current)
                    {
                        return mid;
                    }
                    else if (string.CompareOrdinal(propertyName, current) < 0)
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
                return null;
            }

            sealed class Comparer : IComparer<(bool, JsonProperty)>
            {
                internal static readonly Comparer Instance = new Comparer();

                int IComparer<(bool, JsonProperty)>.Compare((bool, JsonProperty) x, (bool, JsonProperty) y)
                    => string.CompareOrdinal(x.Item2.Name, y.Item2.Name);
            }

            public struct Enumerator
            {
                readonly LottieJsonObjectElement _owner;
                int _currentIndex;

                internal Enumerator(LottieJsonObjectElement owner)
                {
                    _owner = owner;
                    _currentIndex = -1;
                }

                public KeyValuePair<string, LottieJsonElement> Current
                {
                    get
                    {
                        ref var property = ref _owner._properties[_currentIndex].property;
                        return new KeyValuePair<string, LottieJsonElement>(
                            property.Name,
                            new LottieJsonElement(_owner._owner, property.Value));
                    }
                }

                public bool MoveNext() => _owner._properties.Length > ++_currentIndex;
            }
        }
    }
}