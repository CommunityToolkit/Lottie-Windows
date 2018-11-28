// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    abstract class LottieObject
    {
        protected private LottieObject(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public abstract LottieObjectType ObjectType { get; }
    }
}
