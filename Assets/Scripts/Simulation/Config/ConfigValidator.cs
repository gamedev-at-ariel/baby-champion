using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BabyChampion.Simulation
{
    /// <summary>Sanity checks on <see cref="SimConfig"/>, used by the data validator.</summary>
    public static class ConfigValidator
    {
        public const int LastMonth = 35;

        public static List<string> Validate(SimConfig config)
        {
            var errors = new List<string>();
            if (config.ageRates == null) errors.Add("Age rates: missing");
            else ValidateAgeRates(config.ageRates, errors);
            if (config.prices == null) errors.Add("Prices: missing");
            else ValidatePrices(config.prices, errors);
            if (config.tuning == null) errors.Add("Tuning: missing");
            else ValidateTuning(config.tuning, errors);
            if (config.levels == null) errors.Add("Levels: missing");
            else ValidateLevels(config.levels, errors);
            return errors;
        }

        static void ValidateAgeRates(AgeRateTable table, List<string> errors)
        {
            if (table.bands == null || table.bands.Count == 0)
            {
                errors.Add("Age rates: no age bands");
                return;
            }

            foreach (var band in table.bands)
            {
                string label = $"Age band '{band.name}'";
                if (string.IsNullOrWhiteSpace(band.name)) errors.Add($"{label}: has no name");
                if (band.fromMonth < 0 || band.toMonth > LastMonth || band.fromMonth > band.toMonth)
                    errors.Add($"{label}: month range {band.fromMonth}-{band.toMonth} is not within 0-{LastMonth}");
                CheckRange(errors, label, "hungerTo70Minutes", band.hungerTo70Minutes, 30, 600);
                CheckRange(errors, label, "awakeWindowMinutes", band.awakeWindowMinutes, 20, 480);
                CheckRange(errors, label, "sleepRecoveryMinutes", band.sleepRecoveryMinutes, 30, 600);
                CheckRange(errors, label, "diapersPerDay", band.diapersPerDay, 1, 15);
                CheckRange(errors, label, "dirtyShare", band.dirtyShare, 0, 1);
                if (band.boredomTo70Minutes != 0) CheckRange(errors, label, "boredomTo70Minutes", band.boredomTo70Minutes, 20, 600);
            }

            for (int month = 0; month <= LastMonth; month++)
            {
                int covering = table.bands.Count(b => b.Covers(month));
                if (covering == 0) errors.Add($"Age rates: month {month} is not covered by any band");
                else if (covering > 1) errors.Add($"Age rates: month {month} is covered by {covering} bands");
            }
        }

        static void ValidatePrices(PriceList prices, List<string> errors)
        {
            foreach (SupplyType item in Enum.GetValues(typeof(SupplyType)))
            {
                var matches = prices.items?.Where(p => p.item == item).ToList() ?? new List<SupplyPrice>();
                if (matches.Count == 0) errors.Add($"Prices: no price for {item}");
                else if (matches.Count > 1) errors.Add($"Prices: {matches.Count} prices for {item}");
            }
            foreach (var price in prices.items ?? new List<SupplyPrice>())
            {
                if (price.price < 0) errors.Add($"Prices: {price.item} has a negative price ({price.price})");
                if (price.packSize <= 0) errors.Add($"Prices: {price.item} has pack size {price.packSize}");
            }
            if (prices.expressFee < 0) errors.Add($"Prices: negative express fee ({prices.expressFee})");
            CheckRange(errors, "Prices", "deliveryMinutes", prices.deliveryMinutes, 1, 1440);
            CheckRange(errors, "Prices", "expressDeliveryMinutes", prices.expressDeliveryMinutes, 1, prices.deliveryMinutes);
        }

        static void ValidateTuning(Tuning tuning, List<string> errors)
        {
            // Every rate, threshold and duration is a non-negative number; thresholds sit on the 0-100 meter scale.
            foreach (var field in typeof(Tuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(tuning);
                if (value is double d)
                {
                    if (double.IsNaN(d) || double.IsInfinity(d) || d < 0) errors.Add($"Tuning: {field.Name} = {d} must be a non-negative number");
                    else if (field.Name.EndsWith("Threshold") || field.Name.StartsWith("tensionBlocks") || field.Name.StartsWith("stressBlocks"))
                        CheckRange(errors, "Tuning", field.Name, d, 1, 100);
                }
                else if (value is int i && i < 0) errors.Add($"Tuning: {field.Name} = {i} must not be negative");
            }
            CheckRange(errors, "Tuning", "dayLengthMinutes", tuning.dayLengthMinutes, 60, 1440 * 2);
            CheckRange(errors, "Tuning", "minutesPerRealSecond", tuning.minutesPerRealSecond, 0.1, 60);
            CheckRange(errors, "Tuning", "parentSleepTimeMultiplier", tuning.parentSleepTimeMultiplier, 1, 20);
            if (tuning.dayStartHour < 0 || tuning.dayStartHour > 23) errors.Add($"Tuning: dayStartHour = {tuning.dayStartHour} is not an hour");

            foreach (var field in typeof(Tuning).GetFields().Where(f => f.Name.EndsWith("Minutes") && f.FieldType == typeof(double)))
            {
                double minutes = (double)field.GetValue(tuning);
                if (minutes <= 0) errors.Add($"Tuning: {field.Name} must be positive");
            }

            var start = tuning.start;
            if (start == null)
            {
                errors.Add("Tuning: start values missing");
                return;
            }
            foreach (var field in typeof(Tuning.DayStart).GetFields())
            {
                object value = field.GetValue(start);
                if (value is double d) CheckRange(errors, "Tuning start", field.Name, d, 0, 100);
                else if (value is int i && i < 0) errors.Add($"Tuning start: {field.Name} = {i} must not be negative");
            }
            if (start.parentEnergy <= 0) errors.Add("Tuning start: parentEnergy must be positive");
        }

        static void ValidateLevels(LevelTable table, List<string> errors)
        {
            int last = LevelTable.LastLevel;
            if (table.chapters == null || table.chapters.Count == 0) errors.Add("Levels: no chapters");
            else
            {
                for (int i = 0; i < table.chapters.Count; i++)
                {
                    var c = table.chapters[i];
                    string label = $"Chapter {c.number}";
                    if (c.number != i + 1) errors.Add($"{label}: chapters must be numbered 1, 2, 3... in order");
                    if (string.IsNullOrWhiteSpace(c.title)) errors.Add($"{label}: has no title");
                    if (c.firstLevel < 1 || c.lastLevel > last || c.firstLevel > c.lastLevel)
                        errors.Add($"{label}: level range {c.firstLevel}-{c.lastLevel} is not within 1-{last}");
                }
                for (int level = 1; level <= last; level++)
                {
                    int covering = table.chapters.Count(c => c.Contains(level));
                    if (covering != 1) errors.Add($"Levels: level {level} is in {covering} chapters (must be exactly 1)");
                }
            }

            if (table.levels == null || table.levels.Count == 0)
            {
                errors.Add("Levels: no levels");
                return;
            }
            foreach (var group in table.levels.GroupBy(l => l.number).Where(g => g.Count() > 1))
                errors.Add($"Levels: {group.Count()} definitions of level {group.Key}");
            foreach (var level in table.levels) ValidateLevel(level, errors);
        }

        static void ValidateLevel(LevelDefinition level, List<string> errors)
        {
            string label = $"Level {level.number}";
            if (level.number < 1 || level.number > LevelTable.LastLevel) errors.Add($"{label}: number is not within 1-{LevelTable.LastLevel}");
            if (string.IsNullOrWhiteSpace(level.title)) errors.Add($"{label}: has no title");
            if (string.IsNullOrWhiteSpace(level.objective)) errors.Add($"{label}: has no objective text");
            if (string.IsNullOrWhiteSpace(level.briefing)) errors.Add($"{label}: has no briefing");
            if (level.month != level.number - 1) errors.Add($"{label}: month {level.month} should be {level.number - 1} (level N is the baby's N-th month)");
            if (level.startHour < 0 || level.startHour > 23) errors.Add($"{label}: startHour {level.startHour} is not an hour");
            CheckRange(errors, label, "lengthMinutes", level.lengthMinutes, 30, 1440 * 2);
            CheckRange(errors, label, "cryingBudgetMinutes", level.cryingBudgetMinutes, 1, level.lengthMinutes);
            CheckRange(errors, label, "maxContinuousCryMinutes", level.maxContinuousCryMinutes, 1, level.lengthMinutes);
            if (level.allowance < 0) errors.Add($"{label}: negative allowance ({level.allowance})");
            if (level.helpCalls < 0) errors.Add($"{label}: negative helpCalls ({level.helpCalls})");

            var actions = level.actions ?? new List<ActionType>();
            if (actions.Count == 0) errors.Add($"{label}: no available actions");
            foreach (var action in actions)
                if (!Enum.IsDefined(typeof(ActionType), action)) errors.Add($"{label}: action {(int)action} does not exist");
            foreach (var group in actions.GroupBy(a => a).Where(g => g.Count() > 1))
                errors.Add($"{label}: action {group.Key} is listed {group.Count()} times");
            bool Has(ActionType type) => actions.Contains(type);
            if ((Has(ActionType.Breastfeed) || Has(ActionType.BottleFeed))
                && !(Has(ActionType.Breastfeed) && Has(ActionType.BottleFeed) && Has(ActionType.PrepareBottle)))
                errors.Add($"{label}: feeding needs Breastfeed, PrepareBottle and BottleFeed, so every feeding choice works");
            if (level.helpCalls > 0 && !Has(ActionType.CallForHelp)) errors.Add($"{label}: helpCalls is {level.helpCalls} but CallForHelp is not available");
            if (level.helpCalls == 0 && Has(ActionType.CallForHelp)) errors.Add($"{label}: CallForHelp is available but helpCalls is 0");

            foreach (var goal in level.goals ?? new List<LevelGoal>())
            {
                if (!Enum.IsDefined(typeof(GoalType), goal.type))
                {
                    errors.Add($"{label}: goal type {(int)goal.type} does not exist");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(goal.text)) errors.Add($"{label}: goal {goal.type} has no text");
                if (goal.type == GoalType.EnergyAtEnd) CheckRange(errors, label, "EnergyAtEnd goal", goal.target, 0, 99);
                else CheckRange(errors, label, $"{goal.type} goal", goal.target, 1, 100);
                var needed = goal.type == GoalType.Feeds ? ActionType.Breastfeed
                    : goal.type == GoalType.Burps ? ActionType.Burp
                    : goal.type == GoalType.DiaperChanges ? ActionType.ChangeDiaper
                    : goal.type == GoalType.BabyNaps ? ActionType.PutBabyToSleep
                    : (ActionType?)null;
                if (needed is ActionType a && !Has(a)) errors.Add($"{label}: goal {goal.type} needs action {a}, which is not available");
            }

            foreach (var condition in level.failConditions ?? new List<FailCondition>())
            {
                if (!Enum.IsDefined(typeof(FailConditionType), condition.type)) errors.Add($"{label}: fail condition {(int)condition.type} does not exist");
                if (string.IsNullOrWhiteSpace(condition.text)) errors.Add($"{label}: fail condition {condition.type} has no text");
                if (condition.type == FailConditionType.EnergyAtOrBelow) CheckRange(errors, label, "EnergyAtOrBelow", condition.value, 0, 99);
            }

            var stars = level.stars;
            if (stars == null) errors.Add($"{label}: no star rules");
            else
            {
                CheckRange(errors, label, "twoStarWellbeing", stars.twoStarWellbeing, 0, 100);
                CheckRange(errors, label, "threeStarJoy", stars.threeStarJoy, 0, 100);
                if (stars.helpStarCap < 0 || stars.helpStarCap > 3) errors.Add($"{label}: helpStarCap {stars.helpStarCap} is not within 0-3");
            }

            if (level.start != null)
                foreach (var field in typeof(LevelStart).GetFields())
                    if (field.GetValue(level.start) is double d && d >= 0) CheckRange(errors, label + " start", field.Name, d, 0, 100);
        }

        static void CheckRange(List<string> errors, string label, string name, double value, double min, double max)
        {
            if (double.IsNaN(value) || value < min || value > max)
                errors.Add($"{label}: {name} = {value} is outside {min}-{max}");
        }
    }
}
