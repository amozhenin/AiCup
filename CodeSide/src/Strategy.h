#ifndef CODESIDE_STRATEGY_H
#define CODESIDE_STRATEGY_H

#include "constants.h"
#include "logger.h"
#include "sandbox.h"
#include "draw.h"
#include "findpath.h"
#include "testing.h"

#include <algorithm>
#include <cassert>

/**
 * - пошел за аптечкой напролом через противника, не прошел, проиграл
 * - собрал все аптечки, не осталось аптечки соперницу, выиграл
 * - собрал аптечку с минимумом урона, проиграл, т.к. у соперника аптечка осталась
 * - не учитывается падение соперника при прицеливании
 * - пп от соперника и отклонения с учетом этого
 * - урон от ракеты суммируется => вплотную против неё лучше не становиться
 * - пп от стен, если соперник с базукой
 * - пп в пользу стояния на лестнице (легко уклоняться)
 * - стрелдять можно посреди тика, даже если unit.canShot() изначально false
 * - в findpath поощрять за бонусы
 * - предсказывать dx противника
 */

class Strategy {
    TSandbox prevEnv, prevEnv2, env;
    TPathFinder pathFinder;

    std::unordered_map<ELootType, int> weaponPriority = {
            {ELootType::PISTOL, 0},
            {ELootType::ASSAULT_RIFLE, 1},
            {ELootType::ROCKET_LAUNCHER, 2},
            {ELootType::NONE, 3},
    };

    std::optional<std::vector<TAction>> _strategyLoot(const TUnit& unit, std::set<ELootType> lootTypes) {
        std::map<std::pair<int, int>, double> lbPathPenalty;
        pathFinder.traverseReachable(unit, [&](double dist, const TUnit& unit, const TState& state) {
            TLootBox* lb;
            if ((lb = env.findLootBox(unit)) != nullptr) {
                if (lootTypes.count(lb->type)) {
                    if (!lbPathPenalty.count({lb->getRow(), lb->getCol()})) {
                        std::vector<TState> pts;
                        std::vector<TAction> acts;
                        double penalty = 0;
                        if (pathFinder.findPath(unit.position(), pts, acts) && !acts.empty()) {
                            for (const auto& s : pts) {
                                penalty += pathFinder.penalty[s.x][s.y];
                            }
                        }
                        TDrawUtil::debug->draw(CustomData::PlacedText(std::to_string(penalty + dist),
                                                                      Vec2Float{float(lb->x1), float(lb->y2 + 1)},
                                                                      TextAlignment::LEFT,
                                                                      30,
                                                                      ColorFloat(0, 1, 0, 0.75)));
                        lbPathPenalty[{lb->getRow(), lb->getCol()}] = penalty;
                    }
                }
            }
        });

        std::vector<TState> pathPoints;
        std::vector<TAction> actions;

        double minDist = INF;
        TLootBox* selectedLb = nullptr;
        TPoint selectedPos;
        TState selectedState;

        pathFinder.traverseReachable(unit, [&](double dist, const TUnit& unit, const TState& state) {
            TLootBox* lb;
            if ((lb = env.findLootBox(unit)) != nullptr) {
                if (lootTypes.count(lb->type)) {
                    dist += lbPathPenalty[{lb->getRow(), lb->getCol()}];
                    if (dist < minDist) {
                        minDist = dist;
                        selectedLb = lb;
                        selectedPos = unit.position();
                        selectedState = state;
                    }
                }
            }
        });
        if (selectedLb != nullptr) {
            if (pathFinder.findPath(selectedPos, pathPoints, actions) && actions.size() > 0) {
                TDrawUtil().drawPath(statesToPoints(pathPoints));
                return actions;
            }
        }
        return {};
    }

