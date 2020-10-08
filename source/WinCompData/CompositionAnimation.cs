// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionAnimation : CompositionObject
    {
        readonly SortedDictionary<string, CompositionObject> _referencedParameters =
            new SortedDictionary<string, CompositionObject>();

        private protected CompositionAnimation(CompositionAnimation? other)
        {
            if (other != null)
            {
                foreach (var pair in other._referencedParameters)
                {
                    _referencedParameters.Add(pair.Key, pair.Value);
                }

                Target = other.Target;

                ((IDescribable)this).LongDescription = ((IDescribable)other).LongDescription;
                ((IDescribable)this).ShortDescription = ((IDescribable)other).ShortDescription;
                ((IDescribable)this).Name = ((IDescribable)other).Name;
                Comment = other.Comment;
            }
        }

        public string? Target { get; set; }

        // True iff this object's state is expected to never change.
        public bool IsFrozen { get; private set; }

        /// <summary>
        /// Marks the <see cref="CompositionAnimation"/> to indicate that its state
        /// should never change again. Note that this is a weak guarantee as there
        /// are not checks on all mutators to ensure that changes aren't made after
        /// freezing. However correct code must never mutate a frozen object.
        /// </summary>
        public void Freeze()
        {
            IsFrozen = true;
        }

        public void SetReferenceParameter(string key, CompositionObject compositionObject)
        {
            if (IsFrozen)
            {
                throw new InvalidOperationException();
            }

            _referencedParameters.Add(key, compositionObject);
        }

        /// <summary>
        /// Returns the reference parameters that have been set on this <see cref="CompositionAnimation"/>.
        /// The list is returned ordered alphabetically by key.
        /// </summary>
        public IReadOnlyCollection<KeyValuePair<string, CompositionObject>> ReferenceParameters => _referencedParameters;

        internal abstract CompositionAnimation Clone();
    }
}
