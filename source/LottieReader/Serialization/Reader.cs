// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    // A type that has a similar interface to System.Text.Json.Utf8JsonReader.
    // This type helps to hide the choice of Newtonsoft.Json vs System.Text.Json.
    ref struct Reader
    {
        readonly JsonReader _reader;

        internal Reader(JsonReader reader)
        {
            _reader = reader;
        }

        internal JsonToken TokenType => _reader.TokenType;

        internal bool Read() => _reader.Read();

        internal void Skip() => _reader.Skip();

        internal bool GetBoolean() => (bool)_reader.Value;

        internal double GetDouble() => (double)_reader.Value;

        internal long GetInt64() => (long)_reader.Value;

        internal string GetString() => (string)_reader.Value;

        // Not part of System.Text.Json. This is a backdoor to get access to the
        // underlying Newtonsoft reader while we transition the code more to
        // System.Text.Json patterns.
        internal JsonReader NewtonsoftReader => _reader;
    }
}