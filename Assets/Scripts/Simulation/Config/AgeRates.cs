using System;
using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    /// <summary>Need rates for one age band (design Section 7.4).</summary>
    [Serializable]
    public class AgeBand
    {
        public string name;
        /// <summary>First and last baby month (0-based, inclusive) covered by this band.</summary>
        public int fromMonth;
        public int toMonth;
        /// <summary>In-game minutes for baby Hunger to rise from 0 to 70.</summary>
        public double hungerTo70Minutes;
        /// <summary>In-game minutes awake before Tiredness reaches the overtired threshold (from 0).</summary>
        public double awakeWindowMinutes;
        /// <summary>In-game minutes asleep for Tiredness to fall from the overtired threshold to 0.</summary>
        public double sleepRecoveryMinutes;
        /// <summary>Expected wet or dirty diapers per day.</summary>
        public double diapersPerDay;
        /// <summary>Share of diaper events that are dirty (0-1).</summary>
        public double dirtyShare;
        /// <summary>In-game minutes awake and unengaged for Boredom to rise from 0 to 70. 0 = no Boredom yet.</summary>
        public double boredomTo70Minutes;

        public bool Covers(int month) => month >= fromMonth && month <= toMonth;
    }

    /// <summary>All age bands, months 0-35.</summary>
    [Serializable]
    public class AgeRateTable
    {
        public List<AgeBand> bands = new List<AgeBand>();

        public AgeBand ForMonth(int month)
        {
            foreach (var band in bands)
                if (band.Covers(month)) return band;
            throw new ArgumentOutOfRangeException(nameof(month), month, "No age band covers this month");
        }

        public static AgeRateTable CreateDefault() => new AgeRateTable
        {
            bands = new List<AgeBand>
            {
                new AgeBand { name = "0-2 mo", fromMonth = 0, toMonth = 2, hungerTo70Minutes = 150, awakeWindowMinutes = 60, sleepRecoveryMinutes = 50, diapersPerDay = 9, dirtyShare = 0.35, boredomTo70Minutes = 0 },
                new AgeBand { name = "3-5 mo", fromMonth = 3, toMonth = 5, hungerTo70Minutes = 180, awakeWindowMinutes = 105, sleepRecoveryMinutes = 60, diapersPerDay = 7, dirtyShare = 0.3, boredomTo70Minutes = 90 },
                new AgeBand { name = "6-11 mo", fromMonth = 6, toMonth = 11, hungerTo70Minutes = 210, awakeWindowMinutes = 150, sleepRecoveryMinutes = 70, diapersPerDay = 6, dirtyShare = 0.3, boredomTo70Minutes = 80 },
                new AgeBand { name = "12-23 mo", fromMonth = 12, toMonth = 23, hungerTo70Minutes = 240, awakeWindowMinutes = 300, sleepRecoveryMinutes = 90, diapersPerDay = 5, dirtyShare = 0.3, boredomTo70Minutes = 70 },
                new AgeBand { name = "24-35 mo", fromMonth = 24, toMonth = 35, hungerTo70Minutes = 240, awakeWindowMinutes = 330, sleepRecoveryMinutes = 100, diapersPerDay = 4, dirtyShare = 0.3, boredomTo70Minutes = 70 },
            }
        };
    }
}
