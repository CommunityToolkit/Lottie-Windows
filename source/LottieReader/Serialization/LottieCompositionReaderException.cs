// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace CommunityToolkit.WinUI.Lottie.LottieData.Serialization
{
    /// <summary>
    /// Exception thrown to indicate a problem reading a Lottie composition.
    /// </summary>
    sealed class LottieCompositionReaderException : Exception
    {
        public LottieCompositionReaderException()
        {
        }

        public LottieCompositionReaderException(string message)
            : base(message)
        {
        }

        public LottieCompositionReaderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
