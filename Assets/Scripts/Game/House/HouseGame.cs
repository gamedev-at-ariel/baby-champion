using System;
using System.Collections.Generic;
using BabyChampion.Simulation;
using UnityEngine;
using UnityEngine.AI;

namespace BabyChampion.Game
{
    public enum HouseVerb { Sim, PickUpBaby, PutBabyHere, PutBabyDown }

    /// <summary>One entry of the radial menu.</summary>
    public sealed class HouseAction
    {
        public HouseVerb Verb;
        /// <summary>The simulation command, for <see cref="HouseVerb.Sim"/>.</summary>
        public ParentCommand Command;
        /// <summary>Where it happens; null lets the house pick (the nearest suitable furniture, or where the baby is).</summary>
        public Interactable Station;
        public string Label;

        public static HouseAction Sim(ParentCommand command, Interactable station = null) => new HouseAction
        {
            Verb = HouseVerb.Sim, Command = command, Station = station,
            Label = command.type == ActionType.OrderSupplies ? command.ToString() : ActionCatalog.For(command.type).Label,
        };

        public override string ToString() => Label;
    }

    /// <summary>A request to show the radial menu for something the player tapped.</summary>
    public sealed class RadialRequest
    {
        public string Title;
        public Vector2 ScreenPosition;
        public List<HouseAction> Actions;
    }

    /// <summary>
    /// The playable house (milestone M2): owns the simulated day and turns the player's taps into
    /// walking, carrying the baby and simulation commands. Every simulation action still goes
    /// through the parent-controller interface: the player is a <see cref="QueuedController"/>,
    /// and a command is queued when the parent arrives where the action happens.
    /// </summary>
    public sealed class HouseGame : MonoBehaviour
    {
        enum StepKind { Walk, PickUp, Place, Issue }

        struct Step
        {
            public StepKind Kind;
            public Vector3 Point;
            public Interactable Target;
            public ParentCommand Command;
        }

        const float ArriveDistance = 0.35f;

        [SerializeField] AgeRatesAsset ageRates;
        [SerializeField] PricesAsset prices;
        [SerializeField] TuningAsset tuning;
        [SerializeField] int month;
        [SerializeField] FeedingChoice feeding = FeedingChoice.Both;
        [SerializeField] ParentAvatar parent;
        [SerializeField] BabyAvatar baby;
        [SerializeField] Interactable startBabySpot;
        [SerializeField] Transform parentStart;
        [SerializeField] Interactable frontDoor;

        readonly List<Step> _plan = new List<Step>();
        bool _stepStarted;
        ActionType? _lastAction;
        string _lastRejection;
        int _lastDeliveryCount;
        float _timeScaleBeforePause = 1f;

        public CareSimulation Sim { get; private set; }
        public QueuedController Player { get; private set; }
        public int Seed { get; private set; }
        public int Month => month;
        public FeedingChoice Feeding => feeding;
        public ParentAvatar Parent => parent;
        public BabyAvatar Baby => baby;
        public Interactable FrontDoor => frontDoor;
        public Interactable[] Interactables { get; private set; } = new Interactable[0];
        public Room[] Rooms { get; private set; } = new Room[0];

        /// <summary>True while the parent holds the baby; otherwise the baby is at <see cref="BabySpot"/>.</summary>
        public bool BabyCarried { get; private set; }
        public Interactable BabySpot { get; private set; }
        /// <summary>Where the current simulation action happens (null: wherever the parent is).</summary>
        public Interactable Station { get; private set; }
        /// <summary>True while the parent is carrying out a walk-and-act plan.</summary>
        public bool PlanActive => _plan.Count > 0;
        /// <summary>What the parent is heading to do, for the HUD.</summary>
        public string PlanLabel { get; private set; }
        /// <summary>Real time (unscaled) of the last delivery arrival, for the door indicator.</summary>
        public float LastDeliveryTime { get; private set; } = float.NegativeInfinity;

        /// <summary>Short messages for the player: refused actions, deliveries.</summary>
        public event Action<string> Notice;
        public event Action<RadialRequest> RadialRequested;
        public event Action<SimEvent> SimEventRaised;
        public event Action DayStarted;

