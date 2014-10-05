﻿using System;
using System.Collections.Generic;
using System.Linq;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;
using Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk
{
    public partial class MyStrategy : IStrategy
    {
        Point PuckMove(int ticks, APuck pk, AHock hock)
        {
            if (hock == null)
            {
                pk.Move(ticks);
                return new Point(pk);
            }
            if (Math.Abs(hock.GetAngleTo(hock + hock.Speed)) < Deg(15) && hock.Speed.Length > 2)
                hock.Move(1, 0, ticks); // TODO
            return hock.PuckPos();
        }

        public static void GoalieMove(Point goalie, int ticks, Point to)
        {
            if (goalie == null)
                return;
            for (var tick = 0; tick < ticks; tick++)
            {
                if (goalie.Y > to.Y)
                    goalie.Y -= Math.Min(Game.GoalieMaxSpeed, goalie.Y - to.Y);
                else
                    goalie.Y += Math.Min(Game.GoalieMaxSpeed, to.Y - goalie.Y);

                var minY = Opp.NetTop + HoRadius;
                var maxY = Opp.NetBottom - HoRadius;
                if (goalie.Y < minY)
                    goalie.Y = minY;
                if (goalie.Y > maxY)
                    goalie.Y = maxY;
            }
        }

        Pair<int, long> GetFirstOnPuck(IEnumerable<Hockeyist> except, APuck pk, int ticksLimit = 70, bool tryDown = true)
        {
            var cands = World.Hockeyists
                .Where(x => IsInGame(x) && except.Count(y => y.Id == x.Id) == 0)
                .ToArray();
            var times = cands.Select(x => GoToPuck(x, pk, ticksLimit, tryDown).Third).ToArray();
            int whereMin = 0;
            for(var i = 1; i < times.Count(); i++)
                if (times[i] < times[whereMin])
                    whereMin = i;

            return new Pair<int, long>(times[whereMin], cands[whereMin].Id);
        }

        bool TryPass(AHock striker)
        {
            if (striker.Base.RemainingCooldownTicks != 0)
                return false;

            const int passAnglesCount = 5;
            var bestAngle = 0.0;
            double minDanger = Inf;
            var bestPower = 0.0;
#if DEBUG
            foreach (var power in new[] { 0.1, 0.3, 0.5, 0.8, 1.0 })
#else
            foreach (var power in new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.8, 1.0 })
#endif
            {
                for (var passDir = -1; passDir <= 1; passDir += 2)
                {
                    for (var absPassAngle = 0.0; absPassAngle <= Game.PassSector/2; absPassAngle += Game.PassSector/2/passAnglesCount)
                    {
                        var passAngle = absPassAngle*passDir;
                        var pk = GetPassPuck(striker, power, passAngle, Get(OppGoalie)); // проверять на автогол
                        var on = GetFirstOnPuck(new[] {striker.Base}, pk, 70, false);
                        pk.Move(300);
                        if (APuck.PuckLastTicks < on.First)
                            continue;
                        if (!Get(on.Second).IsTeammate)
                            continue;
                        var time = on.First;
                        if (time < minDanger)
                        {
                            minDanger = time;
                            bestAngle = passAngle;
                            bestPower = power;
                        }
                    }
                }
            }
            if (minDanger >= Inf - Eps)
                return false;
            move.Action = ActionType.Pass;
            move.PassAngle = bestAngle;
            move.PassPower = bestPower;
            return true;
        }

        double GetDanger(long id)
        {
            return GetDanger(World.Hockeyists.FirstOrDefault(x => x.Id == id));
        }

        double GetDanger(Hockeyist ho)
        {
            return -World.Hockeyists
                .Where(x => !x.IsTeammate && IsInGame(x))
                .Select(x => GetTicksTo(Get(ho), x)).Min();
        }

        public APuck GetPassPuck(AHock striker, double PassPower, double PassAngle, Point goalie)
        {
            var puckSpeedAbs = 15.0*PassPower + striker.Speed.Length*Math.Cos(striker.Angle + PassAngle - striker.Speed.GetAngle());
            var puckAngle = AngleNormalize(PassAngle + striker.Angle);
            var puckSpeed = new Point(puckAngle)*puckSpeedAbs;
            return new APuck(striker.PuckPos(), puckSpeed, goalie);
        }
    }
}
