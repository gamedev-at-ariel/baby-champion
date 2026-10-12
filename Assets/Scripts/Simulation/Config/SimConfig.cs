using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    /// <summary>Everything the simulation reads from data: age rates, prices and rule tuning.</summary>
    public sealed class SimConfig
    {
        public AgeRateTable ageRates;
        public PriceList prices;
        public Tuning tuning;

        public SimConfig(AgeRateTable ageRates, PriceList prices, Tuning tuning)
        {
            this.ageRates = ageRates;
            this.prices = prices;
            this.tuning = tuning;
        }

        /// <summary>The built-in values. The project's data assets start from these.</summary>
        public static SimConfig CreateDefault() => new SimConfig(AgeRateTable.CreateDefault(), PriceList.CreateDefault(), Tuning.CreateDefault());

        /// <summary>Every problem found in the data; empty when it is usable.</summary>
        public List<string> Validate() => ConfigValidator.Validate(this);
    }
}
