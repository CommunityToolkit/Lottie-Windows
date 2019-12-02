// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionObject : IDisposable, IDescribable
    {
        // Keys used to identify the short description and long description metadata.
        static readonly Guid s_shortDescriptionMetadataKey = new Guid("AF01D303-5572-4540-A4AC-E48F1394E1D1");
        static readonly Guid s_longDescriptionMetadataKey = new Guid("63514254-2B3E-4794-B01D-9F67D5946A7E");

        readonly ListOfNeverNull<Animator> _animators = new ListOfNeverNull<Animator>();

        // Null until the first metatadata is set.
        SortedDictionary<Guid, object> _metadata;

        private protected CompositionObject()
        {
            if (Type == CompositionObjectType.CompositionPropertySet)
            {
                // The property set on a property set is itself.
                Properties = (CompositionPropertySet)this;
            }
            else
            {
                Properties = new CompositionPropertySet(this);
            }
        }

        /// <summary>
        /// Associates metadata with the object. The type of metadata is indicated by the <paramref name="key"/>
        /// <see cref="Guid"/>. If metadata for the given <paramref name="key"/> is already associated with the
        /// object, the meatadata will be replaced with the new <paramref name="value"/>.
        /// </summary>
        /// <param name="key">A <see cref="Guid"/> that identifies that type of metadata.</param>
        /// <param name="value">The value of the metadata.</param>
        public void SetMetadata(in Guid key, object value)
        {
            if (_metadata == null)
            {
                _metadata = new SortedDictionary<Guid, object>();
            }

            _metadata[key] = value;
        }

        /// <summary>
        /// Returns the metadata associated with this object that is identified by the given
        /// <paramref name="key"/>, or null if no such metadata has been associated with this
        /// object yet.
        /// </summary>
        /// <param name="key">A <see cref="Guid"/> that identifies that type of metadata.</param>
        /// <returns>The metadata, or null.</returns>
        public object TryGetMetadata(in Guid key)
        {
            if (_metadata != null && _metadata.TryGetValue(key, out var result))
            {
                return result;
            }

            return null;
        }

        public string Comment { get; set; }

        /// <summary>
        /// Gets or sets a description of the object. This may be used to add comments to generated code.
        /// Cf. the <see cref="Comment"/> property which is a property on real composition
        /// objects that is used for debugging.
        /// </summary>
        string IDescribable.LongDescription
        {
            get => (string)TryGetMetadata(in s_longDescriptionMetadataKey);
            set => SetMetadata(in s_longDescriptionMetadataKey, value);
        }

        /// <summary>
        /// Gets or sets a description of the object. This may be used to add comments to generated code.
        /// Cf. the <see cref="Comment"/> property which is a property on real composition
        /// objects that is used for debugging.
        /// </summary>
        string IDescribable.ShortDescription
        {
            get => (string)TryGetMetadata(in s_shortDescriptionMetadataKey);
            set => SetMetadata(in s_shortDescriptionMetadataKey, value);
        }

        public CompositionPropertySet Properties { get; }

        /// <summary>
        /// Binds an animation to a property.
        /// </summary>
        /// <param name="target">The name of the property.</param>
        /// <param name="animation">The animation.</param>
        public void StartAnimation(string target, CompositionAnimation animation)
        {
            // Clone the animation so that the existing animation object can be reconfigured.
            // If the animation is frozen, it is safe to not do the clone.
            var clone = animation.IsFrozen ? animation : animation.Clone();

            var animator = new Animator
            {
                Animation = clone,
                AnimatedProperty = target,
                AnimatedObject = this,
            };

            if (!(animation is ExpressionAnimation))
            {
                animator.Controller = new AnimationController(this, target);
            }

            _animators.Add(animator);
        }

        /// <summary>
        /// Gets the animators that are bound to this object.
        /// </summary>
        public IReadOnlyList<Animator> Animators => _animators;

        public AnimationController TryGetAnimationController(string target) =>
            _animators.Where(a => a.AnimatedProperty == target).Single().Controller;

        public abstract CompositionObjectType Type { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <summary>
        /// An animation bound to a property on this object.
        /// </summary>
        public sealed class Animator
        {
            /// <summary>
            /// Gets the property being animated by this animator.
            /// </summary>
            public string AnimatedProperty { get; internal set; }

            /// <summary>
            /// Gets the object whose property is being animated by this animator.
            /// </summary>
            public CompositionObject AnimatedObject { get; internal set; }

            public CompositionAnimation Animation { get; internal set; }

            public AnimationController Controller { get; internal set; }

            /// <inheritdoc/>
            public override string ToString() => $"{Animation.Type} bound to {AnimatedProperty}";
        }
    }
}
