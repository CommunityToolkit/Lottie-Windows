using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax.Builders;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.AbstractSyntax.Experiments
{
    public static class Experiments
    {
        static Types s_types = new Types();
        static Methods s_methods = new Methods();
        static Variable _compositorVariable = new Variable(s_types.Compositor, "_c");

        public static void Do()
        {
            var node = new ObjectData
            {
                LongComment = "Here is s comment",
                Name = "MyShapeVisualFactory",
                TypeName = s_types.ShapeVisual,
            };

            GenerateShapeVisualFactory(node);
        }

        static Method GenerateShapeVisualFactory(ObjectData node)
        {
            var builder = new MethodBuilder();
            builder.SetMethodComment(node.LongComment);
            builder.SetMethodName(node.Name);
            builder.SetReturnType(s_types.ShapeVisual);
            var resultLocal = new LocalVariable(node.TypeName, "result");
            builder.AddStatement(
                new Statement.DeclareAndInitializeLocal(
                    resultLocal,
                    new Expression.MethodCall( s_methods.CreateShapeVisual, new Expression.VariableReference(_compositorVariable))));
            var result = builder.ToMethod();
            return result;
        }

        sealed class Types
        {
            internal TypeReference Compositor { get; } = new TypeReference.ImportedTypeReference("Windows.UI.Composition.Compositor");

            internal TypeReference ShapeVisual { get; } = new TypeReference.ImportedTypeReference("Windows.UI.Composition.ShapeVisual");
        }

        sealed class Methods
        {
            internal ImportedMethodCallTargetReference CreateShapeVisual { get; } = new ImportedMethodCallTargetReference(s_types.ShapeVisual, s_types.Compositor, "CreateShapeVisual");
        }

        sealed class ObjectData
        {
            public string LongComment { get; set; }

            public string Name { get; set; }

            public TypeReference TypeName { get; set; }

            public bool RequiresStorage { get; set; }
        }
    }
}
