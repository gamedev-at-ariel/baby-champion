using System.Collections;
using BabyChampion.Game;
using BabyChampion.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BabyChampion.Tests.PlayMode
{
    /// <summary>The HUD shows the simulation, and the placeholder baby shows its state and cause hints.</summary>
    public class HudTests
    {
        readonly HouseFixture _house = new HouseFixture();

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        Room Room(string name)
        {
            foreach (var r in _house.Game.Rooms)
                if (r.name == name) return r;
            Assert.Fail("no room " + name);
            return null;
        }

        [UnityTest]
        public IEnumerator Clock_FollowsTheSimulation()
        {
            yield return _house.Load();
            _house.Start(minutesPerSecond: 30);
            yield return new WaitForSeconds(0.5f);
            yield return null;
            Assert.AreEqual(_house.Game.Sim.ClockText, _house.Hud.ClockText);
            Assert.AreNotEqual("07:00", _house.Hud.ClockText);
        }

        [UnityTest]
        public IEnumerator NeedIcons_ChangeShapeWithUrgency()
        {
            yield return _house.Load();
            _house.Start(s => s.baby.hunger = 10);
            yield return null;
            Assert.AreEqual(Urgency.Calm, _house.Hud.NeedUrgency(Hud.Need.Hunger));

            _house.Start(s => s.baby.hunger = 55);
            yield return null;
            Assert.AreEqual(Urgency.Rising, _house.Hud.NeedUrgency(Hud.Need.Hunger));

            _house.Start(s => s.baby.hunger = 80);
            yield return new WaitForSeconds(2.5f); // one tick, so the baby cries
            Assert.IsTrue(_house.Game.Sim.Baby.Crying);
            Assert.AreEqual(Urgency.Urgent, _house.Hud.NeedUrgency(Hud.Need.Hunger));
            Assert.IsFalse(_house.Hud.NeedVisible(Hud.Need.Boredom), "boredom starts at month 4");
            Assert.IsFalse(_house.Hud.NeedVisible(Hud.Need.Burp));

            _house.Start(s =>
            {
                s.baby.needsBurp = true;
                s.month = 5;
            });
            yield return null;
            Assert.IsTrue(_house.Hud.NeedVisible(Hud.Need.Burp));
            Assert.IsTrue(_house.Hud.NeedVisible(Hud.Need.Boredom));
        }

        [UnityTest]
        public IEnumerator EdgeIndicator_PointsAtACryingBabyOffScreen()
        {
            yield return _house.Load();
            _house.Start(s => s.baby.hunger = 80, minutesPerSecond: 2);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(_house.Game.Sim.Baby.Crying);

            _house.Rig.FrameRoom(Room("Bathroom"));
            _house.Rig.SetDistance(7f);
            _house.Rig.SnapToTarget();
            yield return null;
            yield return null;
            Assert.IsTrue(_house.Hud.BabyIndicatorVisible, "no indicator for the crying baby out of view");

            _house.Rig.FrameRoom(null);
            _house.Rig.SetDistance(13f);
            _house.Rig.SnapToTarget();
            yield return null;
            yield return null;
            Assert.IsFalse(_house.Hud.BabyIndicatorVisible, "indicator shown although the baby is in view");
        }

        [UnityTest]
        public IEnumerator EdgeIndicator_ShowsADeliveryAtTheFrontDoor()
        {
            yield return _house.Load();
            _house.Start(s => s.deliveries.Add(new Delivery { item = SupplyType.Diapers, quantity = 24, arrivesAtMinute = 1 }), minutesPerSecond: 2);
            _house.Rig.FrameRoom(Room("Bedroom"));
            _house.Rig.SetDistance(7f);
            _house.Rig.SnapToTarget();
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(0, _house.Game.Sim.PendingDeliveries.Count, "the delivery did not arrive");
            Assert.IsTrue(_house.Hud.DoorIndicatorVisible, "no indicator for the delivery");
            _house.Rig.FrameRoom(null);
        }

        [UnityTest]
        public IEnumerator OutcomePanel_ShowsWhenTheDayFails()
        {
            yield return _house.Load();
            _house.Start(s => s.parent.energy = 0.05, minutesPerSecond: 2);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(Outcome.Failed, _house.Game.Sim.Outcome);
            Assert.IsTrue(_house.Hud.OutcomeShown);
            _house.Game.Restart(1);
            yield return null;
            Assert.IsFalse(_house.Hud.OutcomeShown);
        }

        [UnityTest]
        public IEnumerator Baby_ShowsTheCauseHint()
        {
            yield return _house.Load();
            var cases = new (System.Action<SimState> setup, CryHint hint)[]
            {
                (s => s.baby.hunger = 80, CryHint.HandToMouth),
                (s => { s.baby.diaper = DiaperState.Dirty; s.baby.soiledMinutes = 30; }, CryHint.Squirming),
                (s => s.baby.tiredness = 90, CryHint.RubbingEyes),
                (s => s.baby.tension = 90, CryHint.Fussing),
            };
            foreach (var (setup, hint) in cases)
            {
                _house.Start(setup, minutesPerSecond: 4);
                yield return new WaitForSeconds(0.6f);
                Assert.IsTrue(_house.Game.Baby.Crying, $"the baby is not crying for {hint}");
                Assert.AreEqual(hint, _house.Game.Baby.Hint);
            }

            _house.Start(s =>
            {
                s.baby.asleep = true;
                s.baby.tiredness = 60;
            });
            yield return null;
            Assert.IsTrue(_house.Game.Baby.Asleep);
            Assert.IsFalse(_house.Game.Baby.Crying);
        }

        [UnityTest]
        public IEnumerator PutToSleep_LeavesTheBabyLyingInTheCrib()
        {
            yield return _house.Load();
            Time.timeScale = 3f;
            _house.Start(s => s.baby.tiredness = 60, minutesPerSecond: 4);
            var crib = _house.Find("Crib");
            _house.Game.Perform(_house.Game.ActionsFor(crib)[0]);
            yield return _house.WaitUntilSettled();
            Assert.IsTrue(_house.HasEvent("Started PutBabyToSleep"));
            float end = Time.realtimeSinceStartup + 15f;
            while (!_house.Game.Sim.Baby.Asleep && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
            Assert.IsTrue(_house.Game.Sim.Baby.Asleep, "the baby did not fall asleep");
            Assert.IsFalse(_house.Game.BabyCarried);
            Assert.AreEqual(crib, _house.Game.BabySpot);
            Assert.AreEqual(BabyDisplayPose.Lying, _house.Game.Baby.Pose);
        }
    }
}
