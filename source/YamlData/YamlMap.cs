// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.YamlData
{
    /// <summary>
    /// A list of named properties.
    /// </summary>
#if PUBLIC_YamlData
    public
#endif
    sealed class YamlMap : YamlObject, IEnumerable<(string key, YamlObject value)>
    {
        readonly List<(string key, YamlObject value)> _keysAndValues = new List<(string key, YamlObject value)>();
        readonly HashSet<string> _keys = new HashSet<string>();

        /// <summary>
        /// Adds the given value to the map.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <param name="value">The property value.</param>
        /// <exception cref="InvalidOperationException">The property name has already been added.</exception>
        public void Add(string key, YamlObject value)
        {
            if (!_keys.Add(key))
            {
                throw new InvalidOperationException();
            }

            _keysAndValues.Add((key, value));
        }

        /// <summary>
        /// Adds the given scalar to the map.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <param name="value">The property value.</param>
        /// <exception cref="InvalidOperationException">The property name has already been added.</exception>
        public void Add(string key, YamlScalar value) => Add(key, (YamlObject)value);

        IEnumerator<(string key, YamlObject value)> IEnumerable<(string key, YamlObject value)>.GetEnumerator()
            => _keysAndValues.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<(string key, YamlObject value)>)this).GetEnumerator();

        internal override YamlObjectKind Kind => YamlObjectKind.Map;
    }
}