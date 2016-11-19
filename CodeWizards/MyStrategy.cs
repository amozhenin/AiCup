using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;

/**
 * TODO:
 *
 * !!-������������ ��������� (�������� �� ��������)
 * !!-������� �� ���� �����
 * !!!-�������� �� ���������� ��������
 * - ���������� ������ "�������������"
 * - �� ���� �� ����� ����� ������ �� �����
 * - �� ��������� �������� �����
 * - ���� �� ��� �������� �����, ���� ����� ???
 * 
 * - ����� ��������� �������� ������� �������
 * - ����� ����� ���� ������
 * 
 */

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk
{
    public partial class MyStrategy : IStrategy
    {
        public static World World;
        public static Game Game;
        public static Wizard Self;
        public static FinalMove FinalMove;

        public static long[] FriendsIds;

        public static AWizard[] Wizards, OpponentWizards;
        public static AMinion[] Minions, OpponentMinions, NeutralMinions;
        public static ABuilding[] OpponentBuildings;
        public static ACombatUnit[] Combats, OpponentCombats, MyCombats;

        public static AProjectile[][] ProjectilesPaths;
        public static Segment[] Roads;

        public void Move(Wizard self, World world, Game game, Move move)
        {
            // ������� ����� �������� �� ������������ ������ � ����������� ����
            Wizards = null;
            OpponentWizards = null;
            Minions = null;
            OpponentMinions = null;
            NeutralMinions = null;
            OpponentBuildings = null;
            Combats = null;
            OpponentCombats = null;
            MyCombats = null;

            if (world.TickIndex == 7245)
            {
                move.Action = ActionType.MagicMissile;
                return;
            }

            TimerStart();
            _move(self, world, game, move);
            TimerEndLog("All", 0);
            //if (world.TickIndex % 1000 == 999 || world.TickIndex == 3525)
            //    _recheckNeighbours();
#if DEBUG
            Visualizer.Visualizer.DrawSince = 7200;
            Visualizer.Visualizer.CreateForm();
            if (world.TickIndex >= Visualizer.Visualizer.DrawSince)
                Visualizer.Visualizer.DangerPoints = CalculateDangerMap();
            else
                Visualizer.Visualizer.DangerPoints = null;
            Visualizer.Visualizer.LookUp(new Point(self));
            Visualizer.Visualizer.Draw();
            if (world.TickIndex >= Visualizer.Visualizer.DrawSince)
                Thread.Sleep(20); // ����� ������ ������������
#endif
        }

        private void _move(Wizard self, World world, Game game, Move move)
        {
            World = world;
            Game = game;
            Self = self;
            FinalMove = new FinalMove(move);

            if (Math.Abs(world.Width - world.Height) > Const.Eps)
                throw new Exception("map width != map height");

            Const.MapSize = world.Width;

            Wizards = world.Wizards
                .Select(x => new AWizard(x))
                .ToArray();

            OpponentWizards = Wizards
                .Where(x => x.IsOpponent)
                .ToArray();

            Minions = world.Minions
                .Select(x => x.Type == MinionType.OrcWoodcutter ? (AMinion) new AOrc(x) : new AFetish(x))
                .ToArray();

            NeutralMinions = Minions
                .Where(x => x.Faction == Faction.Neutral)
                .ToArray();

            Combats =
                Minions.Cast<ACombatUnit>()
                .Concat(Wizards)
                .Concat(BuildingsObserver.Buildings)
                .ToArray();

            MyCombats = Combats
                .Where(x => x.IsTeammate)
                .ToArray();

            FriendsIds = Combats
                .Where(x => x.IsTeammate)
                .Select(x => x.Id)
                .ToArray();

            NeutralMinionsObserver.Update();
            OpponentMinions = Minions
                .Where(x => x.IsOpponent)
                .ToArray();

            OpponentCombats = Combats
                .Where(x => x.IsOpponent)
                .ToArray();

            BuildingsObserver.Update();

            OpponentBuildings = BuildingsObserver.Buildings
                .Where(x => x.IsOpponent)
                .ToArray();

            TreesObserver.Update();
            ProjectilesObserver.Update();
            BonusesObserver.Update();

            InitializeRoads();
            InitializeProjectiles();
            InitializeDijkstra();

            foreach (var bld in OpponentBuildings)
            {
                bld.OpponentsCount = MyCombats.Count(x => x.GetDistanceTo(bld) <= bld.VisionRange);
                var hisWizards = OpponentWizards.Count(x => x.GetDistanceTo(bld) <= bld.VisionRange);
                if (bld.IsBase && bld.OpponentsCount >= 7 || !bld.IsBase && bld.OpponentsCount >= 5 && hisWizards == 0)
                    bld.IsBesieded = true;
            }

            if (Self.IsMaster && World.TickIndex == 0)
            {
                MasterSendMessages();
                //FinalMove.Messages = new[]
                //{
                //    new Model.Message(LaneType.Bottom, null, new byte[] {}),
                //    new Model.Message(LaneType.Bottom, null, new byte[] {}),
                //    new Model.Message(LaneType.Bottom, null, new byte[] {}),
                //    new Model.Message(LaneType.Bottom, null, new byte[] {}),
                //};
                return;
            }

#if DEBUG
            var master = Wizards.FirstOrDefault(x => x.IsTeammate && x.IsMaster);
            var masterName = master == null ? "" : World.Players.FirstOrDefault(x => x.Id == master.Id).Name;
#endif

#if DEBUG
            while (Visualizer.Visualizer.Pause)
            {
                // pause here
            }
#endif
            var target = FindTarget(new AWizard(self));
            if (target != null)
            {
                //move.Turn = self.GetAngleTo(target.X, target.Y);
            }
            else
            {
                var nearest = OpponentCombats
                    .OrderBy(x => x.GetDistanceTo(self) + (x is AWizard ? -40 : (x is ABuilding && ((ABuilding) x).IsBesieded) ? 20 : 0))
                    .Where((x, i) => i == 0 || x.GetDistanceTo(self) < self.VisionRange * 1.7)// ����� �� ���������� �� ������ �����
                    .ToArray();
                if (nearest.Length > 0 && nearest.FirstOrDefault(GoAround) == null)
                {
                    var goTo = nearest[0];
                    FinalMove.MoveTo(null, goTo);
                    var canGo = TryGoByGradient(w => w.GetDistanceTo2(goTo));
                    TryCutTrees(!canGo);
                }
            }

            if (!TryDodgeProjectile())
            {
                if (target == null || 
                    FinalMove.Action == ActionType.Staff ||
                    FinalMove.Action == ActionType.MagicMissile || 
                    FinalMove.Action == ActionType.Fireball ||
                    FinalMove.Action == ActionType.FrostBolt)
                {
                    TryDodgeDanger();
                }
            }
        }

        bool TryCutTrees(bool cutNearest)
        {
            var self = new AWizard(Self);
            var nearestTrees = TreesObserver.Trees.Where(
                t => self.GetDistanceTo(t) < self.CastRange + t.Radius + Game.MagicMissileRadius
                ).ToArray();

            if (nearestTrees.Length == 0)
                return false;

            if (self.RemainingActionCooldownTicks == 0)
            {
                if (self.GetStaffAttacked(nearestTrees).Length > 0)
                {
                    FinalMove.Action = ActionType.Staff;
                    return true;
                }
                if (self.RemainingMagicMissileCooldownTicks == 0)
                {
                    var proj = new AProjectile(self, 0, ProjectileType.MagicMissile);
                    var path = EmulateMagicMissile(proj);
                    if (path.Count == 0 || path[path.Count - 1].EndDistance < self.CastRange - Const.Eps)
                    {
                        FinalMove.MinCastDistance = path[path.Count - 1].EndDistance;
                        FinalMove.Action = ActionType.MagicMissile;
                        return true;
                    }
                }
            }
            if (cutNearest)
            {
                var nearest = nearestTrees.OrderBy(t => self.GetDistanceTo2(t)).FirstOrDefault();
                FinalMove.MoveTo(null, nearest);
            }

            return false;
        }

        bool GoAround(ACircularUnit to)
        {
            TimerStart();
            var ret = _goAround(to);
            TimerEndLog("Dijkstra", 1);
            return ret;
        }

        bool _goAround(ACircularUnit target)
        {
            var path = DijkstraFindPath(new AWizard(Self), target);
            var my = new AWizard(Self);
            if (path == null && my.GetDistanceTo(target) - my.Radius - target.Radius <= 1)
                path = new List<Point> { my }; // ��-�� �������=1, ���� ���� ������ � ����, �� �� ��� �� � ��� ������������, �� ��� �� ���
            if (path == null || path.Count == 0)
                return false;

            if (path.Count == 1)
            {
                FinalMove.Turn = my.GetAngleTo(target);
                return true;
            }

            var obstacles =
                Combats.Where(x => x.Id != Self.Id).Cast<ACircularUnit>()
                .Concat(TreesObserver.Trees)
                .Where(x => my.GetDistanceTo2(x) < Geom.Sqr(my.VisionRange)) //???
                .ToArray();

            SimplifyPath(my, obstacles, path);
            SimplifyPath(my, obstacles, path);//HACK

            var nextPoint = path[1];
            var nextNextPoint = path.Count > 2 ? path[2] : target;
            FinalMove.MoveTo(nextPoint, my.GetDistanceTo(nextNextPoint) < Self.VisionRange * 1.2 ? nextNextPoint : nextPoint);
#if DEBUG
            Visualizer.Visualizer.SegmentsDrawQueue.Add(new object[] { path, Pens.Blue, 3 });
#endif
            return true;
        }

        private bool HasAnyTarget(AWizard self)
        {
            double wizardDangerRange = 40; 

            var my = new AWizard(self);
            foreach (var opp in OpponentCombats)
            {
                var prevCastRange = my.CastRange;

                var bld = opp as ABuilding;
                if (opp is AWizard)
                    my.CastRange += wizardDangerRange; // ����� ��������� �� ���������� �� ��������
                if (bld != null)
                {
                    var opps = bld.OpponentsCount - (bld.GetDistanceTo(Self) <= bld.CastRange ? 1 : 0);
                    if (bld.IsBase || opps < 1)
                        my.CastRange = bld.CastRange + 5;
                }

                if (my.EthalonCanCastMagicMissile(opp, false))
                    return true;

                my.CastRange = prevCastRange;
            }
            return false;
        }

        Point FindTarget(AWizard self)
        {
            var t1 = FindCastTarget(self);
            TimerStart();
            var t2 = FindStaffTarget(self);
            TimerEndLog("FindStaffTarget", 2);
            var t3 = FindCastTarget2(self);

            if (t1.Target != null && t1.Time <= Math.Min(t2.Time, t3.Time))
            {
                FinalMove.Apply(t1.Move);
                return t1.Target;
            }
            if (t2.Target != null && t2.Time <= Math.Min(t1.Time, t3.Time))
            {
                FinalMove.Apply(t2.Move);
                return t2.Target;
            }
            if (t3.Target != null && t3.Time <= Math.Min(t1.Time, t2.Time))
            {
                FinalMove.Apply(t3.Move);
                return t3.Target;
            }
            return null;
        }

        class MovingInfo
        {
            public Point Target;
            public int Time;
            public FinalMove Move;

            public MovingInfo(Point target, int time, FinalMove move)
            {
                Target = target;
                Time = time;
                Move = move;
            }
        }

        bool CheckIntersectionsAndTress(AWizard self, IEnumerable<ACircularUnit> units)
        {
            if (self.CheckIntersections(units) != null)
                return true;
            var nearestTree = TreesObserver.GetNearestTree(self);
            return nearestTree != null && self.IntersectsWith(nearestTree);
        }

        MovingInfo FindStaffTarget(AWizard self)
        {
            var nearest = Combats
                .Where(x => x.Id != self.Id && self.GetDistanceTo2(x) < Geom.Sqr(Game.StaffRange*3))
                .ToArray();
            int minTicks = int.MaxValue;
            var move = new FinalMove(new Move());

            ACircularUnit selTarget = self.GetStaffAttacked(nearest).Cast<ACombatUnit>().FirstOrDefault(x => x.IsOpponent);
            if (selTarget != null) // ���� ��� ����� ����
            {
                move.Action = ActionType.Staff;
                return new MovingInfo(selTarget, 0, move);
            }
            Point selMoveTo = null;

            // TODO: check IsBesieded?
            foreach (var opp in OpponentCombats)
            {
                var dist = self.GetDistanceTo(opp);
                if (dist > Game.StaffRange*3)
                    continue;

                var range = opp.Radius + Game.StaffRange;
                foreach (var delta in new [] { -range, -range / 2, 0, range / 2, range })
                {
                    var angle = Math.Atan2(delta, dist);
                    var moveTo = self + (opp - self).Normalized().RotateClockwise(angle)*self.VisionRange;

                    var nearstCombats = OpponentCombats
                        .Where(x => /*x.Id != opp.Id &&*/ x.GetDistanceTo(self) <= x.VisionRange)
                        .Select(Utility.CloneCombat)
                        .ToArray();

                    var canHitNow = opp.EthalonCanHit(self);

                    var ticks = 0;
                    var my = new AWizard(self);
                    var his = Utility.CloneCombat(opp);
                    var ok = true;

                    while (my.GetDistanceTo2(his) > Geom.Sqr(Game.StaffRange + his.Radius))
                    {
                        his.EthalonMove(my);
                        if (!my.MoveTo(moveTo, his, w => !CheckIntersectionsAndTress(w, nearest)))
                        {
                            ok = false;
                            break;
                        }
                        foreach (var x in nearstCombats)
                            x.EthalonMove(my);
                        ticks++;
                    }

                    if (ok && !(opp is AOrc))
                    {
                        while (Math.Abs(my.GetAngleTo(his)) > Game.StaffSector/2)
                        {
                            my.MoveTo(null, his);
                            foreach (var x in nearstCombats)
                                x.EthalonMove(my);
                            his.EthalonMove(my);
                            ticks++;
                        }
                    }

                    if (ok && ticks < minTicks)
                    {
                        if (my.CanStaffAttack(his))
                        {
                            if (nearstCombats.All(x => canHitNow && x.Id == opp.Id || !x.EthalonCanHit(my)))
                            {
                                // �����-�� � ��������� �������
                                while (my.GetDistanceTo(self) > Game.WizardForwardSpeed)//TODO:HACK
                                {
                                    my.MoveTo(self, null);
                                    foreach (var x in nearstCombats)
                                        x.SkipTick();
                                }
                                if (nearstCombats.All(x => canHitNow && x.Id == opp.Id || !x.EthalonCanHit(my)))
                                {
                                    selTarget = his;
                                    selMoveTo = moveTo;
                                    minTicks = ticks;
                                }
                            }
                        }
                    }
                }
            }
            if (selTarget != null)
            {
                bool angleOk = Math.Abs(self.GetAngleTo(selTarget)) <= Game.StaffSector/2,
                    distOk = self.GetDistanceTo2(selTarget) <= Geom.Sqr(Game.StaffRange + selTarget.Radius);
                
                if (!distOk)
                {
                    move.MoveTo(selMoveTo, selTarget);
                }
                else if (!angleOk)
                {
                    move.MoveTo(null, selTarget);
                }
            }
            return new MovingInfo(selTarget, minTicks, move);
        }

        static double GetCombatPriority(AWizard self, ACombatUnit unit)
        {
            // ��� ������ - ��� ������ �������� � ���� �������
            var res = unit.Life;
            if (unit is AWizard)
                res /= 4;
            var dist = self.GetDistanceTo(unit);
            if (dist <= Game.StaffRange + unit.Radius + 10)
            {
                res -= 60;
                res += Math.Log(dist);
            }
            return res;
        }

        MovingInfo FindCastTarget(AWizard self)
        {
            var move = new FinalMove(new Move());
            if (self.RemainingMagicMissileCooldownTicks > 0 || self.RemainingActionCooldownTicks > 0)
                return new MovingInfo(null, int.MaxValue, move);

            var angles = new List<double>();
            foreach (var x in OpponentCombats)
            {
                var dist = self.GetDistanceTo(x);

                if (dist > self.CastRange + x.Radius + Game.MagicMissileRadius + 3) // TODO: �������� ���������� �������, ���� ���� ������� �������
                    continue;

                const int grid = 20;
                double left = -Game.StaffSector/2, right = -left;
                for (var i = 0; i <= grid; i++)
                {
                    var castAngle = (right - left)/grid*i + left;
                    angles.Add(castAngle);
                }
            }

            ACombatUnit selTarget = null;
            double
                selMinDist = 0,
                selMaxDist = self.CastRange + 20,
                selAngleTo = 0,
                selCastAngle = 0,
                selPriority = int.MaxValue;

            foreach (var angle in angles)
            {
                var proj = new AProjectile(new AWizard(self), angle, ProjectileType.MagicMissile);
                var path = EmulateMagicMissile(proj);
                for (var i = 0; i < path.Count; i++)
                {
                    if (path[i].State == AProjectile.ProjectilePathState.Fire)
                    {
                        var combat = path[i].Target;
                        var angleTo = self.GetAngleTo(combat) - angle;
                        Geom.AngleNormalize(ref angleTo);
                        angleTo = Math.Abs(angleTo);
                        var priority = GetCombatPriority(self, combat);
                        if (combat.IsOpponent && (priority < selPriority || Utility.Equals(priority, selPriority) && angleTo < selAngleTo))
                        {
                            selTarget = combat;
                            selCastAngle = angle;
                            selAngleTo = angleTo;
                            selMinDist = i == 0 ? 0 : (path[i - 1].StartDistance + path[i].StartDistance)/2;
                            selMaxDist = i >= path.Count - 2 ? (self.CastRange + 500) : (path[i + 1].EndDistance + path[i].EndDistance) / 2;
                            selPriority = priority;
                        }
                    }
                }
            }
            if (selTarget == null)
                return new MovingInfo(null, int.MaxValue, move);

            move.Action = ActionType.MagicMissile;
            move.MinCastDistance = selMinDist;
            move.MaxCastDistance = selMaxDist;
            move.CastAngle = selCastAngle;

            return new MovingInfo(selTarget, 0, move);
        }

        MovingInfo FindCastTarget2(AWizard self)
        {
            var move = new FinalMove(new Move());
            var nearest = Combats
                .Where(x => x.Id != self.Id && self.GetDistanceTo2(x) < Geom.Sqr(self.VisionRange * 1.3))
                .ToArray();

            ACircularUnit selTarget = null;
            var minTicks = int.MaxValue;
            double minPriority = int.MaxValue;

            foreach (var opp in OpponentCombats)
            {
                if (self.GetDistanceTo(opp) > self.VisionRange)
                    continue;

                var nearstCombats = nearest
                    .Where(x => x.IsOpponent)
                    .Select(Utility.CloneCombat)
                    .ToArray();

                var canHitNow = opp.EthalonCanHit(self);

                var ticks = 0;
                var my = new AWizard(self);
                var his = Utility.CloneCombat(opp);
                var ok = true;

                while (!my.EthalonCanCastMagicMissile(his, false))
                {
                    if (!my.MoveTo(his, his, w => !CheckIntersectionsAndTress(w, nearest)))
                    {
                        ok = false;
                        break;
                    }
                    foreach (var x in nearstCombats)
                        x.EthalonMove(my);
                    ticks++;
                }

                var priority = GetCombatPriority(self, his);
                if (ok && (ticks < minTicks || ticks == minTicks && priority < minPriority))
                {
                    if (my.EthalonCanCastMagicMissile(his))
                    {
                        if (nearstCombats.All(x => canHitNow && x.Id == opp.Id || !x.EthalonCanHit(my)))
                        {
                            minTicks = ticks;
                            minPriority = priority;
                            selTarget = opp;
                        }
                    }
                } 
            }

            if (selTarget == null)
                return new MovingInfo(null, int.MaxValue, move);

            move.MoveTo(selTarget, selTarget);
            return new MovingInfo(selTarget, minTicks, move);
        }

        List<AProjectile.ProjectilePathSegment> EmulateMagicMissile(AProjectile projectile)
        {
            var units = Combats.Where(x => x.Id != Self.Id).ToArray();
            return projectile.Emulate(units);
        }
    }
}