﻿using System;
using System.Collections.Generic;
using System.Linq;
using Com.CodeGame.CodeRacing2015.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeRacing2015.DevKit.CSharpCgdk 
{
    public class Point : IComparable<Point>
    {
        public double X, Y;

        public double Length
        {
            get { return Math.Sqrt(X*X + Y*Y); }
        }


        /// <summary>
        /// Вектор длины 1 того-же направления
        /// </summary>
        /// <returns></returns>
        public Point Normalized()
        {
            return new Point(X / Length, Y / Length);
        }

        /// <summary>
        /// Скалярное произведение a и b
        /// </summary>
        /// <param name="a">a</param>
        /// <param name="b">b</param>
        /// <returns></returns>
        public static double operator *(Point a, Point b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        /// <summary>
        /// Повернутый на 90* вектор
        /// </summary>
        /// <param name="a">a</param>
        /// <returns></returns>
        public static Point operator ~(Point a)
        {
            return new Point(a.Y, -a.X);
        }

        public static Point operator *(Point a, double b)
        {
            return new Point(a.X * b, a.Y * b);
        }

        public static Point operator +(Point a, Point b)
        {
            return new Point(a.X + b.X, a.Y + b.Y);
        }

        public static Point operator -(Point a, Point b)
        {
            return new Point(a.X - b.X, a.Y - b.Y);
        }

        public Point()
        {
            X = 0;
            Y = 0;
        }

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public Point(Unit unit)
        {
            X = unit.X;
            Y = unit.Y;
        }

        public Point(Point point)
        {
            X = point.X;
            Y = point.Y;
        }

        public double GetAngle()
        {
            return Math.Atan2(Y, X);
        }

        public double GetDistanceTo(double x, double y)
        {
            return Math.Sqrt((X - x) * (X - x) + (Y - y) * (Y - y));
        }

        /// <summary>
        /// Расстояние то точки в квадтаре
        /// </summary>
        /// <param name="x">x</param>
        /// <param name="y">y</param>
        /// <returns></returns>
        public double GetDistanceTo2(double x, double y)
        {
            return (X - x) * (X - x) + (Y - y) * (Y - y);
        }

        public double GetDistanceTo(Unit unit)
        {
            return GetDistanceTo(unit.X, unit.Y);
        }

        public double GetDistanceTo(Point point)
        {
            return GetDistanceTo(point.X, point.Y);
        }

        public double GetDistanceTo2(Point point)
        {
            return GetDistanceTo2(point.X, point.Y);
        }

        public bool Equals(double otherX, double otherY)
        {
            return Math.Abs(X - otherX) < MyStrategy.Eps && Math.Abs(Y - otherY) < MyStrategy.Eps;
        }

        public bool Equals(Point other)
        {
            return Equals(other.X, other.Y);
        }

        public bool Equals(Unit other)
        {
            return Equals(other.X, other.Y);
        }

        public override string ToString()
        {
            return "(" + X + "; " + Y + ")";
        }

        public int CompareTo(Point other)
        {
            if (Math.Abs(X - other.X) < MyStrategy.Eps)
                return Y.CompareTo(other.Y);
            return X.CompareTo(other.X);
        }

        public void Set(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Point Zero
        {
            get { return new Point(0, 0); }
        }

        public static Point One
        {
            get { return new Point(1, 1).Normalized(); }
        }

        public Point Clone()
        {
            return new Point(this);
        }

        public static Point ByAngle(double angle)
        {
            return new Point(Math.Cos(angle), Math.Sin(angle));
        }
    }

    public class Points : List<Point>
    {
        public void Pop()
        {
            RemoveAt(Count - 1);
        }
    }

    public class Cell
    {
        public int I, J;

        public Cell(int i, int j)
        {
            I = i;
            J = j;
        }

        public bool Equals(int otherI, int otherJ)
        {
            return I == otherI && J == otherJ;
        }

        public bool Equals(Cell other)
        {
            return Equals(other.I, other.J);
        }

        public override string ToString()
        {
            return "(" + I + ", " + J + ")";
        }
    }

    public class Geom
    {
        /// <summary>
        /// Знаковая площадь abc
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static double VectorProduct(Point a, Point b, Point c)
        {
            return (c.X - b.X) * (b.Y - a.Y) - (c.Y - b.Y) * (b.X - a.X);
        }

        public static int Sign(double x)
        {
            if (Math.Abs(x) < MyStrategy.Eps)
                return 0;
            return x < 0 ? -1 : 1;
        }

        private static void _swap<T>(ref T lhs, ref T rhs)
        {
            var temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        private static bool _intersect_1D(double a, double b, double c, double d) 
        {
	        if (a > b)  
                _swap(ref a, ref b);
	        if (c > d)  
                _swap(ref c, ref d);
	        return Math.Max(a, c) <= Math.Min(b, d);
        }

        /// <summary>
        /// Проверка пересечения отрезков
        /// </summary>
        /// <param name="start1">Начало первого отрезка</param>
        /// <param name="end1">Конец первого отрезка</param>
        /// <param name="start2">Начало второго отрезка</param>
        /// <param name="end2">Конец второго отрезка</param>
        /// <returns></returns>
        public static bool SegmentsIntersect(Point start1, Point end1, Point start2, Point end2)
        {
            return _intersect_1D(start1.X, end1.X, start2.X, end2.X)
                   && _intersect_1D(start1.Y, end1.Y, start2.Y, end2.Y)
                   && Sign(VectorProduct(start1, end1, start2))*Sign(VectorProduct(start1, end1, end2)) <= 0
                   && Sign(VectorProduct(start2, end2, start1))*Sign(VectorProduct(start2, end2, end1)) <= 0;
        }

        /// <summary>
        /// Проверка пренадлежности точки выпуклому полигону
        /// </summary>
        /// <param name="polygon">Выпуклый полигон</param>
        /// <param name="point">Точка</param>
        /// <returns></returns>
        public static bool ContainPoint(Point[] polygon, Point point)
        {
            bool allLess = true, allGreater = true;
            for (var i = 0; i < polygon.Length; i++)
            {
                var vec = Sign(VectorProduct(polygon[i], polygon[(i + 1) % polygon.Length], point));
                allLess &= vec <= 0;
                allGreater &= vec >= 0;
            }
            return allLess || allGreater;
        }

        /// <summary>
        /// Проверка двух выпуклых полигона на пересечение
        /// </summary>
        /// <param name="a">Полигон 1</param>
        /// <param name="b">Полигон 2</param>
        /// <returns></returns>
        public static bool PolygonsIntersect(Point[] a, Point[] b)
        {
            for (var i = 0; i < a.Length; i++)
            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                for (var j = 0; j < b.Length; j++)
                {
                    if (SegmentsIntersect(a[i], a[(i + 1)%a.Length], b[j], b[(j + 1)%b.Length]))
                        return true;
                }
            }
            return false;
        }
    }
}