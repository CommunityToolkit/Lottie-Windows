// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    // Allows incrementing and decrementing 32 bit floats by the smallest possible value.
    // Used to get the next larger or smaller float.
    [StructLayout(LayoutKind.Explicit)]
    struct Float32
    {
        [FieldOffset(0)]
        public float _valueF;
        [FieldOffset(0)]
        public int _valueI;

        Float32(float value)
        {
            _valueI = 0;
            _valueF = value;
        }

        /// <summary>
        /// Returns the largest value which is less than <paramref name="value"/>.
        /// Does not handle NaN or Infinity.
        /// </summary>
        /// <returns>The largest value which is less than <paramref name="value"/>.</returns>
        internal static float PreviousSmallerThan(float value)
        {
            var temp = new Float32(value);

            // Decrementing the integer representation gives the previous float.
            temp._valueI--;
            return temp._valueF;
        }

        /// <summary>
        /// Returns the smallest value which is larger than <paramref name="value"/>.
        /// Does not handle NaN or Infinity.
        /// </summary>
        /// <returns>The smallest value which is larger than <paramref name="value"/>.</returns>
        internal static float NextLargerThan(float value)
        {
            var temp = new Float32(value);

            // Incrementing the integer representation gives the next float.
            temp._valueI++;
            return temp._valueF;
        }
    }
}