        public bool Paused
        {
            get => Sim != null && Sim.Paused;
            set
            {
                if (Sim == null || Sim.Paused == value) return;
                Sim.Paused = value;
                if (value)
                {
                    _timeScaleBeforePause = Time.timeScale;
                    Time.timeScale = 0f;
                }
                else Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
            }
        }

        void Awake()
        {
            Interactables = FindObjectsByType<Interactable>(FindObjectsSortMode.InstanceID);
            Rooms = FindObjectsByType<Room>(FindObjectsSortMode.InstanceID);
            var args = DevBootstrap.Args;
            if (args.Level is int level) month = Mathf.Clamp(level - 1, 0, ConfigValidator.LastMonth);
            Restart(args.Seed ?? Environment.TickCount);
        }

        // ---------------------------------------------------------------- day control

        /// <summary>A fresh copy of the simulation config from the data assets.</summary>
        public SimConfig CreateConfig() => SimData.ToConfig(ageRates, prices, tuning);

        public void Restart(int seed) => Begin(CareSimulation.NewDay(CreateConfig(), month, feeding, seed), seed);

        public void Restart(int seed, int newMonth, FeedingChoice newFeeding)
        {
            month = Mathf.Clamp(newMonth, 0, ConfigValidator.LastMonth);
            feeding = newFeeding;
            Restart(seed);
        }

        /// <summary>Starts from a saved state (tests and, later, save slots), optionally with different rules.</summary>
        public void StartFromState(SimState state, SimConfig config = null, int seed = 0)
        {
            month = state.month;
            feeding = state.feeding;
            Begin(new CareSimulation(config ?? CreateConfig(), state), seed);
        }

        void Begin(CareSimulation sim, int seed)
        {
            double speed = Sim != null ? Sim.MinutesPerRealSecond : sim.MinutesPerRealSecond;
            Paused = false;
            Seed = seed;
            Sim = sim;
            Sim.MinutesPerRealSecond = speed;
            Player = new QueuedController();
            Sim.AttachController(Player);
            Sim.EventRaised += e => SimEventRaised?.Invoke(e);

            _plan.Clear();
            _stepStarted = false;
            PlanLabel = null;
            Station = null;
            _lastAction = null;
            _lastRejection = null;
            _lastDeliveryCount = 0;
            LastDeliveryTime = float.NegativeInfinity;
            BabyCarried = false;
            BabySpot = startBabySpot;
            if (parent != null && parentStart != null) parent.Teleport(parentStart.position, parentStart.eulerAngles.y);
            Debug.Log($"[House] new day: month {month}, feeding {feeding}, seed {seed}");
            DayStarted?.Invoke();
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            if (Sim == null) return;
            RunPlan();
            Sim.Advance(Time.deltaTime);
            AfterTick();
        }

        void AfterTick()
        {
            var p = Sim.Parent;
            ActionType? action = p.Busy ? p.Action : (ActionType?)null;
            if (_lastAction == ActionType.PutBabyToSleep && action != ActionType.PutBabyToSleep
                && Sim.Baby.Asleep && BabyCarried && Station != null && Station.babySpot)
            {
                BabyCarried = false;
                BabySpot = Station;
            }
            // A queued command has not started yet; keep its station until it does or is refused.
            if (action == null && Player.Pending == 0) Station = null;
            _lastAction = action;

            if (Sim.LastRejection != _lastRejection)
            {
                _lastRejection = Sim.LastRejection;
                if (_lastRejection != null) Notice?.Invoke("Can't " + _lastRejection);
            }

            int deliveries = Sim.PendingDeliveries.Count;
            if (deliveries < _lastDeliveryCount)
            {
                LastDeliveryTime = Time.unscaledTime;
                Notice?.Invoke("A delivery arrived at the front door");
            }
            _lastDeliveryCount = deliveries;

            if (parent != null) parent.SetLowEnergy(p.Energy <= Sim.Config.tuning.lowEnergyThreshold);
        }

        void LateUpdate()
        {
            if (Sim == null) return;
            ShowParent();
            ShowBaby();
        }

