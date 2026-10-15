using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    /// <summary>Everything the simulation reads from data: age rates, prices, rule tuning and levels.</summary>
    public sealed class SimConfig
    {
        public AgeRateTable ageRates;
        public PriceList prices;
        public Tuning tuning;
        public LevelTable levels;

        public SimConfig(AgeRateTable ageRates, PriceList prices, Tuning tuning, LevelTable levels = null)
        {
            this.ageRates = ageRates;
            this.prices = prices;
            this.tuning = tuning;
            this.levels = levels ?? LevelTable.CreateDefault();
        }

        /// <summary>The built-in values. The project's data assets start from these.</summary>
        public static SimConfig CreateDefault() =>
            new SimConfig(AgeRateTable.CreateDefault(), PriceList.CreateDefault(), Tuning.CreateDefault(), LevelTable.CreateDefault());

        /// <summary>Every problem found in the data; empty when it is usable.</summary>
        public List<string> Validate() => ConfigValidator.Validate(this);
    }
}
