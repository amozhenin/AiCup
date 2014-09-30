using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Linq;
using System.Threading;
using System.Windows.Forms.VisualStyles;
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

        public int GetTicksToUp(AHock hock, Point to, double take = -1)
        {
            var ho = hock.Clone();
            var result = 0;
            for (; take < 0 ? !CanStrike(ho, to) : ho.GetDistanceTo2(to) > take*take; result++)
            {
                var turn = ho.GetAngleTo(to);
                var speedUp = GetSpeedTo(turn);
                ho.Move(speedUp, TurnNorm(turn, hock.BaseParams.Agility));

                if (result > 500)
                    return result; // TODO: ��������� �������, ��� ������-�� ������
            }
            return result;
        }

        public int MoveHockTo(AHock ho, Point to, double agility)
        {
            var result = 0;  
            for(; !CanStrike(ho, to); result++)
            {
                var turn = ho.GetAngleTo(to);
                var speedUp = GetSpeedTo(turn);
                ho.Move(speedUp, TurnNorm(turn, agility));

                if (result > 500)
                    return result; // TODO: ��������� �������, ��� ������-�� ������
            }
            return result;
        }

        public int GetTicksToDown(AHock hock, Point to, double take = -1)
        {
            var ho = hock.Clone();
            var result = 0;
            const int limit = 100;
            for (; result < limit && (take < 0 ? !CanStrike(ho, to) : ho.GetDistanceTo2(to) > take * take); result++)
            {
                var turn = RevAngle(ho.GetAngleTo(to));
                var speedUp = -GetSpeedTo(turn);
                ho.Move(speedUp, TurnNorm(turn, hock.BaseParams.Agility));
            }
            if (result >= limit)
                return Inf;
            return result;
        }

        public int GetTicksTo(Point to, Hockeyist my)
        {
            var ho = new AHock(my);
            var up = GetTicksToUp(ho, to);
            var down = GetTicksToDown(ho, to);
            if (up <= down)
                return up;
            return -down;
        }

        public Tuple<Point, int, int> GoToPuck(Hockeyist my, APuck pk, int timeLimit = 300)
        {
            if (timeLimit == -1 || timeLimit > 300)
                timeLimit = 300;

            var res = Inf;
            Point result = null;
            var dir = 1;
            var owner = World.Hockeyists.FirstOrDefault(x => x.Id == puck.OwnerHockeyistId);
            var ho = owner == null ? null : new AHock(owner);
            if (pk == null)
                pk = new APuck(Get(puck), GetSpeed(puck), Get(OppGoalie));
            else
                ho = null;

            int tLeft = 0, tRight = timeLimit;
            var pks = new APuck[tRight + 1];
            var hhs = new AHock[tRight + 1];
            pks[0] = pk.Clone();
            hhs[0] = ho;
            for (int i = 1; i <= tRight; i++)
            {
                pks[i] = pks[i - 1].Clone();
                hhs[i] = ho == null ? null : hhs[i - 1].Clone();
                PuckMove(1, pks[i], hhs[i]);
            }
            while (tLeft <= tRight)
            {
                var c = (tLeft + tRight)/2;
                var needTicks = GetTicksTo(PuckMove(0, pks[c], hhs[c]), my);
                if (Math.Abs(needTicks) < c)
                {
                    tRight = c - 1;
                    res = c;
                    result = PuckMove(0, pks[c], hhs[c]);
                    dir = needTicks >= 0 ? 1 : -1;
                }
                else
                {
                    tLeft = c + 1;
                }
            }
            const int by = 16;
            for (var c = 0; c <= 70 && c <= timeLimit; c += c < by ? 1 : by)
            {
                var needTicks = GetTicksTo(PuckMove(0, pks[c], hhs[c]), my);
                if (Math.Abs(needTicks) <= c)
                {
                    for (var i = 0; i < by; i++, c--)
                    {
                        if (Math.Abs(needTicks) <= c)
                        {
                            res = c;
                            result = PuckMove(0, pks[c], hhs[c]);
                            dir = needTicks >= 0 ? 1 : -1;
                        }
                    }
                    break;
                }
            }
            if (result == null)
                result = Get(puck);
            return new Tuple<Point, int, int>(result, dir, res);
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
            var friends =
                world.Hockeyists.Where(x => x.IsTeammate && x.Id != self.Id && x.Type != HockeyistType.Goalie).ToArray();
            var friend1 = friends.Count() < 2 || friends[0].TeammateIndex < friends[1].TeammateIndex ? friends[0] : friends[1];
            var friend2 = friends.Count() > 1 ? friends[0].TeammateIndex < friends[1].TeammateIndex ? friends[1] : friends[0] : null;
            FillWayPoints();

            if (My.IsJustMissedGoal || My.IsJustScoredGoal)
            {

            }
            else
            {

                move.SpeedUp = Inf;
                var power = GetPower(self, self.SwingTicks);

                if (self.State == HockeyistState.Swinging && self.Id != puck.OwnerHockeyistId)
                {
                    move.Action = ActionType.CancelStrike;
                }
                else if (puck.OwnerHockeyistId == self.Id)
                {
                    var wait = Inf;
                    double selTurn = 0, selSpeedUp = 0;
                    var willSwing = false;
                    var maxProb = 0.15;
                    var selAction = ActionType.Strike;

                    if (self.State != HockeyistState.Swinging)
                    {
                        // ���� �� ����������
                        for (var ticks = 0; ticks < 50; ticks++)
                        {
                            // ���� ���� ������������ (�� � �����!!!), �� ����� ��������� ������� game.SwingActionCooldownTicks
                            var da = 0.01;

                            var moveDir = MyRight() && self.Y > RinkCenter.Y || MyLeft() && self.Y < RinkCenter.Y ? 1 : -1;
                            for (var moveTurn = 0.0; moveTurn <= 
#if DEBUG
                                2
#else
                                3 
#endif
                                * da; moveTurn += da)
                            {
                                var turn = moveDir * moveTurn;

                                var end = ticks + game.SwingActionCooldownTicks;
                                var start = Math.Max(0, end - game.MaxEffectiveSwingTicks);
                                // ����� �������� ������������
                                var p = ProbabStrikeAfter(end - start, self, new[]
                                {
                                    new Tuple<int, double, double>(start, 1, turn),
                                    new Tuple<int, double, double>(end - start, 0, 0)
                                }, ActionType.Strike);
                                if (p > maxProb)
                                {
                                    wait = start;
                                    willSwing = true;
                                    maxProb = p;
                                    selTurn = turn;
                                    selSpeedUp = 1;
                                    selAction = ActionType.Strike;
                                }

                                // ���� �� ����
                                p = ProbabStrikeAfter(0, self,
                                    new[] { new Tuple<int, double, double>(ticks, 1, turn) }, ActionType.Strike);
                                if (p > maxProb)
                                {
                                    wait = ticks;
                                    willSwing = false;
                                    maxProb = p;
                                    selTurn = turn;
                                    selSpeedUp = 1;
                                    selAction = ActionType.Strike;
                                }

                                // ���� �����
                                p = ProbabStrikeAfter(0, self,
                                    new[] { new Tuple<int, double, double>(ticks, 1, turn) }, ActionType.Pass);
                                if (p > maxProb)
                                {
                                    wait = ticks;
                                    willSwing = false;
                                    maxProb = p;
                                    selTurn = turn;
                                    selSpeedUp = 1;
                                    selAction = ActionType.Pass;
                                }
                            }
                        }
                    }
                    else
                    {
                        // ���� ��� ����������
                        for (var ticks = Math.Max(0, game.SwingActionCooldownTicks - self.SwingTicks);
                            ticks < 80;
                            ticks++)
                        {
                            var p = ProbabStrikeAfter(ticks + self.SwingTicks, self,
                                new[] { new Tuple<int, double, double>(ticks, 0, 0) }, ActionType.Strike);
                            if (p > maxProb)
                            {
                                wait = ticks;
                                willSwing = true;
                                maxProb = p;
                                selAction = ActionType.Strike;
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
                        // TODO: pass here
                        //var passTo = AttackPass();

                        var wayPoint = FindWayPoint(self);
                        if (wayPoint == null)
                        {
                            needPassQueue.Enqueue(Get(self));
                            if (!TryPass(new AHock(self)))
                            {
                                move.Turn = new AHock(self).GetAngleTo(GetStrikePoint());
                                move.SpeedUp = GetSpeedTo(move.Turn);
                            }
                        }
                        else
                        {
                            move.Turn = self.GetAngleTo(wayPoint.X, wayPoint.Y);
                            move.SpeedUp = GetSpeedTo(move.Turn);
                        }
                    }
                    else if (wait == 0)
                    {
                        move.Action = selAction;
                        if (selAction == ActionType.Pass)
                        {
                            move.PassPower = 1;
                            move.PassAngle = PassAngleNorm(new AHock(self).GetAngleTo(GetStrikePoint()));
                        }
                    }
                    else
                    {
                        move.SpeedUp = selSpeedUp;
                        move.Turn = selTurn;
                    }
                }
                else
                {
                    var owner = world.Hockeyists.FirstOrDefault(x => x.Id == puck.OwnerHockeyistId);
                    var pk = new APuck(Get(puck), GetSpeed(puck), Get(MyGoalie)) {IsDefend = true};

                    if (puck.OwnerPlayerId == Opp.Id && (CanStrike(self, owner) || CanStrike(self, puck)))
                    { // ���������� ������
                        move.Action = ActionType.Strike;
                    }
                    else if (puck.OwnerPlayerId != My.Id && CanStrike(self, puck)
                             && Strike(new AHock(self), power, Get(OppGoalie), ActionType.Strike, 0.0))
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
                        var toPuck = GoToPuck(self, null);
                        var toPuck1 = GoToPuck(friend1, null);
                        var toPuck2 = friend2 == null ? null : GoToPuck(friend2, null);
                        if (friend2 != null && toPuck1.Third < toPuck2.Third)
                        {
                            Swap(ref friend1, ref friend2);
                            Swap(ref toPuck1, ref toPuck2);
                        }
                        // 1 - ������ ����� ���� �� �����
                        if (toPuck.Third > toPuck1.Third) // ���� � ������ �����, �� ��� �� ������
                        {
                            var to = GetDefendPos2(self, friend1);
                            StayOn(self, to, self.GetAngleTo(puck));
                        }
                            // ����� 1 ���� �� ������
                        else if (friend2 == null || puck.OwnerPlayerId != My.Id)// && toPuck.Third < toPuck2.Third)// || Math.Abs(puck.X - My.NetFront) < RinkWidth / 2)
                        {
                            var range = TurnRange(self.Agility);
                            var bestTime = Inf;
                            double bestTurn = 0.0;
                            var needTime = GetFirstOnPuck(World.Hockeyists.Where(x => x.IsTeammate),
                                new APuck(Get(puck), GetSpeed(puck), Get(OppGoalie))).First;
                            var lookAt = new Point(Opp.NetFront, RinkCenter.Y);
                            for (var turn = -range; turn <= range; turn += range / 10)
                            {
                                var I = new AHock(self);
                                var P = new APuck(Get(puck), GetSpeed(puck), Get(OppGoalie));
                                for (var t = 0; t < needTime - 10 && t < 70; t++)
                                {
                                    if (CanStrike(I, P))
                                    {
                                        var cl = I.Clone();
                                        var tm = GetTicksToUp(cl, lookAt) + t;
                                        if (tm < bestTime)
                                        {
                                            bestTime = tm;
                                            bestTurn = turn;
                                        }
                                    }
                                    I.Move(0, turn);
                                    P.Move(1);
                                }
                            }
                            var i = new AHock(self);
                            var direct = MoveHockTo(i, toPuck.First, self.Agility);
                            direct += MoveHockTo(i, lookAt, self.Agility);
                            if (bestTime < direct && bestTime < Inf)
                            {
                                move.Turn = bestTurn;
                                move.SpeedUp = 0.0;
                            }
                            else if (toPuck.Second > 0)
                            {
                                move.Turn = self.GetAngleTo(toPuck.First.X, toPuck.First.Y);
                                move.SpeedUp = GetSpeedTo(move.Turn);
                            }
                            else
                            {
                                move.Turn = RevAngle(self.GetAngleTo(toPuck.First.X, toPuck.First.Y));
                                move.SpeedUp = -GetSpeedTo(move.Turn);
                            }
                        }
                        else
                        {
                            var x = MyRight() ? Math.Max(RinkCenter.X - RinkWidth / 2, puck.X - 100) : Math.Min(RinkCenter.X + RinkWidth / 2, puck.X + 100);
                            Point c1 = new Point(x, Game.RinkTop + 2 * HoRadius);
                            Point c2 = new Point(x, Game.RinkBottom - 2 * HoRadius);
                            Point c = c1.GetDistanceTo(toPuck2.First) > c2.GetDistanceTo(toPuck2.First) ? c1 : c2;
                            //var c = RinkCenter.Clone();
                            move.Turn = self.GetAngleTo(c.X, c.Y);
                            move.SpeedUp = GetSpeedTo(move.Turn);
                        }
                    }
                }
                if (Eq(move.SpeedUp, Inf))
                    move.SpeedUp = 1;
            }
#if DEBUG
            draw();
            Thread.Sleep(8);
#endif
            drawPathQueue.Clear();
            drawGoalQueue.Clear();
            drawGoal2Queue.Clear();
            drawInfo.Clear();
            needPassQueue.Clear();
        }
    }
}