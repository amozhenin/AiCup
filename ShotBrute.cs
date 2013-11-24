﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk.Model;
using System.Collections;
using System.IO;

namespace Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk
{
    public partial class MyStrategy : IStrategy
    {
        public static Trooper[] Troopers;
        public static int KillBonus = 50;
        private int counter = 0;
        private int OpponentsCount;
        private int MyCount;

        public class State
        {
            public int[] stance;
            public int[] hit;
            public int[] act;
            public Point[] Position;
            public int id;
            public double profit;
            public bool[] medikit;
            public bool[] grenade;
            public int[] opphit;

            public int Stance
            {
                get
                {
                    return stance[id];
                }
                set
                {
                    stance[id] = value;
                }
            }

            public bool Grenade
            {
                get
                {
                    return grenade[id];
                }
                set
                {
                    grenade[id] = value;
                }
            }

            public bool Medikit
            {
                get
                {
                    return medikit[id];
                }
                set
                {
                    medikit[id] = value;
                }
            }

            public int X
            {
                get
                {
                    return Position[id].X;
                }
                set
                {
                    Position[id].X = value;
                }
            }

            public int Y
            {
                set
                {
                    Position[id].Y = value;
                }
                get
                {
                    return Position[id].Y;
                }
            }

            public override string ToString()
            {
                return "" + id + " " + Position[id];
            }
        }

        static int CommanderId; // индекс Commandera, или -1 если его нет
        private static State state;
        private static ArrayList[] stack;
        private static ArrayList[] bestStack;
        private static double bestProfit;

        void dfs_changeStance1(bool allowShot = true)
        {
            var id = state.id;

            int upper = 2, lower = -2;
            for (int deltaStance = upper; deltaStance >= lower; deltaStance--)
            {
                // Изменяю stance на deltaStance
                state.Stance += deltaStance;
                if (state.Stance >= 0 && state.Stance < 3)
                {
                    int cost = game.StanceChangeCost * Math.Abs(deltaStance);
                    if (cost <= state.act[id])
                    {
                        state.act[id] -= cost;
                        if (deltaStance != 0)
                            stack[id].Add("st " + deltaStance);
                        // Отсечение: после того как сел - нет смысла идти
                        dfs_move(deltaStance < 0 ? 0 : 4, true, allowShot);
                        if (deltaStance != 0)
                            stack[id].RemoveAt(stack[id].Count - 1);
                        state.act[id] += cost;
                    }
                }
                state.Stance -= deltaStance;
            }
        }

        private void dfs_move(int bfsRadius, bool mayHeal = true, bool allowShot = true)
        {
            var id = state.id;
            var stance = state.Stance;
            var moveCost = getMoveCost(stance);

            var To = FastBfs(state.X, state.Y, bfsRadius, notFilledMap, state.Position);
            foreach (Point to in To)
            {
                int byX = to.X - state.X;
                int byY = to.Y - state.Y;
                state.X += byX;
                state.Y += byY;
                var cost = (int)(to.profit + Eps)*moveCost;
                if (cost <= state.act[id] && Validate(state))
                {
                    state.act[id] -= cost;
                    if (byX != 0 || byY != 0)
                        stack[id].Add("at " + state.X + " " + state.Y);
                    if (allowShot)
                    {
                        dfs_grenade();
                        if (mayHeal)
                            dfs_heal();
                    }
                    dfs_changeStance5(allowShot);
                    if (byX != 0 || byY != 0)
                        stack[id].RemoveAt(stack[id].Count - 1);
                    state.act[id] += cost;
                }
                state.X -= byX;
                state.Y -= byY;
            }
        }

