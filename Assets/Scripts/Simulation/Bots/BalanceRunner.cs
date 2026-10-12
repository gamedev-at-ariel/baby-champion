using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BabyChampion.Simulation
{
    /// <summary>The result of one headless day.</summary>
    public sealed class RunResult
    {
        public string bot;
        public int seed;
        public Outcome outcome;
        public FailReason failReason;
        public double endMinute;
        public double endEnergy;
        public DayStats stats;

        public bool Success => outcome == Outcome.Survived;
    }

    /// <summary>Success-rate targets for one bot (design 3-plan.md Section 6).</summary>
    public readonly struct BalanceTarget
    {
        public readonly string Bot;
        public readonly double MinRate;
        public readonly double MaxRate;

        public BalanceTarget(string bot, double minRate, double maxRate)
        {
            Bot = bot;
            MinRate = minRate;
            MaxRate = maxRate;
        }

        public bool Met(double rate) => rate >= MinRate - 1e-9 && rate <= MaxRate + 1e-9;
        public override string ToString() => $"{MinRate:P0}-{MaxRate:P0}";
    }

    /// <summary>Plays days headlessly with bots and summarizes them (the balance simulator's core).</summary>
    public static class BalanceRunner
    {
        public static readonly string[] BotNames = { "greedy", "sloppy", "idle" };

        public static IParentController CreateBot(string name, int seed)
        {
            switch (name.ToLowerInvariant())
            {
                case "greedy": return new GreedyBot();
                case "sloppy": return new SloppyBot(seed ^ 0x5BD1E995);
                case "idle": return new IdleBot();
                default: throw new ArgumentException($"Unknown bot '{name}'. Known: {string.Join(", ", BotNames)}");
            }
        }

        /// <summary>Level N is the baby's N-th month (design Section 5.1), so month = level - 1.</summary>
        public static int MonthForLevel(int level) => Math.Max(0, level - 1);

        public static RunResult RunDay(SimConfig config, int month, FeedingChoice feeding, string bot, int seed)
        {
            var sim = CareSimulation.NewDay(config, month, feeding, seed);
            sim.AttachController(CreateBot(bot, seed));
            sim.RunToEnd();
            var stats = sim.CopyStats();
            return new RunResult
            {
                bot = bot,
                seed = seed,
                outcome = sim.Outcome,
                failReason = sim.FailReason,
                endMinute = sim.Minute,
                endEnergy = sim.Parent.Energy,
                stats = stats,
            };
        }

        public static List<RunResult> Run(SimConfig config, int month, FeedingChoice feeding, string bot, int runs, int firstSeed)
        {
            var results = new List<RunResult>(runs);
            for (int i = 0; i < runs; i++) results.Add(RunDay(config, month, feeding, bot, firstSeed + i));
            return results;
        }

        public static double SuccessRate(IReadOnlyCollection<RunResult> results) =>
            results.Count == 0 ? 0 : results.Count(r => r.Success) / (double)results.Count;

        /// <summary>Chapter targets from 3-plan.md Section 6 (1-star rate = surviving the day in M1).</summary>
        public static BalanceTarget TargetFor(string bot, int level)
        {
            int chapter = level <= 3 ? 1 : level <= 13 ? 2 : 5;
            switch (bot)
            {
                case "greedy": return new BalanceTarget(bot, chapter == 1 ? 0.95 : chapter == 2 ? 0.85 : 0.75, 1);
                case "sloppy": return chapter == 1 ? new BalanceTarget(bot, 0.5, 0.8) : chapter == 2 ? new BalanceTarget(bot, 0.3, 0.6) : new BalanceTarget(bot, 0.2, 0.5);
                case "idle": return new BalanceTarget(bot, 0, 0);
                default: throw new ArgumentException($"No target for bot '{bot}'");
            }
        }

        static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>One row per run: outcome, crying minutes and supply use.</summary>
        public static string RunsCsv(IEnumerable<RunResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("bot,seed,outcome,fail_reason,end_time,crying_minutes,longest_cry,"
                          + "cry_hunger,cry_diaper,cry_overtired,cry_gas,cry_upset,cry_bored,min_energy,end_energy,avg_wellbeing,"
                          + "feeds,burps,diaper_changes,naps,diapers_used,wipes_used,formula_used,meals,orders,money_spent,parent_sleep_minutes");
            foreach (var r in results)
            {
                var s = r.stats;
                sb.AppendLine(string.Join(",", r.bot, r.seed, r.outcome, r.failReason, CareSimulation.FormatClock(r.endMinute),
                    F(s.cryingMinutes), F(s.longestCryMinutes),
                    F(s.cryHungerMinutes), F(s.cryDiaperMinutes), F(s.cryOvertiredMinutes), F(s.cryGasMinutes), F(s.cryUpsetMinutes), F(s.cryBoredMinutes),
                    F(s.minEnergy), F(r.endEnergy), F(s.AverageWellbeing),
                    s.feeds, s.burps, s.diaperChanges, s.babyNaps, s.diapersUsed, s.wipesUsed, s.formulaUsed, s.mealsEaten,
                    s.ordersPlaced, s.moneySpent, F(s.parentSleepMinutes)));
            }
            return sb.ToString();
        }

        /// <summary>One row per run: parent Energy at the start of each in-game hour (the Energy curve).</summary>
        public static string EnergyCsv(IEnumerable<RunResult> results, int dayStartHour = 7)
        {
            var list = results.ToList();
            int hours = list.Count == 0 ? 0 : list.Max(r => r.stats.energyByHour.Count);
            var sb = new StringBuilder();
            sb.Append("bot,seed");
            for (int h = 0; h < hours; h++) sb.Append(',').Append(CareSimulation.FormatClock(h * 60, dayStartHour));
            sb.AppendLine();
            foreach (var r in list)
            {
                sb.Append(r.bot).Append(',').Append(r.seed);
                for (int h = 0; h < hours; h++)
                    sb.Append(',').Append(h < r.stats.energyByHour.Count ? F(r.stats.energyByHour[h]) : "");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>Averages per bot, plus the target check.</summary>
        public static string Summary(string bot, int level, IReadOnlyCollection<RunResult> results, out bool targetMet)
        {
            double rate = SuccessRate(results);
            var target = TargetFor(bot, level);
            targetMet = target.Met(rate);
            var fails = results.Where(r => !r.Success).GroupBy(r => r.failReason)
                .OrderByDescending(g => g.Count()).Select(g => $"{g.Key}={g.Count()}");
            double Avg(Func<RunResult, double> f) => results.Count == 0 ? 0 : results.Average(f);
            return $"{bot}: success {rate:P1} (target {target}) {(targetMet ? "OK" : "MISSED")}; "
                   + $"avg crying {F(Avg(r => r.stats.cryingMinutes))} min (hunger {F(Avg(r => r.stats.cryHungerMinutes))}, "
                   + $"diaper {F(Avg(r => r.stats.cryDiaperMinutes))}, overtired {F(Avg(r => r.stats.cryOvertiredMinutes))}, "
                   + $"gas {F(Avg(r => r.stats.cryGasMinutes))}, upset {F(Avg(r => r.stats.cryUpsetMinutes))}, bored {F(Avg(r => r.stats.cryBoredMinutes))}), "
                   + $"longest {F(Avg(r => r.stats.longestCryMinutes))} min, "
                   + $"min energy {F(Avg(r => r.stats.minEnergy))}, wellbeing {F(Avg(r => r.stats.AverageWellbeing))}, "
                   + $"feeds {F(Avg(r => r.stats.feeds))}, diapers {F(Avg(r => r.stats.diapersUsed))}; "
                   + $"fails: {(fails.Any() ? string.Join(" ", fails) : "none")}";
        }
    }
}
