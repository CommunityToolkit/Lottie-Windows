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
    public sealed class CSharpSharedCodeExtractorTests
    {
        const string RootField = "    ContainerVisual _root;";

        const string Root = @"
    // The root of the composition.
    ContainerVisual Root()
    {
        var result = _root = _c.CreateContainerVisual();
        result.Children.InsertAtTop(ShapeVisual_0());
        return result;
    }";

        [TestMethod]
        public void IdenticalMethodsAndTheirFieldsMoveToTheBaseClass()
        {
            var (baseClass, classes) = Extract(
                Class("Icon_UAPv11", RootField, Root, ShapeVisual(48)),
                Class("Icon_UAPv8", RootField, Root, ShapeVisual(48)));

            CollectionAssert.Contains(baseClass.ToArray(), "abstract class Icon_AnimatedVisualBase");
            CollectionAssert.Contains(baseClass.ToArray(), "    internal ContainerVisual _root;");
            CollectionAssert.Contains(baseClass.ToArray(), "    internal ContainerVisual Root()");
            CollectionAssert.Contains(baseClass.ToArray(), "    internal ShapeVisual ShapeVisual_0()");

            // The fields assigned by the constructors of the classes cannot be readonly in the base class.
            CollectionAssert.Contains(baseClass.ToArray(), "    internal Compositor _c;");

            foreach (var c in classes)
            {
                Assert.AreEqual("    : Icon_AnimatedVisualBase", c[1]);
                Assert.AreEqual("    , Microsoft.UI.Xaml.Controls.IAnimatedVisual", c[2]);
                Assert.IsFalse(c.Any(l => l.Contains("ContainerVisual Root()")), "Root was not moved to the base class.");
                Assert.IsFalse(c.Contains(RootField), "_root was not moved to the base class.");

                // The constructor and the interface implementation stay in the class.
                Assert.IsTrue(c.Contains("        Root();"));
                Assert.IsTrue(c.Contains("    public Visual RootVisual => _root;"));
            }
        }

        [TestMethod]
        public void DifferingCalleesOfSharedMethodsBecomeAbstract()
        {
            var (baseClass, classes) = Extract(
                Class("Icon_UAPv11", RootField, Root, ShapeVisual(48)),
                Class("Icon_UAPv8", RootField, Root, ShapeVisual(24)));

            CollectionAssert.Contains(baseClass.ToArray(), "    internal ContainerVisual Root()");
            CollectionAssert.Contains(baseClass.ToArray(), "    internal abstract ShapeVisual ShapeVisual_0();");
            Assert.IsTrue(classes[0].Contains("    internal override ShapeVisual ShapeVisual_0()"));
            Assert.IsTrue(classes[0].Contains("        result.Size = new Vector2(48F, 48F);"));
            Assert.IsTrue(classes[1].Contains("    internal override ShapeVisual ShapeVisual_0()"));
            Assert.IsTrue(classes[1].Contains("        result.Size = new Vector2(24F, 24F);"));
        }

        [TestMethod]
        public void MethodsUsingFieldsWithDifferentDeclarationsAreNotShared()
        {
            var (baseClass, classes) = Extract(
                Class("Icon_UAPv11", RootField, Root, ShapeVisual(48)),
                Class("Icon_UAPv8", "    Visual _root;", Root, ShapeVisual(48)));

            // ShapeVisual_0 is shared, but Root uses _root, which is declared differently.
            CollectionAssert.Contains(baseClass.ToArray(), "    internal ShapeVisual ShapeVisual_0()");
            Assert.IsFalse(baseClass.Any(l => l.Contains("ContainerVisual Root()") || l.Contains("_root")));
            Assert.IsTrue(classes.All(c => c.Contains("    ContainerVisual Root()")));
        }

        [TestMethod]
        public void CallsToOverloadedMethodsRequireAllOverloadsToBeAvailable()
        {
            const string createSpriteShape = @"
    CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry)
    {
        return _c.CreateSpriteShape(geometry);
    }";
            const string createSpriteShapeWithBrush = @"
    CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry, CompositionBrush brush)
    {
        var result = _c.CreateSpriteShape(geometry);
        result.FillBrush = brush;
        return result;
    }";
            const string spriteShape = @"
    CompositionSpriteShape SpriteShape_0()
    {
        return CreateSpriteShape(_c.CreateEllipseGeometry());
    }";

            // The overload with a brush exists only in one class, so the call to CreateSpriteShape
            // in SpriteShape_0 cannot be resolved the same way from the base class.
            var (baseClass, _) = Extract(
                Class("Icon_UAPv11", createSpriteShape, createSpriteShapeWithBrush, spriteShape),
                Class("Icon_UAPv8", createSpriteShape, spriteShape));

            CollectionAssert.Contains(baseClass.ToArray(), "    internal CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry)");
            Assert.IsFalse(baseClass.Any(l => l.Contains("SpriteShape_0")));
        }

        [TestMethod]
        public void NothingIsExtractedWhenNoMethodIsShared()
        {
            Assert.IsNull(CSharpSharedCodeExtractor.Extract(
                "Icon_AnimatedVisualBase",
                new[] { Class("Icon_UAPv11", ShapeVisual(48)), Class("Icon_UAPv8", ShapeVisual(24)) }));
        }

        [TestMethod]
        public void NothingIsExtractedFromASingleClass()
        {
            Assert.IsNull(CSharpSharedCodeExtractor.Extract(
                "Icon_AnimatedVisualBase",
                new[] { Class("Icon", RootField, Root, ShapeVisual(48)) }));
        }

        [TestMethod]
        public void MethodsUsingMembersThatAreNotRecognizedAreNotShared()
        {
            const string sizeProperty = "    Vector2 VisualSize => new Vector2(48F, 48F);";
            const string shapeVisual = @"
    ShapeVisual ShapeVisual_0()
    {
        var result = _c.CreateShapeVisual();
        result.Size = VisualSize;
        return result;
    }";

            // ShapeVisual_0 is identical in both classes, but uses a property, which is not shared.
            var (baseClass, classes) = Extract(
                Class("Icon_UAPv11", RootField, Root, sizeProperty, shapeVisual),
                Class("Icon_UAPv8", RootField, Root, sizeProperty, shapeVisual));

            CollectionAssert.Contains(baseClass.ToArray(), "    internal abstract ShapeVisual ShapeVisual_0();");
            Assert.IsTrue(classes.All(c => c.Contains("    internal override ShapeVisual ShapeVisual_0()")));
        }

        [TestMethod]
        public void AttributesMoveWithTheirMember()
        {
            const string attributedMethod = @"
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    ShapeVisual CreateShapeVisual()
    {
        return _c.CreateShapeVisual();
    }";

            var (baseClass, classes) = Extract(
                Class("Icon_UAPv11", RootField, Root, ShapeVisual(48), attributedMethod),
                Class("Icon_UAPv8", RootField, Root, ShapeVisual(48), attributedMethod));

            var header = baseClass.ToList().IndexOf("    internal ShapeVisual CreateShapeVisual()");
            Assert.IsTrue(header > 0, "CreateShapeVisual was not moved to the base class.");
            Assert.AreEqual("    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]", baseClass[header - 1]);
            Assert.IsFalse(classes.Any(c => c.Any(l => l.Contains("MethodImpl"))));
        }

        [TestMethod]
        public void AttributesOfOverriddenMethodsStayWithTheOverride()
        {
            const string attribute = "    [System.Diagnostics.DebuggerHidden]";

            var (baseClass, classes) = Extract(
                Class("Icon_UAPv11", RootField, Root, attribute + ShapeVisual(48)),
                Class("Icon_UAPv8", RootField, Root, attribute + ShapeVisual(24)));

            // The abstract declaration has no attribute, and each override keeps its attribute.
            var declaration = baseClass.ToList().IndexOf("    internal abstract ShapeVisual ShapeVisual_0();");
            Assert.IsTrue(declaration > 0, "ShapeVisual_0 was not declared abstract.");
            Assert.AreNotEqual(attribute, baseClass[declaration - 1]);
            foreach (var c in classes)
            {
                var header = c.ToList().IndexOf("    internal override ShapeVisual ShapeVisual_0()");
                Assert.IsTrue(header > 0, "ShapeVisual_0 was not overridden.");
                Assert.AreEqual(attribute, c[header - 1]);
            }
        }

        [TestMethod]
        public void AttributesAreNotMistakenForTheBodyOfTheirMethod()
        {
            const string attribute = "    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]";
            const string createSpriteShape = attribute + @"
    CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry)
    {
        return _c.CreateSpriteShape(geometry);
    }";
            const string createSpriteShapeWithBrush = attribute + @"
    CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry, CompositionBrush brush)
    {
        var result = _c.CreateSpriteShape(geometry);
        result.FillBrush = brush;
        return result;
    }";

            // The overload that exists in every class is shared. Reading its header as code would make
            // it call itself, and so the overload that exists only in one class, which cannot be shared.
            var (baseClass, _) = Extract(
                Class("Icon_UAPv11", createSpriteShape, createSpriteShapeWithBrush),
                Class("Icon_UAPv8", createSpriteShape));

            CollectionAssert.Contains(baseClass.ToArray(), "    internal CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry)");
        }

        [TestMethod]
        public void NothingIsExtractedFromClassesWithUnexpectedAttributes()
        {
            // An attribute followed by a comment, and an attribute split over several lines.
            const string commentAfterAttribute = @"
    [System.Diagnostics.DebuggerHidden]
    // A comment.
    ShapeVisual ShapeVisual_1()
    {
        return _c.CreateShapeVisual();
    }";
            const string multiLineAttribute = @"
    [System.Diagnostics.DebuggerDisplay(
        ""Shape"")]
    ShapeVisual ShapeVisual_1()
    {
        return _c.CreateShapeVisual();
    }";

            foreach (var method in new[] { commentAfterAttribute, multiLineAttribute })
            {
                Assert.IsNull(CSharpSharedCodeExtractor.Extract(
                    "Icon_AnimatedVisualBase",
                    new[]
                    {
                        Class("Icon_UAPv11", RootField, Root, ShapeVisual(48), method),
                        Class("Icon_UAPv8", RootField, Root, ShapeVisual(48), method),
                    }));
            }
        }

        // An AnimatedVisual class laid out as the C# generator writes it, with the given members.
        static string[] Class(string name, params string[] members)
        {
            var lines = new List<string>
            {
                $"sealed class {name}",
                "    : Microsoft.UI.Xaml.Controls.IAnimatedVisual",
                "{",
                "    const long c_durationTicks = 10000000;",
                "    readonly Compositor _c;",
                "    readonly ExpressionAnimation _reusableExpressionAnimation;",
            };

            foreach (var member in members)
            {
                lines.AddRange(member.Replace("\r\n", "\n").Split('\n'));
            }

            lines.AddRange(new[]
            {
                string.Empty,
                $"    internal {name}(",
                "        Compositor compositor",
                "        )",
                "    {",
                "        _c = compositor;",
                "        _reusableExpressionAnimation = compositor.CreateExpressionAnimation();",
                "        Root();",
                "    }",
                string.Empty,
                "    public Visual RootVisual => _root;",
                "    void IDisposable.Dispose() => _root?.Dispose();",
                "}",
            });

            return lines.ToArray();
        }

        static string ShapeVisual(float size) => $@"
    ShapeVisual ShapeVisual_0()
    {{
        var result = _c.CreateShapeVisual();
        result.Size = new Vector2({size}F, {size}F);
        return result;
    }}";

        static (IReadOnlyList<string> BaseClass, IReadOnlyList<IReadOnlyList<string>> Classes) Extract(params string[][] classes)
        {
            var result = CSharpSharedCodeExtractor.Extract("Icon_AnimatedVisualBase", classes);
            Assert.IsNotNull(result);
            return result.Value;
        }
    }
}
