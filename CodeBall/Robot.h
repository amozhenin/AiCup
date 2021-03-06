#ifndef CODEBALL_ROBOT_H
#define CODEBALL_ROBOT_H

#include "Unit.h"
#include "Action.h"
#include "model/Robot.h"

struct ARobot : public Unit {
    int id;
    //int playerId;
    double nitroAmount;
    Point touchNormal;
    AAction action; // field for simulator
    double radius_change_speed = 0; // tmp field for simulator
    bool isTeammate;
    bool touch;

    ARobot() : Unit() {
        touch = false;
        nitroAmount = 0;
        //playerId = 0;
        id = 0;
        isTeammate = false;
    }

    explicit ARobot(const model::Robot& robot) {
        id = robot.id;
        //playerId = robot.player_id;
        isTeammate = robot.is_teammate;
        x = robot.x;
        y = robot.y;
        z = robot.z;
        velocity.x = robot.velocity_x;
        velocity.y = robot.velocity_y;
        velocity.z = robot.velocity_z;
        radius = robot.radius;
        nitroAmount = robot.nitro_amount;
        touch = robot.touch;
        touchNormal.x = robot.touch_normal_x;
        touchNormal.y = robot.touch_normal_y;
        touchNormal.z = robot.touch_normal_z;
        //mass = ROBOT_MASS;
    }

    bool isDetouched() const {
        return !touch || touchNormal.y < EPS;
    }

    void invert() {
        Unit::invert();
        isTeammate = !isTeammate;
        touchNormal.x = -touchNormal.x;
        touchNormal.z = -touchNormal.z;
    }
};

#endif //CODEBALL_ROBOT_H
