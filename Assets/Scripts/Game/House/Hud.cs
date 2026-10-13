using System.Collections.Generic;
using BabyChampion.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace BabyChampion.Game
{
    /// <summary>
    /// The in-level HUD (design Section 15.3), built with UI Toolkit: baby need icons (shape-coded),
    /// parent bars with Joy as a glow around the portrait, clock, crying budget, money, supplies,
    /// phone (ordering), pause, the current action with Stop, the radial action menu, edge
    /// indicators for off-screen events, and the end-of-day panel.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class Hud : MonoBehaviour
    {
        public enum Need { Hunger, Diaper, Sleep, Tension, Boredom, Burp }

        sealed class NeedWidget
        {
            public VisualElement Root;
            public UrgencyShape Shape;
            public Label Mark;
            public VisualElement Fill;
        }

        sealed class EdgeIndicator
        {
            public VisualElement Root;
            public EdgeArrow Arrow;
        }

        sealed class TimedLine
        {
            public Label Label;
            public float Until;
        }

        static readonly double[] Speeds = { 1, 2, 4, 8 };
        const float RadialRadius = 210f;
        const float RadialButtonWidth = 140f;
        const float RadialButtonHeight = 84f;
        const float DeliveryIndicatorSeconds = 20f;

        [SerializeField] HouseGame game;
        [SerializeField] CameraRig rig;
        [SerializeField] StyleSheet styleSheet;

        VisualElement _root;
        Label _clock, _objective, _speed, _cryBudget;
        readonly Dictionary<Need, NeedWidget> _needs = new Dictionary<Need, NeedWidget>();
        Label _money;
        Button _phoneButton, _pauseButton;
        VisualElement _supplies;
        VisualElement _portrait;
        HudBar _energy, _hunger, _stress;
        Label _actionText;
        VisualElement _actionProgress, _actionProgressFill;
        Button _stopButton;
        VisualElement _ticker, _toasts;
        readonly List<TimedLine> _tickerLines = new List<TimedLine>();
        readonly List<TimedLine> _toastLines = new List<TimedLine>();

        VisualElement _radialLayer;
        readonly List<Button> _radialButtons = new List<Button>();
        readonly List<Label> _radialReasons = new List<Label>();
        readonly List<HouseAction> _radialActions = new List<HouseAction>();

        VisualElement _phoneLayer, _phoneList;
        VisualElement _pauseLayer, _pauseBody;
        VisualElement _outcomeLayer;
        Label _outcomeTitle, _outcomeText;
        EdgeIndicator _babyIndicator, _doorIndicator;
        VisualElement _tags;
        Label _babyTag, _parentTag;
        readonly List<(Interactable target, Label label, Vector3 offset)> _furnitureTags = new List<(Interactable, Label, Vector3)>();

        public HouseGame Game => game;
        public bool RadialOpen => _radialLayer != null && _radialLayer.style.display == DisplayStyle.Flex;
        public string RadialTitle { get; private set; }
        public IReadOnlyList<HouseAction> RadialActions => _radialActions;
        public IReadOnlyList<Button> RadialButtons => _radialButtons;
        public bool PhoneOpen => _phoneLayer != null && _phoneLayer.style.display == DisplayStyle.Flex;
        public bool PauseMenuOpen => _pauseLayer != null && _pauseLayer.style.display == DisplayStyle.Flex;
        public bool OutcomeShown => _outcomeLayer != null && _outcomeLayer.style.display == DisplayStyle.Flex;
        public string ClockText => _clock?.text;
        public bool BabyIndicatorVisible => _babyIndicator != null && _babyIndicator.Root.style.display == DisplayStyle.Flex;
        public bool DoorIndicatorVisible => _doorIndicator != null && _doorIndicator.Root.style.display == DisplayStyle.Flex;
        public Button PhoneButton => _phoneButton;
        public Button PauseButton => _pauseButton;
        public Button StopButton => _stopButton;
        public VisualElement Root => _root;

        public Urgency NeedUrgency(Need need) => _needs[need].Shape.Urgency;
        public bool NeedVisible(Need need) => _needs[need].Root.style.display == DisplayStyle.Flex;

        void OnEnable()
        {
            var document = GetComponent<UIDocument>();
            Build(document.rootVisualElement);
            if (game != null)
            {
                game.RadialRequested += OpenRadial;
                game.Notice += Toast;
                game.SimEventRaised += OnSimEvent;
                game.DayStarted += OnDayStarted;
            }
        }

        void OnDisable()
        {
            if (game == null) return;
            game.RadialRequested -= OpenRadial;
            game.Notice -= Toast;
            game.SimEventRaised -= OnSimEvent;
            game.DayStarted -= OnDayStarted;
        }

        // ---------------------------------------------------------------- building

        static VisualElement Box(VisualElement parent, string classes, bool pickable = false)
        {
            var e = new VisualElement { pickingMode = pickable ? PickingMode.Position : PickingMode.Ignore };
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) e.AddToClassList(c);
            parent?.Add(e);
            return e;
        }

        static Label Text(VisualElement parent, string classes, string text = "")
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) label.AddToClassList(c);
            parent?.Add(label);
            return label;
        }

        static Button MakeButton(VisualElement parent, string classes, string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) button.AddToClassList(c);
            parent?.Add(button);
            return button;
        }

        void Build(VisualElement documentRoot)
        {
            documentRoot.Clear();
            documentRoot.pickingMode = PickingMode.Ignore;
            if (styleSheet != null && !documentRoot.styleSheets.Contains(styleSheet)) documentRoot.styleSheets.Add(styleSheet);
            _root = Box(documentRoot, "hud");
            // Name tags first, so every other HUD element draws over them.
            _tags = Box(_root, "tags");
            _furnitureTags.Clear();
            _babyTag = Text(_tags, "tag tag--baby", "Baby");
            _parentTag = Text(_tags, "tag tag--you", "You");

            var clockPanel = Box(_root, "panel clock-panel", pickable: true);
            _clock = Text(clockPanel, "clock");
            _objective = Text(clockPanel, "objective", "Free day: get through to 07:00");
            _cryBudget = Text(clockPanel, "objective");
            _speed = Text(clockPanel, "speed");

            var needs = Box(_root, "needs");
            AddNeed(needs, Need.Hunger, "Hungry");
            AddNeed(needs, Need.Diaper, "Diaper");
            AddNeed(needs, Need.Sleep, "Sleepy");
            AddNeed(needs, Need.Tension, "Tense");
            AddNeed(needs, Need.Boredom, "Bored");
            AddNeed(needs, Need.Burp, "Burp");

            var topRight = Box(_root, "top-right");
            var buttons = Box(topRight, "row");
            _money = Text(buttons, "money");
            _phoneButton = MakeButton(buttons, "icon-button", "Phone", OpenPhone);
            _pauseButton = MakeButton(buttons, "icon-button", "Pause", () => SetPaused(true));
            _supplies = Box(topRight, "panel supplies", pickable: true);

            var parentPanel = Box(_root, "panel parent-panel", pickable: true);
            _portrait = Box(parentPanel, "portrait");
            Text(_portrait, "portrait-label", "You");
            var bars = Box(parentPanel, "bars");
            _energy = new HudBar(bars, "Energy");
            _hunger = new HudBar(bars, "Hunger");
            _stress = new HudBar(bars, "Stress");

            var actionPanel = Box(_root, "panel action-panel", pickable: true);
            _actionText = Text(actionPanel, "action-text");
            _actionProgress = Box(actionPanel, "action-progress");
            _actionProgressFill = Box(_actionProgress, "action-progress-fill");
            _stopButton = MakeButton(actionPanel, "stop-button", "Stop", () => game.Perform(HouseAction.Sim(ParentCommand.Of(ActionType.Stop))));

            _ticker = Box(_root, "ticker");
            _toasts = Box(_root, "toasts");

            _babyIndicator = AddEdgeIndicator("Crying!", UrgencyShape.UrgentColor, "edge--crying", OpenBabyRadialFromIndicator);
            _doorIndicator = AddEdgeIndicator("Delivery", new Color(0.2f, 0.55f, 0.3f), "edge--delivery", null);

            _radialLayer = Box(_root, "radial-layer", pickable: true);
            _radialLayer.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == _radialLayer) CloseRadial();
            });
            Hide(_radialLayer);

            BuildPhone();
            BuildPause();
            BuildOutcome();
        }

        void AddNeed(VisualElement parent, Need need, string name)
        {
            var root = Box(parent, "need");
            var shape = new UrgencyShape();
            shape.AddToClassList("need-shape");
            root.Add(shape);
            var mark = Text(shape, "need-mark");
            Text(root, "need-name", name);
            var bar = Box(root, "need-bar");
            var fill = Box(bar, "need-bar-fill");
            _needs[need] = new NeedWidget { Root = root, Shape = shape, Mark = mark, Fill = fill };
        }

        EdgeIndicator AddEdgeIndicator(string text, Color color, string modifier, System.Action onClick)
        {
            var root = Box(_root, "edge " + modifier, pickable: onClick != null);
            var arrow = new EdgeArrow(color);
            root.Add(arrow);
            Text(root, "edge-label", text);
            if (onClick != null) root.RegisterCallback<ClickEvent>(_ => onClick());
            Hide(root);
            return new EdgeIndicator { Root = root, Arrow = arrow };
        }

        void BuildPhone()
        {
            _phoneLayer = Box(_root, "modal-layer", pickable: true);
            var modal = Box(_phoneLayer, "modal", pickable: true);
            Text(modal, "modal-title", "Phone: order supplies");
            Text(modal, "modal-text", "Normal delivery takes 2 hours; express takes 30 minutes and costs extra.");
            _phoneList = Box(modal, "");
            MakeButton(modal, "modal-button", "Close", ClosePhone);
            Hide(_phoneLayer);
        }

        void BuildPause()
        {
            _pauseLayer = Box(_root, "modal-layer", pickable: true);
            var modal = Box(_pauseLayer, "modal", pickable: true);
            Text(modal, "modal-title", "Paused");
            _pauseBody = Box(modal, "");
            Hide(_pauseLayer);
        }

        void BuildOutcome()
        {
            _outcomeLayer = Box(_root, "modal-layer", pickable: true);
            var modal = Box(_outcomeLayer, "modal", pickable: true);
            _outcomeTitle = Text(modal, "modal-title");
            _outcomeText = Text(modal, "modal-text");
            var row = Box(modal, "row");
            MakeButton(row, "modal-button", "Same day again", () => game.Restart(game.Seed));
            MakeButton(row, "modal-button", "New day", () => game.Restart(System.Environment.TickCount));
            Hide(_outcomeLayer);
        }

        static void Show(VisualElement e) => e.style.display = DisplayStyle.Flex;
        static void Hide(VisualElement e) => e.style.display = DisplayStyle.None;
        static void SetShown(VisualElement e, bool shown) => e.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        // ---------------------------------------------------------------- coordinates

        /// <summary>Height of what the panel draws to: the screen, or the target texture while capturing screenshots.</summary>
        float ScreenHeight
        {
            get
            {
                var target = GetComponent<UIDocument>().panelSettings?.targetTexture;
                return target != null ? target.height : Screen.height;
            }
        }

        /// <summary>True when the screen point (origin bottom-left, as the Input System reports it) is on a HUD element.</summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            var panel = _root?.panel;
            if (panel == null) return false;
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, ScreenHeight - screenPosition.y));
            var picked = panel.Pick(panelPosition);
            return picked != null && picked != _root && picked != _root.parent;
        }

        /// <summary>Panel position of a screen point (origin bottom-left).</summary>
        public Vector2 ScreenToPanel(Vector2 screenPosition)
        {
            var panel = _root?.panel;
            return panel == null ? screenPosition : RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, ScreenHeight - screenPosition.y));
        }

        /// <summary>Screen point (origin bottom-left) of the centre of an element, for simulated input in tests.</summary>
        public Vector2 ElementScreenCenter(VisualElement element)
        {
            var center = element.worldBound.center;
            var size = _root.worldBound.size;
            return new Vector2(center.x / size.x * Screen.width, Screen.height - center.y / size.y * Screen.height);
        }

        // ---------------------------------------------------------------- radial menu

        public void OpenRadial(RadialRequest request)
        {
            CloseRadial();
            ClosePhone();
            RadialTitle = request.Title;
            _radialActions.AddRange(request.Actions);
            var size = _root.worldBound.size;
            if (float.IsNaN(size.x) || size.x < 10) size = new Vector2(1280, 720);
            var center = ScreenToPanel(request.ScreenPosition);
            float reachX = RadialRadius + RadialButtonWidth / 2 + 4f, reachY = RadialRadius + RadialButtonHeight / 2 + 4f;
            center.x = Mathf.Clamp(center.x, Mathf.Min(reachX, size.x / 2), Mathf.Max(size.x - reachX, size.x / 2));
            center.y = Mathf.Clamp(center.y, Mathf.Min(reachY, size.y / 2), Mathf.Max(size.y - reachY, size.y / 2));

            var title = Text(_radialLayer, "radial-title", request.Title);
            title.style.left = center.x - 70f;
            title.style.top = center.y - 22f;

            int count = _radialActions.Count;
            for (int i = 0; i < count; i++)
            {
                var action = _radialActions[i];
                float angle = -Mathf.PI / 2 + i * 2 * Mathf.PI / count;
                var button = new Button(() => ChooseRadial(action));
                button.AddToClassList("radial-button");
                Text(button, "radial-label", action.Label);
                var reason = Text(button, "radial-reason");
                button.style.left = center.x + Mathf.Cos(angle) * RadialRadius - RadialButtonWidth / 2;
                button.style.top = center.y + Mathf.Sin(angle) * RadialRadius - RadialButtonHeight / 2;
                _radialLayer.Add(button);
                _radialButtons.Add(button);
                _radialReasons.Add(reason);
            }
            RefreshRadial();
            Show(_radialLayer);
        }

        void ChooseRadial(HouseAction action)
        {
            CloseRadial();
            game.Perform(action);
        }

        /// <summary>Presses a radial entry as if tapped (tests).</summary>
        public void PressRadial(int index)
        {
            if (index < 0 || index >= _radialActions.Count || !_radialButtons[index].enabledSelf) return;
            ChooseRadial(_radialActions[index]);
        }

        public int RadialIndexOf(string label) => _radialActions.FindIndex(a => a.Label == label);

        public void CloseRadial()
        {
            if (_radialLayer == null) return;
            _radialLayer.Clear();
            _radialButtons.Clear();
            _radialReasons.Clear();
            _radialActions.Clear();
            RadialTitle = null;
            Hide(_radialLayer);
        }

        void RefreshRadial()
        {
            for (int i = 0; i < _radialActions.Count; i++)
            {
                var check = game.Check(_radialActions[i]);
                _radialButtons[i].SetEnabled(check.Allowed);
                _radialReasons[i].text = check.Allowed ? "" : check.Reason;
            }
        }

        void OpenBabyRadialFromIndicator()
        {
            var center = _babyIndicator.Root.worldBound.center;
            var size = _root.worldBound.size;
            var screen = new Vector2(center.x / size.x * Screen.width, Screen.height - center.y / size.y * Screen.height);
            OpenRadial(new RadialRequest { Title = "Baby", Actions = game.ActionsForBaby(), ScreenPosition = screen });
        }

        // ---------------------------------------------------------------- phone

        public void OpenPhone()
        {
            CloseRadial();
            RebuildPhone();
            Show(_phoneLayer);
        }

        public void ClosePhone()
        {
            if (_phoneLayer != null) Hide(_phoneLayer);
        }

        /// <summary>The order buttons, in price-list order: normal then express for each item (tests).</summary>
        public readonly List<(ParentCommand command, Button button)> OrderButtons = new List<(ParentCommand, Button)>();

        void RebuildPhone()
        {
            _phoneList.Clear();
            OrderButtons.Clear();
            var sim = game.Sim;
            var prices = sim.Config.prices;
            foreach (var price in prices.items)
            {
                if (price.item == SupplyType.BabyFood && sim.Month < 6) continue;
                if (price.item == SupplyType.Formula && sim.Feeding == FeedingChoice.Breast) continue;
                var row = Box(_phoneList, "order-row");
                Text(row, "order-name", $"{SupplyName(price.item)} ({sim.Supplies.Get(price.item)})");
                AddOrderButton(row, ParentCommand.Order(price.item), $"+{price.packSize}  for {price.price}");
                AddOrderButton(row, ParentCommand.Order(price.item, express: true), $"Express  {price.price + prices.expressFee}");
            }
        }

        void AddOrderButton(VisualElement row, ParentCommand command, string text)
        {
            var button = MakeButton(row, "modal-button", text, () =>
            {
                game.Perform(HouseAction.Sim(command));
                Toast($"Ordered {command.packs}x {SupplyName(command.item).ToLowerInvariant()}");
            });
            OrderButtons.Add((command, button));
        }

        static string SupplyName(SupplyType item) => item == SupplyType.AdultFood ? "Food (you)" : item == SupplyType.BabyFood ? "Baby food" : item.ToString();

        // ---------------------------------------------------------------- pause

        public void SetPaused(bool paused)
        {
            if (game.Sim == null || game.Sim.Outcome != Outcome.Running) paused = false;
            game.Paused = paused;
            if (paused) RebuildPause();
        }

        void RebuildPause()
        {
            CloseRadial();
            _pauseBody.Clear();
            MakeButton(_pauseBody, "modal-button", "Resume", () => SetPaused(false));

            Text(_pauseBody, "modal-text", "Developer options (until levels arrive in M3):");
            var speedRow = Box(_pauseBody, "row");
            Text(speedRow, "modal-text", "In-game minutes per second:");
            foreach (double speed in Speeds)
            {
                bool selected = System.Math.Abs(game.Sim.MinutesPerRealSecond - speed) < 1e-6;
                var b = MakeButton(speedRow, "modal-button", speed.ToString(), () =>
                {
                    game.Sim.MinutesPerRealSecond = speed;
                    RebuildPause();
                });
                b.style.minWidth = 60;
                if (selected) b.AddToClassList("modal-button--selected");
            }

            var dayRow = Box(_pauseBody, "row");
            MakeButton(dayRow, "modal-button", "Month -", () => RestartWith(game.Month - 1, game.Feeding));
            Text(dayRow, "modal-text", $"Month {game.Month}");
            MakeButton(dayRow, "modal-button", "Month +", () => RestartWith(game.Month + 1, game.Feeding));
            MakeButton(dayRow, "modal-button", $"Feeding: {game.Feeding}", () => RestartWith(game.Month, (FeedingChoice)(((int)game.Feeding + 1) % 3)));

            var restartRow = Box(_pauseBody, "row");
            MakeButton(restartRow, "modal-button", "Same day again", () => RestartWith(game.Month, game.Feeding, game.Seed));
            MakeButton(restartRow, "modal-button", "New day", () => RestartWith(game.Month, game.Feeding));
            Text(_pauseBody, "modal-text", $"Seed {game.Seed}.  Space pauses; Q/E or right-drag rotate; scroll zooms.");
        }

        void RestartWith(int month, FeedingChoice feeding, int? seed = null)
        {
            game.Restart(seed ?? System.Environment.TickCount, month, feeding);
        }

        // ---------------------------------------------------------------- messages

        void OnDayStarted()
        {
            CloseRadial();
            ClosePhone();
            _ticker?.Clear();
            _tickerLines.Clear();
        }

        void OnSimEvent(SimEvent e)
        {
            if (_ticker == null) return;
            var label = Text(_ticker, "ticker-line", e.ToString());
            _tickerLines.Add(new TimedLine { Label = label, Until = Time.unscaledTime + 10f });
            while (_tickerLines.Count > 4)
            {
                _tickerLines[0].Label.RemoveFromHierarchy();
                _tickerLines.RemoveAt(0);
            }
        }

        public void Toast(string text)
        {
            if (_toasts == null || string.IsNullOrEmpty(text)) return;
            var label = Text(_toasts, "toast", text);
            _toastLines.Add(new TimedLine { Label = label, Until = Time.unscaledTime + 3.5f });
            while (_toastLines.Count > 3)
            {
                _toastLines[0].Label.RemoveFromHierarchy();
                _toastLines.RemoveAt(0);
            }
        }

        static void Expire(List<TimedLine> lines)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > lines[i].Until)
                {
                    lines[i].Label.RemoveFromHierarchy();
                    lines.RemoveAt(i);
                }
        }

        // ---------------------------------------------------------------- refresh

        void Update()
        {
            if (game == null || game.Sim == null || _root == null) return;
            var sim = game.Sim;
            var t = sim.Config.tuning;

            _clock.text = sim.ClockText;
            _cryBudget.text = $"Crying today: {sim.CryingMinutesToday:0} of {t.cryingBudgetMinutes:0} min";
            _speed.text = sim.TimeMultiplier > 1 ? $"Sleeping: time x{sim.TimeMultiplier:0}" : "";

            UpdateNeeds(sim, t);
            UpdateResources(sim, t);
            UpdateParent(sim, t);
            UpdateAction(sim);
            UpdateIndicators(sim);
            UpdateTags(sim);

            if (RadialOpen) RefreshRadial();
            if (PhoneOpen && Time.frameCount % 15 == 0) UpdatePhoneLabels(sim);
            if (game.Paused && !PauseMenuOpen) RebuildPause();
            SetShown(_pauseLayer, game.Paused);
            if (sim.Outcome != Outcome.Running && !OutcomeShown) ShowOutcome(sim);
            else if (sim.Outcome == Outcome.Running && OutcomeShown) Hide(_outcomeLayer);

            Expire(_tickerLines);
            Expire(_toastLines);
        }

        void UpdateNeeds(CareSimulation sim, Tuning t)
        {
            var b = sim.Baby;
            var causes = b.Crying ? b.CryCauses : CryCause.None;
            SetNeed(Need.Hunger, b.Hunger, t.hungerCryThreshold * 0.7, t.hungerCryThreshold, (causes & CryCause.Hunger) != 0);
            double diaper = b.Diaper == DiaperState.Clean ? 0 : b.Diaper == DiaperState.Wet ? 50 : 100;
            SetNeed(Need.Diaper, diaper, 1, 1000, (causes & CryCause.Diaper) != 0);
            if (b.Asleep)
            {
                var w = _needs[Need.Sleep];
                w.Shape.Set(Urgency.Calm, UrgencyShape.AsleepColor);
                w.Mark.text = "z";
                w.Fill.style.width = Length.Percent((float)b.Tiredness);
            }
            else SetNeed(Need.Sleep, b.Tiredness, t.overtiredThreshold * 0.7, t.overtiredThreshold, (causes & CryCause.Overtired) != 0);
            SetNeed(Need.Tension, b.Tension, t.tensionBlocksSleep, t.tensionCryThreshold, (causes & CryCause.Upset) != 0);

            bool boredomShown = sim.Month >= t.boredomFromMonth;
            SetShown(_needs[Need.Boredom].Root, boredomShown);
            if (boredomShown) SetNeed(Need.Boredom, b.Boredom, t.boredomCryThreshold * 0.7, t.boredomCryThreshold, (causes & CryCause.Bored) != 0);

            bool burpShown = b.NeedsBurp || b.Gas;
            SetShown(_needs[Need.Burp].Root, burpShown);
            if (burpShown) SetNeed(Need.Burp, b.Gas ? 100 : 60, 50, 100, (causes & CryCause.Gas) != 0);
        }

        void SetNeed(Need need, double value, double rising, double urgent, bool crying)
        {
            var w = _needs[need];
            var urgency = crying || value >= urgent ? Urgency.Urgent : value >= rising ? Urgency.Rising : Urgency.Calm;
            var color = urgency == Urgency.Urgent ? UrgencyShape.UrgentColor : urgency == Urgency.Rising ? UrgencyShape.RisingColor : UrgencyShape.CalmColor;
            w.Shape.Set(urgency, color);
            w.Mark.text = urgency == Urgency.Urgent ? "!!" : urgency == Urgency.Rising ? "!" : "";
            w.Fill.style.width = Length.Percent(Mathf.Clamp((float)value, 0f, 100f));
        }

        void UpdateResources(CareSimulation sim, Tuning t)
        {
            _money.text = $"Money {sim.Money}";
            if (Time.frameCount % 10 != 0 && _supplies.childCount > 0) return;
            _supplies.Clear();
            var s = sim.Supplies;
            AddSupply("Diapers", s.Diapers, t.lowSupplyCount);
            AddSupply("Wipes", s.Wipes, t.lowSupplyCount);
            if (sim.Feeding != FeedingChoice.Breast)
            {
                AddSupply("Formula", s.Formula, t.lowSupplyCount);
                AddSupply("Bottles ready", s.BottlesReady, 0);
            }
            if (sim.Month >= 6) AddSupply("Baby food", s.BabyFood, t.lowSupplyCount);
            AddSupply("Food (you)", s.AdultFood, 1);
            foreach (var d in sim.PendingDeliveries)
                Text(_supplies, "delivery", $"Coming {CareSimulation.FormatClock(d.ArrivesAtMinute, t.dayStartHour)}: {d.Quantity} {SupplyName(d.Item).ToLowerInvariant()}");
        }

        void AddSupply(string name, int count, int low)
        {
            var label = Text(_supplies, "supply", $"{name}: {count}");
            if (count < low || (low == 0 && count == 0 && name != "Bottles ready")) label.AddToClassList("supply--low");
        }

        void UpdatePhoneLabels(CareSimulation sim)
        {
            foreach (var (command, button) in OrderButtons)
            {
                var check = game.Check(HouseAction.Sim(command));
                button.SetEnabled(check.Allowed);
            }
        }

        void UpdateParent(CareSimulation sim, Tuning t)
        {
            var p = sim.Parent;
            _energy.Set(p.Energy, t.lowEnergyThreshold, highIsBad: false);
            _hunger.Set(p.Hunger, t.parentHungerThreshold, highIsBad: true);
            _stress.Set(p.Stress, t.stressBlocksSleep, highIsBad: true);
            float joy = Mathf.Clamp01((float)p.Joy / 100f);
            var glow = new Color(1f, 0.78f, 0.2f, 0.15f + joy * 0.85f);
            _portrait.style.borderTopColor = glow;
            _portrait.style.borderBottomColor = glow;
            _portrait.style.borderLeftColor = glow;
            _portrait.style.borderRightColor = glow;
            float width = 2f + joy * 7f;
            _portrait.style.borderTopWidth = width;
            _portrait.style.borderBottomWidth = width;
            _portrait.style.borderLeftWidth = width;
            _portrait.style.borderRightWidth = width;
        }

        void UpdateAction(CareSimulation sim)
        {
            var p = sim.Parent;
            string holding = game.BabyCarried ? "  (holding the baby)" : "";
            if (game.PlanActive && game.PlanLabel != null)
            {
                _actionText.text = $"Going to: {game.PlanLabel}{holding}";
                Hide(_actionProgress);
            }
            else if (game.Parent != null && game.Parent.Walking)
            {
                _actionText.text = "Walking" + holding;
                Hide(_actionProgress);
            }
            else if (p.Busy)
            {
                string label = ActionCatalog.For(p.Action).Label;
                if (p.ActionIsContinuous)
                {
                    _actionText.text = $"{label} ({p.ActionElapsedMinutes:0} min){holding}";
                    Hide(_actionProgress);
                }
                else
                {
                    _actionText.text = label + holding;
                    Show(_actionProgress);
                    _actionProgressFill.style.width = Length.Percent((float)p.ActionProgress * 100f);
                }
            }
            else
            {
                _actionText.text = "Tap the baby or furniture to act" + holding;
                Hide(_actionProgress);
            }
            _stopButton.SetEnabled(p.Busy || game.PlanActive);
        }

        void UpdateIndicators(CareSimulation sim)
        {
            var camera = rig != null ? rig.Camera : Camera.main;
            bool babyCrying = sim.Baby.Crying && game.Baby != null;
            PlaceIndicator(_babyIndicator, babyCrying, babyCrying ? game.Baby.transform.position + Vector3.up * 0.4f : Vector3.zero, camera);
            bool delivery = game.FrontDoor != null && Time.unscaledTime - game.LastDeliveryTime < DeliveryIndicatorSeconds;
            PlaceIndicator(_doorIndicator, delivery, delivery ? game.FrontDoor.transform.position : Vector3.zero, camera);
        }

        /// <summary>Name tags over the furniture, the baby and the parent, so the greybox reads.</summary>
        void UpdateTags(CareSimulation sim)
        {
            var camera = rig != null ? rig.Camera : Camera.main;
            var panel = _root.panel;
            if (camera == null || panel == null) return;
            if (_furnitureTags.Count == 0 && game.Interactables.Length > 0)
                foreach (var target in game.Interactables)
                {
                    var label = Text(_tags, "tag", target.DisplayName);
                    _furnitureTags.Insert(0, (target, label, Vector3.up * (TopOf(target) + 0.25f)));
                    label.SendToBack();
                }
            foreach (var (target, label, offset) in _furnitureTags)
            {
                // The baby's own tag replaces the tag of the spot it is in.
                if (!game.BabyCarried && game.BabySpot == target) Hide(label);
                else PlaceTag(label, target.transform.position + offset, camera, panel);
            }

            if (game.Baby != null)
            {
                bool crying = sim.Baby.Crying;
                _babyTag.text = crying ? "Baby: crying!" : sim.Baby.Asleep ? "Baby (asleep)" : "Baby";
                _babyTag.EnableInClassList("tag--crying", crying);
                PlaceTag(_babyTag, game.Baby.transform.position + Vector3.up * 0.95f, camera, panel);
            }
            if (game.Parent != null) PlaceTag(_parentTag, game.Parent.transform.position + Vector3.up * 2.15f, camera, panel);
        }

        static float TopOf(Interactable target)
        {
            float top = 0f;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, r.bounds.max.y - target.transform.position.y);
            return top;
        }

        static void PlaceTag(Label label, Vector3 world, Camera camera, IPanel panel)
        {
            var v = camera.WorldToViewportPoint(world);
            if (v.z <= 0 || v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f)
            {
                Hide(label);
                return;
            }
            // Viewport rather than screen coordinates: correct also while rendering into a screenshot texture.
            var size = label.parent.worldBound.size;
            label.style.left = v.x * size.x;
            label.style.top = (1f - v.y) * size.y;
            Show(label);
        }

        /// <summary>Shows the indicator at the screen edge when its target is off screen.</summary>
        void PlaceIndicator(EdgeIndicator indicator, bool active, Vector3 world, Camera camera)
        {
            if (!active || camera == null)
            {
                Hide(indicator.Root);
                return;
            }
            var v = camera.WorldToViewportPoint(world);
            if (v.z < 0)
            {
                v.x = 1f - v.x;
                v.y = 1f - v.y;
            }
            bool onScreen = v.z > 0 && v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f;
            if (onScreen)
            {
                Hide(indicator.Root);
                return;
            }
            var d = new Vector2(v.x - 0.5f, v.y - 0.5f);
            if (d.sqrMagnitude < 1e-6f) d = Vector2.down;
            float scale = Mathf.Max(Mathf.Abs(d.x) / 0.44f, Mathf.Abs(d.y) / 0.40f);
            var edge = new Vector2(0.5f, 0.5f) + d / scale;
            var size = _root.worldBound.size;
            indicator.Root.style.left = edge.x * size.x;
            indicator.Root.style.top = (1f - edge.y) * size.y;
            indicator.Arrow.style.rotate = new Rotate(Angle.Degrees(Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg));
            Show(indicator.Root);
        }

        void ShowOutcome(CareSimulation sim)
        {
            CloseRadial();
            ClosePhone();
            var stats = sim.CopyStats();
            _outcomeTitle.text = sim.Outcome == Outcome.Survived ? "Morning! You made it through the day." : FailText(sim.FailReason);
            _outcomeText.text =
                $"Ended at {sim.ClockText}.\n" +
                $"Crying {stats.cryingMinutes:0} min (longest {stats.longestCryMinutes:0}), average wellbeing {stats.AverageWellbeing:0}.\n" +
                $"Feeds {stats.feeds}, diapers {stats.diaperChanges}, naps {stats.babyNaps}, your sleep {stats.parentSleepMinutes / 60:0.0} h, lowest energy {stats.minEnergy:0}.";
            Show(_outcomeLayer);
        }

        static string FailText(FailReason reason)
        {
            switch (reason)
            {
                case FailReason.ParentCollapsed: return "You collapsed from exhaustion. Grandma is on her way!";
                case FailReason.ContinuousCrying: return "The baby cried for too long without a break.";
                case FailReason.CryingBudget: return "Too much crying today.";
                default: return "The day is over.";
            }
        }
    }
}
