// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        internal readonly struct LottieJsonArrayElement
        {
            readonly LottieCompositionReader _owner;
            readonly JsonElement _wrapped;

            internal LottieJsonArrayElement(LottieCompositionReader owner, JsonElement wrapped)
            {
                Debug.Assert(wrapped.ValueKind == JsonValueKind.Array, "Precondition");
                _owner = owner;
                _wrapped = wrapped;
            }

            internal int Count => _wrapped.GetArrayLength();

            public LottieJsonElement this[int index] => new LottieJsonElement(_owner, _wrapped[index]);

            public Vector2? AsVector2()
            {
                double? x = null;
                double? y = null;
                for (var i = 0; i < Count; i++)
                {
                    switch (i)
                    {
                        case 0:
                            x = this[0].AsDouble();
                            break;
                        case 1:
                            y = this[1].AsDouble();
                            break;
                    }
                }

                return x is null
                    ? (Vector2?)null
                    : new Vector2(x.Value, y ?? 0);
            }

            public Vector3? AsVector3()
            {
                double? x = null;
                double? y = null;
                double? z = null;
                for (var i = 0; i < Count; i++)
                {
                    switch (i)
                    {
                        case 0:
                            x = this[0].AsDouble();
                            break;
                        case 1:
                            y = this[1].AsDouble();
                            break;
                        case 2:
                            z = this[2].AsDouble();
                            break;
                    }
                }

                return x is null
                    ? (Vector3?)null
                    : new Vector3(x.Value, y ?? 0, z ?? 0);
            }

            public Enumerator GetEnumerator() => new Enumerator(this);

            internal T[] Select<T>(LottieJsonElementReader<T> reader)
            {
                var count = Count;
                var result = new T[count];
                for (var i = 0; i < count; i++)
                {
                    result[i] = reader(this[i]);
                }

                return result;
            }

            internal T[] Select<T>(Func<LottieJsonElement, T> reader)
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

                public bool MoveNext() => _owner._wrapped.GetArrayLength() > ++_currentIndex;

                public void Dispose()
                {
                }
            }
        }
    }
}