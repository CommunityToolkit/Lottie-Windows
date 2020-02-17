// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        internal readonly struct LottieJsonArrayElement
        {
            readonly LottieCompositionReader _owner;
            readonly JArray _wrapped;

            internal LottieJsonArrayElement(LottieCompositionReader owner, JToken wrapped)
            {
                Debug.Assert(wrapped.Type == JTokenType.Array, "Precondition");
                _owner = owner;
                _wrapped = (JArray)wrapped;
            }

            internal int Count => _wrapped.Count;

            internal LottieJsonElement this[int index] => new LottieJsonElement(_owner, _wrapped[index]);

            public Enumerator GetEnumerator() => new Enumerator(this);

            internal T[] Select<T>(LottieJsonElementReader<T> reader)
            {
                var count = Count;
                var result = new T[count];
                for (var i = 0; i < Count; i++)
                {
                    result[i] = reader(this[i]);
                }

                return result;
            }

            internal struct Enumerator
            {
                readonly LottieJsonArrayElement _owner;
                int _currentIndex;

                internal Enumerator(LottieJsonArrayElement owner)
                {
                    _owner = owner;
                    _currentIndex = -1;
                }

                public LottieJsonElement Current => new LottieJsonElement(_owner._owner, _owner._wrapped[_currentIndex]);

                public bool MoveNext() => _owner._wrapped.Count > ++_currentIndex;

                public void Dispose()
                {
                }
            }
        }
    }
}