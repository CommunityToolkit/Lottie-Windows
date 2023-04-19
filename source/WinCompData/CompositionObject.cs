// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
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
        static readonly Guid s_nameMetadataKey = new Guid("6EB18A31-FA33-43B9-8EE1-57B489DC3404");

        readonly List<Animator> _animators = new List<Animator>();

        // Null until the first metadata is set.
        SortedDictionary<Guid, object>? _metadata;

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
        public void SetMetadata(in Guid key, object? value)
        {
            if (_metadata is null)
            {
                _metadata = new SortedDictionary<Guid, object>();
            }

            if (value is null)
            {
                _metadata.Remove(key);
            }
            else
            {
                _metadata[key] = value;
            }
        }

        /// <summary>
        /// Returns the metadata associated with this object that is identified by the given
        /// <paramref name="key"/>, or null if no such metadata has been associated with this
        /// object yet.
        /// </summary>
        /// <param name="key">A <see cref="Guid"/> that identifies that type of metadata.</param>
        /// <returns>The metadata, or null.</returns>
        public object? TryGetMetadata(in Guid key)
        {
            if (_metadata is object && _metadata.TryGetValue(key, out var result))
            {
                return result;
            }

            return null;
        }

        public string? Comment { get; set; }

#pragma warning disable CA1033 // Interface methods should be callable by child types

        /// <summary>
        /// Gets or sets a description of the object. This may be used to add comments to generated code.
        /// Cf. the <see cref="Comment"/> property which is a property on real composition
        /// objects that is used for debugging.
        /// </summary>
        string? IDescribable.LongDescription
        {
            get => (string?)TryGetMetadata(in s_longDescriptionMetadataKey);
            set => SetMetadata(in s_longDescriptionMetadataKey, value);
        }

        /// <summary>
        /// Gets or sets a description of the object. This may be used to add comments to generated code.
        /// Cf. the <see cref="Comment"/> property which is a property on real composition
        /// objects that is used for debugging.
        /// </summary>
        string? IDescribable.ShortDescription
        {
            get => (string?)TryGetMetadata(in s_shortDescriptionMetadataKey);
            set => SetMetadata(in s_shortDescriptionMetadataKey, value);
        }

        /// <summary>
        /// Gets or sets a name for the object. This may be used for variable names in generated code.
        /// </summary>
        string? IDescribable.Name
        {
            get => (string?)TryGetMetadata(in s_nameMetadataKey);
            set => SetMetadata(in s_nameMetadataKey, value);
        }
#pragma warning restore CA1033 // Interface methods should be callable by child types

        public CompositionPropertySet Properties { get; }

        /// <summary>
        /// Binds an animation to a property with a given custom controller.
        /// </summary>
        /// <param name="target">The name of the property.</param>
        /// <param name="animation">The animation.</param>
        /// <param name="customController">Custom controller.</param>
        /// <returns>New animator.</returns>
        public Animator StartAnimation(string target, CompositionAnimation animation, AnimationController? customController)
        {
            // Remove any existing animation.
            StopAnimation(target);

            // Clone the animation so that the existing animation object can be reconfigured.
            // If the animation is frozen, it is safe to not do the clone.
            var clone = animation.IsFrozen ? animation : animation.Clone();

            var controller = animation is ExpressionAnimation
                ? null
                : new AnimationController(this, target);

            if (customController is not null)
            {
                Debug.Assert(customController.IsCustom, "Should be custom!");
                Debug.Assert(animation is not ExpressionAnimation, "Should not be ExpressionAnimation!");
                controller = customController;
            }

            var animator = new Animator(
                                animatedProperty: target,
                                animatedObject: this,
                                animation: clone,
                                controller: controller);

            _animators.Add(animator);
            return animator;
        }

        /// <summary>
        /// Binds an animation to a property.
        /// </summary>
        /// <param name="target">The name of the property.</param>
        /// <param name="animation">The animation.</param>
        /// <returns>New animator.</returns>
        public Animator StartAnimation(string target, CompositionAnimation animation) => StartAnimation(target, animation, null);

        /// <summary>
        /// Stops an animation that was previously started.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        public void StopAnimation(string propertyName)
        {
            // We also need to stop animations on any sub-channels and the root property.
            // Examples:
            //  Sub-channels: stopping Offset must also stop
            //    Offset.X, Offset.Y, etc..
            //  Root property: stopping Offset.X must also
            //    stop Offset.
            // If there's a dot in the name it is a sub-channel name.
            var subChannelPrefix = $"{propertyName}.";
            var firstDotIndex = propertyName.IndexOf('.');
            var rootPropertyName = $"{(firstDotIndex >= 0 ? propertyName.Substring(0, firstDotIndex) : string.Empty)}.";

            for (var i = 0; i < _animators.Count; i++)
            {
                var animatorPropertyName = _animators[i].AnimatedProperty;

                if (animatorPropertyName == propertyName ||
                    animatorPropertyName == rootPropertyName ||
                    animatorPropertyName.StartsWith(subChannelPrefix))
                {
                    _animators.RemoveAt(i);

                    // Adjust the iteration variable to ensure we don't miss the
                    // animator just after the one we just removed.
                    i--;
                }
            }
        }

        /// <summary>
        /// Gets the animators that are bound to this object.
        /// </summary>
        public IReadOnlyList<Animator> Animators => _animators;

        public AnimationController? TryGetAnimationController(string target) =>
            _animators.Where(a => a.AnimatedProperty == target).SingleOrDefault()?.Controller;

        public abstract CompositionObjectType Type { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public override string ToString() => Type.ToString();

        /// <summary>
        /// An animation bound to a property on this object.
        /// </summary>
        public sealed class Animator
        {
            internal Animator(
                string animatedProperty,
                CompositionObject animatedObject,
                CompositionAnimation animation,
                AnimationController? controller)
            {
                AnimatedProperty = animatedProperty;
                AnimatedObject = animatedObject;
                Animation = animation;
                Controller = controller;
            }

            /// <summary>
            /// Gets the property being animated by this animator.
            /// This could be the name of a property on the object, the name
            /// of a property in the <see cref="CompositionPropertySet"/> of the
            /// object, or a dotted path to a property of a property on the object
            /// or in the <see cref="CompositionPropertySet"/> of the object.
            /// </summary>
            public string AnimatedProperty { get; }

            /// <summary>
            /// Gets the object whose property is being animated by this animator.
            /// </summary>
            public CompositionObject AnimatedObject { get; }

            public CompositionAnimation Animation { get; }

            /// <summary>
            /// The controller for this <see cref="Animator"/> or null
            /// if the animation is an <see cref="ExpressionAnimation"/>.
            /// </summary>
            public AnimationController? Controller { get; private set; }

            /// <inheritdoc/>
            public override string ToString() => AnimatedProperty;
        }
    }
}
