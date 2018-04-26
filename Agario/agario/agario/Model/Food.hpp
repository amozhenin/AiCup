#pragma once

#include "CircularUnit.hpp"
#include "../Config.hpp"

struct Food : CircularUnit
{
	Food(const nlohmann::json &obj) : CircularUnit(obj)
	{
		mass = Config::FOOD_MASS;
		radius = Config::FOOD_RADIUS;
	}
};