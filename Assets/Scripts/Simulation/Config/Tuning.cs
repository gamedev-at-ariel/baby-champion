using System;

namespace BabyChampion.Simulation
{
    /// <summary>
    /// Rule thresholds and rates of design Sections 6, 7 and 13 that do not depend on age.
    /// Rates are per in-game minute; meters run 0-100.
    /// </summary>
    [Serializable]
    public class Tuning
    {
        [Serializable]
        public class DayStart
        {
            public double babyHunger = 30;
            public double babyTiredness = 20;
            public double parentEnergy = 70;
            public double parentHunger = 20;
            public double parentStress = 10;
            public double parentJoy = 40;
            public int money = 150;
            public int diapers = 12;
            public int wipes = 30;
            public int formula = 8;
            public int babyFood = 0;
            public int adultFood = 4;
        }

        // Time (Section 6)
        public double dayLengthMinutes = 1440;
        public int dayStartHour = 7;
        public double minutesPerRealSecond = 2;
        public double parentSleepTimeMultiplier = 4;

        // Baby (Section 7.1)
        public double hungerCryThreshold = 70;
        public double wetCryAfterMinutes = 60;
        public double dirtyCryAfterMinutes = 10;
        public double rashAfterDirtyMinutes = 120;
        public double overtiredThreshold = 80;
        public double tensionBlocksSleep = 50;
        public double tensionCryThreshold = 75;
        public double boredomCryThreshold = 70;
        public int boredomFromMonth = 4;
        public int selfSootheFromMonth = 6;
        public double minTirednessToSleep = 25;
        /// <summary>A rested baby (Tiredness 0) keeps sleeping until Hunger reaches this, then wakes without crying.</summary>
        public double restedWakeHunger = 50;
        public double burpWindowMinutes = 15;
        public double gasDurationMinutes = 45;
        public double longCryMinutes = 10;
        public double tensionFromOvertired = 1.0;
        public double tensionFromLongCrying = 0.5;
        public double tensionFromGas = 0.6;
        public double tensionFromRash = 0.1;
        public double tensionDecayCalm = 0.2;
        public double tensionDecayAsleep = 0.5;
        public double sootheTensionPerMinute = 3;
        public double playBoredomPerMinute = 8;
        public double playExtraTirednessPerMinute = 0.5;
        public double boredomDecayAsleep = 0.5;
        public double engagedBoredomMultiplier = 0.5;

        // Parent (Section 7.2)
        public double energyDrainAwake = 0.1;
        public double cryingEnergyMultiplier = 2;
        public double carryingEnergyMultiplier = 1.5;
        public double energySleepRecovery = 0.1;
        public double energyRestRecovery = 0.08;
        public double lowEnergyThreshold = 20;
        public double lowEnergyDurationMultiplier = 1.25;
        public double parentHungerTo70Minutes = 300;
        public double parentHungerThreshold = 70;
        public double hungryRecoveryMultiplier = 0.5;
        public double hungryDrainMultiplier = 1.5;
        public double breastfeedParentHunger = 8;
        public double stressBlocksSleep = 60;
        public double stressFromCrying = 0.5;
        public double stressFromLowSupplies = 0.05;
        public double stressFromLowMoney = 0.03;
        public int lowSupplyCount = 2;
        public int lowMoney = 20;
        public double stressDecayCalm = 0.1;
        public double stressDecayEating = 0.5;
        public double stressDecayResting = 0.3;
        public double joyDecay = 0.02;
        public double joyFromPlay = 0.5;

        // Action durations, in-game minutes (Section 9)
        public double breastfeedMinutes = 25;
        public double prepareBottleMinutes = 10;
        public double bottleFeedMinutes = 15;
        public double burpMinutes = 4;
        public double changeDiaperMinutes = 5;
        public double putToSleepMinutes = 10;
        public double playMinutes = 10;
        public double eatMinutes = 15;
        public double minBabyHungerToFeed = 20;
        public double minParentHungerToEat = 10;
        public int maxBottlesReady = 2;

        // Failure (Section 13.2)
        public double cryingBudgetMinutes = 140;
        public double maxContinuousCryMinutes = 30;

        public DayStart start = new DayStart();

        public static Tuning CreateDefault() => new Tuning();
    }
}
