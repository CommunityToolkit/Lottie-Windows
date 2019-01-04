// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    sealed class YamlMap : YamlObject, IEnumerable<(string key, YamlObject value)>
    {
        readonly List<(string key, YamlObject value)> _keysAndValues = new List<(string key, YamlObject value)>();
        readonly HashSet<string> _keys = new HashSet<string>();

        internal YamlMap()
        {
        }

        public IEnumerator<(string key, YamlObject value)> GetEnumerator()
            => _keysAndValues.GetEnumerator();

        internal override YamlObjectKind Kind => YamlObjectKind.Map;

        internal void Add(string key, YamlScalar value) => Add(key, (YamlObject)value);

        internal void Add(string key, YamlObject value)
        {
            if (!_keys.Add(key))
            {
                throw new InvalidOperationException();
            }

            _keysAndValues.Add((key, value));
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}