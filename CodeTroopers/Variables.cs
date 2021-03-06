﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk.Model;
using System.Collections;

namespace Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk
{
    public partial class MyStrategy : IStrategy
    {
        public static int KillBonus = 50;
        public static int changedCommander = -1;
        public static Point BonusGoal = null;
        public static long WhoseBonus = -1;
        public static Point PointGoal = null;
        public static ArrayList AlivePlayers = null;
        public static ArrayList queue = new ArrayList();
        public static ArrayList TypeQueue = new ArrayList();
        public static int Inf = 0x3f3f3f3f;
        public static double Eps = 1e-9;
        public static double MaxTeamRadius;
        public static Hashtable Hitpoints = null;

        public static int DangerNothing = 0;
        public static int DangerVisible = 1;
        public static int DangerShoot = 2;
        public static int DangerHighShoot = 3;
        public static long MapHash = -1;
        public static bool AllowTakeBonus = true;

        public static Random random = new Random();

        public static TrooperType[] CommanderPriority =
        {
            TrooperType.Scout, 
            TrooperType.Commander,
            TrooperType.Soldier,
            TrooperType.Sniper, 
            TrooperType.FieldMedic,
        };

        public static TrooperType[] ShootingPriority =
        {
            TrooperType.Scout, 
            TrooperType.Commander,
            TrooperType.FieldMedic,
            TrooperType.Sniper, 
            TrooperType.Soldier, 
        };

        public World world;
        public Move move;
        public Game game;
        public Trooper self, commander;
        public Trooper[] troopers;
        public Trooper[] Team;
        public Trooper[] Friends; 
        public Trooper[] Opponents;
        public Bonus[] Bonuses;
        public CellType[][] Cells;
        public int[,] map, notFilledMap;
        public int[,] danger;
        public int Width, Height;
        public static int[][,,][,,] Distance = new int[20][,,][,,];

        private static ArrayList PastTroopersInfo = new ArrayList();
        private static int[] OpponentsMemoryAppearTime;
        private static TrooperType[] OpponentsMemoryType;

        public static long CheeserMap = 2050471340533109056L;
        public static long Lab2Map = 8060058084774534976L;

        public static double[,,] CellDangerFrom;
        public static double[,,] CellDangerTo;
        public static double[,,] CellDanger;
    }
}
