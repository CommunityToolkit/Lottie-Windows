// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class LottieObject
    {
        private protected LottieObject(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public abstract LottieObjectType ObjectType { get; }
    }
}
