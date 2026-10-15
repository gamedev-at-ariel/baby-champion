using System.Collections;
using System.Linq;
using BabyChampion.Game;
using BabyChampion.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BabyChampion.Tests.PlayMode
{
    /// <summary>
    /// Levels in the house (3-plan.md M3): bots play Levels 1 and 2 at high speed through the real
    /// house, briefing, results screen and autosave; retry restores money and supplies; a Call for
    /// help caps the stars; progress survives a save and reload across the level boundary.
    /// </summary>
    public class LevelFlowTests
    {
        const double Fast = 1500;

        readonly HouseFixture _house = new HouseFixture();

        [SetUp]
        public void SetUp()
        {
            SaveStore.Storage = new MemorySaveStorage();
            GameSession.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameSession.Clear();
            SaveStore.Storage = null;
        }

        static SaveSlot NewSave(FeedingChoice feeding = FeedingChoice.Both)
        {
            var save = SaveSlot.Create("Noa", BabySex.Girl, 1, 2, feeding, new Tuning());
            SaveStore.Save(0, save);
            GameSession.Use(0, save);
            return save;
        }

        IEnumerator LoadLevel(int level)
        {
            GameSession.PendingLevel = level;
            yield return _house.Load();
        }

        /// <summary>Hands the parent to a bot and runs the level at high speed until it ends.</summary>
        IEnumerator PlayOut(IParentController bot, float timeoutSeconds = 90f)
        {
            var game = _house.Game;
            game.BeginPlay();
            game.Sim.AttachController(bot);
            game.Sim.MinutesPerRealSecond = Fast;
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (game.Sim.Outcome == Outcome.Running && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreNotEqual(Outcome.Running, game.Sim.Outcome, $"the level did not end in {timeoutSeconds} s (at {game.Sim.ClockText})");
            yield return null; // the HUD shows the result
        }

        [UnityTest]
        public IEnumerator GreedyBot_CompletesLevels1And2_AndTheSaveCarriesAcrossTheLevelBoundary()
        {
            NewSave();
            yield return LoadLevel(1);
            var game = _house.Game;
            var hud = _house.Hud;
            Assert.AreEqual(1, game.Level?.number, "the house did not start the pending level");
            Assert.IsTrue(game.WaitingToStart);
            Assert.IsTrue(hud.BriefingShown);
            Assert.AreEqual("Noa", game.BabyName);

            game.StartLevel(1, seed: 11);
            yield return PlayOut(new GreedyBot());
            Assert.AreEqual(Outcome.Survived, game.Sim.Outcome, game.Sim.FailText);
            Assert.That(game.LastResult.Stars, Is.GreaterThanOrEqualTo(1));
            Assert.IsTrue(hud.OutcomeShown);
            Assert.AreEqual("Level complete!", hud.OutcomeTitle);
            Assert.AreEqual(game.LastResult.Stars, hud.OutcomeStars);
            var endOfLevel1 = game.Sim.CopyHousehold();
            int starsOfLevel1 = game.LastResult.Stars;

            // "Next level" goes straight on with what Level 1 ended with.
            Assert.IsTrue(hud.PressOutcome("Next level"));
            yield return null;
            Assert.AreEqual(2, game.Level.number);
            Assert.IsTrue(game.WaitingToStart);
            Assert.AreEqual(endOfLevel1.money + game.Level.allowance, game.Sim.Money);

            // The app restarts: progress is read back from storage, and Level 2 starts from it.
            GameSession.Clear();
            var saved = SaveStore.Load(0);
            Assert.IsTrue(saved.Completed(1));
            Assert.IsTrue(saved.IsUnlocked(2));
            Assert.AreEqual(starsOfLevel1, saved.BestStars(1));
            Assert.AreEqual(endOfLevel1.money, saved.household.money);
            Assert.AreEqual(JsonCodec.Serialize(endOfLevel1.supplies), JsonCodec.Serialize(saved.household.supplies));
            GameSession.Use(0, saved);
            yield return LoadLevel(2);
            game = _house.Game;
            hud = _house.Hud;
            Assert.AreEqual(2, game.Level.number);
            Assert.AreEqual("21:00", game.Sim.ClockText);
            Assert.AreEqual(endOfLevel1.money + game.Level.allowance, game.Sim.Money);
            Assert.AreEqual(endOfLevel1.supplies.diapers, game.Sim.Supplies.Diapers);
            Assert.AreEqual(endOfLevel1.supplies.adultFood, game.Sim.Supplies.AdultFood);

            game.StartLevel(2, seed: 5);
            yield return PlayOut(new GreedyBot());
            Assert.AreEqual(Outcome.Survived, game.Sim.Outcome, game.Sim.FailText);
            Assert.That(game.LastResult.Stars, Is.GreaterThanOrEqualTo(1));
            Assert.AreEqual("07:00", game.Sim.ClockText);
            Assert.IsTrue(SaveStore.Load(0).Completed(2));
            Assert.IsFalse(hud.OutcomeButtons.Any(b => b.text == "Next level"), "Level 3 is not built yet");
        }

        IEnumerator IdleFails(int level)
        {
            NewSave();
            yield return LoadLevel(level);
            var game = _house.Game;
            var hud = _house.Hud;
            game.StartLevel(level, seed: 3);
            yield return PlayOut(new IdleBot());

            var headless = BalanceRunner.RunLevel(game.CreateConfig(), game.Level, game.Feeding, "idle", 3);
            Assert.AreEqual(Outcome.Failed, game.Sim.Outcome);
            Assert.AreEqual(headless.failReason, game.Sim.FailReason, "the house and the headless simulation disagree");
            Assert.AreEqual(FailReason.ContinuousCrying, game.Sim.FailReason);
            Assert.IsTrue(hud.OutcomeShown);
            Assert.AreEqual(0, hud.OutcomeStars);
            CollectionAssert.Contains(hud.OutcomeButtons.Select(b => b.text).ToList(), "Try again");

            var saved = SaveStore.Load(0);
            Assert.AreEqual(1, saved.Record(level).attempts);
            Assert.IsFalse(saved.Completed(level));
            Assert.IsFalse(saved.IsUnlocked(level + 1));
        }

        [UnityTest]
        public IEnumerator IdleBot_FailsLevel1_WithTheRightReason() => IdleFails(1);

        [UnityTest]
        public IEnumerator IdleBot_FailsLevel2_WithTheRightReason() => IdleFails(2);

        [UnityTest]
        public IEnumerator Retry_RestoresMoneyAndSupplies()
        {
            NewSave(FeedingChoice.Formula);
            yield return LoadLevel(1);
            var game = _house.Game;
            var hud = _house.Hud;
            game.StartLevel(1, seed: 3);
            int money = game.Sim.Money;
            string supplies = JsonCodec.Serialize(game.Sim.CopyHousehold().supplies);
            int food = game.Sim.Supplies.AdultFood;

            game.Sim.MinutesPerRealSecond = 40;
            Assert.IsTrue(game.Perform(HouseAction.Sim(ParentCommand.Order(SupplyType.Diapers, 2))));
            Assert.IsTrue(game.Perform(HouseAction.Sim(ParentCommand.Order(SupplyType.AdultFood, 1, express: true))));
            float end = Time.realtimeSinceStartup + 10f;
            while (game.Sim.Supplies.AdultFood <= food && Time.realtimeSinceStartup < end) yield return null;
            Assert.Less(game.Sim.Money, money, "the orders were not paid");
            Assert.AreNotEqual(supplies, JsonCodec.Serialize(game.Sim.CopyHousehold().supplies), "the express delivery did not arrive");

            yield return PlayOut(new IdleBot());
            Assert.AreEqual(Outcome.Failed, game.Sim.Outcome);
            game.Sim.MinutesPerRealSecond = 2; // a retry keeps the speed (a playtest option)
            Assert.IsTrue(hud.PressOutcome("Try again"));
            yield return null;
            Assert.AreEqual(Outcome.Running, game.Sim.Outcome);
            Assert.AreEqual(1, game.Level.number);
            Assert.AreEqual(money, game.Sim.Money);
            Assert.AreEqual(supplies, JsonCodec.Serialize(game.Sim.CopyHousehold().supplies));
            Assert.AreEqual(0, game.Sim.PendingDeliveries.Count);
            Assert.That(game.Sim.Minute, Is.LessThan(2), "the retry starts at 07:00");
            Assert.AreEqual(money, SaveStore.Load(0).household.money, "a failed level does not change the save's money");
        }

        [UnityTest]
        public IEnumerator CallForHelp_CapsTheLevelAtTwoStars()
        {
            yield return _house.Load();
            var game = _house.Game;
            var hud = _house.Hud;
            var config = _house.QuietConfig();
            var level = config.levels.Find(1);
            level.lengthMinutes = 60;
            level.goals.Clear();
            level.start = new LevelStart { babyHunger = 10, babyTiredness = 30, babyAsleep = true, parentEnergy = 90, parentJoy = 100 };

            IEnumerator Play(bool help)
            {
                game.StartFromState(CareSimulation.StartLevel(config, level, FeedingChoice.Both, 1).CaptureState(), config);
                game.Sim.MinutesPerRealSecond = 4;
                if (help)
                {
                    hud.OpenPhone();
                    yield return null;
                    Assert.IsTrue(hud.HelpButton.enabledSelf, "the phone's Call Grandma button is greyed out");
                    hud.ClosePhone();
                    Assert.IsTrue(game.Perform(HouseAction.Sim(ParentCommand.Of(ActionType.CallForHelp))));
                    float wait = Time.realtimeSinceStartup + 5f;
                    while (!game.Sim.HelpActive && Time.realtimeSinceStartup < wait) yield return null;
                    yield return null;
                    Assert.IsTrue(game.Sim.HelpActive);
                    Assert.IsTrue(game.Helper.Visible, "Grandma is not shown");
                    var pickUp = game.ActionsForBaby().Find(a => a.Verb == HouseVerb.PickUpBaby);
                    Assert.AreEqual("Grandma is looking after the baby", game.Check(pickUp).Reason);
                    hud.OpenPhone();
                    yield return null;
                    Assert.IsFalse(hud.HelpButton.enabledSelf, "Grandma can be called twice");
                    hud.ClosePhone();
                }
                game.Sim.MinutesPerRealSecond = 120;
                float end = Time.realtimeSinceStartup + 20f;
                while (game.Sim.Outcome == Outcome.Running && Time.realtimeSinceStartup < end) yield return null;
                yield return null;
            }

            yield return Play(help: false);
            Assert.AreEqual(Outcome.Survived, game.Sim.Outcome, game.Sim.FailText);
            Assert.AreEqual(3, game.LastResult.Stars);
            Assert.AreEqual(3, hud.OutcomeStars);

            yield return Play(help: true);
            Assert.AreEqual(Outcome.Survived, game.Sim.Outcome, game.Sim.FailText);
            Assert.AreEqual(2, game.LastResult.Stars);
            Assert.AreEqual(2, hud.OutcomeStars);
            Assert.IsFalse(game.Helper.Visible, "Grandma stays after the level");
        }

        [UnityTest]
        public IEnumerator Briefing_HoldsTheClock_UntilStart()
        {
            NewSave();
            yield return LoadLevel(2);
            var game = _house.Game;
            var hud = _house.Hud;
            game.Sim.MinutesPerRealSecond = 30;
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(hud.BriefingShown);
            Assert.AreEqual(0, game.Sim.Minute);
            Assert.AreEqual("Level 2: Night Shift", hud.ObjectiveText);
            Assert.AreEqual("", hud.CoachText, "only the tutorial level coaches");

            hud.PressStart();
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(hud.BriefingShown);
            Assert.That(game.Sim.Minute, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator Level1_CoachesThePlayer()
        {
            NewSave();
            yield return LoadLevel(1);
            var hud = _house.Hud;
            yield return null;
            Assert.AreEqual("", hud.CoachText, "no tips over the briefing");
            hud.PressStart();
            yield return null;
            StringAssert.StartsWith("Welcome home!", hud.CoachText);

            _house.Start(s =>
            {
                s.level = LevelTable.CreateDefault().Find(1);
                s.baby.needsBurp = true;
                s.minute = 60;
            });
            yield return null;
            StringAssert.StartsWith("Fed! Now burp", hud.CoachText);
        }
    }
}