        private void dfs_heal()
        {
            int id = state.id;

            if (state.Medikit && game.MedikitUseCost < state.act[id])
            {
                state.Medikit = false;
                for (int i = 0; i < MyCount; i++)
                {
                    Trooper to = Troopers[i];
                    // если имеет смысл юзать, и если он рядом
                    if (state.hit[i] < 0.8*to.MaximalHitpoints && state.Position[id].Nearest(to))
                    {
                        var oldhit = state.hit[i];
                        state.act[id] -= game.MedikitUseCost;
                        state.hit[i] = Math.Min(to.MaximalHitpoints, state.hit[i] + (i == id ? game.MedikitHealSelfBonusHitpoints : game.MedikitBonusHitpoints));
                        stack[id].Add("med " + to.X + " " + to.Y);
                        dfs_move(4);
                        stack[id].RemoveAt(stack[id].Count - 1);
                        state.hit[i] = oldhit;
                        state.act[id] += game.MedikitUseCost;
                    }
                }
                state.Medikit = true;
            }
            else if (Troopers[id].Type == TrooperType.FieldMedic)
            {
                for (int i = 0; i < MyCount; i++)
                {
                    Trooper to = Troopers[i];
                    // если он рядом
                    if (state.hit[i] < to.MaximalHitpoints && state.Position[id].Nearest(to))
                    {
                        var can = state.act[id]/game.FieldMedicHealCost;
                        var by = i == id ? game.FieldMedicHealSelfBonusHitpoints : game.FieldMedicHealBonusHitpoints;
                        var need = Math.Min(can, (to.MaximalHitpoints - state.hit[i] + by - 1) / by);
                        var cost = need*game.FieldMedicHealCost;
                        var healBy = by*need;

                        var oldhit = state.hit[i];
                        state.act[id] -= cost;
                        state.hit[i] = Math.Min(to.MaximalHitpoints, state.hit[i] + healBy);
                        stack[id].Add("heal " + to.X + " " + to.Y + " " + need);
                        dfs_move(4, false);
                        stack[id].RemoveAt(stack[id].Count - 1);
                        state.hit[i] = oldhit;
                        state.act[id] += cost;
                    }
                }
            }
        }