    std::optional<std::vector<TAction>> _strategy(const TUnit& unit) {
        std::vector<TState> pathPoints;
        std::vector<TAction> actions;

#if M_DRAW_REACHABILITY_X > 0 && M_DRAW_REACHABILITY_Y > 0
        pathFinder.traverseReachable(unit, [&](double dist, const TUnit& unit, const TState& state) {
            if (state.x % M_DRAW_REACHABILITY_X == 0 && state.y % M_DRAW_REACHABILITY_Y == 0) {
                auto p = state.getPoint();
                TDrawUtil::debug->draw(CustomData::Rect({float(p.x), float(p.y)}, {0.05, 0.05}, ColorFloat(0, 1, 0, 1)));
            }
        });
#endif

        if (unit.weapon.type == ELootType::NONE) {
            auto maybeAct = _strategyLoot(unit, {ELootType::PISTOL, ELootType::ROCKET_LAUNCHER, ELootType::ASSAULT_RIFLE});
            if (maybeAct) {
                return maybeAct;
            }
        }
        if (unit.health <= 80) {
            auto maybeAct = _strategyLoot(unit, {ELootType::HEALTH_PACK});
            if (maybeAct) {
                return maybeAct;
            }
        }

        TUnit* target = nullptr;
        for (auto& u : env.units) {
            if (u.playerIdx != unit.playerIdx) {
                target = &u;
            }
        }

        if (unit.weapon.type != ELootType::ROCKET_LAUNCHER) {
            for (const auto &opp : env.units) {
                if (!opp.isMy()) {
                    if (opp.health <= 80 && unit.health <= opp.health) {
                        auto maybeAct = _strategyLoot(unit, {ELootType::ROCKET_LAUNCHER});
                        if (maybeAct) {
                            return maybeAct;
                        }
                        break;
                    }
                }
            }
        }

        if (target != nullptr && unit.weapon.fireTimer > 0.5) {
            double rangeMin = 5, rangeMax = 7;
//            if (unit.weapon.fireTimer > 0.5) {
                rangeMin = 10;
                rangeMax = 14;
//            }


            double minDist = INF;
            TPoint selectedPoint;
            pathFinder.traverseReachable(unit, [&](double dist, const TUnit& unit, const TState& state) {
                auto dist2ToTarget = unit.center().getDistanceTo2(target->center());
                if (dist < minDist && isIn(SQR(rangeMin), SQR(rangeMax), dist2ToTarget)) {
                    minDist = dist;
                    selectedPoint = unit.position();
                }
            });
            if (minDist < 1e-9) {
                return std::vector<TAction>(1, TAction());
            }
            if (minDist < INF && pathFinder.findPath(selectedPoint, pathPoints, actions) && actions.size() > 0) {
                TDrawUtil().drawPath(statesToPoints(pathPoints));
                return actions;
            }
        }



        if (target != nullptr) {
            if (pathFinder.findPath(target->position(), pathPoints, actions) && actions.size() > 0) {
                TDrawUtil().drawPath(statesToPoints(pathPoints));
                return actions;
            }
        }
        return {};
    }

public:
    UnitAction getAction(const Unit& _unit, const Game& game, Debug& debug) {
        //return TAction().toUnitAction();
        TDrawUtil().drawGrid();
        TUnit unit(_unit);
        env = TSandbox(unit, game);
        TDrawUtil().drawMinesRadius(env);
        TDrawUtil().drawUnits(env);
        if (env.currentTick == 0) {
            TPathFinder::initMap();
        }
        pathFinder = TPathFinder(&env, unit);

        if (env.currentTick == 667) {
            env.currentTick += 0;
        }
        if (env.currentTick > 1) {
            prevEnv2 = prevEnv;
            prevEnv.doTick();
            _compareState(prevEnv, env, prevEnv2);
        }

        //auto action = _ladderLeftStrategy(unit, env, debug);


        auto maybeActions = _strategy(unit);
        auto actions = maybeActions ? maybeActions.value() : std::vector<TAction>();

        TSandbox notDodgeEnv = env;
        //notDodgeEnv.oppShotSimpleStrategy = true;
        for (int i = 0; i < 40; i++) {
            notDodgeEnv.getUnit(unit.id)->action = i < actions.size() ? actions[i] : TAction();
            notDodgeEnv.doTick();
        }
        auto startOppScore = env.score[1];
        auto scorer = [&](TSandbox& env) {
            return env.getUnit(unit.id)->health - (env.score[1] - startOppScore) / 2;
        };
        auto bestScore = scorer(notDodgeEnv);
        std::optional<std::vector<TPoint>> bestPath;

        OP_START(DODGE);
        for (int dirX = -1; dirX <= 1; dirX++) {
            for (int dirY = -1; dirY <= 1; dirY += 1) {
                TSandbox dodgeEnv = env;
                //dodgeEnv.oppShotSimpleStrategy = true;
                std::vector<TPoint> path;
                TAction act;
                if (dirY > 0)
                    act.jump = true;
                else if (dirY < 0)
                    act.jumpDown = true;
                act.velocity = dirX * UNIT_MAX_HORIZONTAL_SPEED;
                const int simulateTicks = 27;
                for (int i = 0; i < simulateTicks; i++) {
                    dodgeEnv.getUnit(unit.id)->action = act;
                    dodgeEnv.doTick();
                    path.push_back(dodgeEnv.getUnit(unit.id)->position());
                }
                if (scorer(dodgeEnv) > bestScore || (scorer(dodgeEnv) == bestScore && dodgeEnv.getUnit(unit.id)->health > env.getUnit(unit.id)->health)) {
                    bestScore = scorer(dodgeEnv);
                    bestPath = path;
                    actions = std::vector<TAction>(simulateTicks, act);
                }
            }
        }
        OP_END(DODGE);
        if (bestPath) {
            TDrawUtil().drawPath(bestPath.value(), ColorFloat(1, 0, 0, 1));
        }

        TAction action = actions.size() > 0 ? actions[0] : TAction();

        auto maybeShot = shotStrategy(unit, actions);
        if (maybeShot) {
            action.shoot = maybeShot.value().shoot;
            action.aim = maybeShot.value().aim;
        }

        auto reloadSwapAction = _reloadSwap(unit);
        if (!action.shoot && reloadSwapAction.reload) {
            action.reload = true;
        }
        if (!action.shoot && reloadSwapAction.swapWeapon) {
            action.swapWeapon = true;
        }
        TDrawUtil().drawAim(unit, action);

        for (auto& u : env.units) {
            if (u.id == unit.id) {
                u.action = action;
            }
        }
        prevEnv = env;
        return action.toUnitAction();
    }

