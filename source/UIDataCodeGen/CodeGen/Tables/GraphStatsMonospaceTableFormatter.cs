// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.WinCompData;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen.Tables
{
    sealed class GraphStatsMonospaceTableFormatter : MonospaceTableFormatter
    {
        /// <summary>
        /// Returns text that describes the graph statistics.
        /// </summary>
        /// <returns>A formatted string for each line in the table.</returns>
        internal static IEnumerable<string> GetGraphStatsLines(
            IEnumerable<(string? name, IEnumerable<object> objects)> objects)
        {
            var objs = objects.ToArray();

            var headerColumns = new ColumnData[1 + objs.Length];
            headerColumns[0] = ColumnData.Create("Object stats");
            for (var i = 0; i < objs.Length; i++)
            {
                var name = objs[i].name;
                if (name is null)
                {
                    name = "Count";
                }
                else
                {
                    name += " count";
                }

                headerColumns[i + 1] = ColumnData.Create(name);
            }

            // Get the CompositionObjects for each animated visual.
            var compositionObjects =
                (from x in objs
                 select (from o in x.objects
                         where o is CompositionObject
                         select (CompositionObject)o).ToArray()).ToArray();

            var animatorCounts = GetAnimatorCountRecords(compositionObjects);

            var rows = new[] {
                Row.HeaderTop,
                new Row.ColumnData(headerColumns),
                Row.HeaderBottom,
                GetCompositionObjectCountRecord(compositionObjects, "All CompositionObjects", (o) => true),
                Row.Separator,
                animatorCounts.expressions,
                animatorCounts.keyFrames,
                animatorCounts.referenceParameters,
                animatorCounts.operations,
                Row.Separator,
                GetCompositionObjectCountRecord(compositionObjects, "Animated brushes", (o) => o is CompositionBrush b && b.Animators.Count > 0),
                GetCompositionObjectCountRecord(compositionObjects, "Animated gradient stops", (o) => o is CompositionColorGradientStop s && s.Animators.Count > 0),
                GetCompositionObjectCountRecord(compositionObjects, "ExpressionAnimations", (o) => o.Type == CompositionObjectType.ExpressionAnimation),
                GetCompositionObjectCountRecord(compositionObjects, "PathKeyFrameAnimations", (o) => o.Type == CompositionObjectType.PathKeyFrameAnimation),
                Row.Separator,
                GetCompositionObjectCountRecord(compositionObjects, "ContainerVisuals", (o) => o.Type == CompositionObjectType.ContainerVisual),
                GetCompositionObjectCountRecord(compositionObjects, "ShapeVisuals", (o) => o.Type == CompositionObjectType.ShapeVisual),
                Row.Separator,
                GetCompositionObjectCountRecord(compositionObjects, "ContainerShapes", (o) => o.Type == CompositionObjectType.CompositionContainerShape),
                GetCompositionObjectCountRecord(compositionObjects, "CompositionSpriteShapes", (o) => o.Type == CompositionObjectType.CompositionSpriteShape),
                Row.Separator,
                GetCompositionObjectCountRecord(compositionObjects, "Brushes", (o) => o is CompositionBrush),
                GetCompositionObjectCountRecord(compositionObjects, "Gradient stops", (o) => o is CompositionColorGradientStop),
                GetCompositionObjectCountRecord(compositionObjects, "CompositionVisualSurface", (o) => o is CompositionVisualSurface),
                Row.BodyBottom,
            };

            // Convert the rows into strings.
            return GetTableLines(rows);
        }

        static Row GetCompositionObjectCountRecord(
            CompositionObject[][] objects,
            string name,
            Func<CompositionObject, bool> filter)
        {
            var result = new ColumnData[objects.Length + 1];
            result[0] = ColumnData.Create(name, TextAlignment.Left);

            for (var i = 0; i < objects.Length; i++)
            {
                var count = objects[i].Where(filter).Count();
                result[i + 1] = ColumnData.Create(count == 0 ? "-" : count.ToString(), TextAlignment.Right);
            }

            return new Row.ColumnData(result);
        }

        // Returns a row describing the number of animators for each animated visual.
        // The parameter is a set of objects for each generator.
        static (Row expressions, Row keyFrames, Row referenceParameters, Row operations)
            GetAnimatorCountRecords(CompositionObject[][] objects)
        {
            var expressions = new ColumnData[objects.Length + 1];
            var keyFrames = new ColumnData[objects.Length + 1];
            var referenceParameters = new ColumnData[objects.Length + 1];
            var operations = new ColumnData[objects.Length + 1];

            expressions[0] = ColumnData.Create("Expression animators", TextAlignment.Left);
            keyFrames[0] = ColumnData.Create("KeyFrame animators", TextAlignment.Left);
            referenceParameters[0] = ColumnData.Create("Reference parameters", TextAlignment.Left);
            operations[0] = ColumnData.Create("Expression operations", TextAlignment.Left);

            for (var i = 0; i < objects.Length; i++)
            {
                (expressions[i + 1], keyFrames[i + 1], referenceParameters[i + 1], operations[i + 1]) = GetAnimatorCountColumns(objects[i]);
            }

            return (new Row.ColumnData(expressions), new Row.ColumnData(keyFrames), new Row.ColumnData(referenceParameters), new Row.ColumnData(operations));
        }

        /// <summary>
        /// Returns columns describing the counts of animators for the given <see cref="CompositionObject"/>s.
        /// </summary>
        /// <returns>The animator counts columns.</returns>
        static (ColumnData expressions, ColumnData keyFrames, ColumnData referenceParameters, ColumnData operations)
            GetAnimatorCountColumns(IEnumerable<CompositionObject> objects)
        {
            var expressions = 0;
            var keyFrames = 0;
            var referenceParameters = 0;
            var operations = 0;

            foreach (var obj in objects)
            {
                var animators = obj.Animators;
                referenceParameters += animators.Sum(a => a.Animation.ReferenceParameters.Count());

                // Count the expressions and operations in ExpressionAnimations
                var expressionAnimations =
                    (from a in animators
                     where a.Animation.Type == CompositionObjectType.ExpressionAnimation
                     select (ExpressionAnimation)a.Animation).ToArray();

                expressions += expressionAnimations.Length;
                operations += expressionAnimations.Sum(e => e.Expression.OperationsCount);

                // Count the expressions and operations in KeyFrameAnimations
                var expressionKeyFrames =
                    (from a in animators
                     where a.Animation.Type != CompositionObjectType.ExpressionAnimation
                     from keyFrame in ((KeyFrameAnimation_)a.Animation).KeyFrames
                     where keyFrame.Type == KeyFrameType.Expression
                     select (KeyFrameAnimation_.ExpressionKeyFrame)keyFrame).ToArray();

                expressions += expressionKeyFrames.Length;
                operations += expressionKeyFrames.Sum(e => e.Expression.OperationsCount);

                // Key frame animations are the animations that are not expression animations.
                keyFrames += animators.Count - expressionAnimations.Length;
            }

            return (ColumnData.Create(expressions), ColumnData.Create(keyFrames), ColumnData.Create(referenceParameters), ColumnData.Create(operations));
        }
    }
}
