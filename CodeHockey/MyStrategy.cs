using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;
using Point = Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk.Point;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk 
{
    public partial class MyStrategy : IStrategy
    {
        public Puck puck;
        public Move move;
        public static Player Opp, My;
        public static Hockeyist OppGoalie;
        public static Hockeyist MyGoalie;
        public static World World;
        public static Game Game;
        
        public static double HoRadius;
        public static double RinkWidth, RinkHeight;
        public static Point RinkCenter;
        public static double PuckRadius;
        public static double HoPuckDist = 55.0;

        public Point GetStrikePoint()
        {
            const double delta = 1;
            const double shift = 15;
            double x = Opp.NetFront,
                bestDist = 0,
                bestY = 0,
                minY = Math.Min(Opp.NetBottom, Opp.NetTop),
                maxY = Math.Max(Opp.NetBottom, Opp.NetTop);
            var OppGoalie = Get(MyStrategy.OppGoalie) ?? Point.Zero;
            for (var y = minY + shift; y <= maxY - shift; y += delta)
            {
                if (OppGoalie.GetDistanceTo(x, y) > bestDist)
                {
                    bestDist = OppGoalie.GetDistanceTo(x, y);
                    bestY = y;
                }
            }
            return new Point(x, bestY);
        }

        Point GetStrikeFrom(Point myPositionPoint, Point mySpeed, double myAngle, double myAngularSpeed, Hockeyist my)
        {
            var x1 = Game.RinkLeft + RinkWidth * 0.4;
            var x2 = Game.RinkRight - RinkWidth * 0.4;
            var y1 = Game.RinkTop + RinkHeight * 0.12;
            var y2 = Game.RinkBottom - RinkHeight * 0.12;

            var a = new Point(MyRight() ? x1 : x2, y1);
            var b = new Point(MyRight() ? x1 : x2, y2);
            if (Math.Abs(myAngle) < Deg(20) || Math.Abs(myAngle) > Deg(160))
                return a.GetDistanceTo(myPositionPoint) < b.GetDistanceTo(myPositionPoint) ? a : b;
            return GetTicksTo(myPositionPoint, mySpeed, myAngle, myAngularSpeed, a, my)
                < GetTicksTo(myPositionPoint, mySpeed, myAngle, myAngularSpeed, b, my) ? a : b;
        }

        public int GetTicksTo(Point myPosition, Point mySpeed, double myAngle, double myAngularSpeed, Point to, Hockeyist my)
        {
            var ho = new AHo(myPosition, mySpeed, myAngle, myAngularSpeed, my);
            int result = 0;
            for (; ho.GetDistanceTo2(to) > Game.StickLength * Game.StickLength; result++)
            {
                var turn = ho.GetAngleTo(to);
                var speedUp = 1.0;
                if (Math.Abs(turn) > Deg(40))
                    speedUp = 0.2;
                else if (Math.Abs(turn) > Deg(60))
                    speedUp = 0.05;
                ho.Move(speedUp, TurnNorm(turn));
            }
            return result;
        }

        public Point GoToPuck(Point myPosition, Point mySpeed, double myAngle, double myAngularSpeed, Hockeyist my, out int res)
        {
            double best = Inf;
            res = Inf;
            Point result = null;
            var pk = new APuck(Get(puck), GetSpeed(puck), Get(OppGoalie));
            var owner = World.Hockeyists.FirstOrDefault(x => x.Id == puck.OwnerHockeyistId);
            var ho = owner == null ? null : new AHo(Get(owner), GetSpeed(owner), owner.Angle, owner.AngularSpeed, owner);

            for (var ticks = 0; ticks < 800; ticks++)
            {
                var needTicks = GetTicksTo(myPosition, mySpeed, myAngle, myAngularSpeed, PuckMove(0, pk, ho), my);

                if (needTicks <= ticks && ticks - needTicks < best)
                {
                    best = ticks - needTicks;
                    res = ticks;
                    result = PuckMove(0, pk, ho);
                }
                PuckMove(1, pk, ho);
            }
            if (result == null)
                return new Point(myPosition);
            return result;
        }

        public Point GoToPuck(Point myPosition, Point mySpeed, double myAngle, double myAngularSpeed, Hockeyist my)
        {
            int ticks;
            return GoToPuck(myPosition, mySpeed, myAngle, myAngularSpeed, my, out ticks);
        }

        Point GetDefendPos2()
        {
            if (MyGoalie == null)
                return new Point(Game.RinkLeft + RinkWidth*0.07, RinkCenter.Y);
            var y = MyGoalie.Y > RinkCenter.Y ? My.NetTop + 1.2 * HoRadius : My.NetBottom - 1.2 * HoRadius;
            return MyLeft() ? new Point(Game.RinkLeft + RinkWidth*0.07, y) : new Point(Game.RinkRight - RinkWidth*0.07, y);
        }

        public void StayOn(Hockeyist self, Point to, double needAngle)
        {
            if (to.GetDistanceTo(self) < 1.5 * HoRadius)
            {
                move.SpeedUp = 0;
                move.Turn = needAngle;//!
                return;
            }

            var v0 = GetSpeed(self).Length;
            var S = to.GetDistanceTo(self);
            var s1Res = 0.0;
            var tRes = Inf + 0.0;
            for (double s1 = 0; s1 <= S; s1 += 2)
            {
                var s2 = S - s1;
                var t1 = Math.Sqrt(v0 * v0 + 2 * s1) - v0;
                var vm = v0 + t1;
                var a = vm * vm / 2 / s2;
                var t2 = Math.Sqrt(2 * s2 / a);
                var t = t1 + t2;
                if (t < tRes)
                {
                    tRes = t;
                    s1Res = s1;
                }
            }

            double angle = self.GetAngleTo(to.X, to.Y); // �������� �� ������ ��������

            if (Math.Abs(angle) > Deg(90))
                move.Turn = angle < 0 ? Deg(180) + angle : angle - Deg(180); // ??
            else
                move.Turn = angle;

            if (s1Res > Eps)
            {
                move.SpeedUp = Math.Abs(angle) > Deg(90) ? -1 : 1;
            }
            else
            {
                var curSpeed = GetSpeed(self).Length;
                var restDist = to.GetDistanceTo(self);
                var a = curSpeed * curSpeed / 2 / restDist;
                move.SpeedUp = a / (Math.Abs(angle) > Deg(90) ? Game.HockeyistSpeedDownFactor : Game.HockeyistSpeedUpFactor);
            }
        }

        public void Move(Hockeyist self, World world, Game game, Move move)
        {
            ShowWindow();
            this.puck = world.Puck;
            this.move = move;
            MyStrategy.World = world;
            MyStrategy.Game = game;
            MyStrategy.Opp = world.GetOpponentPlayer();
            MyStrategy.My = world.GetMyPlayer();
            MyStrategy.RinkWidth = game.RinkRight - game.RinkLeft;
            MyStrategy.RinkHeight = game.RinkBottom - game.RinkTop;
            MyStrategy.OppGoalie = world.Hockeyists.FirstOrDefault(x => !x.IsTeammate && x.Type == HockeyistType.Goalie);
            MyStrategy.MyGoalie = world.Hockeyists.FirstOrDefault(x => x.IsTeammate && x.Type == HockeyistType.Goalie);
            MyStrategy.HoRadius = self.Radius;
            MyStrategy.RinkCenter = new Point(game.RinkLeft + RinkWidth/2, game.RinkTop + RinkHeight/2);
            MyStrategy.PuckRadius = puck.Radius;
            var friend =
                world.Hockeyists.FirstOrDefault(x => x.IsTeammate && x.Id != self.Id && x.Type != HockeyistType.Goalie);

            move.SpeedUp = Inf;

            var net = GetStrikePoint();
            var angleToNet = self.GetAngleTo(net.X, net.Y);
            var power = GetPower(self.SwingTicks);

            if (self.State == HockeyistState.Swinging && self.Id != puck.OwnerHockeyistId)
            {
                move.Action = ActionType.CancelStrike;
            }
            else if (puck.OwnerHockeyistId == self.Id)
            {
                drawInfo.Enqueue(StrikeProbability(Get(puck), GetSpeed(self), GetPower(self.SwingTicks), self.Angle, Get(OppGoalie)) + "");

                move.Turn = angleToNet;
                int wait = Inf;
                double selTurn = 0, selSpeedUp = 0;
                bool willSwing = false;
                double maxProb = 0.35;

                if (self.State != HockeyistState.Swinging)
                {
                    // ���� �� ����������
                    for (int ticks = 0; ticks < 40; ticks++)
                    {
                        double p;
                        // ���� ���� ������������ (�� � �����!!!), �� ����� ��������� ������� game.SwingActionCooldownTicks
                        var da = 0.01;
                        for (int dir = -1; dir <= 1; dir += 2)
                        {
                            for (var _turn = 0.0; _turn <= 2*da; _turn += da)
                            {
                                if (_turn == 0 && dir == 1)
                                    continue;
                                var turn = dir*_turn;

                                var end = ticks + game.SwingActionCooldownTicks;
                                var start = Math.Max(0, end - game.MaxEffectiveSwingTicks);
                                // ����� �������� ������������
                                p = ProbabStrikeAfter(start, end - start, self, new[]
                                {
                                    new Tuple<int, double, double>(start, 1, turn),
                                    new Tuple<int, double, double>(end - start, 0, 0)
                                });
                                if (p > maxProb)
                                {
                                    wait = start;
                                    willSwing = true;
                                    maxProb = p;
                                    selTurn = turn;
                                    selSpeedUp = 1;
                                }

                                // ���� �� ����
                                p = ProbabStrikeAfter(ticks, 0, self,
                                    new[] {new Tuple<int, double, double>(ticks, 1, turn)});
                                if (p > maxProb)
                                {
                                    wait = ticks;
                                    willSwing = false;
                                    maxProb = p;
                                    selTurn = turn;
                                    selSpeedUp = 1;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // ���� ��� ����������
                    for (int ticks = Math.Max(0, game.SwingActionCooldownTicks - self.SwingTicks); ticks < 60; ticks++)
                    {
                        var p = ProbabStrikeAfter(ticks, ticks + self.SwingTicks, self,
                            new[] {new Tuple<int, double, double>(ticks, 0, 0)});
                        if (p > maxProb)
                        {
                            wait = ticks;
                            willSwing = true;
                            maxProb = p;
                        }
                    }
                }
                drawInfo.Enqueue((wait == Inf ? 0 : maxProb) + "");
                if (!willSwing && self.State == HockeyistState.Swinging)
                {
                    move.Action = ActionType.CancelStrike;
                }
                else if (willSwing && wait == 0 && self.State != HockeyistState.Swinging)
                {
                    move.Action = ActionType.Swing;
                }
                else if (wait == Inf)
                {
                    var to = GetStrikeFrom(Get(self), GetSpeed(self), self.Angle, self.AngularSpeed, self);
                    if (self.GetDistanceTo(to.X, to.Y) < 200)
                        to = GetStrikePoint();
                    move.Turn = self.GetAngleTo(to.X, to.Y);
                    drawGoal2Queue.Enqueue(to);
                }
                else if (wait == 0)
                {
                    move.Action = ActionType.Strike;
                }
                else
                {
                    move.SpeedUp = selSpeedUp;
                    move.Turn = selTurn;
                }

                if (Math.Abs(move.Turn) > Deg(40))
                    move.SpeedUp = 0.2;
                else if (Math.Abs(move.Turn) > Deg(60))
                    move.SpeedUp = 0.05;
            }
            else
            {
                var owner = world.Hockeyists.FirstOrDefault(x => x.Id == puck.OwnerHockeyistId);
                var pk = new APuck(Get(puck), GetSpeed(puck), Get(MyGoalie)) {IsDefend = true};

                if (puck.OwnerPlayerId == Opp.Id && (CanStrike(self, owner) || CanStrike(self, puck)))
                {
                    move.Action = ActionType.Strike;
                }
                else if (puck.OwnerPlayerId != My.Id && CanStrike(self, puck) 
                    && Strike(new Point(puck), GetSpeed(self), power, self.Angle, Get(OppGoalie)))
                {
                    move.Action = ActionType.Strike;
                }
                else if (puck.OwnerPlayerId != self.PlayerId && CanStrike(self, puck))
                {
                    if (pk.Move(200, goalCheck: true) == 1) // ���� ������� �� �������
                        move.Action = ActionType.Strike;
                    else
                        move.Action = ActionType.TakePuck;
                }
                else
                {
                    var to = GetDefendPos2();
                    if (puck.OwnerPlayerId == My.Id || to.GetDistanceTo(self) < to.GetDistanceTo(friend))
                    {
                        var puckBe = Math.Abs(self.GetAngleTo(puck)) >= Deg(90)
                            ? PuckMove(100, Get(puck), GetSpeed(puck))
                            : Get(puck);
                        if (self.GetDistanceTo(puck) > 300 || !Defend(self, pk.Move(200, goalCheck: true) == 1))
                        {
                            StayOn(self, to, self.GetAngleTo(puckBe.X, puckBe.Y));
                        }
                    }
                    else
                    {
                        to = GoToPuck(Get(self), GetSpeed(self), self.Angle, self.AngularSpeed, self);
                        move.Turn = self.GetAngleTo(to.X, to.Y);
                    }
                    drawGoalQueue.Enqueue(new Point(to));
                }
            }
            if (Eq(move.SpeedUp, Inf))
                move.SpeedUp = 1;
#if DEBUG
            draw();
            Thread.Sleep(15);
#endif
            drawPathQueue.Clear();
            drawGoalQueue.Clear();
            drawGoal2Queue.Clear();
            drawInfo.Clear();
        }
    }
}