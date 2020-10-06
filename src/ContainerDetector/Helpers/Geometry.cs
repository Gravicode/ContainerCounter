using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ContainerDetector
{
    public class Geometry
    {
        public static double GetAngle(Point p1, Point p2)
        {
            (double x1, double y1) = (p1.X, p1.Y);
            (double x2, double y2) = (p2.X, p2.Y);
            var w = x2 - x1;
            var h = y2 - y1;

            var atan = Math.Atan(h / w) / Math.PI * 180;
            if (w < 0 || h < 0)
                atan += 180;
            if (w > 0 && h < 0)
                atan -= 180;
            if (atan < 0)
                atan += 360;

            return atan % 360;
        }
        /// <summary>
        /// Return the distance between 2 points
        /// </summary>
        public static double Euclidean(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        /// <summary>
        /// Calculates the similarity between 2 points using Euclidean distance.
        /// Returns a value between 0 and 1 where 1 means they are identical
        /// </summary>
        public static double EuclideanSimilarity(Point p1, Point p2)
        {
            return 1 / (1 + Euclidean(p1, p2));
        }
        /// <summary>
        /// Determines if the given point is inside the polygon
        /// </summary>
        /// <param name="polygon">the vertices of polygon</param>
        /// <param name="testPoint">the given point</param>
        /// <returns>true if the point is inside the polygon; otherwise, false</returns>
        public static bool IsPointInPolygon(Point[] polygon, Point testPoint)
        {
            bool result = false;
            int j = polygon.Count() - 1;
            for (int i = 0; i < polygon.Count(); i++)
            {
                if (polygon[i].Y < testPoint.Y && polygon[j].Y >= testPoint.Y || polygon[j].Y < testPoint.Y && polygon[i].Y >= testPoint.Y)
                {
                    if (polygon[i].X + (testPoint.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < testPoint.X)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }
    }
}
