// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer
{
    /// <summary>
    /// Thrown when a buffer is not a well formed serialized composition.
    /// </summary>
    /// <remarks>
    /// Buffers are untrusted input, so every failure to read one is reported as this
    /// exception rather than being allowed to surface as an index or cast failure.
    /// </remarks>
#if PUBLIC_CompDataFlatbuffer
    public
#endif
    sealed class FlatBufferFormatException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FlatBufferFormatException"/> class.
        /// </summary>
        /// <param name="message">A description of what was wrong with the buffer.</param>
        public FlatBufferFormatException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FlatBufferFormatException"/> class.
        /// </summary>
        /// <param name="message">A description of what was wrong with the buffer.</param>
        /// <param name="innerException">The failure that revealed the problem.</param>
        public FlatBufferFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