        void ShowParent()
        {
            if (parent == null) return;
            var p = Sim.Parent;
            ActionType? action = p.Busy && !parent.Walking ? p.Action : (ActionType?)null;
            var pose = ParentPose.Stand;
            if (action != null && Station != null)
            {
                switch (action.Value)
                {
                    case ActionType.Breastfeed:
                    case ActionType.BottleFeed:
                        pose = Station.seatPose == ParentPose.Stand ? ParentPose.Stand : ParentPose.Sit;
                        break;
                    case ActionType.Eat:
                    case ActionType.Rest:
                    case ActionType.Sleep:
                        pose = Station.seatPose;
                        break;
                    case ActionType.Play:
                        pose = ParentPose.Kneel;
                        break;
                }
                parent.Face(Station.FacePosition);
            }
            parent.Show(pose, action, Station);
        }

        void ShowBaby()
        {
            if (baby == null) return;
            var p = Sim.Parent;
            Transform anchor;
            BabyDisplayPose pose;
            if (BabyCarried && parent != null)
            {
                if (p.Busy && p.Action == ActionType.ChangeDiaper && Station != null && Station.babyPoint != null && !parent.Walking)
                {
                    anchor = Station.babyPoint;
                    pose = BabyDisplayPose.Lying;
                }
                else if (p.Busy && p.Action == ActionType.Burp)
                {
                    anchor = parent.Shoulder;
                    pose = BabyDisplayPose.OnShoulder;
                }
                else
                {
                    anchor = parent.Arms;
                    pose = BabyDisplayPose.InArms;
                }
            }
            else
            {
                var spot = BabySpot;
                anchor = spot == null ? null : spot.babyPoint != null ? spot.babyPoint : spot.transform;
                pose = spot == null ? BabyDisplayPose.Lying : (BabyDisplayPose)spot.babyPose;
            }
            baby.Show(anchor, pose, Sim.Baby);
        }

        // ---------------------------------------------------------------- tapping

        /// <summary>Handles a tap on the 3-D view: opens a radial menu, or walks to the floor point.</summary>
        public void Tap(Vector2 screenPosition, Camera camera = null)
        {
            camera = camera != null ? camera : Camera.main;
            if (camera == null || Sim == null) return;
            var ray = camera.ScreenPointToRay(screenPosition);
            var hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Collide);
            if (hits.Length == 0) return;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
                if (baby != null && hit.collider.GetComponentInParent<BabyAvatar>() == baby)
                {
                    OpenRadial("Baby", ActionsForBaby(), screenPosition);
                    return;
                }

            foreach (var hit in hits)
            {
                if (parent != null && hit.collider.GetComponentInParent<ParentAvatar>() == parent)
                {
                    var actions = ActionsForParent();
                    if (actions.Count > 0)
                    {
                        OpenRadial("You", actions, screenPosition);
                        return;
                    }
                    continue;
                }
                if (hit.collider.isTrigger) continue;
                var target = hit.collider.GetComponentInParent<Interactable>();
                if (target != null)
                {
                    var actions = ActionsFor(target);
                    if (actions.Count > 0)
                    {
                        OpenRadial(target.DisplayName, actions, screenPosition);
                        return;
                    }
                }
                WalkTo(hit.point);
                return;
            }
        }

        /// <summary>Opens the radial menu of a piece of furniture, as a tap on it would.</summary>
        public void OpenRadialFor(Interactable target, Vector2 screenPosition) =>
            OpenRadial(target.DisplayName, ActionsFor(target), screenPosition);

        void OpenRadial(string title, List<HouseAction> actions, Vector2 screen) =>
            RadialRequested?.Invoke(new RadialRequest { Title = title, Actions = actions, ScreenPosition = screen });

        public List<HouseAction> ActionsFor(Interactable target)
        {
            var list = new List<HouseAction>();
            foreach (var type in target.actions)
                if (ActionCatalog.Offered(type, feeding)) list.Add(HouseAction.Sim(ParentCommand.Of(type), target));
            if (target.babySpot)
                list.Add(new HouseAction { Verb = HouseVerb.PutBabyHere, Station = target, Label = "Put baby here" });
            return list;
        }

        public List<HouseAction> ActionsForBaby()
        {
            var list = new List<HouseAction>();
            foreach (var type in ActionCatalog.BabyActions)
                if (ActionCatalog.Offered(type, feeding)) list.Add(HouseAction.Sim(ParentCommand.Of(type)));
            list.Add(BabyCarried
                ? new HouseAction { Verb = HouseVerb.PutBabyDown, Label = "Put down" }
                : new HouseAction { Verb = HouseVerb.PickUpBaby, Label = "Pick up" });
            return list;
        }

