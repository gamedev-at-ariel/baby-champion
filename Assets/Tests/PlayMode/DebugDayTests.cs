using System.Collections;
using BabyChampion.Game;
using BabyChampion.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BabyChampion.Tests.PlayMode
{
    /// <summary>The M1 debug scene runs a simulated day in real time and accepts the player's commands.</summary>
    public class DebugDayTests
    {
        DebugDayRunner _runner;

        IEnumerator LoadDebugDay()
        {
            yield return SceneManager.LoadSceneAsync("DebugDay", LoadSceneMode.Single);
            _runner = Object.FindAnyObjectByType<DebugDayRunner>();
            Assert.IsNotNull(_runner, "DebugDay has no DebugDayRunner");
            Assert.IsNotNull(_runner.Sim, "the runner did not start a day");
        }

        [UnityTest]
        public IEnumerator DebugDay_AdvancesTheClockInRealTime()
        {
            yield return LoadDebugDay();
            _runner.Sim.MinutesPerRealSecond = 60;
            double start = _runner.Sim.Minute;
            yield return new WaitForSeconds(0.5f);
            Assert.That(_runner.Sim.Minute, Is.GreaterThan(start + 5));
            Assert.AreEqual(Outcome.Running, _runner.Sim.Outcome);
        }

        [UnityTest]
        public IEnumerator DebugDay_Pause_StopsTheClock()
        {
            yield return LoadDebugDay();
            _runner.Sim.Paused = true;
            double start = _runner.Sim.Minute;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(start, _runner.Sim.Minute);
        }

        [UnityTest]
        public IEnumerator DebugDay_PlayerCommands_ReachTheSimulation()
        {
            yield return LoadDebugDay();
            _runner.Sim.MinutesPerRealSecond = 60;
            int money = _runner.Sim.Money;
            _runner.Player.Enqueue(ParentCommand.Order(SupplyType.Diapers));
            yield return new WaitForSeconds(0.2f);
            Assert.Less(_runner.Sim.Money, money);
            Assert.AreEqual(1, _runner.Sim.PendingDeliveries.Count);
        }

        [UnityTest]
        public IEnumerator DebugDay_Restart_BeginsAFreshDay()
        {
            yield return LoadDebugDay();
            _runner.Sim.MinutesPerRealSecond = 60;
            yield return new WaitForSeconds(0.3f);
            _runner.Restart(123);
            Assert.AreEqual(0, _runner.Sim.Minute);
            Assert.AreEqual(123, _runner.Seed);
        }
    }
}
