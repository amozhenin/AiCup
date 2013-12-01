using System;
using System.Collections;
using System.Linq;
using Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk
{
    public partial class MyStrategy : IStrategy
    {
        public void Move(Trooper self, World world, Game game, Move move)
        {
            this.self = self;
            this.world = world;
            this.game = game;
            this.move = move;
            InitializeConstants();
            ProcessApproximation();
            var allowHill = !CheckShootMe();
            if (BonusGoal != null && GetTrooper(MyStrategy.WhoseBonus) == null)
                BonusGoal = null;
            if (BonusGoal != null && IsHaveBonus(GetTrooper(MyStrategy.WhoseBonus), GetBonusAt(BonusGoal)))
                BonusGoal = null;
            if (IfFieldRationNeed())
            {
                Go(ActionType.EatFieldRation);
                return;
            }
            Reached(new Point(self));

            if (Opponents.Count() != 0)
            {
                // ����� ����� ���� ������ ���� ��������� ��������
                PointGoal = new Point(Opponents[0]);

                var action = BruteForceDo();
                if (action != null)
                {
                    if (Equal(self, action) && self.ActionPoints < GetMoveCost())
                        Go(ActionType.EndTurn);
                    else
                        Go(action.Action, new Point(action.X, action.Y));
                    return;
                }
            }

            if (self.Type == TrooperType.FieldMedic)
            {
                var ifHelp = IfHelpTeammate();
                if (ifHelp != null)
                {
                    var goal = GetTrooperAt(ifHelp.X, ifHelp.Y);
                    if (goal != null && goal.Hitpoints < goal.MaximalHitpoints && ifHelp.Nearest(self) && game.FieldMedicHealCost <= self.ActionPoints)
                    {
                        Go(ActionType.Heal, ifHelp);
                        return;
                    }
                    if (IsCanMove())
                    {
                        var to = GoToUnit(self, ifHelp, map, beginFree: true, endFree: true);
                        if (to != null)
                        {
                            Go(ActionType.Move, to);
                            return;
                        }
                    }
                }
            }

            var ifUseMedikit = IfUseMedikit();
            if (ifUseMedikit != null)
            {
                Go(ActionType.UseMedikit, ifUseMedikit);
                return;
            }

            Trooper whoseBonus = null;
            var ifTeamBonus = IfTeamBonus(ref whoseBonus);
            if (ifTeamBonus != null && BonusGoal == null && map[ifTeamBonus.X, ifTeamBonus.Y] == 0 && !Equal(ifTeamBonus, self))
            {
                BonusGoal = ifTeamBonus;
                MyStrategy.WhoseBonus = whoseBonus.Id;
            }

            var waitingHelp = false; //allowHill && IfNeedHelp() && self.Type != TrooperType.FieldMedic && GetBestHelper() != null;
            var allowNothing = true;
            if (!waitingHelp && IsCanMove() && BonusGoal != null && MyStrategy.WhoseBonus == self.Id)
            {
                if (IsCanUpper())
                {
                    Go(ActionType.RaiseStance);
                    return;
                }
                allowNothing = false;
                var to = GoToUnit(self, BonusGoal, map, beginFree: true, endFree: false);
                // ���� ���� �� ������ ���� ��� �����, �� ��� ����� ���� � ����
                if (to == null)
                {
                    to = GoToUnit(self, BonusGoal, notFilledMap, beginFree: true, endFree: true);
                    if (to != null && map[to.X, to.Y] == 0 && self.ActionPoints >= 2 * GetMoveCost(self)) // TODO: ???
                    {
                        Go(ActionType.Move, to);
                        return;
                    }
                }
                if (self.ActionPoints >= 2 * GetMoveCost(self))
                {
                    Go(ActionType.Move, to);
                    return;
                }
            }

            if (allowHill && IfRequestEnemyDisposition())
            {
                Go(ActionType.RequestEnemyDisposition);
                return;
            }

            // ������� ���������� ������ �� ������
            if (IsCanMove() && BonusGoal != null && MyStrategy.WhoseBonus != self.Id)
            {
                if (IsCanUpper())
                {
                    Go(ActionType.RaiseStance);
                    return;
                }
                var bestTurn = SkipPath(GetTrooper(MyStrategy.WhoseBonus), BonusGoal);
                var to = bestTurn == null ? null : GoScouting(bestTurn, IfNothingCommander() ?? new Point(commander));//GoToUnit(self, bestTurn, map, beginFree: true, endFree: false);
                if (to == null || Equal(to, self) && self.ActionPoints < GetMoveCost()) // ���� Equal(to, self)) ���� ������ move, ����� �� �� ������ �������
                    Go(ActionType.EndTurn);
                else
                    Go(ActionType.Move, to);
                return;
            }

            var ifNothing = IfNothing();
            if (allowNothing && ifNothing != null && IsCanMove())
            {
                if (IsCanUpper())
                {
                    Go(ActionType.RaiseStance);
                    return;
                }
                //Point lookAt = IfNothingCommander();
                //if (lookAt == null)
                //    lookAt = new Point(self); // TODO: �������� ��� ������
                //var to = GoScouting(ifNothing, lookAt); //GoToUnit(self, ifNothing, map, beginFree: true, endFree: false);
                var to = self.Id == commander.Id || IfNothingCommander() == null
                    ? GoToUnit(self, ifNothing, map, beginFree: true, endFree: false)
                    : GoScouting(ifNothing, IfNothingCommander());
                if (to == null || Equal(self, to) && self.ActionPoints < GetMoveCost())
                {
                    if (to == null && changedCommander == -1) // ������ �� ��������
                    {
                        // �������� �������������
                        // ���������� ������
                        ChangeCommander();
                    }
                    else
                    {
                        Go(ActionType.EndTurn);
                        return;
                    }
                }
                else if (!waitingHelp/* && (self.Id != GetCurrentLeaderId() || self.ActionPoints >= 2 * GetMoveCost(self))*/)
                {
                    Go(ActionType.Move, to);
                    return;
                }
            }

            Point go = GoScouting(new Point(self), IfNothingCommander() ?? new Point(self)); // �������� ��� ������
            if (Equal(self, go) && self.ActionPoints < GetMoveCost())
                Go(ActionType.EndTurn);
            else
                Go(ActionType.Move, go);
        }
    }
}