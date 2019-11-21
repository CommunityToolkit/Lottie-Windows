// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.GenericData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class GenericDataList : GenericDataObject, IReadOnlyList<GenericDataObject>
    {
        static GenericDataList s_empty;
        readonly IReadOnlyList<GenericDataObject> _items;

        GenericDataList(IEnumerable<GenericDataObject> items)
        {
            _items = items.ToArray();
        }

        public static GenericDataList Create(IEnumerable<GenericDataObject> items)
        {
            var result = new GenericDataList(items);
            return result._items.Count == 0
                    ? Empty
                    : result;
        }

        public static GenericDataList Empty => s_empty ?? (s_empty = new GenericDataList(new GenericDataObject[0]));

        public GenericDataObject this[int index] => _items[index];

        public override GenericDataObjectType Type => GenericDataObjectType.List;

        public int Count => _items.Count;

        public IEnumerator<GenericDataObject> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public override string ToString()
            => Count == 0
                ? "[]"
                : $"[{string.Join(", ", _items.Select(x => ToString(x)))}]";
    }
}