        public List<HouseAction> ActionsForParent()
        {
            var list = new List<HouseAction>();
            if (Sim.Parent.Busy)
            {
                var stop = HouseAction.Sim(ParentCommand.Of(ActionType.Stop));
                stop.Label = "Stop " + ActionCatalog.For(Sim.Parent.Action).Label.ToLowerInvariant();
                list.Add(stop);
            }
            if (BabyCarried) list.Add(new HouseAction { Verb = HouseVerb.PutBabyDown, Label = "Put baby down" });
            return list;
        }

        // ---------------------------------------------------------------- actions

        /// <summary>Whether an action can start now; greyed out with the reason otherwise.</summary>
        public ActionCheck Check(HouseAction action)
        {
            if (Sim == null) return ActionCheck.No("No day running");
            if (Sim.Outcome != Outcome.Running) return ActionCheck.No("The day is over");
            switch (action.Verb)
            {
                case HouseVerb.PickUpBaby:
                    return BabyCarried ? ActionCheck.No("You're holding the baby") : ActionCheck.Ok;
                case HouseVerb.PutBabyDown:
                    return BabyCarried ? ActionCheck.Ok : ActionCheck.No("You're not holding the baby");
                case HouseVerb.PutBabyHere:
                    return !BabyCarried && BabySpot == action.Station ? ActionCheck.No("The baby is already here") : ActionCheck.Ok;
            }

            var type = action.Command.type;
            if (type != ActionType.Stop && type != ActionType.OrderSupplies)
            {
                var info = ActionCatalog.For(type);
                if (info.HandsFree && BabyCarried) return ActionCheck.No("Put the baby down first");
                if (info.AtStation && action.Station == null && NearestStation(type, transformOrigin: BabyOrigin(info)) == null)
                    return ActionCheck.No("Nowhere to do that here");
            }
            return Sim.Check(action.Command);
        }

        /// <summary>Starts an action: walks (carrying the baby when needed), then queues the command.</summary>
        public bool Perform(HouseAction action)
        {
            var check = Check(action);
            if (!check.Allowed)
            {
                Notice?.Invoke(check.Reason);
                return false;
            }

            var steps = new List<Step>();
            string label = action.Label;
            switch (action.Verb)
            {
                case HouseVerb.PickUpBaby:
                    FetchBaby(steps);
                    break;
                case HouseVerb.PutBabyDown:
                case HouseVerb.PutBabyHere:
                {
                    var spot = action.Station != null ? action.Station : NearestBabySpot();
                    if (spot == null)
                    {
                        Notice?.Invoke("Nowhere safe to put the baby");
                        return false;
                    }
                    FetchBaby(steps);
                    steps.Add(Walk(spot.StandPosition));
                    steps.Add(new Step { Kind = StepKind.Place, Target = spot });
                    label = "Put baby in " + spot.DisplayName.ToLowerInvariant();
                    break;
                }
                default:
                {
                    var type = action.Command.type;
                    if (type == ActionType.Stop || type == ActionType.OrderSupplies)
                    {
                        if (type == ActionType.Stop) CancelPlan();
                        Player.Enqueue(action.Command);
                        return true;
                    }
                    var info = ActionCatalog.For(type);
                    var station = action.Station;
                    if (info.AtStation && station == null) station = NearestStation(type, BabyOrigin(info));
                    if (info.NeedsBaby)
                    {
                        bool babyAlreadyThere = info.PlaceBabyFirst && !BabyCarried && BabySpot == station;
                        if (!babyAlreadyThere) FetchBaby(steps);
                        if (info.AtStation) steps.Add(Walk(station.StandPosition));
                        if (info.PlaceBabyFirst) steps.Add(new Step { Kind = StepKind.Place, Target = station });
                    }
                    else if (info.AtStation) steps.Add(Walk(station.StandPosition));
                    steps.Add(new Step { Kind = StepKind.Issue, Command = action.Command, Target = station });
                    if (station != null) label += " (" + station.DisplayName.ToLowerInvariant() + ")";
                    break;
                }
            }
            StartPlan(steps, label);
            return true;
        }

