using System;
using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    /// <summary>What a level asks the player to achieve (design Section 13.1). Counted from today's stats.</summary>
    public enum GoalType
    {
        /// <summary>Feeds finished by the parent.</summary>
        Feeds,
        Burps,
        DiaperChanges,
        /// <summary>Times the baby fell asleep (put to sleep or, later, self-soothed).</summary>
        BabyNaps,
        /// <summary>Parent Energy strictly above the target when time runs out.</summary>
        EnergyAtEnd,
    }

    [Serializable]
    public class LevelGoal
    {
        public GoalType type;
        public double target;
        /// <summary>Shown in the briefing and the objective checklist.</summary>
        public string text;
    }

    /// <summary>Level-specific failure conditions, on top of the general ones of design Section 13.2.</summary>
    public enum FailConditionType
    {
        /// <summary>Fails as soon as parent Energy is at or below the value.</summary>
        EnergyAtOrBelow,
        /// <summary>Fails as soon as the baby gets diaper rash.</summary>
        DiaperRash,
    }

    [Serializable]
    public class FailCondition
    {
        public FailConditionType type;
        public double value;
        /// <summary>Shown on the failure screen.</summary>
        public string text;
    }

    /// <summary>Star rules of design Section 13.3.</summary>
    [Serializable]
    public class StarRules
    {
        /// <summary>2 stars: average baby Wellbeing at least this.</summary>
        public double twoStarWellbeing = 70;
        /// <summary>3 stars: Joy at least this at the end (the signature Golden Moment joins in M4).</summary>
        public double threeStarJoy = 60;
        /// <summary>Most stars a level can earn after a Call for help.</summary>
        public int helpStarCap = 2;
    }

    /// <summary>Start values that differ from the Tuning day-start values; a negative number keeps the default.</summary>
    [Serializable]
    public class LevelStart
    {
        public double babyHunger = -1;
        public double babyTiredness = -1;
        public double parentEnergy = -1;
        public double parentHunger = -1;
        public double parentStress = -1;
        public double parentJoy = -1;
        public bool babyAsleep;
    }

    /// <summary>One level (design Section 14): objective, time limit, crying budget, fail conditions, stars and actions.</summary>
    [Serializable]
    public class LevelDefinition
    {
        public int number;
        public string title;
        /// <summary>The baby's age in months, 0-based (level N is month N-1).</summary>
        public int month;
        /// <summary>One line, shown on the level map and in the HUD.</summary>
        public string objective;
        /// <summary>A few sentences, shown before the level starts.</summary>
        public string briefing;
        /// <summary>A tip shown on the failure screen, on top of the one for the fail reason.</summary>
        public string failHint;
        /// <summary>Shows step-by-step coaching hints (Level 1).</summary>
        public bool tutorial;
        public int startHour = 7;
        public double lengthMinutes = 1440;
        public double cryingBudgetMinutes = 180;
        public double maxContinuousCryMinutes = 30;
        /// <summary>Money received when the level starts (parental-leave allowance).</summary>
        public int allowance;
        /// <summary>How often the grandparents can be called (0: not in this level).</summary>
        public int helpCalls = 1;
        public List<LevelGoal> goals = new List<LevelGoal>();
        public List<FailCondition> failConditions = new List<FailCondition>();
        /// <summary>The parent actions available in this level (Stop always is).</summary>
        public List<ActionType> actions = new List<ActionType>();
        public StarRules stars = new StarRules();
        public LevelStart start = new LevelStart();
        /// <summary>The signature Golden Moment, shown on the briefing; it can be caught from M4.</summary>
        public string signatureMoment;

        public bool Allows(ActionType type) => type == ActionType.Stop || (actions != null && actions.Contains(type));
    }

    [Serializable]
    public class Chapter
    {
        public int number;
        public string title;
        public int firstLevel;
        public int lastLevel;

        public bool Contains(int level) => level >= firstLevel && level <= lastLevel;
    }

    /// <summary>All chapters and the levels built so far.</summary>
    [Serializable]
    public class LevelTable
    {
        public const int LastLevel = 36;

        public List<Chapter> chapters = new List<Chapter>();
        public List<LevelDefinition> levels = new List<LevelDefinition>();

        /// <summary>The level with this number, or null when it is not built yet.</summary>
        public LevelDefinition Find(int number)
        {
            foreach (var level in levels)
                if (level.number == number) return level;
            return null;
        }

        public Chapter ChapterOf(int level)
        {
            foreach (var chapter in chapters)
                if (chapter.Contains(level)) return chapter;
            return null;
        }

        static readonly ActionType[] NewbornActions =
        {
            ActionType.Breastfeed, ActionType.PrepareBottle, ActionType.BottleFeed, ActionType.Burp, ActionType.ChangeDiaper,
            ActionType.Soothe, ActionType.PutBabyToSleep, ActionType.Play, ActionType.Eat, ActionType.Rest, ActionType.Sleep,
            ActionType.OrderSupplies, ActionType.CallForHelp,
        };

        public static LevelTable CreateDefault() => new LevelTable
        {
            chapters = new List<Chapter>
            {
                new Chapter { number = 1, title = "Newborn", firstLevel = 1, lastLevel = 3 },
                new Chapter { number = 2, title = "Getting to Know You", firstLevel = 4, lastLevel = 6 },
                new Chapter { number = 3, title = "Solids & Squirms", firstLevel = 7, lastLevel = 9 },
                new Chapter { number = 4, title = "On the Move", firstLevel = 10, lastLevel = 13 },
                new Chapter { number = 5, title = "Toddler", firstLevel = 14, lastLevel = 24 },
                new Chapter { number = 6, title = "Big Kid", firstLevel = 25, lastLevel = 36 },
            },
            levels = new List<LevelDefinition>
            {
                new LevelDefinition
                {
                    number = 1, title = "First Day Home", month = 0, tutorial = true,
                    objective = "Feed, burp, change and settle the baby, and get through to tomorrow morning.",
                    briefing = "Your first full day at home together. The baby will tell you what they need - "
                               + "watch the need icons and the body language. Get through to 07:00 tomorrow without too much crying.",
                    failHint = "Feed the baby when the hunger icon turns into a triangle, before the crying starts.",
                    startHour = 7, lengthMinutes = 1440, cryingBudgetMinutes = 180, maxContinuousCryMinutes = 30,
                    allowance = 0, helpCalls = 1,
                    goals = new List<LevelGoal>
                    {
                        new LevelGoal { type = GoalType.Feeds, target = 1, text = "Feed the baby" },
                        new LevelGoal { type = GoalType.Burps, target = 1, text = "Burp after a feed" },
                        new LevelGoal { type = GoalType.DiaperChanges, target = 1, text = "Change a diaper" },
                        new LevelGoal { type = GoalType.BabyNaps, target = 1, text = "Put the baby to sleep" },
                    },
                    actions = new List<ActionType>(NewbornActions),
                    signatureMoment = "The baby grips your finger.",
                },
                new LevelDefinition
                {
                    number = 2, title = "Night Shift", month = 1,
                    objective = "Night feeds every 2-3 hours. Sleep whenever the baby sleeps, and reach 07:00 with Energy above 20.",
                    briefing = "It's 21:00 and the baby is tired after a long day. Settle them in the crib, then get some sleep yourself: "
                               + "time runs four times faster while you sleep. The baby will wake you for feeds through the night.",
                    failHint = "Go to bed as soon as the baby is asleep - every hour of sleep restores Energy.",
                    startHour = 21, lengthMinutes = 600, cryingBudgetMinutes = 60, maxContinuousCryMinutes = 30,
                    allowance = 40, helpCalls = 1,
                    goals = new List<LevelGoal>
                    {
                        new LevelGoal { type = GoalType.Feeds, target = 3, text = "Night feeds" },
                        new LevelGoal { type = GoalType.EnergyAtEnd, target = 20, text = "Energy above 20 at 07:00" },
                    },
                    actions = new List<ActionType>(NewbornActions),
                    start = new LevelStart { babyHunger = 15, babyTiredness = 65, parentEnergy = 40, parentHunger = 20, parentStress = 20 },
                    signatureMoment = "Asleep on your chest at 3 AM.",
                },
            },
        };
    }
}
