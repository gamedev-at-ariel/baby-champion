using System.Collections;
using System.Collections.Generic;
using BabyChampion.Game;
using BabyChampion.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BabyChampion.Tests.PlayMode
{
    /// <summary>
    /// Every entry of every radial menu (furniture, baby, parent) and every phone order triggers the
    /// matching simulation command, after the parent has walked (and carried the baby) to where it happens.
    /// </summary>
    public class RadialMenuTests
    {
        readonly HouseFixture _house = new HouseFixture();

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        /// <summary>A state in which the action is allowed.</summary>
        static void Prepare(SimState s, ActionType type)
        {
            switch (type)
            {
                case ActionType.Breastfeed:
                case ActionType.BottleFeed:
                    s.baby.hunger = 50;
                    s.supplies.bottlesReady = 1;
                    break;
                case ActionType.PrepareBottle:
                    s.supplies.bottlesReady = 0;
                    break;
                case ActionType.Burp:
                    s.baby.needsBurp = true;
                    s.baby.minutesSinceFeed = 0;
                    break;
                case ActionType.ChangeDiaper:
                    s.baby.diaper = DiaperState.Wet;
                    break;
                case ActionType.PutBabyToSleep:
                    s.baby.tiredness = 50;
                    break;
                case ActionType.Eat:
                    s.parent.hunger = 50;
                    break;
                case ActionType.Sleep:
                    s.baby.asleep = true;
                    s.baby.tiredness = 60;
                    break;
                case ActionType.Stop:
                    s.parent.action = new ActiveAction { type = ActionType.Soothe };
                    break;
            }
        }

        IEnumerator RunEntry(string menu, System.Func<List<HouseAction>> entries, int index, List<string> failures, List<string> done)
        {
            var action = entries()[index];
            _house.Start(s =>
            {
                if (action.Verb == HouseVerb.Sim) Prepare(s, action.Command.type);
            });
            yield return null;

            // Put-down entries need the baby in arms first.
            if (action.Verb == HouseVerb.PutBabyDown || (action.Verb == HouseVerb.PutBabyHere && _house.Game.BabySpot == action.Station))
            {
                _house.Game.Perform(new HouseAction { Verb = HouseVerb.PickUpBaby, Label = "Pick up" });
                yield return _house.WaitUntilSettled();
            }

            action = entries()[index];
            var check = _house.Game.Check(action);
            string name = $"{menu} / {action.Label}";
            if (!check.Allowed)
            {
                failures.Add($"{name}: not allowed in its prepared state ({check.Reason})");
                yield break;
            }

            _house.Hud.CloseRadial();
            var request = new RadialRequest { Title = menu, Actions = entries(), ScreenPosition = new Vector2(Screen.width / 2f, Screen.height / 2f) };
            OpenRadial(request);
            Assert.IsTrue(_house.Hud.RadialOpen, $"{name}: the radial menu did not open");
            Assert.AreEqual(action.Label, _house.Hud.RadialActions[index].Label);
            Assert.IsTrue(_house.Hud.RadialButtons[index].enabledSelf, $"{name}: the button is greyed out");
            _house.Hud.PressRadial(index);
            Assert.IsFalse(_house.Hud.RadialOpen, $"{name}: the radial menu stayed open");
            yield return _house.WaitUntilSettled();

            bool ok;
            switch (action.Verb)
            {
                case HouseVerb.PickUpBaby:
                    ok = _house.Game.BabyCarried;
                    break;
                case HouseVerb.PutBabyHere:
                    ok = !_house.Game.BabyCarried && _house.Game.BabySpot == action.Station;
                    break;
                case HouseVerb.PutBabyDown:
                    ok = !_house.Game.BabyCarried && _house.Game.BabySpot != null;
                    break;
                default:
                    var type = action.Command.type;
                    ok = type == ActionType.Stop ? _house.HasEvent("Stopped Soothe") && !_house.Game.Sim.Parent.Busy : _house.HasEvent("Started " + type);
                    var station = action.Station != null ? action.Station : _house.Game.Station;
                    if (ok && ActionCatalog.For(type).NeedsBaby) ok = _house.Game.BabyCarried || (station != null && _house.Game.BabySpot == station);
                    if (ok && action.Station != null && ActionCatalog.For(type).AtStation)
                    {
                        var p = _house.Game.Parent.transform.position;
                        var stand = action.Station.StandPosition;
                        ok = new Vector2(p.x - stand.x, p.z - stand.z).magnitude < 0.7f;
                        if (!ok) failures.Add($"{name}: the parent is not at the {action.Station.name}");
                    }
                    break;
            }
            if (!ok) failures.Add($"{name}: no effect. Events: {string.Join(" | ", _house.Events)}");
            else done.Add(name);
        }

        // Same path as a tap: HouseGame raises RadialRequested and the HUD builds the menu.
        void OpenRadial(RadialRequest request) => _house.Hud.OpenRadial(request);

        [UnityTest]
        public IEnumerator EveryFurnitureEntry_TriggersItsCommand()
        {
            yield return _house.Load();
            Time.timeScale = 3f;
            var failures = new List<string>();
            var done = new List<string>();
            foreach (var target in _house.Game.Interactables)
            {
                int count = _house.Game.ActionsFor(target).Count;
                for (int i = 0; i < count; i++)
                    yield return RunEntry(target.DisplayName, () => _house.Game.ActionsFor(target), i, failures, done);
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.That(done.Count, Is.GreaterThanOrEqualTo(15), string.Join(", ", done));
        }

        [UnityTest]
        public IEnumerator EveryBabyEntry_TriggersItsCommand([Values] FeedingChoice feeding)
        {
            yield return _house.Load();
            Time.timeScale = 3f;
            _house.Start(feeding: feeding);
            var failures = new List<string>();
            var done = new List<string>();
            int count = _house.Game.ActionsForBaby().Count;
            for (int i = 0; i < count; i++)
                yield return RunEntry("Baby", () => _house.Game.ActionsForBaby(), i, failures, done);
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.That(done.Count, Is.EqualTo(count));
        }

        [UnityTest]
        public IEnumerator ParentEntries_StopAndPutDown()
        {
            yield return _house.Load();
            Time.timeScale = 3f;
            _house.Start(s => Prepare(s, ActionType.Stop));
            var entries = _house.Game.ActionsForParent();
            Assert.AreEqual(1, entries.Count, "a busy parent with free hands offers only Stop");
            var failures = new List<string>();
            var done = new List<string>();
            yield return RunEntry("You", () => _house.Game.ActionsForParent(), 0, failures, done);

            _house.Start();
            _house.Game.Perform(new HouseAction { Verb = HouseVerb.PickUpBaby, Label = "Pick up" });
            yield return _house.WaitUntilSettled();
            var putDown = _house.Game.ActionsForParent();
            Assert.AreEqual(HouseVerb.PutBabyDown, putDown[0].Verb);
            _house.Game.Perform(putDown[0]);
            yield return _house.WaitUntilSettled();
            Assert.IsFalse(_house.Game.BabyCarried);
            Assert.IsTrue(_house.Game.BabySpot.babySpot);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator HandsFreeActions_AreGreyedOutWhileHoldingTheBaby()
        {
            yield return _house.Load();
            _house.Start(s => s.parent.hunger = 50);
            _house.Game.Perform(new HouseAction { Verb = HouseVerb.PickUpBaby, Label = "Pick up" });
            yield return _house.WaitUntilSettled();
            var eat = _house.Game.ActionsFor(_house.Find("KitchenTable"))[0];
            var check = _house.Game.Check(eat);
            Assert.IsFalse(check.Allowed);
            Assert.AreEqual("Put the baby down first", check.Reason);

            _house.Hud.CloseRadial();
            OpenRadial(new RadialRequest { Title = "Kitchen table", Actions = _house.Game.ActionsFor(_house.Find("KitchenTable")), ScreenPosition = Vector2.zero });
            yield return null;
            Assert.IsFalse(_house.Hud.RadialButtons[0].enabledSelf);
            _house.Hud.CloseRadial();
        }

        [UnityTest]
        public IEnumerator PhoneOrders_TriggerOrderCommands()
        {
            yield return _house.Load();
            _house.Start(s => s.money = 1000, minutesPerSecond: 6);
            _house.Hud.OpenPhone();
            Assert.IsTrue(_house.Hud.PhoneOpen);
            var buttons = new List<(ParentCommand command, UnityEngine.UIElements.Button button)>(_house.Hud.OrderButtons);
            Assert.That(buttons.Count, Is.GreaterThanOrEqualTo(8), "normal and express for diapers, wipes, formula and food");
            foreach (var (command, button) in buttons)
            {
                int before = _house.Game.Sim.PendingDeliveries.Count;
                using (var e = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled())
                {
                    e.target = button;
                    button.SendEvent(e);
                }
                yield return _house.WaitUntilSettled();
                Assert.AreEqual(before + 1, _house.Game.Sim.PendingDeliveries.Count, $"{command} did not place an order");
            }
            Assert.IsTrue(_house.HasEvent("Ordered"));
        }
    }
}
