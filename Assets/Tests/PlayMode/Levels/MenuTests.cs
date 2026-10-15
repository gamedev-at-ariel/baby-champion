using System.Collections;
using System.Linq;
using BabyChampion.Game;
using BabyChampion.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace BabyChampion.Tests.PlayMode
{
    /// <summary>The menus (design Section 15.4): title, save slots, baby setup and the level map, into the house and back.</summary>
    public class MenuTests
    {
        MenuScreens _menu;

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

        IEnumerator LoadMenu()
        {
            yield return SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            yield return null;
            _menu = Object.FindAnyObjectByType<MenuScreens>();
            Assert.IsNotNull(_menu, "the Menu scene has no MenuScreens");
        }

        static IEnumerator WaitFor<T>(float seconds = 10f) where T : Object
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Object.FindAnyObjectByType<T>() == null && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<T>(), $"no {typeof(T).Name} after {seconds} s");
        }

        /// <summary>Presses a menu button by its text and lets the panel lay out the next page.</summary>
        IEnumerator Click(string text)
        {
            Assert.IsTrue(_menu.Press(text), $"no enabled button '{text}' on the {_menu.Current} page");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewBaby_LevelMap_Level1InTheHouse_AndBackToTheMap()
        {
            yield return LoadMenu();
            Assert.AreEqual(MenuScreens.Page.Title, _menu.Current);
            yield return Click("Play");
            Assert.AreEqual(MenuScreens.Page.Slots, _menu.Current);
            Assert.AreEqual(SaveStore.SlotCount, _menu.Root.Query<Button>().ToList().Count(b => b.text == "New baby"));

            _menu.CreateBaby(1, "Ari", BabySex.Boy, 3, 2, FeedingChoice.Breast);
            Assert.AreEqual(MenuScreens.Page.Map, _menu.Current);
            Assert.AreEqual("Ari", SaveStore.Load(1).babyName);
            Assert.IsTrue(_menu.LevelTiles[1].enabledSelf);
            Assert.IsFalse(_menu.LevelTiles[2].enabledSelf, "Level 2 opens after Level 1");
            Assert.IsFalse(_menu.LevelTiles[3].enabledSelf, "Level 3 is not built yet");
            Assert.AreEqual(LevelTable.LastLevel, _menu.LevelTiles.Count);

            Assert.IsFalse(_menu.PressLevel(2), "a locked level started");
            Assert.IsTrue(_menu.PressLevel(1));
            yield return WaitFor<HouseGame>();
            var game = Object.FindAnyObjectByType<HouseGame>();
            Assert.AreEqual(1, game.Level?.number);
            Assert.AreEqual("Ari", game.BabyName);
            Assert.AreEqual(FeedingChoice.Breast, game.Feeding);
            Assert.AreEqual(BabyLooks.Outfit(2), game.Baby.OutfitColor);
            Assert.IsTrue(game.WaitingToStart);

            game.GoToMap();
            yield return WaitFor<MenuScreens>();
            _menu = Object.FindAnyObjectByType<MenuScreens>();
            Assert.AreEqual(MenuScreens.Page.Map, _menu.Current, "the house returns to the level map");
        }

        [UnityTest]
        public IEnumerator SavedBabies_AreListed_AndDeletingAsksFirst()
        {
            SaveStore.Save(2, SaveSlot.Create("Maya", BabySex.Girl, 0, 3, FeedingChoice.Both, new Tuning()));
            yield return LoadMenu();
            _menu.Show(MenuScreens.Page.Slots);
            var labels = _menu.Root.Query<Label>().ToList().Select(l => l.text).ToList();
            CollectionAssert.Contains(labels, "Maya");
            Assert.AreEqual(2, _menu.Root.Query<Button>().ToList().Count(b => b.text == "New baby"));

            yield return Click("Delete");
            Assert.IsNotNull(SaveStore.Load(2), "deleted without asking");
            yield return Click("Keep");
            Assert.IsNotNull(SaveStore.Load(2));
            yield return Click("Delete");
            yield return Click("Delete");
            Assert.IsNull(SaveStore.Load(2));
            Assert.AreEqual(3, _menu.Root.Query<Button>().ToList().Count(b => b.text == "New baby"));
        }

        [UnityTest]
        public IEnumerator BabySetup_SavesTheChoices()
        {
            yield return LoadMenu();
            _menu.Show(MenuScreens.Page.Slots);
            yield return Click("New baby");
            Assert.AreEqual(MenuScreens.Page.Setup, _menu.Current);
            _menu.Root.Q<TextField>().value = "  Lior ";
            yield return Click("Boy");
            yield return Click("Formula");
            yield return Click("Start");
            Assert.AreEqual(MenuScreens.Page.Map, _menu.Current);
            var save = SaveStore.Load(0);
            Assert.AreEqual("Lior", save.babyName);
            Assert.AreEqual(BabySex.Boy, save.sex);
            Assert.AreEqual(FeedingChoice.Formula, save.feeding);
            Assert.AreEqual("Lior", GameSession.Slot.babyName);
        }
    }
}
