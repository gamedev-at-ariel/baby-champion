using System.Collections.Generic;
using System.Linq;
using BabyChampion.Editor;
using BabyChampion.Simulation;
using NUnit.Framework;

namespace BabyChampion.Tests.EditMode
{
    /// <summary>Data validation, JSON export and the balance targets on the project's own data.</summary>
    public class DataAndBalanceTests
    {
        [Test]
        public void DefaultData_IsValid()
        {
            CollectionAssert.IsEmpty(SimConfig.CreateDefault().Validate());
        }

        [Test]
        public void Validator_FindsGapsNegativesAndBadRanges()
        {
            var config = SimConfig.CreateDefault();
            config.ageRates.bands.RemoveAt(1);                    // months 3-5 uncovered
            config.ageRates.bands[0].hungerTo70Minutes = 0;       // missing rate
            config.prices.items[0].price = -1;                    // negative price
            config.prices.items.RemoveAll(p => p.item == SupplyType.Formula);
            config.tuning.hungerCryThreshold = 150;               // off the 0-100 scale
            config.tuning.eatMinutes = 0;                         // duration must be positive

            var errors = config.Validate();
            void Expect(string fragment) => Assert.IsTrue(errors.Any(e => e.Contains(fragment)), $"no error mentioning '{fragment}' in:\n{string.Join("\n", errors)}");
            Expect("month 3 is not covered");
            Expect("hungerTo70Minutes");
            Expect("negative price");
            Expect("no price for Formula");
            Expect("hungerCryThreshold");
            Expect("eatMinutes");
        }

        [Test]
        public void ProjectDataAssets_ExistAndAreValid()
        {
            CollectionAssert.IsEmpty(DataAssets.Missing());
            CollectionAssert.IsEmpty(DataAssets.LoadConfig().Validate());
        }

        [Test]
        public void JsonExport_RoundTripsTheData()
        {
            var config = DataAssets.LoadConfig();
            foreach (object data in new object[] { config.ageRates, config.prices, config.tuning })
            {
                string json = JsonCodec.Serialize(data, pretty: true);
                object back = JsonCodec.Deserialize(json, data.GetType());
                Assert.AreEqual(json, JsonCodec.Serialize(back, pretty: true));
            }
        }

        [Test]
        public void JsonCodec_HandlesNestedListsEnumsAndExactDoubles()
        {
            var state = new SimState { minute = 0.1 + 0.2, feeding = FeedingChoice.Formula };
            state.deliveries.Add(new Delivery { item = SupplyType.Wipes, quantity = 40, arrivesAtMinute = 1.0 / 3 });
            state.stats.energyByHour.AddRange(new[] { 70.123456789, 1e-7 });
            var back = JsonCodec.Deserialize<SimState>(JsonCodec.Serialize(state));
            Assert.AreEqual(state.minute, back.minute);
            Assert.AreEqual(FeedingChoice.Formula, back.feeding);
            Assert.AreEqual(SupplyType.Wipes, back.deliveries.Single().item);
            Assert.AreEqual(1.0 / 3, back.deliveries.Single().arrivesAtMinute);
            CollectionAssert.AreEqual(state.stats.energyByHour, back.stats.energyByHour);
            Assert.IsNull(back.parent.action);
        }

        [Test]
        public void JsonCodec_RejectsMalformedInput()
        {
            Assert.Throws<System.FormatException>(() => JsonCodec.Deserialize<SimState>("{\"minute\": }"));
            Assert.Throws<System.FormatException>(() => JsonCodec.Deserialize<SimState>("{\"minute\": 1} trailing"));
        }

        [Test]
        public void BalanceSim_WritesRunsEnergyAndSummaryFiles()
        {
            var report = BalanceSim.Simulate(DataAssets.LoadConfig(), level: 1, bots: "greedy", runs: 5, seed: 1, FeedingChoice.Both, check: false);
            Assert.That(report.Files, Has.Some.EndsWith("-runs.csv"));
            Assert.That(report.Files, Has.Some.EndsWith("-energy.csv"));
            Assert.That(report.Files, Has.Some.EndsWith("-summary.csv"));
            foreach (var file in report.Files) FileAssert.Exists(file);
        }

        /// <summary>3-plan.md M1: Greedy survives at least 95%, Idle fails 100%, Sloppy is in between (Section 6: 50-80%).</summary>
        [TestCase(FeedingChoice.Both)]
        [TestCase(FeedingChoice.Formula)]
        public void NewbornDay_MeetsBalanceTargets(FeedingChoice feeding)
        {
            var config = DataAssets.LoadConfig();
            var rates = new Dictionary<string, double>();
            foreach (var bot in BalanceRunner.BotNames)
            {
                var results = BalanceRunner.Run(config, month: 0, feeding, bot, runs: 200, firstSeed: 1000);
                rates[bot] = BalanceRunner.SuccessRate(results);
                Assert.IsTrue(BalanceRunner.TargetFor(bot, 1).Met(rates[bot]), $"{bot}: {rates[bot]:P1}, target {BalanceRunner.TargetFor(bot, 1)}");
            }
            Assert.That(rates["sloppy"], Is.LessThan(rates["greedy"]).And.GreaterThan(rates["idle"]));
        }

        [TestCase(4)]
        [TestCase(7)]
        [TestCase(13)]
        [TestCase(25)]
        [TestCase(36)]
        public void EveryAgeBand_RunsAFullGreedyDay(int level)
        {
            // Only later milestones tune these ages; here the rules must simply hold up for a whole day.
            var result = BalanceRunner.RunDay(DataAssets.LoadConfig(), BalanceRunner.MonthForLevel(level), FeedingChoice.Both, "greedy", 1);
            Assert.That(result.endMinute, Is.GreaterThan(0));
            Assert.That(result.stats.energyByHour.Count, Is.GreaterThan(0));
        }
    }
}
