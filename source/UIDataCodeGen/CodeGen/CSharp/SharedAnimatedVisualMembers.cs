// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen.CSharp
{
    /// <summary>
    /// Finds the code that is the same in every AnimatedVisual class of an IAnimatedVisualSource that
    /// has several versions, so that it can be moved into an abstract base class that the classes
    /// derive from and exist only once.
    /// </summary>
    /// <remarks>
    /// The behavior of every AnimatedVisual class is unchanged:
    /// <list type="bullet">
    /// <item>a method is shared only if it has the same attribute, signature and code in every class;</item>
    /// <item>the fields that a shared method uses are moved to the base class with the same declaration,
    /// so all of the code of a class still uses a single storage location for each field;</item>
    /// <item>the methods that a shared method calls and that differ between the classes are declared
    /// abstract in the base class, and overridden by each class.</item>
    /// </list>
    /// This works on the <see cref="AnimatedVisualClassMembers"/> that the generator records for each
    /// class, so the classes are compared by their members and not by their text: each method records
    /// the fields and methods that it uses as the generator writes their names.
    /// </remarks>
    sealed class SharedAnimatedVisualMembers
    {
        SharedAnimatedVisualMembers(
            IReadOnlyList<AnimatedVisualField> fields,
            IReadOnlyList<AnimatedVisualMethod> methods,
            IReadOnlyList<AnimatedVisualMethod> abstractMethods)
        {
            Fields = fields;
            Methods = methods;
            AbstractMethods = abstractMethods;
            FieldNames = new HashSet<string>(fields.Select(f => f.Name));
            MethodSignatures = new HashSet<string>(methods.Select(m => m.Signature));
            AbstractMethodSignatures = new HashSet<string>(abstractMethods.Select(m => m.Signature));
        }

        /// <summary>
        /// Gets the fields of the base class.
        /// </summary>
        internal IReadOnlyList<AnimatedVisualField> Fields { get; }

        /// <summary>
        /// Gets the methods of the base class that are implemented in it.
        /// </summary>
        internal IReadOnlyList<AnimatedVisualMethod> Methods { get; }

        /// <summary>
        /// Gets the methods that are abstract in the base class, and that every class overrides.
        /// </summary>
        internal IReadOnlyList<AnimatedVisualMethod> AbstractMethods { get; }

        HashSet<string> FieldNames { get; }

        HashSet<string> MethodSignatures { get; }

        HashSet<string> AbstractMethodSignatures { get; }

        /// <summary>
        /// Gets a value indicating whether the field is in the base class.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>True if the base class declares the field, and the class does not.</returns>
        internal bool IsInBaseClass(AnimatedVisualField field) => FieldNames.Contains(field.Name);

        /// <summary>
        /// Gets a value indicating whether the method is implemented in the base class.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>True if the base class implements the method, and the class does not.</returns>
        internal bool IsInBaseClass(AnimatedVisualMethod method) => MethodSignatures.Contains(method.Signature);

        /// <summary>
        /// Gets a value indicating whether the method overrides an abstract method of the base class.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>True if the base class declares the method abstract.</returns>
        internal bool IsOverride(AnimatedVisualMethod method) => AbstractMethodSignatures.Contains(method.Signature);

        /// <summary>
        /// Finds the members that the given classes can share.
        /// </summary>
        /// <param name="classes">The members of each class.</param>
        /// <returns>The shared members, or null if no method can be shared.</returns>
        internal static SharedAnimatedVisualMembers? Find(IReadOnlyList<AnimatedVisualClassMembers> classes)
        {
            if (classes.Count < 2)
            {
                return null;
            }

            // Methods are identified by their signature because helper methods can be overloaded.
            var methodsByClass = classes.Select(c => c.Methods.ToDictionary(m => m.Signature)).ToArray();
            var fieldsByClass = classes.Select(c => c.Fields.ToDictionary(f => f.Name)).ToArray();
            var first = classes[0];

            var signaturesByName = classes
                .SelectMany(c => c.Methods)
                .GroupBy(m => m.Name)
                .ToDictionary(g => g.Key, g => g.Select(m => m.Signature).Distinct().ToArray());

            // A field can be shared if it has the same declaration in every class, and if it can be declared
            // in a base class: a field that a constructor assigns cannot be read-only there, but any other
            // read-only field stays in the class.
            bool IsShareableField(string name)
                => fieldsByClass.All(d => d.TryGetValue(name, out var f) &&
                                          f.Declaration == fieldsByClass[0][name].Declaration &&
                                          (!f.IsReadonly || f.IsAssignedByConstructor));

            // The fields that every class has go to the base class with the first method that goes there,
            // as any method may use them. Their declarations must be the same in every class.
            var commonFields = first.Fields.Where(f => f.IsCommon).ToArray();
            if (!commonFields.All(f => IsShareableField(f.Name)))
            {
                return null;
            }

            // Start with the methods that have the same code in every class.
            var shared = new HashSet<string>(
                first.Methods
                    .Where(m => methodsByClass.All(d => d.TryGetValue(m.Signature, out var other) && other.Key == m.Key))
                    .Select(m => m.Signature));

            // A method that is used by shared code but differs between the classes must be overridable:
            // it must be an instance method with the same single line signature in every class.
            bool IsOverridable(string signature)
                => methodsByClass.All(d => d.TryGetValue(signature, out var m) &&
                                           !m.IsStatic &&
                                           m.HeaderLines.Count == 1);

            // The fields that a method uses, and the signatures of the methods that it calls. A use of the
            // name of an overloaded method is a use of any of the overloads.
            IEnumerable<string> UsedFields(string signature)
                => methodsByClass[0][signature].Uses.Fields.Where(name => !commonFields.Any(f => f.Name == name));
            IEnumerable<string> Callees(string signature)
                => methodsByClass[0][signature].Uses.Methods.Where(signaturesByName.ContainsKey).SelectMany(name => signaturesByName[name]);

            // Remove the methods that use members that cannot be shared, until nothing changes.
            bool changed;
            do
            {
                changed = false;
                foreach (var signature in shared.ToArray())
                {
                    if (!UsedFields(signature).All(IsShareableField) ||
                        !Callees(signature).All(callee => shared.Contains(callee) || IsOverridable(callee)))
                    {
                        shared.Remove(signature);
                        changed = true;
                    }
                }
            }
            while (changed);

            if (shared.Count == 0)
            {
                return null;
            }

            var abstractSignatures = new HashSet<string>(shared.SelectMany(Callees).Where(c => !shared.Contains(c)));
            var sharedFieldNames = new HashSet<string>(shared.SelectMany(UsedFields).Concat(commonFields.Select(f => f.Name)));

            return new SharedAnimatedVisualMembers(
                first.Fields.Where(f => sharedFieldNames.Contains(f.Name)).ToArray(),
                first.Methods.Where(m => shared.Contains(m.Signature)).ToArray(),
                first.Methods.Where(m => abstractSignatures.Contains(m.Signature)).ToArray());
        }
    }
}
