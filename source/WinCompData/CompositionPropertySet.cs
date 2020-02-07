// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    // CompositionPropertySet was introduced in version 2. Boolean types
    // were added in version 3.
    [UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionPropertySet : CompositionObject
    {
        static readonly SortedDictionary<string, PropertySetValueType> s_empty = new SortedDictionary<string, PropertySetValueType>();

        // Initialized to an empty sentinel value, then replaced with a new object
        // when the first value is added.
        SortedDictionary<string, PropertySetValueType> _names = s_empty;
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

        public void InsertColor(string propertyName, Color value)
            => Insert(propertyName, in value, PropertySetValueType.Color, ref _colorProperties);

        public void InsertScalar(string propertyName, float value)
            => Insert(propertyName, in value, PropertySetValueType.Scalar, ref _scalarProperties);

        public void InsertVector2(string propertyName, Vector2 value)
            => Insert(propertyName, in value, PropertySetValueType.Vector2, ref _vector2Properties);

        public void InsertVector3(string propertyName, Vector3 value)
            => Insert(propertyName, in value, PropertySetValueType.Vector3, ref _vector3Properties);

        public void InsertVector4(string propertyName, Vector4 value)
            => Insert(propertyName, in value, PropertySetValueType.Vector4, ref _vector4Properties);

        public CompositionGetValueStatus TryGetColor(string propertyName, out Color value)
            => TryGet(propertyName, PropertySetValueType.Color, ref _colorProperties, out value);

        public CompositionGetValueStatus TryGetScalar(string propertyName, out float value)
            => TryGet(propertyName, PropertySetValueType.Scalar, ref _scalarProperties, out value);

        public CompositionGetValueStatus TryGetVector2(string propertyName, out Vector2 value)
            => TryGet(propertyName, PropertySetValueType.Vector2, ref _vector2Properties, out value);

        public CompositionGetValueStatus TryGetVector3(string propertyName, out Vector3 value)
            => TryGet(propertyName, PropertySetValueType.Vector3, ref _vector3Properties, out value);

        public CompositionGetValueStatus TryGetVector4(string propertyName, out Vector4 value)
            => TryGet(propertyName, PropertySetValueType.Vector4, ref _vector4Properties, out value);

        /// <summary>
        /// Returns the names and types of the values that have been added to this <see cref="CompositionPropertySet"/>.
        /// The results are ordered by name.
        /// </summary>
        public IReadOnlyDictionary<string, PropertySetValueType> Names => _names;

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionPropertySet;

        // For debugging purposes only. Show the list of names in the PropertySet.
        public override string ToString()
        {
            var entries = _names.Count == 0
                ? "<none>"
                : string.Join(", ", _names.Select(entry => $"{entry.Value}:{entry.Key}"));
            return $"[{entries}]";
        }

        void Insert<T>(string propertyName, in T value, PropertySetValueType type, ref PropertyBag<T> bag)
        {
            Debug.Assert(type != PropertySetValueType.None, "Precondition");

            // Ensure the names set exists.
            if (_names == s_empty)
            {
                // CompositionPropertySet ignores the case of property names.
                _names = new SortedDictionary<string, PropertySetValueType>(StringComparer.OrdinalIgnoreCase);
            }

            // Try to add the name to the set of names.
            if (_names.TryGetValue(propertyName, out var existingPropertyType))
            {
                // The name already existed. That's ok as long the name is associated
                // with the correct type.
                if (existingPropertyType != type)
                {
                    throw new ArgumentException();
                }
            }
            else
            {
                _names.Add(propertyName, type);
            }

            // Set the value.
            bag.SetValue(propertyName, in value);
        }

        CompositionGetValueStatus TryGet<T>(string propertyName, PropertySetValueType type, ref PropertyBag<T> bag, out T value)
        {
            if (!_names.TryGetValue(propertyName, out var existingPropertyType))
            {
                // The name isn't in this property set.
                value = default(T);
                return CompositionGetValueStatus.NotFound;
            }

            // The name is in the property set - does it refer to the right type?
            if (existingPropertyType != type)
            {
                // The name is in the property set, but not for this type.
                value = default(T);
                return CompositionGetValueStatus.TypeMismatch;
            }

            if (!bag.TryGetValue(propertyName, out value))
            {
                throw new InvalidOperationException();
            }

            return CompositionGetValueStatus.Succeeded;
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
        }
    }
}
