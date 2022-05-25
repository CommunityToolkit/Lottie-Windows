// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.YamlData
{
    /// <summary>
    /// A sequence of objects.
    /// </summary>
#if PUBLIC_YamlData
    public
#endif
    sealed class YamlSequence : YamlObject, IEnumerable<YamlObject>
    {
        readonly List<YamlObject> _sequence = new List<YamlObject>();

        /// <summary>
        /// Appends the given object to the sequence.
        /// </summary>
        /// <param name="obj">The object to append.</param>
        public void Add(YamlObject obj)
        {
            _sequence.Add(obj);
        }

        internal override YamlObjectKind Kind => YamlObjectKind.Sequence;

        IEnumerator<YamlObject> IEnumerable<YamlObject>.GetEnumerator()
        {
            return ((IEnumerable<YamlObject>)_sequence).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<YamlObject>)this).GetEnumerator();
    }
}