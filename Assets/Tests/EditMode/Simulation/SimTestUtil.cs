using System;
using BabyChampion.Simulation;

namespace BabyChampion.Tests.EditMode
{
    /// <summary>Builds simulations in a chosen state for rule tests.</summary>
    static class SimTestUtil
    {
        /// <summary>Default rules with no random diaper events and no failure, so one rule can be watched in isolation.</summary>
        public static SimConfig QuietConfig()
        {
            var config = SimConfig.CreateDefault();
            foreach (var band in config.ageRates.bands) band.diapersPerDay = 0;
            config.tuning.cryingBudgetMinutes = 1e9;
            config.tuning.maxContinuousCryMinutes = 1e9;
            return config;
        }

        /// <summary>
        /// A newborn day (unless <paramref name="month"/> is given) whose state is adjusted by
        /// <paramref name="setup"/>. The baby starts calm: fed, rested, clean diaper, awake.
        /// </summary>
        public static CareSimulation Create(Action<SimState> setup = null, SimConfig config = null, int month = 0,
            FeedingChoice feeding = FeedingChoice.Both, IParentController controller = null)
        {
            config = config ?? QuietConfig();
            var state = CareSimulation.NewDay(config, month, feeding, seed: 1).CaptureState();
            state.baby.hunger = 0;
            state.baby.tiredness = 0;
            state.parent.hunger = 0;
            state.parent.stress = 0;
            state.parent.energy = 80;
            setup?.Invoke(state);
            var sim = new CareSimulation(config, state);
            if (controller != null) sim.AttachController(controller);
            return sim;
        }

        public static void Ticks(this CareSimulation sim, int count)
        {
            for (int i = 0; i < count; i++) sim.Tick();
        }

        /// <summary>A sim whose player controller has the given commands queued.</summary>
        public static CareSimulation WithCommands(this CareSimulation sim, params ParentCommand[] commands)
        {
            var player = new QueuedController();
            foreach (var command in commands) player.Enqueue(command);
            sim.AttachController(player);
            return sim;
        }
    }
}
