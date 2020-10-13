// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.GenericData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class GenericDataMap : GenericDataObject, IReadOnlyDictionary<string, GenericDataObject?>
    {
        readonly Dictionary<string, GenericDataObject?> _items;

        GenericDataMap(IDictionary<string, GenericDataObject?> items)
        {
            _items = items.ToDictionary(x => x.Key, x => x.Value);
        }

        public static GenericDataMap Create(IDictionary<string, GenericDataObject?> items)
            => items.Count == 0
                ? Empty
                : new GenericDataMap(items);

        public static GenericDataMap Empty { get; } = new GenericDataMap(new Dictionary<string, GenericDataObject?>(0));

        public GenericDataObject? this[string key] => _items[key];

        public override GenericDataObjectType Type => GenericDataObjectType.Map;

        public IEnumerable<string> Keys => ((IReadOnlyDictionary<string, GenericDataObject>)_items).Keys;

        public IEnumerable<GenericDataObject> Values => ((IReadOnlyDictionary<string, GenericDataObject>)_items).Values;

        public int Count => _items.Count;

        public bool ContainsKey(string key) => _items.ContainsKey(key);

        public IEnumerator<KeyValuePair<string, GenericDataObject?>> GetEnumerator() => ((IReadOnlyDictionary<string, GenericDataObject?>)_items).GetEnumerator();

        public bool TryGetValue(string key, out GenericDataObject? value) => _items.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => ((IReadOnlyDictionary<string, GenericDataObject?>)_items).GetEnumerator();

        public override string ToString()
            => _items.Count == 0
                ? "{}"
                : $"{{{string.Join(", ", _items.Select(p => $"\"{p.Key}\":{ToString(p.Value)}"))}}}";

        public static implicit operator GenericDataMap(Dictionary<string, GenericDataObject?> value) => Create(value);
    }
}