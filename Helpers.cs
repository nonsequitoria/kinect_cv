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

    public class Utilities
    {
        public static double Distance(PointF a, PointF b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        public static double Distance(SkeletonPoint a, SkeletonPoint b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
        }

        public static PointF MidPoint(PointF a, PointF b)
        {
            return new PointF(a.X + (b.X - a.X) / 2, a.Y + (b.Y - a.Y) / 2);
        }

        public static SkeletonPoint MidPoint(SkeletonPoint a, SkeletonPoint b)
        {
            SkeletonPoint sp = new SkeletonPoint();
            sp.X = a.X + (b.X - a.X) / 2;
            sp.Y = a.Y + (b.Y - a.Y) / 2;
            sp.Z =  a.Z + (b.Z - a.Z) / 2;
            return sp;
        }
    }
}
