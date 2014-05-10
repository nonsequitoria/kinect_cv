using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;

// Emgu library
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

// default to using System.Drawing graphics to make using EMGU easier 
using Point = System.Drawing.Point;
using PointF = System.Drawing.PointF;
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace KinectCV.Helpers
{
    // extension methods to make it easier to do conversions, etc.
    public static class Extensions
    {
        public static PointF ToPointF(this DepthImagePoint p)
        {
            return new PointF(p.X, p.Y);
        }

        public static PointF ToPointF(this ColorImagePoint p)
        {
            return new PointF(p.X, p.Y);
        }

    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = 

    // general helper functions 
    public class Utilities
    {
        public static double Distance(PointF a, PointF b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        public static double Distance(SkeletonPoint a, SkeletonPoint b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        public static PointF MidPoint(PointF a, PointF b)
        {
            return new PointF(a.X + (b.X - a.X) / 2, a.Y + (b.Y - a.Y) / 2);
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        public static SkeletonPoint MidPoint(SkeletonPoint a, SkeletonPoint b)
        {
            SkeletonPoint sp = new SkeletonPoint();
            sp.X = a.X + (b.X - a.X) / 2;
            sp.Y = a.Y + (b.Y - a.Y) / 2;
            sp.Z =  a.Z + (b.Z - a.Z) / 2;
            return sp;
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        public static MCvFont WriteDebugTextFont = new MCvFont(FONT.CV_FONT_HERSHEY_PLAIN, 1.5, 1.5);
        public static Bgr WriteDebugTextColor = new Bgr(255, 255, 255);

        public static void WriteDebugText(Image<Bgr, Byte> img, int x, int y, string text, params object[] args)
        {
            img.Draw(String.Format(text, args), ref WriteDebugTextFont, new Point(x, y), WriteDebugTextColor);
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        void Stamp(Image<Bgr, Byte> img, Image<Bgr, Byte> stamp, int x, int y)
        {
            Rectangle a = new Rectangle(x, y, (int)stamp.Width, (int)stamp.Height);
            img.ROI = a;
            stamp.Convert<Bgr, Byte>().CopyTo(img);
            img.ROI = Rectangle.Empty;
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        public static void ColorToHSV(Color color, out double hue, out double saturation, out double value)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = (max == 0) ? 0 : 1d - (1d * min / max);
            value = max / 255d;
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        public static Color HSVToColor(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return Color.FromArgb(255, t, p, v);
            else
                return Color.FromArgb(255, v, p, q);
        }
    }
}
