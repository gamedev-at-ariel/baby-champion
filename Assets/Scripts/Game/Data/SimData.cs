using BabyChampion.Simulation;

namespace BabyChampion.Game
{
    public static class SimData
    {
        /// <summary>
        /// Builds a simulation config from the data assets. The data is deep-copied, so nothing
        /// the simulation does can change the assets. Without a levels asset, the built-in levels are used.
        /// </summary>
        public static SimConfig ToConfig(AgeRatesAsset ageRates, PricesAsset prices, TuningAsset tuning, LevelsAsset levels = null) =>
            new SimConfig(Copy(ageRates.table), Copy(prices.prices), Copy(tuning.tuning), levels != null ? Copy(levels.table) : null);

        static T Copy<T>(T value) => JsonCodec.Deserialize<T>(JsonCodec.Serialize(value));
    }
}
