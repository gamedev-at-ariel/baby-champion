using System;
using System.Collections.Generic;
using BabyChampion.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BabyChampion.Game
{
    /// <summary>
    /// The menus of design Section 15.4, built with UI Toolkit: title, save slots (up to 3 babies),
    /// baby setup (name, sex, appearance, feeding choice) and the level map by chapter. Choosing a
    /// level hands it to the house through <see cref="GameSession"/> and loads the House scene.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuScreens : MonoBehaviour
    {
        public enum Page { Title, Slots, Setup, Map }

        [SerializeField] LevelsAsset levels;
        [SerializeField] TuningAsset tuning;
        [SerializeField] StyleSheet styleSheet;

        static bool _devJumpDone;

        VisualElement _root;
        LevelTable _builtInLevels;
        int _setupSlot;
        string _name = "";
        BabySex _sex;
        int _skin, _outfit;
        FeedingChoice _feeding = FeedingChoice.Both;
        int _confirmDelete = -1;
        readonly List<(Button button, Action action)> _buttons = new List<(Button, Action)>();

        public Page Current { get; private set; }
        public VisualElement Root => _root;
        /// <summary>The level tiles of the map, by level number (tests).</summary>
        public readonly Dictionary<int, Button> LevelTiles = new Dictionary<int, Button>();
        /// <summary>Set false in tests to choose a level without loading the House scene.</summary>
        public static bool LoadScenes = true;
        /// <summary>The level last chosen on the map.</summary>
        public int ChosenLevel { get; private set; }

        LevelTable Levels => levels != null ? levels.table : _builtInLevels ?? (_builtInLevels = LevelTable.CreateDefault());
        Tuning Tuning => tuning != null ? tuning.tuning : new Tuning();

        void OnEnable()
        {
            var document = GetComponent<UIDocument>();
            _root = document.rootVisualElement;
            _root.Clear();
            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet)) _root.styleSheets.Add(styleSheet);
            _root.AddToClassList("menu-root");

            var args = DevBootstrap.Args;
            if (args.Level is int level && !_devJumpDone && Levels.Find(level) != null)
            {
                // Development builds: -level N (or ?level=N) plays that level straight away, without saving.
                _devJumpDone = true;
                GameSession.Clear();
                StartLevel(level);
                return;
            }
            if (GameSession.ReturnToMap && GameSession.Slot != null) Show(Page.Map);
            else Show(args.SkipIntro ? Page.Slots : Page.Title);
            GameSession.ReturnToMap = false;
        }

        // ---------------------------------------------------------------- helpers

        static VisualElement Box(VisualElement parent, string classes)
        {
            var e = new VisualElement();
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) e.AddToClassList(c);
            parent?.Add(e);
            return e;
        }

        static Label Text(VisualElement parent, string classes, string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) label.AddToClassList(c);
            parent?.Add(label);
            return label;
        }

        Button MakeButton(VisualElement parent, string classes, string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) button.AddToClassList(c);
            parent?.Add(button);
            _buttons.Add((button, onClick));
            return button;
        }

        /// <summary>Presses the first enabled button with this text on the current page, as a tap would (tests). False when there is none.</summary>
        public bool Press(string text)
        {
            var entry = _buttons.Find(b => b.button.text == text && b.button.enabledInHierarchy && b.button.panel != null);
            if (entry.button == null) return false;
            entry.action();
            return true;
        }

        /// <summary>Presses a level tile on the map (tests). False when it is locked.</summary>
        public bool PressLevel(int number)
        {
            if (!LevelTiles.TryGetValue(number, out var tile) || !tile.enabledSelf) return false;
            StartLevel(number);
            return true;
        }

        public void Show(Page page)
        {
            Current = page;
            _root.Clear();
            LevelTiles.Clear();
            _buttons.Clear();
            var screen = Box(_root, "menu-screen");
            switch (page)
            {
                case Page.Title: BuildTitle(screen); break;
                case Page.Slots: BuildSlots(screen); break;
                case Page.Setup: BuildSetup(screen); break;
                case Page.Map: BuildMap(screen); break;
            }
        }

        // ---------------------------------------------------------------- title

        void BuildTitle(VisualElement screen)
        {
            var panel = Box(screen, "menu-panel menu-panel--title");
            Text(panel, "menu-logo", "Baby Champion");
            Text(panel, "menu-tagline", "From the first day home to the first day of kindergarten - the laughs, the firsts, the cuddles and the long nights.");
            MakeButton(panel, "menu-button menu-button--primary menu-button--big", "Play", () => Show(Page.Slots));
            Text(panel, "menu-note", "Version " + Application.version);
        }

        // ---------------------------------------------------------------- save slots

        void BuildSlots(VisualElement screen)
        {
            var header = Box(screen, "menu-header");
            MakeButton(header, "menu-button", "Back", () => Show(Page.Title));
            Text(header, "menu-heading", "Choose your baby");
            var row = Box(screen, "slot-row");
            for (int i = 0; i < SaveStore.SlotCount; i++) BuildSlotCard(row, i);
            Text(screen, "menu-note menu-note--center", "Progress is saved automatically at the end of every level.");
        }

        void BuildSlotCard(VisualElement row, int index)
        {
            var card = Box(row, "slot-card");
            Text(card, "slot-number", $"Slot {index + 1}");
            var save = SaveStore.Load(index);
            if (save == null)
            {
                Text(card, "slot-name slot-name--empty", "Empty");
                Text(card, "menu-note", "Start a new baby's story.");
                MakeButton(card, "menu-button menu-button--primary", "New baby", () => BeginSetup(index));
                return;
            }

            var swatch = Box(card, "slot-swatch");
            swatch.style.backgroundColor = BabyLooks.Outfit(save.outfit);
            Text(card, "slot-name", save.babyName);
            int next = save.NextLevel(Levels);
            var level = Levels.Find(next);
            Text(card, "menu-text", level != null ? $"Next: Level {next}, {level.title}" : $"Level {next}");
            Text(card, "menu-text", $"{save.TotalStars} star{(save.TotalStars == 1 ? "" : "s")}  -  money {save.household.money}");
            if (_confirmDelete == index)
            {
                Text(card, "menu-warning", $"Delete {save.babyName}'s save? This can't be undone.");
                var confirm = Box(card, "menu-row");
                MakeButton(confirm, "menu-button menu-button--danger", "Delete", () =>
                {
                    SaveStore.Delete(index);
                    _confirmDelete = -1;
                    Show(Page.Slots);
                });
                MakeButton(confirm, "menu-button", "Keep", () =>
                {
                    _confirmDelete = -1;
                    Show(Page.Slots);
                });
                return;
            }
            var buttons = Box(card, "menu-row");
            MakeButton(buttons, "menu-button menu-button--primary", "Play", () => ChooseSlot(index));
            MakeButton(buttons, "menu-button", "Delete", () =>
            {
                _confirmDelete = index;
                Show(Page.Slots);
            });
        }

        /// <summary>Opens a saved baby on the level map.</summary>
        public void ChooseSlot(int index)
        {
            var save = SaveStore.Load(index);
            if (save == null)
            {
                BeginSetup(index);
                return;
            }
            GameSession.Use(index, save);
            Show(Page.Map);
        }

        // ---------------------------------------------------------------- baby setup

        void BeginSetup(int slot)
        {
            _setupSlot = slot;
            _name = "";
            _sex = BabySex.Girl;
            _skin = 0;
            _outfit = 0;
            _feeding = FeedingChoice.Both;
            Show(Page.Setup);
        }

        void BuildSetup(VisualElement screen)
        {
            var header = Box(screen, "menu-header");
            MakeButton(header, "menu-button", "Back", () => Show(Page.Slots));
            Text(header, "menu-heading", "A new baby!");

            var panel = Box(screen, "menu-panel menu-panel--setup");
            var nameRow = Box(panel, "setup-row");
            Text(nameRow, "setup-label", "Name");
            var field = new TextField { value = _name, maxLength = SaveSlot.MaxNameLength };
            field.AddToClassList("setup-name");
            field.RegisterValueChangedCallback(e => _name = e.newValue);
            nameRow.Add(field);

            var sexRow = Box(panel, "setup-row");
            Text(sexRow, "setup-label", "Baby");
            foreach (BabySex sex in Enum.GetValues(typeof(BabySex)))
                Choice(sexRow, sex == BabySex.Girl ? "Girl" : "Boy", _sex == sex, () => _sex = sex);

            var skinRow = Box(panel, "setup-row");
            Text(skinRow, "setup-label", "Skin");
            for (int i = 0; i < BabyLooks.SkinTones.Length; i++)
            {
                int index = i;
                Swatch(skinRow, BabyLooks.SkinTones[i], _skin == i, () => _skin = index);
            }

            var outfitRow = Box(panel, "setup-row");
            Text(outfitRow, "setup-label", "Outfit");
            for (int i = 0; i < BabyLooks.Outfits.Length; i++)
            {
                int index = i;
                Swatch(outfitRow, BabyLooks.Outfits[i], _outfit == i, () => _outfit = index);
            }

            var feedingRow = Box(panel, "setup-row");
            Text(feedingRow, "setup-label", "Feeding");
            Choice(feedingRow, "Breastfeeding", _feeding == FeedingChoice.Breast, () => _feeding = FeedingChoice.Breast);
            Choice(feedingRow, "Formula", _feeding == FeedingChoice.Formula, () => _feeding = FeedingChoice.Formula);
            Choice(feedingRow, "Both", _feeding == FeedingChoice.Both, () => _feeding = FeedingChoice.Both);
            Text(panel, "menu-note", "Every family feeds differently. This only changes which feeding actions you have in the game.");

            var footer = Box(screen, "menu-row menu-row--end");
            MakeButton(footer, "menu-button menu-button--primary menu-button--big", "Start", () =>
                CreateBaby(_setupSlot, _name, _sex, _skin, _outfit, _feeding));
        }

        void Choice(VisualElement row, string text, bool selected, Action select)
        {
            var button = MakeButton(row, "menu-choice", text, () =>
            {
                select();
                Show(Page.Setup);
            });
            if (selected) button.AddToClassList("menu-choice--selected");
        }

        void Swatch(VisualElement row, Color color, bool selected, Action select)
        {
            var button = MakeButton(row, "menu-swatch", "", () =>
            {
                select();
                Show(Page.Setup);
            });
            button.style.backgroundColor = color;
            if (selected) button.AddToClassList("menu-swatch--selected");
        }

        /// <summary>Creates and saves a new baby in the slot, then opens the level map.</summary>
        public SaveSlot CreateBaby(int slot, string name, BabySex sex, int skin, int outfit, FeedingChoice feeding)
        {
            var save = SaveSlot.Create(name, sex, skin, outfit, feeding, Tuning);
            SaveStore.Save(slot, save);
            GameSession.Use(slot, save);
            Show(Page.Map);
            return save;
        }

        // ---------------------------------------------------------------- level map

        void BuildMap(VisualElement screen)
        {
            var save = GameSession.Slot;
            if (save == null)
            {
                Show(Page.Slots);
                return;
            }
            var header = Box(screen, "menu-header");
            MakeButton(header, "menu-button", "Babies", () => Show(Page.Slots));
            Text(header, "menu-heading", $"{save.babyName}'s first years");
            var total = Box(header, "menu-stars");
            total.Add(new StarIcon(true));
            Text(total, "menu-text", $"{save.TotalStars}");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("map-scroll");
            screen.Add(scroll);
            int next = save.NextLevel(Levels);
            foreach (var chapter in Levels.chapters)
            {
                var box = Box(scroll, "chapter");
                Text(box, "chapter-title", $"Chapter {chapter.number}: {chapter.title}");
                var tiles = Box(box, "chapter-levels");
                for (int n = chapter.firstLevel; n <= chapter.lastLevel; n++) BuildTile(tiles, n, save, n == next);
            }
        }

        void BuildTile(VisualElement parent, int number, SaveSlot save, bool isNext)
        {
            var level = Levels.Find(number);
            bool open = level != null && save.IsUnlocked(number);
            var tile = MakeButton(parent, "level-tile", "", () => StartLevel(number));
            tile.SetEnabled(open);
            if (isNext && open) tile.AddToClassList("level-tile--next");
            Text(tile, "level-number", number.ToString());
            Text(tile, "level-title", level != null ? level.title : "Coming soon");
            if (level != null && !open) Text(tile, "level-state", "Locked");
            else if (open)
            {
                var stars = Box(tile, "level-stars");
                int earned = save.BestStars(number);
                for (int i = 1; i <= 3; i++) stars.Add(new StarIcon(i <= earned));
            }
            LevelTiles[number] = tile;
        }

        /// <summary>Plays a level: hands it to the house and loads the House scene.</summary>
        public void StartLevel(int number)
        {
            ChosenLevel = number;
            GameSession.PendingLevel = number;
            if (LoadScenes) SceneManager.LoadScene(GameSession.HouseScene);
        }
    }
}
