// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    sealed class GraphStatsMonospaceTableFormatter : MonospaceTableFormatter
    {
        internal static IEnumerable<string> GetGraphStatsLines(IEnumerable<(string name, IEnumerable<object> objects)> objects)
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

            var compositionObjects =
                (from x in objs
                 select (from o in x.objects
                         where o is CompositionObject
                         select (CompositionObject)o).ToArray()).ToArray();

            var rows = new[] {
                Row.HeaderTop,
                new Row.ColumnData(headerColumns),
                Row.HeaderBottom,
                GetCompositionObjectCountRecord(compositionObjects, "All CompositionObjects", (o) => true),
                Row.Separator,
                GetAnimatorCountRecord(compositionObjects),
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

        static Row GetAnimatorCountRecord(CompositionObject[][] objects)
        {
            var result = new ColumnData[objects.Length + 1];
            result[0] = ColumnData.Create("Animators", TextAlignment.Left);

            for (var i = 0; i < objects.Length; i++)
            {
                var count = objects[i].Select(o => o.Animators.Count).Sum();
                result[i + 1] = ColumnData.Create(count);
            }

            return new Row.ColumnData(result);
        }
    }
}
