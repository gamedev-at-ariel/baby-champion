using System;
using System.Linq;
using System.Reflection;
using BabyChampion.Simulation;
using NUnit.Framework;

namespace BabyChampion.Tests.EditMode
{
    /// <summary>The parent-controller interface (design Section 19.4), save/load and determinism.</summary>
    public class ControllerAndSaveTests
    {
        [Test]
        public void WithoutAController_TheParentNeverActs()
        {
            var sim = SimTestUtil.Create(s => s.baby.hunger = 90);
            sim.Ticks(20);
            Assert.IsFalse(sim.Parent.Busy);
            Assert.AreEqual(0, sim.FeedsToday);
        }

        [Test]
        public void ActionsEnterOnlyThroughTheControllerInterface()
        {
            // No public API of the simulation assembly accepts a command or action type,
            // apart from the controller interface itself and read-only queries/factories.
            var allowed = new[]
            {
                $"{nameof(CareSimulation)}.{nameof(CareSimulation.Check)}",
                $"{nameof(QueuedController)}.{nameof(QueuedController.Enqueue)}",
                $"{nameof(ParentCommand)}.{nameof(ParentCommand.Of)}",
                $"{nameof(ParentCommand)}.{nameof(ParentCommand.Order)}",
                $"{nameof(ParentCommand)}.{nameof(ParentCommand.Equals)}",
            };
            var offenders = typeof(CareSimulation).Assembly.GetExportedTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Cast<MethodBase>().Concat(t.GetConstructors()))
                .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(ParentCommand) || p.ParameterType == typeof(ActionType)))
                .Select(m => $"{m.DeclaringType.Name}.{m.Name}")
                .Where(name => !allowed.Contains(name))
                .ToList();
            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void SimulationExposesNoMutableState()
        {
            var stateTypes = new[] { typeof(SimState), typeof(BabyState), typeof(ParentState), typeof(SupplyStock), typeof(DayStats), typeof(ActiveAction) };
            var exposing = typeof(CareSimulation).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => stateTypes.Contains(p.PropertyType)).Select(p => p.Name).ToList();
            CollectionAssert.IsEmpty(exposing, "views must be read-only; state is only copied out");

            foreach (var view in new[] { typeof(BabyView), typeof(ParentView), typeof(SupplyView) })
                Assert.IsFalse(view.GetProperties().Any(p => p.CanWrite), $"{view.Name} has a setter");
        }

        [Test]
        public void SaveLoad_RoundTrip_GivesIdenticalState()
        {
            var sim = CareSimulation.NewDay(SimConfig.CreateDefault(), 0, FeedingChoice.Formula, 42);
            sim.AttachController(new GreedyBot());
            sim.Ticks(437); // mid-day, likely mid-action, with deliveries pending or not
            string saved = sim.SaveJson();

            var loaded = CareSimulation.LoadJson(SimConfig.CreateDefault(), saved);
            Assert.AreEqual(saved, loaded.SaveJson());
        }

        [Test]
        public void SaveLoad_ContinuesExactlyLikeTheOriginal()
        {
            var config = SimConfig.CreateDefault();
            var original = CareSimulation.NewDay(config, 0, FeedingChoice.Both, 7);
            original.AttachController(new GreedyBot());
            original.Ticks(300);

            var loaded = CareSimulation.LoadJson(config, original.SaveJson());
            loaded.AttachController(new GreedyBot());
            original.Ticks(600);
            loaded.Ticks(600);
            Assert.AreEqual(original.SaveJson(), loaded.SaveJson());
        }

        [Test]
        public void SaveJson_HoldsReadableFields()
        {
            var sim = SimTestUtil.Create(s => s.baby.diaper = DiaperState.Wet);
            string json = sim.SaveJson();
            StringAssert.Contains("\"diaper\":\"Wet\"", json);
            StringAssert.Contains("\"feeding\":\"Both\"", json);
        }

        [Test]
        public void LoadJson_RejectsUnknownVersions()
        {
            var state = SimTestUtil.Create().CaptureState();
            state.version = 99;
            Assert.Throws<FormatException>(() => CareSimulation.LoadJson(SimConfig.CreateDefault(), JsonCodec.Serialize(state)));
        }

        [Test]
        public void SameSeed_GivesTheSameDay_DifferentSeedsDiffer()
        {
            var config = SimConfig.CreateDefault();
            string Day(int seed, string bot)
            {
                var sim = CareSimulation.NewDay(config, 0, FeedingChoice.Both, seed);
                sim.AttachController(BalanceRunner.CreateBot(bot, seed));
                sim.RunToEnd();
                return sim.SaveJson();
            }
            Assert.AreEqual(Day(5, "sloppy"), Day(5, "sloppy"));
            Assert.AreNotEqual(Day(5, "sloppy"), Day(6, "sloppy"));
        }
    }
}
