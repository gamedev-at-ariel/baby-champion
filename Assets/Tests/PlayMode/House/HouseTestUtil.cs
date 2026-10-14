using System;
using System.Collections;
using System.Collections.Generic;
using BabyChampion.Game;
using BabyChampion.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BabyChampion.Tests.PlayMode
{
    /// <summary>Loads the M2 house scene and puts the simulation into chosen states.</summary>
    public sealed class HouseFixture
    {
        public HouseGame Game;
        public Hud Hud;
        public CameraRig Rig;
        public readonly List<string> Events = new List<string>();

        public IEnumerator Load()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            Game = UnityEngine.Object.FindAnyObjectByType<HouseGame>();
            Hud = UnityEngine.Object.FindAnyObjectByType<Hud>();
            Rig = UnityEngine.Object.FindAnyObjectByType<CameraRig>();
            Assert.IsNotNull(Game, "House has no HouseGame");
            Assert.IsNotNull(Hud, "House has no Hud");
            Assert.IsNotNull(Rig, "House has no CameraRig");
            Assert.IsNotNull(Game.Sim, "HouseGame did not start a day");
            Game.SimEventRaised += e => Events.Add(e.Text);
        }

        /// <summary>Rules without random diaper events or crying failures, so a test controls the state.</summary>
        public SimConfig QuietConfig()
        {
            var config = Game.CreateConfig();
            foreach (var band in config.ageRates.bands) band.diapersPerDay = 0;
            config.tuning.cryingBudgetMinutes = 1e9;
            config.tuning.maxContinuousCryMinutes = 1e9;
            return config;
        }

        /// <summary>
        /// Starts a calm newborn day (fed, rested, clean, parent fine), adjusted by <paramref name="setup"/>,
        /// with a slow clock so walking does not change the state much.
        /// </summary>
        public void Start(Action<SimState> setup = null, FeedingChoice feeding = FeedingChoice.Both, double minutesPerSecond = 0.5)
        {
            var state = CareSimulation.NewDay(Game.CreateConfig(), 0, feeding, 7).CaptureState();
            state.baby.hunger = 10;
            state.baby.tiredness = 10;
            state.parent.energy = 60;
            state.parent.hunger = 10;
            state.parent.stress = 0;
            setup?.Invoke(state);
            Game.StartFromState(state, QuietConfig(), seed: 7);
            Game.Sim.MinutesPerRealSecond = minutesPerSecond;
            Events.Clear();
        }

        /// <summary>Waits until the parent has finished walking and the queued command has been taken.</summary>
        public IEnumerator WaitUntilSettled(float timeoutSeconds = 20f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < end && (Game.PlanActive || Game.Player.Pending > 0 || Game.Parent.Walking))
                yield return null;
            Assert.IsFalse(Game.PlanActive, $"the parent did not finish '{Game.PlanLabel}' in {timeoutSeconds} s");
            // One more tick, so the command has been applied and its events raised.
            double minute = Game.Sim.Minute;
            while (Time.realtimeSinceStartup < end && Game.Sim.Minute <= minute) yield return null;
        }

        public bool HasEvent(string prefix) => Events.Exists(e => e.StartsWith(prefix));

        public Interactable Find(string name)
        {
            foreach (var i in Game.Interactables)
                if (i.name == name) return i;
            Assert.Fail($"No interactable named {name}");
            return null;
        }
    }
}