    std::optional<TAction> shotStrategy(const TUnit& unit, const std::vector<TAction>& actions) {
        if (unit.weapon.type == ELootType::NONE) {
            return {};
        }

        TUnit* target = nullptr;
        for (auto& u : env.units) {
            if (u.playerIdx != unit.playerIdx) {
                target = &u;
            }
        }
        if (target == nullptr) {
            return {};
        }

        OP_START(SHOT_STRAT);

        auto oppDodgeStrategy = [](TSandbox& afterShotEnv, int unitId, int simulateTicks) {
            int maxHealth = 0;
            int initialHealth = afterShotEnv.getUnit(unitId)->health;
            TAction bestAction;
            for (int dirX = -1; dirX <= 1; dirX++) {
                for (int dirY = -1; dirY <= 1; dirY += 1) {
                    TSandbox dodgeEnv = afterShotEnv;
                    auto opp = dodgeEnv.getUnit(unitId);
                    TAction act;
                    if (dirY > 0) {
                        act.jump = true;
                    } else if (dirY < 0) {
                        act.jumpDown = true;
                    }
                    act.velocity = dirX * UNIT_MAX_HORIZONTAL_SPEED;
                    opp->action = act;
                    for (int i = 0; i < simulateTicks && opp->health > maxHealth; i++) {
                        dodgeEnv.doTick(5);
                    }
                    if (opp->health > maxHealth) {
                        maxHealth = opp->health;
                        bestAction = act;
                        if (opp->health == initialHealth) {
                            return bestAction;
                        }
                    }
                }
            }
            return bestAction;
        };

        TAction action = actions.empty() ? TAction() : actions[0];
        action.aim = calcAim(unit, *target, actions);
        if (unit.canShot()) {
            const int itersCount = 6;
            int successCount = 0;
            int friendlyFails = 0;
            for (int it = -itersCount; it <= itersCount; it++) {
                auto testEnv = env;
                auto testNothingEnv = env;
                testEnv.getUnit(unit.id)->action = action;
                testEnv.getUnit(unit.id)->action.shoot = true;
                testEnv.shotSpreadToss = 1.0 * it / itersCount;
                const int simulateTicks = 15;
                for (int i = 0; i < simulateTicks; i++) {
                    testEnv.doTick();
                    testNothingEnv.doTick();
                    testEnv.getUnit(unit.id)->action.shoot = false;
                    if (i == 0) {
                        for (auto& u : testEnv.units) {
                            if (u.id != unit.id || unit.weapon.type == ELootType::ROCKET_LAUNCHER) {
                                u.action = oppDodgeStrategy(testEnv, u.id, simulateTicks - 1);
                            }
                        }
                    }
                }
                auto score = (testEnv.score[0] - testNothingEnv.score[0]) - (testEnv.friendlyLoss[0] - testNothingEnv.friendlyLoss[0])*3/2;
                if (score > 0 || (testEnv.score[0] - testNothingEnv.score[0] > KILL_SCORE && testEnv.score[0] > testEnv.score[1])) {
                    successCount++;
                } else if (score < 0) {
                    friendlyFails++;
                }
            }
            double probability = successCount / (itersCount*2 + 1.0);
            double failProbability = friendlyFails / (itersCount*2 + 1.0);
#ifdef DEBUG
            TDrawUtil::debug->draw(CustomData::PlacedText(std::to_string(probability),
                                                           Vec2Float{float(unit.x1), float(unit.y2 + 2)},
                                                           TextAlignment::LEFT,
                                                           30,
                                                           ColorFloat(0, 0, 1, 1)));
            if (failProbability > 0.01) {
                TDrawUtil::debug->draw(CustomData::PlacedText(std::to_string(failProbability),
                                                              Vec2Float{float(unit.x1), float(unit.y2 + 2.5)},
                                                              TextAlignment::LEFT,
                                                              30,
                                                              ColorFloat(1, 0, 0, 1)));
            }
#endif
            if (probability >= 0.5 && failProbability < 0.01) {
                action.shoot = true;
            }
        }

        OP_END(SHOT_STRAT);
        return action;
    }