        private void dfs_grenade()
        {
            int id = state.id;

            if (state.Grenade && state.act[id] >= game.GrenadeThrowCost)
            {
                Point bestPoint = Point.Inf;
                foreach (Trooper trooper in Opponents)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int ni = trooper.X + _i[k];
                        int nj = trooper.Y + _j[k];
                        if (ni >= 0 && nj >= 0 && ni < Width && nj < Height && notFilledMap[ni, nj] == 0
                            && state.Position[id].GetDistanceTo(ni, nj) <= game.GrenadeThrowRange)
                        {
                            int profit = 0;
                            int loop = 0;
                            foreach (Trooper tr in Opponents)
                            {
                                int dist = Math.Abs(ni - tr.X) + Math.Abs(nj - tr.Y);
                                if (dist == 0)
                                    profit += Math.Min(state.opphit[loop], game.GrenadeDirectDamage);
                                else if (dist == 1)
                                    profit += Math.Min(state.opphit[loop], game.GrenadeCollateralDamage);
                                loop++;
                            }
                            if (bestPoint.profit < profit)
                                bestPoint.Set(ni, nj, profit);
                        }
                    }
                }
                if (bestPoint.profit >= game.GrenadeDirectDamage)
                {
                    int loop = 0;
                    int[] Damage = new int[OpponentsCount];
                    for (int i = 0; i < OpponentsCount; i++)
                    {
                        Trooper opp = Opponents[i];
                        int dist = Math.Abs(bestPoint.X - opp.X) + Math.Abs(bestPoint.Y - opp.Y);
                        if (dist == 0)
                        {
                            Damage[i] = Math.Min(state.opphit[loop], game.GrenadeDirectDamage);
                            state.opphit[i] -= Damage[i];
                        }
                        else if (dist == 1)
                        {
                            Damage[i] = Math.Min(state.opphit[loop], game.GrenadeCollateralDamage);
                            state.opphit[i] -= Damage[i];
                        }
                        loop++;
                    }
                    state.Grenade = false;
                    stack[id].Add("gr " + bestPoint.X + " " + bestPoint.Y);
                    state.act[id] -= game.GrenadeThrowCost;
                    state.profit += bestPoint.profit;
                    dfs_move(4);
                    state.profit -= bestPoint.profit;
                    state.act[id] += game.GrenadeThrowCost;
                    stack[id].RemoveAt(stack[id].Count - 1);
                    state.grenade[id] = true;
                    for (int i = 0; i < OpponentsCount; i++)
                        state.opphit[i] += Damage[i];
                }
            }
        }

        void dfs_changeStance5(bool allowShot = true)
        {
            var id = state.id;

            int upper = 2, lower = -2;
            for (int deltaStance = upper; deltaStance >= lower; deltaStance--)
            {
                state.Stance += deltaStance;
                if (state.Stance >= 0 && state.Stance < 3)
                {
                    var cost = game.StanceChangeCost * Math.Abs(deltaStance);
                    if (cost <= state.act[id])
                    {
                        state.act[id] -= cost;
                        if (deltaStance != 0)
                            stack[id].Add("st " + deltaStance);
                        if (allowShot)
                            dfs_shot();
                        dfs_end();
                        if (deltaStance != 0)
                            stack[id].RemoveAt(stack[id].Count - 1);
                        state.act[id] += cost;
                    }
                }
                state.Stance -= deltaStance;
            }
        }

        private void dfs_shot()
        {
            var id = state.id;

            int bestIdx = -1;
            int minHit = Inf;
            // Выбираю цель с меньшим количеством жизней
            for (int idx = 0; idx < OpponentsCount; idx++)
            {
                var opp = Opponents[idx];
                if (state.opphit[idx] > 0 &&
                    world.IsVisible(Troopers[id].ShootingRange, state.X, state.Y, getStance(state.Stance), opp.X,
                        opp.Y, opp.Stance))
                {
                    if (state.opphit[idx] < minHit)
                    {
                        minHit = state.opphit[idx];
                        bestIdx = idx;
                    }
                }
            }
            if (bestIdx != -1)
            {
                var oldOppHit = state.opphit[bestIdx];
                var oldProfit = state.profit;

                var opp = Opponents[bestIdx];
                var can = state.act[id]/Troopers[id].ShootCost;
                var damage = Troopers[id].GetDamage(getStance(state.Stance));
                var need = Math.Min(can, (minHit + damage - 1)/damage);
                for (int cnt = 1; cnt <= need; cnt++)
                {
                    // cnt - сколько выстрелов сделаю
                    var p = damage*cnt;
                    state.opphit[bestIdx] = Math.Max(0, oldOppHit - p);
                    state.profit += id == 0 ? p * 1.2 : p;
                    if (oldOppHit - p <= 0)
                        state.profit += KillBonus;
                    state.act[id] -= cnt*Troopers[id].ShootCost;
                    var oldStackSize = stack[id].Count;
                    for (int k = 0; k < cnt; k++)
                        stack[id].Add("sh " + opp.X + " " + opp.Y);
                    dfs_changeStance1(allowShot: false);
                    stack[id].RemoveRange(oldStackSize, stack[id].Count - oldStackSize);
                    state.act[id] += cnt*Troopers[id].ShootCost;
                    state.profit = oldProfit;
                    state.opphit[bestIdx] = oldOppHit;
                }
            }
        }

        void dfs_end()
        {
            var id = state.id;
            double profit = state.profit;

            // Штраф за большой радиус
            double centerX = 0, centerY = 0;
            foreach (Point position in state.Position)
            {
                centerX += position.X;
                centerY += position.Y;
            }
            centerX /= MyCount;
            centerY /= MyCount;
            foreach (Point position in state.Position)
                profit -= position.GetDistanceTo(centerX, centerY);

            var oldHit = state.hit[id];
            bool ok = false;
            for (int i = 0; i < OpponentsCount; i++)
            {
                if (state.opphit[i] > 0)
                {
                    var opp = Opponents[i];
                    if (world.IsVisible(opp.ShootingRange, opp.X, opp.Y, opp.Stance, state.X, state.Y,
                        getStance(state.Stance)))
                    {
                        state.hit[id] -= 100;
                        ok = true;
                    }
                }
            }
            if (!ok)
            {
                for (int i = 0; i < OpponentsCount; i++)
                {
                    if (state.opphit[i] > 0)
                    {
                        var opp = Opponents[i];
                        if (world.IsVisible(opp.VisionRange, opp.X, opp.Y, opp.Stance, state.X, state.Y,
                            getStance(state.Stance)))
                        {
                            state.hit[id] -= 50;
                            break;
                        }
                    }
                }
            }

            if (id == 1 || id == MyCount - 1)
            {
                // К профиту прибавляю итоговое количество жизней
                for (int i = 0; i < MyCount; i++)
                    profit += state.hit[i] * 3;

                counter++;
                if (profit > bestProfit)
                {
                    bestProfit = profit;
                    bestStack = new ArrayList[MyCount];
                    for (int i = 0; i < MyCount; i++)
                        bestStack[i] = stack[i].Clone() as ArrayList;
                }
            }
            else
            {
                // EndTurn
                var oldProfit = state.profit;
                state.profit = profit;
                int prevId = state.id;
                state.id = (state.id + 1)%MyCount;
                dfs_changeStance1();
                state.profit = oldProfit;
                state.id = prevId;
            }
            state.hit[id] = oldHit;
        }

        int getInitialActionPoints(Trooper tr)
        {
            int x = tr.X, y = tr.Y;
            int points = tr.InitialActionPoints;
            if (CommanderId != -1 && tr.GetDistanceTo(state.Position[CommanderId].X, state.Position[CommanderId].Y) <= game.CommanderAuraRange)
                points += game.CommanderAuraBonusActionPoints;
            return points;
        }

        private bool Validate(State state)
        {
            // TODO: можно ускорить если заполнять карту
            if (
                !(state.X >= 0 && state.Y >= 0 && state.X < Width && state.Y < Height &&
                  notFilledMap[state.X, state.Y] == 0))
                return false;
            
            // Проверяю чтобы не стать в занятую клетку
            return state.Position.Count(p => p.X == state.X && p.Y == state.Y) < 2;
        }

        Move BruteForceDo()
        {
            int fictive = 0;
            if (queue.Count < Team.Count())
            {
                foreach (Trooper tr in Team)
                {
                    if (!queue.Contains(tr.Id))
                    {
                        queue.Add(tr.Id);
                        fictive++;
                    }
                }
            }

            state = new State();
            state.Position = new Point[Team.Count()];
            state.stance = new int[Team.Count()];
            state.act = new int[Team.Count()];
            state.hit = new int[Team.Count()];
            state.medikit = new bool[Team.Count()];
            state.grenade = new bool[Team.Count()];
            Troopers = new Trooper[Team.Count()];
            CommanderId = -1;
            foreach (Trooper tr in troopers)
            {
                if (tr.IsTeammate)
                {
                    int pos = getQueuePlace2(tr, true) - 1;
                    state.Position[pos] = new Point(tr);
                    state.stance[pos] = getStanceId(tr.Stance);
                    state.medikit[pos] = tr.IsHoldingMedikit;
                    state.grenade[pos] = tr.IsHoldingGrenade;
                    Troopers[pos] = tr;
                    if (tr.Type == TrooperType.Commander)
                        CommanderId = pos;
                }
            }
            state.id = 0;
            state.profit = 0;
            state.act[0] = Troopers[0].ActionPoints;
            MyCount = state.Position.Count();
            for(int i = 0; i < MyCount; i++)
                state.hit[i] = Troopers[i].Hitpoints;
            for (int i = 1; i < Troopers.Count(); i++)
                state.act[i] = getInitialActionPoints(Troopers[i]);
            stack = new ArrayList[MyCount];
            bestStack = new ArrayList[MyCount];
            for (int i = 0; i < MyCount; i++)
            {
                stack[i] = new ArrayList();
                bestStack[i] = null;
            }
            bestProfit = -Inf;
            counter = 0;
            OpponentsCount = Opponents.Count();
            MyCount = state.Position.Count();
            state.opphit = new int[OpponentsCount];
            for (int i = 0; i < OpponentsCount; i++)
            {
                state.opphit[i] = Opponents[i].Hitpoints;
            }
            dfs_changeStance1();

            if (bestStack[0] == null)
                return null;
            var move = new Move();
            if (bestStack[0].Count == 0)
                return move; // EndTurn
            var cmd = ((string)bestStack[0][0]).Split(' ');
            if (cmd[0] == "st")
            {
                // change stance
                int ds = int.Parse(cmd[1]);
                if (ds < 0)
                    move.Action = ActionType.LowerStance;
                else if (ds > 0)
                    move.Action = ActionType.RaiseStance;
                else
                    throw new Exception("");
            }
            else if (cmd[0] == "at")
            {
                int x = int.Parse(cmd[1]);
                int y = int.Parse(cmd[2]);
                Point To = goToUnit(self, new Point(x, y), map, beginFree: true, endFree: false);
                move.Action = ActionType.Move;
                move.X = To.X;
                move.Y = To.Y;
            }
            else if (cmd[0] == "sh")
            {
                int x = int.Parse(cmd[1]);
                int y = int.Parse(cmd[2]);
                move.Action = ActionType.Shoot;
                move.X = x;
                move.Y = y;
            }
            else if (cmd[0] == "med")
            {
                Point to = new Point(int.Parse(cmd[1]), int.Parse(cmd[2]));
                move.Action = ActionType.UseMedikit;
                move.X = to.X;
                move.Y = to.Y;
            }
            else if (cmd[0] == "gr")
            {
                Point to = new Point(int.Parse(cmd[1]), int.Parse(cmd[2]));
                move.Action = ActionType.ThrowGrenade;
                move.X = to.X;
                move.Y = to.Y;
            }
            else if (cmd[0] == "heal")
            {
                Point to = new Point(int.Parse(cmd[1]), int.Parse(cmd[2]));
                move.Action = ActionType.Heal;
                move.X = to.X;
                move.Y = to.Y;
            }
            else
            {
                throw new NotImplementedException(cmd.ToString());
            }

            return move;
        }
    }
}