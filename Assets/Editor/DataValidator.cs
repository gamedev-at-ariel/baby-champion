using System.Collections.Generic;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace BabyChampion.Editor
{
    /// <summary>
    /// Checks all level and tuning data: the simulation data assets exist and their values are
    /// complete and sensible (<see cref="ConfigValidator"/>). Later milestones add level checks.
    /// CLI: tools/unity.sh -nographics -quit -executeMethod BabyChampion.Editor.DataValidator.Run
    /// </summary>
    public static class DataValidator
    {
        public const string ReportPath = "artifacts/validation.txt";

        public static List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var path in DataAssets.Missing()) errors.Add($"Missing data asset {path} (run unity command bc_setup_data)");
            if (errors.Count > 0) return errors;

            errors.AddRange(DataAssets.LoadConfig().Validate());
            return errors;
        }

        /// <summary>Batch-mode entry point. Exits with code 1 when any check fails.</summary>
        public static void Run()
        {
            var errors = Validate();
            WriteReport(errors);
            EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }

        [CliCommand("bc_validate_data", "Run the Baby Champion data validator and write artifacts/validation.txt")]
        public static string RunInEditor()
        {
            var errors = Validate();
            WriteReport(errors);
            return errors.Count == 0 ? "OK: no data errors" : $"FAILED: {errors.Count} error(s)\n" + string.Join("\n", errors);
        }

        static void WriteReport(List<string> errors)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            var lines = new List<string> { $"Data validation: {(errors.Count == 0 ? "PASSED" : "FAILED")} ({errors.Count} error(s))" };
            lines.AddRange(errors);
            File.WriteAllLines(ReportPath, lines);
            foreach (var line in lines)
            {
                if (errors.Count == 0) Debug.Log(line); else Debug.LogError(line);
            }
        }
    }
}
