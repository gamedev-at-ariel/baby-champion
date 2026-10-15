using System.Collections.Generic;
using System.Linq;
using BabyChampion.Simulation;
using NUnit.Framework;

namespace BabyChampion.Tests.EditMode
{
    /// <summary>Level rules (M3): time window, start values, goals, fail conditions and stars.</summary>
    public class LevelTests
    {
        /// <summary>A short level with no goals, a calm sleeping baby and no random diaper events.</summary>
        static LevelDefinition ShortLevel(double minutes = 60)
        {
            var level = LevelTable.CreateDefault().Find(1);
            level.lengthMinutes = minutes;
            level.goals.Clear();
            level.start = new LevelStart { babyHunger = 10, babyTiredness = 30, babyAsleep = true, parentEnergy = 90, parentHunger = 0, parentStress = 0, parentJoy = 100 };
            return level;
        }

        static CareSimulation Start(LevelDefinition level, IParentController controller = null, Household household = null)
        {
            var sim = CareSimulation.StartLevel(SimTestUtil.QuietConfig(), level, FeedingChoice.Both, 1, household);
            if (controller != null) sim.AttachController(controller);
            return sim;
        }

        [Test]
        public void Levels_UseTheirOwnTimeWindow()
        {
            var table = LevelTable.CreateDefault();
            var day = Start(table.Find(1));
            Assert.AreEqual("07:00", day.ClockText);
            Assert.AreEqual(1440, day.LengthMinutes);

            var night = Start(table.Find(2), new GreedyBot());
            Assert.AreEqual("21:00", night.ClockText);
            night.RunToEnd();
            Assert.AreEqual(600, night.Minute, 1e-9, "the night shift ends at 07:00");
            Assert.AreEqual("07:00", night.ClockText);
            StringAssert.StartsWith("07:00 Morning! Night Shift", night.RecentEvents.Last().ToString(), "events use the level's clock");
        }

        [Test]
        public void StartLevel_AppliesStartValuesTheHouseholdAndTheAllowance()
        {
            var night = LevelTable.CreateDefault().Find(2);
            var household = new Household { money = 77, supplies = new SupplyStock { diapers = 3, wipes = 9, formula = 1, adultFood = 2 } };
            var sim = Start(night, household: household);
            Assert.AreEqual(77 + night.allowance, sim.Money);
            Assert.AreEqual(3, sim.Supplies.Diapers);
            Assert.AreEqual(9, sim.Supplies.Wipes);
            Assert.AreEqual(night.start.parentEnergy, sim.Parent.Energy);
            Assert.AreEqual(night.start.babyTiredness, sim.Baby.Tiredness);
            Assert.AreEqual(1, sim.Month);
            Assert.AreEqual(2, sim.LevelNumber);

            household.money = 0; // the simulation took a copy
            Assert.AreEqual(77 + night.allowance, sim.Money);
        }

        [Test]
        public void TheLevelCopy_CannotChangeTheRules()
        {
            var sim = Start(ShortLevel());
            sim.Level.lengthMinutes = 5;
            Assert.AreEqual(60, sim.LengthMinutes);
        }

