// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionPropertySet : CompositionObject
    {
        readonly Dictionary<string, float> _scalarProperties = new Dictionary<string, float>();
        readonly Dictionary<string, Vector2> _vector2Properties = new Dictionary<string, Vector2>();

        internal CompositionPropertySet(CompositionObject owner)
        {
            Owner = owner;
        }

        public CompositionObject Owner { get; }

        public void InsertScalar(string name, float value) => _scalarProperties.Add(name, value);

        public void InsertVector2(string name, Vector2 value) => _vector2Properties.Add(name, value);

        public IEnumerable<KeyValuePair<string, float>> ScalarProperties => _scalarProperties;

        public IEnumerable<KeyValuePair<string, Vector2>> Vector2Properties => _vector2Properties;

        public IEnumerable<string> PropertyNames => _scalarProperties.Keys.Concat(_vector2Properties.Keys);

        public bool IsEmpty => _scalarProperties.Count + _vector2Properties.Count == 0;

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionPropertySet;
    }
}
