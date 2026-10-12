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

        static void CheckRange(List<string> errors, string label, string name, double value, double min, double max)
        {
            if (double.IsNaN(value) || value < min || value > max)
                errors.Add($"{label}: {name} = {value} is outside {min}-{max}");
        }
    }
}
