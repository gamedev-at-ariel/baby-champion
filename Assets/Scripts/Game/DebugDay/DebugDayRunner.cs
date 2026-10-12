using System.Collections.Generic;
using BabyChampion.Simulation;
using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>
    /// M1 debug scene: runs one in-game day of the simulation with text, bars and a button per
    /// action. IMGUI on purpose: it is throwaway tooling, and the real UI technology is chosen in M2.
    /// The player is a <see cref="QueuedController"/>, so buttons go through the same
    /// parent-controller interface as the bots.
    /// </summary>
    public sealed class DebugDayRunner : MonoBehaviour
    {
        const float RefWidth = 1280f;
        const float RefHeight = 720f;
        static readonly double[] Speeds = { 1, 2, 4, 8 };

        [SerializeField] AgeRatesAsset ageRates;
        [SerializeField] PricesAsset prices;
        [SerializeField] TuningAsset tuning;
        [SerializeField] int month;
        [SerializeField] FeedingChoice feeding = FeedingChoice.Both;

        GUIStyle _label, _small, _header, _button, _big;
        double _minutesPerSecond;

        public CareSimulation Sim { get; private set; }
        public QueuedController Player { get; private set; }
        public int Seed { get; private set; }

        void Awake()
        {
            var args = DevBootstrap.Args;
            if (args.Level is int level) month = Mathf.Clamp(level - 1, 0, ConfigValidator.LastMonth);
            Restart(args.Seed ?? System.Environment.TickCount);
        }

        public void Restart(int seed)
        {
            Seed = seed;
            var config = SimData.ToConfig(ageRates, prices, tuning);
            double speed = Sim != null ? Sim.MinutesPerRealSecond : config.tuning.minutesPerRealSecond;
            Sim = CareSimulation.NewDay(config, month, feeding, seed);
            Sim.MinutesPerRealSecond = speed;
            Player = new QueuedController();
            Sim.AttachController(Player);
            Debug.Log($"[DebugDay] new day: month {month}, feeding {feeding}, seed {seed}");
        }

        void Update() => Sim.Advance(Time.deltaTime);

        // ---------------------------------------------------------------- drawing

        void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;

            DrawTopBar(width);
            DrawBaby(new Rect(10, 60, 400, 330));
            DrawParent(new Rect(430, 60, 400, 330));
            DrawSupplies(new Rect(850, 60, width - 860, 330));
            DrawActions(new Rect(10, 400, 820, 310));
            DrawLog(new Rect(850, 400, width - 860, 310));
            if (Sim.Outcome != Outcome.Running) DrawOutcome(width);
        }

        void DrawTopBar(float width)
        {
            GUILayout.BeginArea(new Rect(10, 8, width - 20, 44));
            GUILayout.BeginHorizontal();
            string clock = $"{Sim.ClockText}  ({Sim.DayProgress:P0} of the day)";
            if (Sim.TimeMultiplier > 1) clock += $"  x{Sim.TimeMultiplier} while you sleep";
            GUILayout.Label(clock, _header, GUILayout.Width(420));

            if (GUILayout.Button(Sim.Paused ? "Resume" : "Pause", _button, GUILayout.Width(90))) Sim.Paused = !Sim.Paused;
            GUILayout.Label("min/s:", _label, GUILayout.Width(52));
            foreach (double speed in Speeds)
            {
                bool selected = System.Math.Abs(Sim.MinutesPerRealSecond - speed) < 1e-6;
                if (GUILayout.Toggle(selected, speed.ToString(), _button, GUILayout.Width(36)) && !selected) Sim.MinutesPerRealSecond = speed;
            }
            GUILayout.Space(16);
            if (GUILayout.Button("-", _button, GUILayout.Width(28)) && month > 0) { month--; Restart(Seed); }
            GUILayout.Label($"Month {month} ({Sim.AgeBand.name})", _label, GUILayout.Width(150));
            if (GUILayout.Button("+", _button, GUILayout.Width(28)) && month < ConfigValidator.LastMonth) { month++; Restart(Seed); }
            if (GUILayout.Button(feeding.ToString(), _button, GUILayout.Width(80)))
            {
                feeding = (FeedingChoice)(((int)feeding + 1) % 3);
                Restart(Seed);
            }
            if (GUILayout.Button("New day", _button, GUILayout.Width(90))) Restart(System.Environment.TickCount);
            GUILayout.Label($"seed {Seed}", _small);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawBaby(Rect area)
        {
            var b = Sim.Baby;
            var t = Sim.Config.tuning;
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(Pad(area));
            GUILayout.Label(b.Asleep ? "BABY - asleep" : "BABY - awake", _header);
            Bar("Hunger", b.Hunger, t.hungerCryThreshold, highIsBad: true);
            Bar("Tiredness", b.Tiredness, t.overtiredThreshold, highIsBad: true);
            Bar("Tension", b.Tension, t.tensionBlocksSleep, highIsBad: true);
            if (Sim.Month >= t.boredomFromMonth) Bar("Boredom", b.Boredom, t.boredomCryThreshold, highIsBad: true);
            Bar("Wellbeing", b.Wellbeing, 70, highIsBad: false);

            string diaper = b.Diaper == DiaperState.Clean ? "clean" : $"{b.Diaper.ToString().ToLower()} for {b.SoiledMinutes:0} min";
            var flags = new List<string>();
            if (b.NeedsBurp) flags.Add("needs a burp");
            if (b.Gas) flags.Add("GAS");
            if (b.Rash) flags.Add("RASH");
            GUILayout.Label($"Diaper: {diaper}{(flags.Count > 0 ? "   |   " + string.Join(", ", flags) : "")}", _label);

            var crying = new GUIStyle(_header) { wordWrap = true, normal = { textColor = b.Crying ? new Color(1f, 0.45f, 0.4f) : new Color(0.6f, 0.9f, 0.6f) } };
            GUILayout.Label(b.Crying ? $"CRYING: {CryHints.Describe(b.CryCauses)}, {b.CryStreakMinutes:0} min" : "Calm", crying);
            var stats = Sim.CopyStats();
            GUILayout.Label($"Crying today {stats.cryingMinutes:0} / {t.cryingBudgetMinutes:0} min   longest {stats.longestCryMinutes:0} / {t.maxContinuousCryMinutes:0} min", _small);
            GUILayout.EndArea();
        }

        void DrawParent(Rect area)
        {
            var p = Sim.Parent;
            var t = Sim.Config.tuning;
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(Pad(area));
            GUILayout.Label("YOU", _header);
            Bar("Energy", p.Energy, t.lowEnergyThreshold, highIsBad: false);
            Bar("Hunger", p.Hunger, t.parentHungerThreshold, highIsBad: true);
            Bar("Stress", p.Stress, t.stressBlocksSleep, highIsBad: true);
            Bar("Joy", p.Joy, 20, highIsBad: false); // no effect until M4

            GUILayout.Space(6);
            if (!p.Busy) GUILayout.Label("Doing: nothing", _label);
            else if (p.ActionIsContinuous) GUILayout.Label($"Doing: {p.Action} ({p.ActionElapsedMinutes:0} min) - tap Stop to end", _label);
            else
            {
                GUILayout.Label($"Doing: {p.Action} ({p.ActionElapsedMinutes:0}/{p.ActionDurationMinutes:0} min)", _label);
                Fill(GUILayoutUtility.GetRect(360, 10), (float)p.ActionProgress, new Color(0.5f, 0.7f, 1f));
            }
            if (Sim.LowOnSupplies) GUILayout.Label("Low on supplies (raises Stress)", _small);
            if (!string.IsNullOrEmpty(Sim.LastRejection)) GUILayout.Label("Last refused: " + Sim.LastRejection, _small);
            GUILayout.EndArea();
        }

        void DrawSupplies(Rect area)
        {
            var s = Sim.Supplies;
            var priceList = Sim.Config.prices;
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(Pad(area));
            GUILayout.Label($"MONEY {Sim.Money}", _header);
            GUILayout.Label($"Bottles ready: {s.BottlesReady}", _label);
            foreach (var price in priceList.items)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{price.item}: {s.Get(price.item)}", _label, GUILayout.Width(150));
                OrderButton(ParentCommand.Order(price.item), $"+{price.packSize} ({price.price})");
                OrderButton(ParentCommand.Order(price.item, express: true), $"express ({price.price + priceList.expressFee})");
                GUILayout.EndHorizontal();
            }
            foreach (var d in Sim.PendingDeliveries)
                GUILayout.Label($"On the way: {d.Quantity} {d.Item}, arrives {CareSimulation.FormatClock(d.ArrivesAtMinute, Sim.Config.tuning.dayStartHour)}", _small);
            GUILayout.EndArea();
        }

        void OrderButton(ParentCommand command, string label)
        {
            var check = Sim.Check(command);
            GUI.enabled = check.Allowed;
            if (GUILayout.Button(new GUIContent(label, check.Reason), _button)) Player.Enqueue(command);
            GUI.enabled = true;
        }

        static readonly ActionType[] ActionButtons =
        {
            ActionType.Breastfeed, ActionType.PrepareBottle, ActionType.BottleFeed, ActionType.Burp,
            ActionType.ChangeDiaper, ActionType.Soothe, ActionType.PutBabyToSleep, ActionType.Play,
            ActionType.Eat, ActionType.Rest, ActionType.Sleep, ActionType.Stop,
        };

        void DrawActions(Rect area)
        {
            GUI.Box(area, GUIContent.none);
            const int columns = 4;
            float cellWidth = (area.width - 20) / columns;
            const float cellHeight = 72;
            for (int i = 0; i < ActionButtons.Length; i++)
            {
                var command = ParentCommand.Of(ActionButtons[i]);
                var check = Sim.Check(command);
                var cell = new Rect(area.x + 10 + i % columns * cellWidth, area.y + 10 + i / columns * cellHeight, cellWidth - 8, cellHeight - 8);
                GUI.enabled = check.Allowed;
                if (GUI.Button(new Rect(cell.x, cell.y, cell.width, 40), Label(ActionButtons[i]), _button)) Player.Enqueue(command);
                GUI.enabled = true;
                if (!check.Allowed) GUI.Label(new Rect(cell.x, cell.y + 40, cell.width, 24), check.Reason, _small);
            }
        }

        static string Label(ActionType type)
        {
            switch (type)
            {
                case ActionType.PrepareBottle: return "Prepare bottle";
                case ActionType.BottleFeed: return "Bottle-feed";
                case ActionType.ChangeDiaper: return "Change diaper";
                case ActionType.PutBabyToSleep: return "Put to sleep";
                case ActionType.Eat: return "Eat (you)";
                case ActionType.Rest: return "Rest (you)";
                case ActionType.Sleep: return "Sleep (you)";
                default: return type.ToString();
            }
        }

        void DrawLog(Rect area)
        {
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(Pad(area));
            GUILayout.Label("EVENTS", _header);
            var events = Sim.RecentEvents;
            for (int i = events.Count - 1; i >= 0 && i >= events.Count - 11; i--) GUILayout.Label(events[i].ToString(), _small);
            GUILayout.EndArea();
        }

        void DrawOutcome(float width)
        {
            var rect = new Rect(width / 2 - 280, 170, 560, 270);
            Fill(rect, 1f, new Color(0.08f, 0.09f, 0.12f));
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(Pad(rect));
            var stats = Sim.CopyStats();
            GUILayout.Label(Sim.Outcome == Outcome.Survived ? "MORNING! You made it through the day." : FailText(Sim.FailReason), _big);
            GUILayout.Label($"Ended at {Sim.ClockText}", _label);
            GUILayout.Label($"Crying {stats.cryingMinutes:0} min (longest {stats.longestCryMinutes:0}), average wellbeing {stats.AverageWellbeing:0}", _label);
            GUILayout.Label($"Feeds {stats.feeds}, diapers {stats.diaperChanges}, naps {stats.babyNaps}, your sleep {stats.parentSleepMinutes / 60:0.0} h, lowest energy {stats.minEnergy:0}", _label);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Same day again", _button, GUILayout.Height(40))) Restart(Seed);
            if (GUILayout.Button("New day", _button, GUILayout.Height(40))) Restart(System.Environment.TickCount);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
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

        // ---------------------------------------------------------------- helpers

        void Bar(string name, double value, double threshold, bool highIsBad)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _label, GUILayout.Width(90));
            var rect = GUILayoutUtility.GetRect(220, 18, GUILayout.Width(220));
            float v = Mathf.Clamp01((float)value / 100f);
            bool bad = highIsBad ? value >= threshold : value <= threshold;
            bool warn = highIsBad ? value >= threshold * 0.8 : value <= threshold * 1.25;
            Fill(rect, v, bad ? new Color(0.9f, 0.35f, 0.3f) : warn ? new Color(0.95f, 0.75f, 0.3f) : new Color(0.45f, 0.8f, 0.45f));
            float tx = rect.x + rect.width * Mathf.Clamp01((float)threshold / 100f);
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(tx - 1, rect.y - 2, 2, rect.height + 4), Texture2D.whiteTexture);
            GUI.color = old;
            GUILayout.Label($"{value:0}", _label, GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }

        static void Fill(Rect rect, float amount, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0.15f, 0.15f, 0.18f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(amount), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        static Rect Pad(Rect r) => new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 12);

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = false };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            _header = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }
    }
}
