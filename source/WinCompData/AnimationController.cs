// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(6)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class AnimationController : CompositionObject
    {
        internal AnimationController(CompositionObject targetObject, string targetProperty)
        {
            TargetObject = targetObject;
            TargetProperty = targetProperty;
        }

        internal AnimationController()
        {
        }

        // AnimationController can be created separately from an composition animation,
        // in this case it will be marked as "Custom", it does not have TargetObject and TargetProperty.
        // Custom controller should be configured only once and then can be used for many animations.
        public bool IsCustom => TargetObject is null;

        public CompositionObject? TargetObject { get; }

        public string? TargetProperty { get; }

        public bool IsPaused { get; private set; }

        public void Pause()
        {
            IsPaused = true;
        }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.AnimationController;
    }
}
