using System.Collections.Generic;
using System.IO;
using BabyChampion.Game;
using BabyChampion.Simulation;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace BabyChampion.Editor
{
    /// <summary>
    /// The simulation's data assets (age rates, prices, tuning): creation from the built-in
    /// defaults, loading, and JSON export/import for tooling (design Section 19.4).
    /// </summary>
    public static class DataAssets
    {
        public const string Folder = "Assets/Data";
        public const string AgeRatesPath = Folder + "/AgeRates.asset";
        public const string PricesPath = Folder + "/Prices.asset";
        public const string TuningPath = Folder + "/Tuning.asset";
        public const string LevelsPath = Folder + "/Levels.asset";
        public const string ExportFolder = "artifacts/data";

        public static AgeRatesAsset AgeRates => AssetDatabase.LoadAssetAtPath<AgeRatesAsset>(AgeRatesPath);
        public static PricesAsset Prices => AssetDatabase.LoadAssetAtPath<PricesAsset>(PricesPath);
        public static TuningAsset Tuning => AssetDatabase.LoadAssetAtPath<TuningAsset>(TuningPath);
        public static LevelsAsset Levels => AssetDatabase.LoadAssetAtPath<LevelsAsset>(LevelsPath);

        /// <summary>Missing assets, by path.</summary>
        public static List<string> Missing()
        {
            var missing = new List<string>();
            if (AgeRates == null) missing.Add(AgeRatesPath);
            if (Prices == null) missing.Add(PricesPath);
            if (Tuning == null) missing.Add(TuningPath);
            if (Levels == null) missing.Add(LevelsPath);
            return missing;
        }

        public static SimConfig LoadConfig()
        {
            var missing = Missing();
            if (missing.Count > 0) throw new FileNotFoundException("Missing data assets (run unity command bc_setup_data): " + string.Join(", ", missing));
            return SimData.ToConfig(AgeRates, Prices, Tuning, Levels);
        }

        [CliCommand("bc_setup_data", "Create the data assets (age rates, prices, tuning, levels) from the built-in defaults; reset=true overwrites them")]
        public static string SetupData([CliArg("reset", "Overwrite existing assets with the defaults")] bool reset = false)
        {
            Directory.CreateDirectory(Folder);
            var log = new List<string>
            {
                Ensure<AgeRatesAsset>(AgeRatesPath, reset, a => a.table = AgeRateTable.CreateDefault()),
                Ensure<PricesAsset>(PricesPath, reset, a => a.prices = PriceList.CreateDefault()),
                Ensure<TuningAsset>(TuningPath, reset, a => a.tuning = Simulation.Tuning.CreateDefault()),
                Ensure<LevelsAsset>(LevelsPath, reset, a => a.table = LevelTable.CreateDefault()),
            };
            AssetDatabase.SaveAssets();
            return "data: " + string.Join("; ", log);
        }

        static string Ensure<T>(string path, bool reset, System.Action<T> setDefaults) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null && !reset) return $"{path} kept";
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                setDefaults(asset);
                AssetDatabase.CreateAsset(asset, path);
                return $"{path} created";
            }
            setDefaults(asset);
            EditorUtility.SetDirty(asset);
            return $"{path} reset to defaults";
        }

        [CliCommand("bc_export_data", "Export the simulation data assets as JSON to artifacts/data/")]
        public static string ExportData()
        {
            var config = LoadConfig();
            Directory.CreateDirectory(ExportFolder);
            File.WriteAllText(ExportFolder + "/age-rates.json", JsonCodec.Serialize(config.ageRates, pretty: true));
            File.WriteAllText(ExportFolder + "/prices.json", JsonCodec.Serialize(config.prices, pretty: true));
            File.WriteAllText(ExportFolder + "/tuning.json", JsonCodec.Serialize(config.tuning, pretty: true));
            File.WriteAllText(ExportFolder + "/levels.json", JsonCodec.Serialize(config.levels, pretty: true));
            return $"exported age-rates.json, prices.json, tuning.json, levels.json to {ExportFolder}";
        }

        [CliCommand("bc_import_data", "Import data from JSON (age-rates.json, prices.json, tuning.json, levels.json) into the data assets")]
        public static string ImportData([CliArg("folder", "Folder holding the JSON files")] string folder = ExportFolder)
        {
            var config = new SimConfig(
                JsonCodec.Deserialize<AgeRateTable>(File.ReadAllText(Path.Combine(folder, "age-rates.json"))),
                JsonCodec.Deserialize<PriceList>(File.ReadAllText(Path.Combine(folder, "prices.json"))),
                JsonCodec.Deserialize<Simulation.Tuning>(File.ReadAllText(Path.Combine(folder, "tuning.json"))),
                JsonCodec.Deserialize<LevelTable>(File.ReadAllText(Path.Combine(folder, "levels.json"))));
            var errors = config.Validate();
            if (errors.Count > 0) throw new System.InvalidOperationException("Not imported, the data is invalid:\n" + string.Join("\n", errors));

            SetupData();
            AgeRates.table = config.ageRates;
            Prices.prices = config.prices;
            Tuning.tuning = config.tuning;
            Levels.table = config.levels;
            EditorUtility.SetDirty(AgeRates);
            EditorUtility.SetDirty(Prices);
            EditorUtility.SetDirty(Tuning);
            EditorUtility.SetDirty(Levels);
            AssetDatabase.SaveAssets();
            return $"imported data from {folder}";
        }
    }
}
