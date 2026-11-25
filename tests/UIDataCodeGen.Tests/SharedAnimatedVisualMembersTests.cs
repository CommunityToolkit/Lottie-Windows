// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.UIData.CodeGen.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen.Tests
{
    [TestClass]
    public sealed class SharedAnimatedVisualMembersTests
    {
        [TestMethod]
        public void IdenticalMethodsAndTheFieldsTheyUseAreShared()
        {
            var shared = Find(
                Class(Root(), Factory("ShapeVisual_0", size: 48)),
                Class(Root(), Factory("ShapeVisual_0", size: 48)));

            CollectionAssert.AreEqual(new[] { "Root", "ShapeVisual_0" }, shared.Methods.Select(m => m.Name).ToArray());
            Assert.AreEqual(0, shared.AbstractMethods.Count);

            // The Compositor is in every class, and _root is used by a shared method. The other field stays.
            CollectionAssert.AreEqual(new[] { "_c", "_root" }, shared.Fields.Select(f => f.Name).ToArray());
        }

        [TestMethod]
        public void MethodsThatDifferAreAbstractInTheBaseClassAndOverriddenByTheClasses()
        {
            var classes = new[]
            {
                Class(Root(), Factory("ShapeVisual_0", size: 48)),
                Class(Root(), Factory("ShapeVisual_0", size: 24)),
            };
            var shared = SharedAnimatedVisualMembers.Find(classes)!;

            CollectionAssert.AreEqual(new[] { "Root" }, shared.Methods.Select(m => m.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "ShapeVisual_0" }, shared.AbstractMethods.Select(m => m.Name).ToArray());
            Assert.IsTrue(classes.All(c => shared.IsOverride(c.Methods.Single(m => m.Name == "ShapeVisual_0"))));
            Assert.IsTrue(classes.All(c => shared.IsInBaseClass(c.Methods.Single(m => m.Name == "Root"))));

            var abstractDeclaration = new CodeBuilder();
            shared.AbstractMethods[0].WriteAbstractDeclaration(abstractDeclaration);
            Assert.AreEqual("internal abstract ShapeVisual ShapeVisual_0();", abstractDeclaration.ToString().Trim());
        }

        [TestMethod]
        public void MethodsThatUseFieldsDeclaredDifferentlyAreNotShared()
        {
            var shared = Find(
                Class(Root(), Factory("ShapeVisual_0", size: 48)),
                Class(new[] { Root(), Factory("ShapeVisual_0", size: 48) }, rootDeclaration: "Visual _root;"));

            // Root uses _root, which is declared differently, but ShapeVisual_0 does not.
            CollectionAssert.AreEqual(new[] { "ShapeVisual_0" }, shared.Methods.Select(m => m.Name).ToArray());
            Assert.IsFalse(shared.Fields.Any(f => f.Name == "_root"));
        }

        [TestMethod]
        public void ReadonlyFieldsOnlyMoveToTheBaseClassIfTheConstructorAssignsThem()
        {
            // A read-only field that the constructor of the class does not assign must stay in the class.
            var surface = new AnimatedVisualField("_surface", "readonly Surface _surface;", "Surface _surface;");
            var brush = Factory("Brush_0", size: 1, usesField: "_surface");
            Assert.IsNull(SharedAnimatedVisualMembers.Find(new[]
            {
                Class(new[] { brush }, extraField: surface),
                Class(new[] { brush }, extraField: surface),
            }));

            // The Compositor is read-only in the classes, but its constructor assigns it, so it can be in the base class.
            var shared = Find(Class(Factory("Brush_0", size: 1)), Class(Factory("Brush_0", size: 1)));
            Assert.IsTrue(shared.Fields.Single().IsAssignedByConstructor);
        }

        [TestMethod]
        public void NothingIsSharedIfTheCompositorFieldIsDeclaredDifferently()
        {
            Assert.IsNull(SharedAnimatedVisualMembers.Find(new[]
            {
                Class(new[] { Factory("ShapeVisual_0", size: 48) }, compositorDeclaration: "readonly Compositor _c;"),
                Class(new[] { Factory("ShapeVisual_0", size: 48) }, compositorDeclaration: "readonly Compositor2 _c;"),
            }));
        }

        [TestMethod]
        public void MethodsThatUseAnOverloadMissingInSomeClassesAreNotShared()
        {
            var createSpriteShape = Method("CreateSpriteShape", "CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry)", "return null;");
            var createSpriteShapeWithBrush = Method("CreateSpriteShape", "CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry, CompositionBrush brush)", "return null;");
            var spriteShape = Method("SpriteShape_0", "CompositionSpriteShape SpriteShape_0()", "return CreateSpriteShape(null);", usesMethod: "CreateSpriteShape");

            // Using CreateSpriteShape is using both overloads, and one of the classes has only one of them.
            var shared = Find(
                Class(createSpriteShape, createSpriteShapeWithBrush, spriteShape),
                Class(createSpriteShape, spriteShape));

            CollectionAssert.AreEqual(new[] { "CreateSpriteShape" }, shared.Methods.Select(m => m.Name).ToArray());
        }

        [TestMethod]
        public void AnOverloadThatDiffersIsOverriddenWhileTheMethodThatUsesItIsShared()
        {
            var createSpriteShape = Method("CreateSpriteShape", "CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry)", "return null;");
            var spriteShape = Method("SpriteShape_0", "CompositionSpriteShape SpriteShape_0()", "return CreateSpriteShape(null);", usesMethod: "CreateSpriteShape");
            var classes = new[]
            {
                Class(createSpriteShape, Method("CreateSpriteShape", "CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry, CompositionBrush brush)", "return A;"), spriteShape),
                Class(createSpriteShape, Method("CreateSpriteShape", "CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry, CompositionBrush brush)", "return B;"), spriteShape),
            };

            var shared = SharedAnimatedVisualMembers.Find(classes)!;

            CollectionAssert.AreEqual(new[] { "CreateSpriteShape", "SpriteShape_0" }, shared.Methods.Select(m => m.Name).ToArray());
            Assert.AreEqual(1, shared.AbstractMethods.Count);
            Assert.AreEqual(2, shared.AbstractMethods[0].HeaderLines.Single().Split(',').Length);
        }

        [TestMethod]
        public void OnlyInstanceMethodsWithASingleLineHeaderCanBeOverridden()
        {
            var caller = Method("Caller", "void Caller()", "Callee();", usesMethod: "Callee");

            // A static method that differs between the classes.
            Assert.IsNull(SharedAnimatedVisualMembers.Find(new[]
            {
                Class(caller, Method("Callee", "static void Callee()", "A();", isStatic: true)),
                Class(caller, Method("Callee", "static void Callee()", "B();", isStatic: true)),
            }));

            // A method that differs between the classes and has its parameters on separate lines.
            Assert.IsNull(SharedAnimatedVisualMembers.Find(new[]
            {
                Class(caller, Method("Callee", "void Callee(\n    int a)", "A();")),
                Class(caller, Method("Callee", "void Callee(\n    int a)", "B();")),
            }));
        }

        [TestMethod]
        public void AttributesMakeMethodsDifferentButCommentsDoNot()
        {
            var plain = Method("Helper", "void Helper()", "Run();");
            var withAttribute = Method("Helper", "void Helper()", "Run();", attribute: "[NoInlining]");
            var withComments = Method("Helper", "void Helper()", "Run();", comment: "A comment.", bodyComment: "Another comment.");

            Assert.IsNull(SharedAnimatedVisualMembers.Find(new[] { Class(plain), Class(withAttribute) }));
            Assert.AreEqual(1, Find(Class(plain), Class(withComments)).Methods.Count);
        }

        [TestMethod]
        public void NothingIsSharedWithoutIdenticalMethodsOrWithASingleClass()
        {
            Assert.IsNull(SharedAnimatedVisualMembers.Find(new[] { Class(Factory("ShapeVisual_0", size: 48)), Class(Factory("ShapeVisual_0", size: 24)) }));
            Assert.IsNull(SharedAnimatedVisualMembers.Find(new[] { Class(Root(), Factory("ShapeVisual_0", size: 48)) }));
        }

        [TestMethod]
        public void MethodsAreWrittenWithTheirCommentAttributeAndModifiers()
        {
            var method = Method("Helper", "void Helper(\n    int a)", "Run();", attribute: "[NoInlining]", comment: "Does things.");

            var builder = new CodeBuilder();
            method.Write(builder, "internal ");

            CollectionAssert.AreEqual(
                new[] { "// Does things.", "[NoInlining]", "internal void Helper(", "    int a)", "{", "    Run();", "}" },
                builder.ToString().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray());
        }

        // The shared members of classes that are expected to share some.
        static SharedAnimatedVisualMembers Find(params AnimatedVisualClassMembers[] classes)
        {
            var shared = SharedAnimatedVisualMembers.Find(classes);
            Assert.IsNotNull(shared);
            return shared;
        }

        // A class with the given methods, and these fields: the Compositor, which every class has, the field with
        // the root, and a field that no method uses.
        static AnimatedVisualClassMembers Class(params AnimatedVisualMethod[] methods) => Class(methods, "ContainerVisual _root;");

        static AnimatedVisualClassMembers Class(
            AnimatedVisualMethod[] methods,
            string rootDeclaration = "ContainerVisual _root;",
            string compositorDeclaration = "readonly Compositor _c;",
            AnimatedVisualField? extraField = null)
        {
            var fields = new List<AnimatedVisualField>
            {
                new AnimatedVisualField("_c", compositorDeclaration, compositorDeclaration.Replace("readonly ", string.Empty), isAssignedByConstructor: true, isCommon: true),
                new AnimatedVisualField("_root", rootDeclaration),
                new AnimatedVisualField("_unused", "Unused _unused;"),
            };

            if (extraField is not null)
            {
                fields.Add(extraField);
            }

            return new AnimatedVisualClassMembers(fields, methods);
        }

        // The method that creates the root, which uses the field with the root.
        static AnimatedVisualMethod Root()
            => Method("Root", "ContainerVisual Root()", "return _root = ShapeVisual_0();", usesField: "_root", usesMethod: "ShapeVisual_0");

        static AnimatedVisualMethod Factory(string name, float size, string? usesField = null)
            => Method(name, $"ShapeVisual {name}()", $"return _c.CreateShapeVisual({size});", usesField: usesField);

        static AnimatedVisualMethod Method(
            string name,
            string header,
            string statement,
            string? attribute = null,
            string? comment = null,
            string? bodyComment = null,
            bool isStatic = false,
            string? usesField = null,
            string? usesMethod = null)
        {
            var method = new AnimatedVisualMethod(name) { Attribute = attribute, IsStatic = isStatic };
            method.Comment.WriteComment(comment);
            foreach (var line in header.Split('\n'))
            {
                method.Header.WriteLine(line);
            }

            method.Body.OpenScope();
            method.Body.WriteComment(bodyComment);
            method.Body.WriteLine(statement);
            method.Body.CloseScope();

            if (usesField is not null)
            {
                method.Uses.UseField(usesField);
            }

            if (usesMethod is not null)
            {
                method.Uses.UseMethod(usesMethod);
            }

            return method;
        }
    }
}
