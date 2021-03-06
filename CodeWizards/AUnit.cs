﻿using System;
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk
{
    public class AUnit : Point
    {
        public double Angle;
        public long Id;
        public Faction Faction;

        public AUnit(Unit unit) : base(unit)
        {
            Id = unit.Id;
            Angle = unit.Angle;
            Faction = unit.Faction;
        }

        public AUnit(AUnit unit) : base(unit)
        {
            Id = unit.Id;
            Angle = unit.Angle;
            Faction = unit.Faction;
        }

        public AUnit()
        {
        }

        public double GetAngleTo(double x, double y)
        {
            var absoluteAngleTo = Math.Atan2(y - Y, x - X);
            var relativeAngleTo = absoluteAngleTo - Angle;
            return Geom.AngleNormalize(relativeAngleTo);
        }

        public double GetAngleTo(Unit unit)
        {
            return GetAngleTo(unit.X, unit.Y);
        }

        public double GetAngleTo(Point unit)
        {
            return GetAngleTo(unit.X, unit.Y);
        }
    }
}