    TPoint calcAim(const TUnit& unit, const TUnit& target, const std::vector<TAction>& actions) {
        return target.center() - unit.center(); // TODO

        auto midAngle = unit.center().getAngleTo(target.center());
        double L = -M_PI / 4, R = M_PI / 4;
        for (int it = 0; it < 50; it++) {
            double m1 = L + (R - L) / 3, m2 = R - (R - L) / 3;
            if (_calcAimDist(unit, target, midAngle + m1, actions) < _calcAimDist(unit, target, midAngle + m2, actions)) {
                R = m2;
            } else {
                L = m1;
            }
        }
        double aimAngle = (L + R) / 2 + midAngle;
        return TPoint::byAngle(aimAngle);
    }

    double _calcAimDist(const TUnit& startUnit, const TUnit& target, double aimAngle, const std::vector<TAction>& actions) {
        TSandbox snd = env;
        snd.oppFallFreeze = true;
        snd.bullets.clear();
        auto aim = TPoint::byAngle(aimAngle);
        for (int i = 0; i < 60; i++) {
            auto u = snd.getUnit(startUnit.id);
            u->action = i < actions.size() ? actions[i] : TAction();
            if (u->canShot()) {
                auto tar = snd.getUnit(target.id)->center();
                return std::abs(TPoint::getAngleBetween(aim, tar - u->center()));
            }
            snd.doTick(4);
        }
        return M_PI;
    }

    TAction _reloadSwap(const TUnit& unit) {
        TAction act;

        auto lb = env.findLootBox(unit);
        if (lb != nullptr && (lb->type == ELootType::PISTOL || lb->type == ELootType::ASSAULT_RIFLE || lb->type == ELootType::ROCKET_LAUNCHER)) {
            if (weaponPriority[lb->type] < weaponPriority[unit.weapon.type]) {
                bool free = true;
                for (const auto &opp : env.units) {
                    if (!opp.isMy()) {
                        if (unit.getManhattanDistTo(opp) >= 11) {
                            continue;
                        }
                        if (unit.getManhattanDistTo(opp) < 7 && opp.weapon.fireTimer > 0.5) {
                            continue;
                        }
                        if (opp.weapon.fireTimer > 0.8) {
                            continue;
                        }
                        free = false;
                    }
                }
                act.swapWeapon = free;
            }
            if (unit.weapon.type != ELootType::ROCKET_LAUNCHER && lb->type == ELootType::ROCKET_LAUNCHER) {
                for (const auto &opp : env.units) {
                    if (!opp.isMy()) {
                        if (opp.getManhattanDistTo(unit) <= 4 && unit.health > 20 && opp.health <= 80) {
                            act.swapWeapon = true;
                        }
                        int cnt = 0;
                        for (auto& b : env.lootBoxes) {
                            cnt += b.type == ELootType::HEALTH_PACK;
                        }
                        if (cnt == 0 && opp.health <= 80 && unit.health <= opp.health) {
                            act.swapWeapon = true;
                        }
                    }
                }
            }
        }

        if (!unit.isMagazineFull()) {
            double minDist = INF;
            for (const auto &opp : env.units) {
                if (opp.isMy()) {
                    continue;
                }
                minDist = std::min(minDist, unit.getManhattanDistTo(opp));
                // TODO: проверять через traverseReachable, когда не будет штрафа
            }
            act.reload = minDist > 17;
        }
        return act;
    }
};

#endif //CODESIDE_STRATEGY_H
