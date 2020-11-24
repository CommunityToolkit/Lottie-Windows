// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// An effect applied to a layer.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    abstract class Effect : LottieObject
    {
        private protected Effect(
            string name,
            bool isEnabled)
            : base(name)
        {
            IsEnabled = isEnabled;
        }

        /// <summary>
        /// True iff the effect is enabled.
        /// </summary>
        public bool IsEnabled { get; }

        public override sealed LottieObjectType ObjectType => LottieObjectType.Effect;

        public abstract EffectType Type { get; }

        public enum EffectType
        {
            // Type = 25.
            DropShadow,

            // Type = 29.
            GaussianBlur,
        }
    }
}