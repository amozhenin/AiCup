﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk
{
    public class WizardPath : List<Point>
    {
        public static int SegmentDivideParts = 2;

        public double GetLength()
        {
            var length = 0.0;
            for (var i = 1; i < Count; i++)
                length += GetSegmentWeight(this[i - 1], this[i], i == 0);
            return length;
        }

        public void Simplify(ACircularUnit[] obstacles, double maxLength)
        {
            _simplify(obstacles, maxLength);
            _simplify(obstacles, maxLength);
        }

        private void _simplify(ACircularUnit[] obstacles, double maxLength)
        {
            double length = 0;
            for (var i = 2; i < Count; i++)
            {
                var a = this[i - 2];
                var b = this[i - 1];
                var c = this[i];

                if (_getNearestTree(a, b) != null)
                    break;

                var bcTree = _getNearestTree(b, c);
                var acTree = _getNearestTree(a, c);
                if (bcTree != null || acTree != null)
                {
                    if (acTree == bcTree)
                        RemoveAt(i - 1);
                    break;
                }

                if (length + a.GetDistanceTo(b) > maxLength)
                    break;

                if (obstacles.All(ob => !Geom.SegmentCircleIntersects(a, c, ob, Const.WizardRadius + ob.Radius + MagicConst.RadiusAdditionalEpsilon)))
                {
                    RemoveAt(i - 1);
                    i--;
                }
                else
                {
                    length += a.GetDistanceTo(b);
                }
            }
        }

        private ATree _getNearestTree(Point a, Point b)
        {
            var dir = b - a;

            for (var i = 0; i <= SegmentDivideParts; i++)
            {
                Point p = dir * (1.0 * i / SegmentDivideParts) + a;
                var trees = TreesObserver.GetNearestTrees(p);
                foreach(var tree in trees)
                    if (Geom.SegmentCircleIntersects(a, b, tree,
                        tree.Radius + Const.WizardRadius + MagicConst.RadiusAdditionalEpsilon))
                        return tree;
            }
            return null;
        }

        public ATree GetNearestTree()
        {
            for(var k = 1; k < Count; k++)
            {
                var a = this[k - 1];
                var b = this[k];
                var tree = _getNearestTree(a, b);
                if (tree != null)
                    return tree;
            }
            return null;
        }

        public static double GetSegmentWeight(Point a, Point b, bool checkStart)
        {
            var dir = b - a;
            var res = dir.Length;
            ATree prev = null;
            for (var i = (checkStart ? 0 : 1); i <= SegmentDivideParts; i++)
            {
                var p = dir * (1.0 * i / SegmentDivideParts) + a;
                var tree = TreesObserver.GetNearestTree(p);
                if (tree != null && tree != prev &&
                    Geom.SegmentCircleIntersects(a, b, tree,
                        tree.Radius + Const.WizardRadius + MagicConst.RadiusAdditionalEpsilon))
                {
                    res += Math.Ceiling(tree.Life / 12) * MagicConst.TreeObstacleWeight;
                    prev = tree;
                }
            }
            return res;
        }
    }

    public partial class MyStrategy
    {
        private static readonly int[] _dx = { 0, 0, -1, 1, 1, 1, -1, -1 };
        private static readonly int[] _dy = { -1, 1, 0, 0, 1, -1, 1, -1 };

        private static double[,] _distMap;
        private static Cell[,] _distPrev;
        public static Point[,] _points;
        public static int GridSize = 80;
        public static double CellLength;
        public static double CellDiagLength;

        private void InitializeDijkstra()
        {
            if (_distMap == null)
            {
                CellLength = Const.MapSize / GridSize;
                CellDiagLength = CellLength*Math.Sqrt(2);

                _distMap = new double[GridSize + 1, GridSize + 1];
                _distPrev = new Cell[GridSize + 1, GridSize + 1];
                _points = new Point[GridSize + 1, GridSize + 1];
                for (var i = 0; i <= GridSize; i++)
                {
                    for (var j = 0; j <= GridSize; j++)
                    {
                        _points[i, j] = new Point(Const.MapSize / GridSize*i, Const.MapSize / GridSize*j);
                    }
                }
            }
        }

        private static AWizard _startState;

        static Cell FindNearestCell(ACircularUnit my)
        {
            double ds = Const.MapSize/GridSize;

            var I = (int) (my.X/ds + Const.Eps);
            var J = (int) (my.Y/ds + Const.Eps);
            int seldI = int.MaxValue, seldJ = int.MaxValue;
            double minDist = int.MaxValue;
            var obstacles = _obstacles
                .Concat(BuildingsObserver.Buildings)
                .Concat(TreesObserver.Trees)
                .ToArray();

            for (var di = 0; di < 2; di++)
            {
                for (var dj = 0; dj < 2; dj++)
                {
                    var dst = WizardPath.GetSegmentWeight(_points[I + di, J + dj], my, true);
                    if (dst < minDist && 
                        obstacles.All(ob =>
                            !Geom.SegmentCircleIntersects(my, _points[I + di, J + dj], ob, ob.Radius + my.Radius + MagicConst.RadiusAdditionalEpsilon))
                        )
                    {
                        minDist = dst;
                        seldI = di;
                        seldJ = dj;
                    }
                }
            }
            if (seldI == int.MaxValue)
                return null;
            return new Cell(I + seldI, J + seldJ);
        }

        private static ACircularUnit[] _obstacles;

        private static bool CanPass(Point from, Point to)
        {
            if (to.X < Const.WizardRadius || to.Y < Const.WizardRadius || to.X > Const.MapSize - Const.WizardRadius || to.Y > Const.MapSize - Const.WizardRadius)
                return false;

            if (from.GetDistanceTo2(_startState) > Geom.Sqr(_startState.VisionRange)) // нет смысла проверять препятствия далеко
                return true;

            return _obstacles.All(ob =>
                !Geom.SegmentCircleIntersects(from, to, ob, ob.Radius + Const.WizardRadius + MagicConst.RadiusAdditionalEpsilon));
        }

        public enum DijkstraStopStatus
        {
            Continue,
            Take,
            Stop,
            TakeAndStop,
        }

        public delegate bool DijkstraCellStopFunc(Cell pos);
        public delegate DijkstraStopStatus DijkstraPointStopFunc(Point pos);
        public delegate double DijkstraPointCostFunc(Point pos);

        public static void DijkstraStart(Cell start, DijkstraCellStopFunc stopCondition, DijkstraPointCostFunc costFunc)
        {
            var q = new PriorityQueue<Pair<double, Cell>>();
            q.Push(new Pair<double, Cell>(0.0, start));
            for (var i = 0; i <= GridSize; i++)
            {
                for (var j = 0; j <= GridSize; j++)
                {
                    _distMap[i, j] = Const.Infinity;
                    _distPrev[i, j] = null;
                }
            }

            _distMap[start.I, start.J] = 0;
            while (q.Count > 0)
            {
                var cur = q.Top().Second;
                var minDist = -q.Top().First;
                q.Pop();

                if (minDist > _distMap[cur.I, cur.J])
                    continue;

                if (stopCondition(cur))
                {
                    break;
                }

                for (var k = 0; k < _dx.Length; k++)
                {
                    var I = cur.I + _dx[k];
                    var J = cur.J + _dy[k];
                    if (I < 0 || J < 0 || I > GridSize || J > GridSize)
                        continue;

                    var distTo = _distMap[cur.I, cur.J] + WizardPath.GetSegmentWeight(_points[cur.I, cur.J], _points[I, J], false);
                    if (costFunc != null)
                        distTo += costFunc(_points[I, J]);

                    if (distTo < _distMap[I, J] && CanPass(_points[cur.I, cur.J], _points[I, J]))
                    {
                        _distMap[I, J] = distTo;
                        _distPrev[I, J] = cur;
                        q.Push(new Pair<double, Cell>(-distTo, new Cell(I, J)));
                    }
                };
            }
        }

        public static List<Cell> DijkstraGeneratePath(Cell start, Cell end)
        {
            if (start.Equals(end))
                return new List<Cell> {start};

            if (_distPrev[end.I, end.J] == null || _distMap[end.I, end.J] >= int.MaxValue)
            {
                // path not found
                return null;
            }

            var res = new List<Cell>();
            var cur = end.Clone();
            do
            {
                res.Add(cur.Clone());

                if (cur.Equals(start))
                    break;

                cur = _distPrev[cur.I, cur.J];
            } while (true);

            res.Reverse();
            return res;
        }

        public static WizardPath[] DijkstraFindPath(AWizard start, DijkstraPointStopFunc stopFunc, DijkstraPointCostFunc costFunc)
        {
            start = new AWizard(start);
            _startState = start;
            
            _obstacles = Combats
                .Where(x => !x.IsOpponent && x.Id != start.Id && x.GetDistanceTo2(start) < Geom.Sqr(start.VisionRange)) // (нейтральные включительно)
                .ToArray();

            var startCell = FindNearestCell(start);
            if (startCell == null)
                return new WizardPath[] {};

            var endCells = new List<Cell>();

            DijkstraStart(startCell, cell =>
            {
                var point = _points[cell.I, cell.J];
                var status = stopFunc(point);

                if (status == DijkstraStopStatus.Take || status == DijkstraStopStatus.TakeAndStop)
                    endCells.Add(cell);

                if (status == DijkstraStopStatus.Stop || status == DijkstraStopStatus.TakeAndStop)
                    return true;
                return false;
            }, costFunc);

            return endCells.Select(endCell =>
            {
                var cellsPath = DijkstraGeneratePath(startCell, endCell);

                var res = new WizardPath {start};
                res.AddRange(cellsPath.Select(cell => _points[cell.I, cell.J]));
                return res;
            }).ToArray();
        }
    }
}
