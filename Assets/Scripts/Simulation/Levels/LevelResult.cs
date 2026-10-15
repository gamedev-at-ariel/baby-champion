using System;
using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    /// <summary>How far one goal of the level is.</summary>
    public readonly struct GoalProgress
    {
        public readonly GoalType Type;
        public readonly string Text;
        public readonly double Target;
        public readonly double Current;
        /// <summary>Done (counted goals), or on track right now (Energy at the end).</summary>
        public readonly bool Met;

        public GoalProgress(LevelGoal goal, double current)
        {
            Type = goal.type;
            Text = goal.text;
            Target = goal.target;
            Current = current;
            Met = IsMet(goal.type, goal.target, current);
        }

        public static bool IsMet(GoalType type, double target, double current) =>
            type == GoalType.EnergyAtEnd ? current > target : current >= target - 1e-9;
    }

    /// <summary>One star condition and whether it was met, for the success screen.</summary>
    public readonly struct StarCheck
    {
        public readonly int Star;
        public readonly string Text;
        public readonly bool Met;

        public StarCheck(int star, string text, bool met)
        {
            Star = star;
            Text = text;
            Met = met;
        }
    }

    /// <summary>The outcome of a finished level with its stars (design Section 13.3).</summary>
    public sealed class LevelResult
    {
        public int Level;
        public bool Completed;
        public FailReason FailReason;
        public int Stars;
        public double AverageWellbeing;
        public double EndJoy;
        public bool HelpUsed;
        public readonly List<StarCheck> Checks = new List<StarCheck>();
    }

    public static class LevelScoring
    {
        /// <summary>
        /// 1 star: objective completed with no failure. 2: also average Wellbeing at the threshold.
        /// 3: also Joy at the threshold at the end. A Call for help caps the stars.
        /// </summary>
        public static LevelResult Score(int level, StarRules rules, bool completed, FailReason failReason,
            double averageWellbeing, double endJoy, bool helpUsed)
        {
            rules = rules ?? new StarRules();
            var result = new LevelResult
            {
                Level = level, Completed = completed, FailReason = completed ? FailReason.None : failReason,
                AverageWellbeing = averageWellbeing, EndJoy = endJoy, HelpUsed = helpUsed,
            };
            bool wellbeing = averageWellbeing >= rules.twoStarWellbeing - 1e-9;
            bool joy = endJoy >= rules.threeStarJoy - 1e-9;
            result.Checks.Add(new StarCheck(1, "Objective completed", completed));
            result.Checks.Add(new StarCheck(2, $"Average wellbeing {Math.Floor(averageWellbeing):0} (needs {rules.twoStarWellbeing:0})", wellbeing));
            result.Checks.Add(new StarCheck(3, $"Joy {Math.Floor(endJoy):0} at the end (needs {rules.threeStarJoy:0})", joy));

            int stars = !completed ? 0 : !wellbeing ? 1 : !joy ? 2 : 3;
            if (helpUsed && stars > rules.helpStarCap)
            {
                stars = rules.helpStarCap;
                result.Checks.Add(new StarCheck(rules.helpStarCap + 1, $"Grandma helped: at most {rules.helpStarCap} stars", false));
            }
            result.Stars = stars;
            return result;
        }
    }
}