        /// <summary>Walks to a point on the floor.</summary>
        public void WalkTo(Vector3 point) => StartPlan(new List<Step> { Walk(point) }, null);

        public void CancelPlan()
        {
            _plan.Clear();
            _stepStarted = false;
            PlanLabel = null;
            if (parent != null) parent.StopWalking();
        }

        void FetchBaby(List<Step> steps)
        {
            if (BabyCarried) return;
            if (BabySpot != null) steps.Add(Walk(BabySpot.StandPosition));
            steps.Add(new Step { Kind = StepKind.PickUp });
        }

        static Step Walk(Vector3 point) => new Step { Kind = StepKind.Walk, Point = point };

        void StartPlan(List<Step> steps, string label)
        {
            CancelPlan();
            bool walks = steps.Exists(s => s.Kind == StepKind.Walk && FlatDistance(s.Point, parent.transform.position) > ArriveDistance);
            bool picksUp = steps.Exists(s => s.Kind == StepKind.PickUp);
            var p = Sim.Parent;
            if (p.Busy)
            {
                bool keepSoothing = p.Action == ActionType.Soothe && BabyCarried;
                bool handsNeeded = picksUp && ActionCatalog.For(p.Action).HandsFree;
                if ((walks && !keepSoothing) || handsNeeded) Player.Enqueue(ParentCommand.Of(ActionType.Stop));
            }
            _plan.AddRange(steps);
            PlanLabel = label;
            RunPlan();
        }

        void RunPlan()
        {
            while (_plan.Count > 0)
            {
                var step = _plan[0];
                switch (step.Kind)
                {
                    case StepKind.Walk:
                        if (!_stepStarted)
                        {
                            if (FlatDistance(step.Point, parent.transform.position) <= ArriveDistance) break;
                            _stepStarted = true;
                            if (!parent.WalkTo(step.Point))
                            {
                                Fail("Can't get there");
                                return;
                            }
                            return;
                        }
                        if (parent.Walking) return;
                        if (parent.WalkFailed)
                        {
                            Fail("Can't get there");
                            return;
                        }
                        break;
                    case StepKind.PickUp:
                        BabyCarried = true;
                        BabySpot = null;
                        break;
                    case StepKind.Place:
                        BabyCarried = false;
                        BabySpot = step.Target;
                        break;
                    case StepKind.Issue:
                        Station = step.Target;
                        Player.Enqueue(step.Command);
                        break;
                }
                _plan.RemoveAt(0);
                _stepStarted = false;
                if (_plan.Count == 0) PlanLabel = null;
            }
        }

        void Fail(string reason)
        {
            CancelPlan();
            Notice?.Invoke(reason);
        }

        // ---------------------------------------------------------------- places

        Vector3 BabyOrigin(ActionInfo info) =>
            info.NeedsBaby && !BabyCarried && BabySpot != null ? BabySpot.StandPosition : parent.transform.position;

        /// <summary>The closest furniture (by walking distance from <paramref name="transformOrigin"/>) offering the action.</summary>
        public Interactable NearestStation(ActionType type, Vector3 transformOrigin) =>
            Nearest(i => i.Offers(type), transformOrigin);

        public Interactable NearestBabySpot() => Nearest(i => i.babySpot, parent.transform.position);

        Interactable Nearest(Predicate<Interactable> match, Vector3 origin)
        {
            Interactable best = null;
            float bestDistance = float.MaxValue;
            var path = new NavMeshPath();
            foreach (var candidate in Interactables)
            {
                if (candidate == null || !match(candidate)) continue;
                float d = PathLength(origin, candidate.StandPosition, path);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }
            return best;
        }

        static float PathLength(Vector3 from, Vector3 to, NavMeshPath path)
        {
            if (!NavMesh.SamplePosition(from, out var a, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var b, 2f, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return Vector3.Distance(from, to) + 1000f;
            float length = 0;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>The room the parent is in, or null.</summary>
        public Room RoomAt(Vector3 position)
        {
            foreach (var room in Rooms)
                if (room != null && room.Contains(position)) return room;
            return null;
        }
    }
}
