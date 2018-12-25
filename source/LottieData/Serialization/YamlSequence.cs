// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    sealed class YamlSequence : YamlObject, IEnumerable<YamlObject>
    {
        readonly List<YamlObject> _sequence = new List<YamlObject>();

        public IEnumerator<YamlObject> GetEnumerator()
        {
            return ((IEnumerable<YamlObject>)_sequence).GetEnumerator();
        }

        internal override YamlObjectKind Kind => YamlObjectKind.Sequence;

        internal void Add(YamlObject obj)
        {
            _sequence.Add(obj);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}