using System;
using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    public enum DiaperState { Clean, Wet, Dirty }

    public enum FeedingChoice { Breast, Formula, Both }

    public enum Outcome { Running, Survived, Failed }

    public enum FailReason { None, ParentCollapsed, CryingBudget, ContinuousCrying }

    /// <summary>Why the baby is crying. Several causes can be active at once.</summary>
    [Flags]
    public enum CryCause { None = 0, Hunger = 1, Diaper = 2, Overtired = 4, Gas = 8, Upset = 16, Bored = 32 }

    /// <summary>The visible body-language hint for a crying cause (design Section 7.1).</summary>
    public enum CryHint { None, HandToMouth, RubbingEyes, Squirming, Fussing }

    [Serializable]
    public class BabyState
    {
        public double hunger;
        public double tiredness;
        public double tension;
        public double boredom;
        public DiaperState diaper;
        /// <summary>Minutes since the diaper last got wetter or dirtier.</summary>
        public double soiledMinutes;
        public bool rash;
        public bool asleep;
        public bool needsBurp;
        public double minutesSinceFeed;
        public bool gas;
        public double gasMinutes;
        public CryCause cryCauses;
        public double cryStreakMinutes;
        public double wellbeing = 100;

        public bool Crying => cryCauses != CryCause.None;
    }

    [Serializable]
    public class ParentState
    {
        public double energy;
        public double hunger;
        public double stress;
        public double joy;
        /// <summary>The action in progress, or null when idle.</summary>
        public ActiveAction action;
    }

    [Serializable]
    public class ActiveAction
    {
        public ActionType type;
        public double elapsedMinutes;
        /// <summary>0 for continuous actions (soothe, rest, sleep), which run until stopped.</summary>
        public double durationMinutes;
        /// <summary>Baby Hunger when a feed started; the feed lowers it evenly to 0.</summary>
        public double startHunger;

        public bool Continuous => durationMinutes <= 0;
        public double Progress => Continuous ? 0 : Math.Min(1, elapsedMinutes / durationMinutes);
    }

    [Serializable]
    public class SupplyStock
    {
        public int diapers;
        public int wipes;
        public int formula;
        public int babyFood;
        public int adultFood;
        public int bottlesReady;

        public int Get(SupplyType item)
        {
            switch (item)
            {
                case SupplyType.Diapers: return diapers;
                case SupplyType.Wipes: return wipes;
                case SupplyType.Formula: return formula;
                case SupplyType.BabyFood: return babyFood;
                case SupplyType.AdultFood: return adultFood;
                default: throw new ArgumentOutOfRangeException(nameof(item));
            }
        }

        public void Add(SupplyType item, int amount)
        {
            switch (item)
            {
                case SupplyType.Diapers: diapers += amount; break;
                case SupplyType.Wipes: wipes += amount; break;
                case SupplyType.Formula: formula += amount; break;
                case SupplyType.BabyFood: babyFood += amount; break;
                case SupplyType.AdultFood: adultFood += amount; break;
                default: throw new ArgumentOutOfRangeException(nameof(item));
            }
        }
    }

    [Serializable]
    public class Delivery
    {
        public SupplyType item;
        public int quantity;
        public double arrivesAtMinute;
    }

    /// <summary>Per-day measurements for scoring and the balance simulator.</summary>
    [Serializable]
    public class DayStats
    {
        public double cryingMinutes;
        public double longestCryMinutes;
        /// <summary>Crying minutes per cause; a minute with two causes counts for both.</summary>
        public double cryHungerMinutes;
        public double cryDiaperMinutes;
        public double cryOvertiredMinutes;
        public double cryGasMinutes;
        public double cryUpsetMinutes;
        public double cryBoredMinutes;
        public double minEnergy = 100;
        public double wellbeingSum;
        public int wellbeingSamples;
        /// <summary>Parent Energy at the start of each in-game hour.</summary>
        public List<double> energyByHour = new List<double>();
        public int feeds;
        public int burps;
        public int diaperChanges;
        public int babyNaps;
        public int diapersUsed;
        public int wipesUsed;
        public int formulaUsed;
        public int mealsEaten;
        public int ordersPlaced;
        public int moneySpent;
        public double parentSleepMinutes;

        public double AverageWellbeing => wellbeingSamples == 0 ? 0 : wellbeingSum / wellbeingSamples;
    }

    /// <summary>The complete, serializable state of one simulated day. Save files are this object as JSON.</summary>
    [Serializable]
    public class SimState
    {
        public int version = 1;
        public int month;
        public FeedingChoice feeding;
        /// <summary>In-game minutes since the day started (07:00).</summary>
        public double minute;
        public uint randomState;
        public int money;
        public BabyState baby = new BabyState();
        public ParentState parent = new ParentState();
        public SupplyStock supplies = new SupplyStock();
        public List<Delivery> deliveries = new List<Delivery>();
        public DayStats stats = new DayStats();
        public Outcome outcome;
        public FailReason failReason;
    }
}
