// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class AnimationController : CompositionObject
    {
        internal AnimationController(CompositionObject targetObject, string targetProperty)
        {
            TargetObject = targetObject;
            TargetProperty = targetProperty;
        }

        public CompositionObject TargetObject { get; }

        public string TargetProperty { get; }

        public bool IsPaused { get; private set; }

        public void Pause()
        {
            IsPaused = true;
        }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.AnimationController;
    }
}
