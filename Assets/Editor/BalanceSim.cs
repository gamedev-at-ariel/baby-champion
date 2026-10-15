using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BabyChampion.Simulation;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace BabyChampion.Editor
{
    /// <summary>
    /// Plays days headlessly with test bots and reports success rates, crying minutes, the Energy
    /// curve and supply use (3-plan.md Section 2). Results go to artifacts/balance/.
    /// CLI: tools/unity.sh -nographics -quit -executeMethod BabyChampion.Editor.BalanceSim.Run
    ///        -level 1 -bot all -runs 500 -seed 1 [-feeding both|breast|formula] [-nocheck]
    /// Editor: unity command bc_balance_sim -- --level 1 --bot all --runs 500 --seed 1
    /// </summary>
    public static class BalanceSim
    {
        public const string OutputFolder = "artifacts/balance";

        public sealed class Report
        {
            public bool AllTargetsMet = true;
            public readonly List<string> Lines = new List<string>();
            public readonly List<string> Files = new List<string>();
        }

        public static Report Simulate(SimConfig config, int level, string bots, int runs, int seed, FeedingChoice feeding, bool check)
        {
            var report = new Report();
            var botList = bots == "all" ? BalanceRunner.BotNames : bots.Split(',').Select(b => b.Trim().ToLowerInvariant()).ToArray();
            var definition = config.levels?.Find(level);
            string what = definition != null
                ? $"level {level} '{definition.title}'"
                : $"level {level} (not built yet: a free day of month {BalanceRunner.MonthForLevel(level)})";
            Directory.CreateDirectory(OutputFolder);
            report.Lines.Add($"Balance simulator: {what}, feeding {feeding}, {runs} runs from seed {seed}");

            var summaries = new StringBuilder("bot,level,feeding,runs,success_rate,target,target_met\n");
            foreach (var bot in botList)
            {
                var results = BalanceRunner.Run(config, level, feeding, bot, runs, seed);
                string prefix = $"{OutputFolder}/level{level}-{feeding.ToString().ToLowerInvariant()}-{bot}";
                File.WriteAllText(prefix + "-runs.csv", BalanceRunner.RunsCsv(results));
                File.WriteAllText(prefix + "-energy.csv", BalanceRunner.EnergyCsv(results));
                report.Files.Add(prefix + "-runs.csv");
                report.Files.Add(prefix + "-energy.csv");

                report.Lines.Add(BalanceRunner.Summary(bot, level, results, out bool met));
                if (check && !met) report.AllTargetsMet = false;
                var target = BalanceRunner.TargetFor(bot, level);
                summaries.AppendLine(string.Join(",", bot, level, feeding, runs,
                    BalanceRunner.SuccessRate(results).ToString("0.###", CultureInfo.InvariantCulture), target, met));
            }
            string summaryPath = $"{OutputFolder}/level{level}-{feeding.ToString().ToLowerInvariant()}-summary.csv";
            File.WriteAllText(summaryPath, summaries.ToString());
            report.Files.Add(summaryPath);
            report.Lines.Add(check ? (report.AllTargetsMet ? "Targets: all met" : "Targets: MISSED") : "Targets: not checked");
            return report;
        }

        /// <summary>Batch-mode entry point. Exits with code 1 when a target is missed or the data is invalid.</summary>
        public static void Run()
        {
            int code;
            try
            {
                var args = Environment.GetCommandLineArgs();
                var report = Simulate(DataAssets.LoadConfig(), IntArg(args, "-level", 1), StringArg(args, "-bot", "all"),
                    IntArg(args, "-runs", 500), IntArg(args, "-seed", 1), FeedingArg(StringArg(args, "-feeding", "both")),
                    !args.Contains("-nocheck"));
                foreach (var line in report.Lines) Debug.Log("[BalanceSim] " + line);
                code = report.AllTargetsMet ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[BalanceSim] " + e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        [CliCommand("bc_balance_sim", "Run the Baby Champion balance simulator (bots: greedy, sloppy, idle, all) and write artifacts/balance/")]
        public static string RunInEditor(
            [CliArg("level", "Level number: its level definition, or a free day of month level - 1 when not built yet")] int level = 1,
            [CliArg("bot", "greedy, sloppy, idle, a comma list, or all")] string bot = "all",
            [CliArg("runs", "Runs per bot")] int runs = 500,
            [CliArg("seed", "First seed; run i uses seed + i")] int seed = 1,
            [CliArg("feeding", "breast, formula or both")] string feeding = "both",
            [CliArg("check", "Fail when a balance target is missed")] bool check = true)
        {
            var report = Simulate(DataAssets.LoadConfig(), level, bot, runs, seed, FeedingArg(feeding), check);
            string text = string.Join("\n", report.Lines);
            if (!report.AllTargetsMet) throw new InvalidOperationException(text);
            return text;
        }

        static FeedingChoice FeedingArg(string value) => (FeedingChoice)Enum.Parse(typeof(FeedingChoice), value, ignoreCase: true);

        static string StringArg(string[] args, string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        static int IntArg(string[] args, string name, int fallback)
        {
            string value = StringArg(args, name, null);
            return value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
        }
    }
}
