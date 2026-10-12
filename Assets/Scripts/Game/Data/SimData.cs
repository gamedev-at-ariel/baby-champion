using BabyChampion.Simulation;

namespace BabyChampion.Game
{
    public static class SimData
    {
        /// <summary>
        /// Builds a simulation config from the three assets. The data is deep-copied, so nothing
        /// the simulation does can change the assets.
        /// </summary>
        public static SimConfig ToConfig(AgeRatesAsset ageRates, PricesAsset prices, TuningAsset tuning) =>
            new SimConfig(Copy(ageRates.table), Copy(prices.prices), Copy(tuning.tuning));

        static T Copy<T>(T value) => JsonCodec.Deserialize<T>(JsonCodec.Serialize(value));
    }
}
