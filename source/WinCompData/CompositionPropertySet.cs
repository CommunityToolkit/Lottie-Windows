// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    // CompositionPropertySet was introduced in version 2. Boolean types
    // were added in version 3.
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionPropertySet : CompositionObject
    {
        HashSet<string> _names;
        PropertyBag<Color> _colorProperties;
        PropertyBag<float> _scalarProperties;
        PropertyBag<Vector2> _vector2Properties;
        PropertyBag<Vector3> _vector3Properties;
        PropertyBag<Vector4> _vector4Properties;

        internal CompositionPropertySet(CompositionObject owner)
        {
            Owner = owner;
        }

        public CompositionObject Owner { get; }

        public void InsertColor(string propertyName, Color value) => Insert(propertyName, in value, ref _colorProperties);

        public void InsertScalar(string propertyName, float value) => Insert(propertyName, in value, ref _scalarProperties);

        public void InsertVector2(string propertyName, Vector2 value) => Insert(propertyName, in value, ref _vector2Properties);

        public void InsertVector3(string propertyName, Vector3 value) => Insert(propertyName, in value, ref _vector3Properties);

        public void InsertVector4(string propertyName, Vector4 value) => Insert(propertyName, in value, ref _vector4Properties);

        public CompositionGetValueStatus TryGetColor(string propertyName, out Color value) => TryGet(propertyName, ref _colorProperties, out value);

        public CompositionGetValueStatus TryGetScalar(string propertyName, out float value) => TryGet(propertyName, ref _scalarProperties, out value);

        public CompositionGetValueStatus TryGetVector2(string propertyName, out Vector2 value) => TryGet(propertyName, ref _vector2Properties, out value);

        public CompositionGetValueStatus TryGetVector3(string propertyName, out Vector3 value) => TryGet(propertyName, ref _vector3Properties, out value);

        public CompositionGetValueStatus TryGetVector4(string propertyName, out Vector4 value) => TryGet(propertyName, ref _vector4Properties, out value);

        public IEnumerable<KeyValuePair<string, Color>> ColorProperties => _colorProperties.Entries;

        public IEnumerable<KeyValuePair<string, float>> ScalarProperties => _scalarProperties.Entries;

        public IEnumerable<KeyValuePair<string, Vector2>> Vector2Properties => _vector2Properties.Entries;

        public IEnumerable<KeyValuePair<string, Vector3>> Vector3Properties => _vector3Properties.Entries;

        public IEnumerable<KeyValuePair<string, Vector4>> Vector4Properties => _vector4Properties.Entries;

        public IEnumerable<string> PropertyNames => _names ?? Enumerable.Empty<string>();

        public bool IsEmpty => !(_names?.Count > 0);

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionPropertySet;

        void Insert<T>(string propertyName, in T value, ref PropertyBag<T> bag)
        {
            // Ensure the names set exists.
            if (_names is null)
            {
                // CompositionPropertySet ignores the case of property names.
                _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            // Try to add the name to the set of names.
            if (!_names.Add(propertyName))
            {
                // The name already existed. That's ok as long the name is associated
                // with the correct type.
                if (!bag.ContainsKey(propertyName))
                {
                    throw new ArgumentException();
                }
            }

            // Set the value.
            bag.SetValue(propertyName, in value);
        }

        CompositionGetValueStatus TryGet<T>(string propertyName, ref PropertyBag<T> bag, out T value)
        {
            if (_names is null || !_names.Contains(propertyName))
            {
                // The name isn't in this property set.
                value = default(T);
                return CompositionGetValueStatus.NotFound;
            }

            // The name is in the property set - does it refer to the right type?
            return bag.TryGetValue(propertyName, out value)
                ? CompositionGetValueStatus.Succeeded
                : CompositionGetValueStatus.TypeMismatch;
        }

        struct PropertyBag<T>
        {
            Dictionary<string, T> _dictionary;

            internal bool IsEmpty => (_dictionary?.Count ?? 0) == 0;

            internal void SetValue(string propertyName, in T value)
            {
                if (_dictionary == null)
                {
                    // CompositionPropertySet ignores the case of property names.
                    _dictionary = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
                }

                // Set or replace the value in the dictionary.
                _dictionary[propertyName] = value;
            }

            internal bool ContainsKey(string propertyName) => _dictionary?.ContainsKey(propertyName) ?? false;

            internal bool TryGetValue(string propertyName, out T value)
            {
                if (_dictionary is null)
                {
                    value = default(T);
                    return false;
                }

                return _dictionary.TryGetValue(propertyName, out value);
            }

            internal IEnumerable<string> Names => _dictionary?.Keys ?? Enumerable.Empty<string>();

            internal IEnumerable<KeyValuePair<string, T>> Entries
                => _dictionary ?? Enumerable.Empty<KeyValuePair<string, T>>();
        }
    }
}
