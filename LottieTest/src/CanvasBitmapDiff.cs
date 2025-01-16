using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace LottieTest
{
    internal class CanvasBitmapDiff
    {
        private Color[] diff;
        private float diffAccumulated;
        private float diffMax;
        private int width;
        private int height;

        private CanvasBitmap first;
        private CanvasBitmap second;

        private static Color ColorDiff(Color p, Color q)
        {
            byte r = (byte)Math.Abs(p.R - q.R);
            byte g = (byte)Math.Abs(p.G - q.G);
            byte b = (byte)Math.Abs(p.B - q.B);
            return Color.FromArgb(255, r, g, b);
        }

        public CanvasBitmapDiff(CanvasBitmap a, CanvasBitmap b)
        {
            first = a;
            second = b;

            Color[] aColors = a.GetPixelColors();
            Color[] bColors = b.GetPixelColors();
            diff = new Color[aColors.Length];

            diffAccumulated = 0;
            diffMax = 0;

            for (int i = 0; i < diff.Length; i++)
            {
                diff[i] = ColorDiff(aColors[i], bColors[i]);

                diffAccumulated += diff[i].R;
                diffAccumulated += diff[i].G;
                diffAccumulated += diff[i].B;

                diffMax = Math.Max(diffMax, diff[i].R);
                diffMax = Math.Max(diffMax, diff[i].G);
                diffMax = Math.Max(diffMax, diff[i].B);
            }

            width = (int)a.SizeInPixels.Width;
            height = (int)a.SizeInPixels.Height;
        }

        public float Average => diffAccumulated / diff.Length;

        public float Max => diffMax;

        public int GetNumberOfDifferentPixels(float maxDifference = 128)
        {
            int res = 0;

            for (int i = 0; i < diff.Length; i++)
            {
                float pixelDiff = (float)Math.Sqrt(diff[i].R * diff[i].R + diff[i].G * diff[i].G + diff[i].B * diff[i].B);
                if (pixelDiff > maxDifference)
                {
                    res++;
                }
            }

            return res;
        }

        public int GetNumberOfBackgroundPixels()
        {
            int res = 0;

            var colors = first.GetPixelColors();
            var colorsB = second.GetPixelColors();
            Color background = colors[0];

            for (int i = 0; i < colors.Length; i++)
            {
                var c = ColorDiff(background, colors[i]);
                var cb = ColorDiff(background, colors[i]);
                if ((float)Math.Sqrt(c.R * c.R + c.G * c.G + c.B * c.B) < 4.0f && (float)Math.Sqrt(cb.R * cb.R + cb.G * cb.G + cb.B * cb.B) < 4.0f)
                {
                    res++;
                }
            }

            return res;
        }

        public int GetNumberOfForegroundPixels()
        {
            return GetNumberOfPixels() - GetNumberOfBackgroundPixels();
        }

        public int GetNumberOfPixels()
        {
            return diff.Length;
        }

        public CanvasBitmap GetDiffCanvas()
        {
            return CanvasBitmap.CreateFromColors(CanvasDevice.GetSharedDevice(), diff, width, height);
        }

        public CanvasBitmap GetDiffCanvasExaggerated(float maxDifference = 128)
        {
            Color[] color = new Color[diff.Length];

            for (int i = 0; i < diff.Length; i++)
            {
                float pixelDiff = (float)Math.Sqrt(diff[i].R * diff[i].R + diff[i].G * diff[i].G + diff[i].B * diff[i].B);
                color[i] = (pixelDiff > maxDifference ? Colors.Pink : Colors.Black);
            }

            return CanvasBitmap.CreateFromColors(CanvasDevice.GetSharedDevice(), color, width, height);

        }

        public CanvasBitmap GetFirstCanvas()
        {
            return first;
        }

        public CanvasBitmap GetSecondCanvas()
        {
            return second;
        }
    }
}