        [Test]
        public void Level_IsCompleted_WhenEveryGoalIsMetAtTimeUp()
        {
            var level = ShortLevel();
            level.start.babyAsleep = false;
            level.start.babyHunger = 40;
            level.goals.Add(new LevelGoal { type = GoalType.Feeds, target = 1, text = "Feed" });
            var sim = Start(level).WithCommands(ParentCommand.Of(ActionType.Breastfeed));
            sim.RunToEnd();
            Assert.AreEqual(Outcome.Survived, sim.Outcome);
            Assert.IsTrue(sim.Goals.Single().Met);
            Assert.That(sim.Result.Stars, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Level_FailsWithObjectiveMissed_WhenAGoalIsNotMet()
        {
            var level = ShortLevel();
            level.goals.Add(new LevelGoal { type = GoalType.DiaperChanges, target = 1, text = "Change a diaper" });
            var sim = Start(level);
            sim.RunToEnd();
            Assert.AreEqual(Outcome.Failed, sim.Outcome);
            Assert.AreEqual(FailReason.ObjectiveMissed, sim.FailReason);
            StringAssert.Contains("Change a diaper", sim.FailText);
            Assert.AreEqual(0, sim.Result.Stars);
        }

        [TestCase(25, true)]
        [TestCase(20.5, false)]
        public void EnergyAtEndGoal_NeedsEnergyAboveTheTarget(double startEnergy, bool completed)
        {
            var level = ShortLevel(10);
            level.start.parentEnergy = startEnergy;
            level.goals.Add(new LevelGoal { type = GoalType.EnergyAtEnd, target = 20, text = "Energy above 20" });
            var sim = Start(level);
            sim.RunToEnd();
            Assert.AreEqual(completed, sim.Outcome == Outcome.Survived, $"energy at the end {sim.Parent.Energy}");
        }

        [Test]
        public void LevelCryingBudget_ReplacesTheTuningOne()
        {
            var level = ShortLevel();
            level.cryingBudgetMinutes = 5;
            level.start.babyAsleep = false;
            level.start.babyHunger = 90;
            var sim = Start(level);
            sim.RunToEnd();
            Assert.AreEqual(FailReason.CryingBudget, sim.FailReason);
            Assert.That(sim.CryingMinutesToday, Is.EqualTo(6).Within(1));
        }

        [Test]
        public void FailConditions_EndTheLevelAtOnce()
        {
            var level = ShortLevel(600);
            level.start.babyAsleep = false;
            level.failConditions.Add(new FailCondition { type = FailConditionType.EnergyAtOrBelow, value = 89, text = "Too tired for the party" });
            var sim = Start(level);
            sim.RunToEnd();
            Assert.AreEqual(FailReason.LevelRule, sim.FailReason);
            Assert.AreEqual("Too tired for the party", sim.FailText);
            Assert.That(sim.Minute, Is.LessThan(30));

            var rash = ShortLevel(600);
            rash.failConditions.Add(new FailCondition { type = FailConditionType.DiaperRash, text = "Rash" });
            rash.cryingBudgetMinutes = rash.maxContinuousCryMinutes = 600;
            var dirty = CareSimulation.StartLevel(SimTestUtil.QuietConfig(), rash, FeedingChoice.Both, 1).CaptureState();
            dirty.baby.diaper = DiaperState.Dirty;
            var sim2 = new CareSimulation(SimTestUtil.QuietConfig(), dirty);
            sim2.RunToEnd();
            Assert.AreEqual(FailReason.LevelRule, sim2.FailReason);
            Assert.AreEqual("Rash", sim2.FailText);
        }

        [Test]
        public void ActionsNotInTheLevel_AreRefused()
        {
            var level = ShortLevel();
            level.start.babyAsleep = false;
            level.actions.Remove(ActionType.Play);
            var sim = Start(level);
            Assert.AreEqual("Not in this level", sim.Check(ParentCommand.Of(ActionType.Play)).Reason);
            Assert.IsTrue(sim.Check(ParentCommand.Of(ActionType.Soothe)).Allowed);
        }

        [TestCase(true, 80, 70, false, 3)]
        [TestCase(true, 80, 50, false, 2)]
        [TestCase(true, 60, 90, false, 1)]
        [TestCase(false, 95, 95, false, 0)]
        [TestCase(true, 80, 70, true, 2)]
        [TestCase(true, 60, 90, true, 1)]
        public void Stars_FollowTheRules(bool completed, double wellbeing, double joy, bool help, int stars)
        {
            var result = LevelScoring.Score(1, new StarRules(), completed, FailReason.CryingBudget, wellbeing, joy, help);
            Assert.AreEqual(stars, result.Stars);
            Assert.AreEqual(completed ? FailReason.None : FailReason.CryingBudget, result.FailReason);
            Assert.AreEqual(3 + (help && stars == 2 && wellbeing >= 70 && joy >= 60 ? 1 : 0), result.Checks.Count);
        }

        [Test]
        public void SaveLoad_KeepsTheLevelAndTheHelper()
        {
            var sim = Start(LevelTable.CreateDefault().Find(2)).WithCommands(ParentCommand.Of(ActionType.CallForHelp));
            sim.Ticks(30);
            var loaded = CareSimulation.LoadJson(SimTestUtil.QuietConfig(), sim.SaveJson());
            Assert.AreEqual(2, loaded.LevelNumber);
            Assert.AreEqual("21:30", loaded.ClockText);
            Assert.IsTrue(loaded.HelpActive);
            Assert.AreEqual(sim.SaveJson(), loaded.SaveJson());
        }
    }

    /// <summary>Call for help (design Section 9): the grandparent looks after the baby for 2 hours, once per level.</summary>
    public class CallForHelpTests
    {
        static CareSimulation Day(System.Action<SimState> setup = null)
        {
            var level = LevelTable.CreateDefault().Find(1);
            var state = CareSimulation.StartLevel(SimTestUtil.QuietConfig(), level, FeedingChoice.Both, 1).CaptureState();
            state.level.cryingBudgetMinutes = state.level.maxContinuousCryMinutes = 1440;
            state.baby.hunger = 0;
            state.baby.tiredness = 0;
            state.parent.energy = 50;
            state.parent.stress = 0;
            setup?.Invoke(state);
            return new CareSimulation(SimTestUtil.QuietConfig(), state);
        }

        [Test]
        public void Grandma_CaresForTheBaby_WhileTheParentSleeps()
        {
            var sim = Day(s => s.baby.hunger = 60).WithCommands(ParentCommand.Of(ActionType.CallForHelp), ParentCommand.Of(ActionType.Sleep));
            sim.Ticks(2);
            Assert.IsTrue(sim.HelpActive);
            Assert.IsFalse(sim.Baby.Asleep);
            Assert.AreEqual(ActionType.Sleep, sim.Parent.Action, "with Grandma here, the parent can sleep while the baby is awake");

            sim.Ticks(100);
            Assert.AreEqual(ActionType.Sleep, sim.Parent.Action, "the parent sleeps through: Grandma handles the baby");
            Assert.AreEqual(0, sim.FeedsToday, "Grandma's feeds do not count towards the level's goals");
            Assert.That(sim.CopyStats().helperTasks, Is.GreaterThan(0));
            Assert.That(sim.CopyStats().cryingMinutes, Is.LessThan(5));
            Assert.That(sim.Parent.Energy, Is.GreaterThan(60));
        }

        [Test]
        public void Grandma_ComesOncePerLevel_ForTwoHours()
        {
            var sim = Day().WithCommands(ParentCommand.Of(ActionType.CallForHelp));
            sim.Tick();
            Assert.AreEqual(sim.Minute - 1 + 120, sim.HelpUntilMinute, 1e-9);
            Assert.AreEqual("Grandma is already here", sim.Check(ParentCommand.Of(ActionType.CallForHelp)).Reason);
            sim.Ticks(120);
            Assert.IsFalse(sim.HelpActive);
            Assert.AreEqual(0, sim.HelpCallsLeft);
            Assert.AreEqual("Grandma already came today", sim.Check(ParentCommand.Of(ActionType.CallForHelp)).Reason);
        }

        [Test]
        public void WhileGrandmaHelps_BabyCareIsHers()
        {
            var sim = Day(s =>
            {
                s.baby.hunger = 30;
                s.parent.action = new ActiveAction { type = ActionType.Soothe };
            }).WithCommands(ParentCommand.Of(ActionType.CallForHelp));
            sim.Tick();
            Assert.IsFalse(sim.Parent.Busy, "calling Grandma hands the baby over");
            foreach (var type in new[] { ActionType.Breastfeed, ActionType.Soothe, ActionType.Play })
                Assert.AreEqual("Grandma is looking after the baby", sim.Check(ParentCommand.Of(type)).Reason, type.ToString());
            Assert.IsTrue(sim.Check(ParentCommand.Order(SupplyType.Diapers)).Allowed);
        }

        [Test]
        public void Grandma_UsesHomeDiapers_AndFormulaUnlessBreastfeeding()
        {
            var sim = Day(s =>
            {
                s.feeding = FeedingChoice.Formula;
                s.baby.hunger = 60;
                s.baby.diaper = DiaperState.Dirty;
            }).WithCommands(ParentCommand.Of(ActionType.CallForHelp));
            int diapers = sim.Supplies.Diapers, formula = sim.Supplies.Formula;
            sim.Ticks(60);
            Assert.AreEqual(diapers - 1, sim.Supplies.Diapers);
            Assert.AreEqual(formula - 1, sim.Supplies.Formula);

            var breast = Day(s =>
            {
                s.feeding = FeedingChoice.Breast;
                s.baby.hunger = 60;
            }).WithCommands(ParentCommand.Of(ActionType.CallForHelp));
            int before = breast.Supplies.Formula;
            breast.Ticks(40);
            Assert.That(breast.Baby.Hunger, Is.LessThan(20), "Grandma fed expressed milk");
            Assert.AreEqual(before, breast.Supplies.Formula);
        }

        [Test]
        public void Help_CapsTheLevelAtTwoStars()
        {
            LevelResult Play(bool help)
            {
                var level = LevelTable.CreateDefault().Find(1);
                level.lengthMinutes = 60;
                level.goals.Clear();
                level.start = new LevelStart { babyHunger = 10, babyTiredness = 30, babyAsleep = true, parentEnergy = 90, parentJoy = 100 };
                var sim = CareSimulation.StartLevel(SimTestUtil.QuietConfig(), level, FeedingChoice.Both, 1);
                if (help) sim.WithCommands(ParentCommand.Of(ActionType.CallForHelp));
                sim.RunToEnd();
                return sim.Result;
            }
            Assert.AreEqual(3, Play(help: false).Stars);
            var helped = Play(help: true);
            Assert.AreEqual(2, helped.Stars);
            Assert.IsTrue(helped.HelpUsed);
            Assert.IsTrue(helped.Checks.Any(c => c.Text.StartsWith("Grandma helped")));
        }

        [Test]
        public void ALevelWithoutHelp_RefusesTheCall()
        {
            var level = LevelTable.CreateDefault().Find(1);
            level.helpCalls = 0;
            level.actions.Remove(ActionType.CallForHelp);
            var sim = CareSimulation.StartLevel(SimTestUtil.QuietConfig(), level, FeedingChoice.Both, 1);
            Assert.IsFalse(sim.Check(ParentCommand.Of(ActionType.CallForHelp)).Allowed);
        }
    }

    /// <summary>Save slots (design Section 19.6): progress, unlocking and carried-over money and supplies.</summary>
    public class SaveSlotTests
    {
        static SaveSlot NewSlot() => SaveSlot.Create("  Noa  ", BabySex.Girl, 1, 2, FeedingChoice.Formula, new Tuning());

        static LevelResult Result(int level, bool completed, int stars) =>
            new LevelResult { Level = level, Completed = completed, Stars = completed ? stars : 0 };

        [Test]
        public void Create_CleansTheName_AndStartsWithTheTuningHousehold()
        {
            var slot = NewSlot();
            Assert.AreEqual("Noa", slot.babyName);
            Assert.AreEqual("Baby", SaveSlot.CleanName("   "));
            Assert.AreEqual(SaveSlot.MaxNameLength, SaveSlot.CleanName(new string('x', 50)).Length);
            Assert.AreEqual(new Tuning().start.money, slot.household.money);
            Assert.AreEqual(new Tuning().start.diapers, slot.household.supplies.diapers);
        }

        [Test]
        public void Levels_UnlockInOrder()
        {
            var slot = NewSlot();
            var table = LevelTable.CreateDefault();
            Assert.IsTrue(slot.IsUnlocked(1));
            Assert.IsFalse(slot.IsUnlocked(2));
            Assert.AreEqual(1, slot.NextLevel(table));

            slot.RecordResult(Result(1, false, 0), new Household { money = 1 });
            Assert.IsFalse(slot.IsUnlocked(2), "a failed attempt unlocks nothing");
            slot.RecordResult(Result(1, true, 2), new Household { money = 2 });
            Assert.IsTrue(slot.IsUnlocked(2));
            Assert.AreEqual(2, slot.NextLevel(table));
            Assert.AreEqual(2, slot.Record(1).attempts);
            Assert.AreEqual(1, slot.Record(1).completions);
        }

        [Test]
        public void OnlyACompletedLevel_CarriesMoneyAndSuppliesOver_AndBestStarsAreKept()
        {
            var slot = NewSlot();
            int start = slot.household.money;
            slot.RecordResult(Result(1, false, 0), new Household { money = 5 });
            Assert.AreEqual(start, slot.household.money, "a retry starts with the same money");

            var end = new Household { money = 99, supplies = new SupplyStock { diapers = 7 } };
            slot.RecordResult(Result(1, true, 3), end);
            Assert.AreEqual(99, slot.household.money);
            Assert.AreEqual(7, slot.household.supplies.diapers);
            end.money = 0;
            Assert.AreEqual(99, slot.household.money, "the slot keeps a copy");

            slot.RecordResult(Result(1, true, 1), new Household { money = 50 });
            Assert.AreEqual(3, slot.BestStars(1));
            Assert.AreEqual(3, slot.TotalStars);
            Assert.AreEqual(1, slot.lastLevel);
        }

        [Test]
        public void Json_RoundTrips_AndRejectsUnknownVersions()
        {
            var slot = NewSlot();
            slot.RecordResult(Result(1, true, 2), new Household { money = 42, supplies = new SupplyStock { wipes = 3 } });
            string json = slot.ToJson();
            var back = SaveSlot.FromJson(json);
            Assert.AreEqual(json, back.ToJson());
            Assert.AreEqual(FeedingChoice.Formula, back.feeding);
            Assert.AreEqual(2, back.outfit);

            back.version = 99;
            Assert.Throws<System.FormatException>(() => SaveSlot.FromJson(back.ToJson()));
        }
    }

    /// <summary>The data validator's level checks (3-plan.md M3).</summary>
    public class LevelValidationTests
    {
        static List<string> Errors(System.Action<LevelTable> change)
        {
            var config = SimConfig.CreateDefault();
            change(config.levels);
            return config.Validate();
        }

        static void Expect(List<string> errors, string fragment) =>
            Assert.IsTrue(errors.Any(e => e.Contains(fragment)), $"no error mentioning '{fragment}' in:\n{string.Join("\n", errors)}");

        [Test]
        public void DefaultLevels_AreValid()
        {
            CollectionAssert.IsEmpty(SimConfig.CreateDefault().Validate());
        }

        [Test]
        public void Validator_FindsBrokenLevels()
        {
            var errors = Errors(t =>
            {
                var one = t.Find(1);
                one.month = 4;                                            // level N is month N-1
                one.lengthMinutes = 0;                                    // time limit must be positive
                one.startHour = 25;
                one.actions.Remove(ActionType.ChangeDiaper);              // a goal needs it
                one.actions.Remove(ActionType.CallForHelp);               // helpCalls is 1
                one.goals.Add(new LevelGoal { type = (GoalType)99, target = 1, text = "?" });
                one.start.parentEnergy = 150;
                one.stars.helpStarCap = 5;
                t.levels.Add(new LevelDefinition { number = 2, title = "Copy", month = 1 }); // duplicate, no briefing, no actions
                t.chapters.RemoveAt(5);                                    // levels 25-36 in no chapter
            });
            Expect(errors, "month 4 should be 0");
            Expect(errors, "lengthMinutes");
            Expect(errors, "startHour 25");
            Expect(errors, "needs action ChangeDiaper");
            Expect(errors, "CallForHelp is not available");
            Expect(errors, "goal type 99 does not exist");
            Expect(errors, "parentEnergy");
            Expect(errors, "helpStarCap");
            Expect(errors, "2 definitions of level 2");
            Expect(errors, "has no briefing");
            Expect(errors, "no available actions");
            Expect(errors, "level 25 is in 0 chapters");
        }

        [Test]
        public void FeedingActions_MustSupportEveryFeedingChoice()
        {
            Expect(Errors(t => t.Find(1).actions.Remove(ActionType.PrepareBottle)), "every feeding choice works");
        }

        [Test]
        public void ImportingAnUnknownAction_IsAFormatError()
        {
            string json = JsonCodec.Serialize(LevelTable.CreateDefault()).Replace("\"Burp\"", "\"Juggle\"");
            var e = Assert.Throws<System.FormatException>(() => JsonCodec.Deserialize<LevelTable>(json));
            StringAssert.Contains("'Juggle' is not a ActionType", e.Message);
        }
    }
}
