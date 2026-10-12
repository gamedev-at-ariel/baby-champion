using System;
using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    /// <summary>Something that happened in the simulation, for the event log.</summary>
    public readonly struct SimEvent
    {
        public readonly double Minute;
        public readonly string Text;

        public SimEvent(double minute, string text)
        {
            Minute = minute;
            Text = text;
        }

        public override string ToString() => $"{CareSimulation.FormatClock(Minute)} {Text}";
    }

    /// <summary>
    /// One in-game day of baby care (design Sections 6-10 and 13.2), in plain C#.
    /// Time advances in fixed one-minute ticks. Parent actions enter only through the attached
    /// <see cref="IParentController"/>; everything else on this class is read-only or time control.
    /// </summary>
    public sealed class CareSimulation
    {
        public const double TickMinutes = 1;
        const int MaxRecentEvents = 40;

        readonly SimConfig _config;
        readonly Tuning _t;
        readonly SimState _s;
        readonly SeededRandom _random;
        readonly AgeBand _band;
        readonly List<SimEvent> _recentEvents = new List<SimEvent>();
        IParentController _controller;
        double _pendingRealSeconds;

        public event Action<SimEvent> EventRaised;

        public static CareSimulation NewDay(SimConfig config, int month, FeedingChoice feeding, int seed)
        {
            var start = config.tuning.start;
            var state = new SimState
            {
                month = month,
                feeding = feeding,
                money = start.money,
                randomState = new SeededRandom(seed).State,
                baby = { hunger = start.babyHunger, tiredness = start.babyTiredness },
                parent = { energy = start.parentEnergy, hunger = start.parentHunger, stress = start.parentStress, joy = start.parentJoy },
                supplies =
                {
                    diapers = start.diapers, wipes = start.wipes, formula = start.formula,
                    babyFood = start.babyFood, adultFood = start.adultFood,
                },
            };
            return new CareSimulation(config, state);
        }

        public CareSimulation(SimConfig config, SimState state)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _s = state ?? throw new ArgumentNullException(nameof(state));
            _t = config.tuning;
            _band = config.ageRates.ForMonth(state.month);
            _random = new SeededRandom(1) { State = state.randomState };
            MinutesPerRealSecond = _t.minutesPerRealSecond;
        }

        // ---------------------------------------------------------------- read-only view

        public SimConfig Config => _config;
        public int Month => _s.month;
        public AgeBand AgeBand => _band;
        public FeedingChoice Feeding => _s.feeding;
        /// <summary>In-game minutes since the day started.</summary>
        public double Minute => _s.minute;
        public string ClockText => FormatClock(_s.minute, _t.dayStartHour);
        public double DayProgress => Math.Min(1, _s.minute / _t.dayLengthMinutes);
        public Outcome Outcome => _s.outcome;
        public FailReason FailReason => _s.failReason;
        public int Money => _s.money;
        public BabyView Baby => new BabyView(_s.baby);
        public ParentView Parent => new ParentView(_s.parent);
        public SupplyView Supplies => new SupplyView(_s.supplies);
        public string LastRejection { get; private set; }
        public int FeedsToday => _s.stats.feeds;
        public double CryingMinutesToday => _s.stats.cryingMinutes;
        public IReadOnlyList<SimEvent> RecentEvents => _recentEvents;

        public IReadOnlyList<DeliveryInfo> PendingDeliveries
        {
            get
            {
                var list = new List<DeliveryInfo>();
                foreach (var d in _s.deliveries) list.Add(new DeliveryInfo(d.item, d.quantity, d.arrivesAtMinute));
                return list;
            }
        }

        public bool HasPendingDelivery(SupplyType item) => _s.deliveries.Exists(d => d.item == item);

        /// <summary>A copy of today's measurements.</summary>
        public DayStats CopyStats() => JsonCodec.Deserialize<DayStats>(JsonCodec.Serialize(_s.stats));

        /// <summary>A deep copy of the full state.</summary>
        public SimState CaptureState() => JsonCodec.Deserialize<SimState>(SaveJson());

        public string SaveJson()
        {
            _s.randomState = _random.State;
            return JsonCodec.Serialize(_s);
        }

        public static CareSimulation LoadJson(SimConfig config, string json)
        {
            var state = JsonCodec.Deserialize<SimState>(json);
            if (state.version != 1) throw new FormatException($"Unsupported save version {state.version}");
            return new CareSimulation(config, state);
        }

        public static string FormatClock(double minute, int dayStartHour = 7)
        {
            int total = dayStartHour * 60 + (int)Math.Floor(minute);
            return $"{total / 60 % 24:00}:{total % 60:00}";
        }

        // ---------------------------------------------------------------- control

        /// <summary>The only entry point for parent actions.</summary>
        public void AttachController(IParentController controller) => _controller = controller;

        public bool Paused { get; set; }

        /// <summary>Time compression: in-game minutes per real second while the parent is awake (default 2).</summary>
        public double MinutesPerRealSecond { get; set; }

        /// <summary>4x while the parent sleeps (design Section 6), otherwise 1x.</summary>
        public double TimeMultiplier => IsParentAsleep ? _t.parentSleepTimeMultiplier : 1;

        public bool IsParentAsleep => _s.parent.action != null && _s.parent.action.type == ActionType.Sleep;

        /// <summary>Advances by real time, applying compression, sleep fast-forward and pause. Returns the ticks run.</summary>
        public int Advance(double realSeconds)
        {
            if (Paused || _s.outcome != Outcome.Running || realSeconds <= 0 || MinutesPerRealSecond <= 0) return 0;
            _pendingRealSeconds += Math.Min(realSeconds, 10);
            int ticks = 0;
            while (_s.outcome == Outcome.Running)
            {
                double secondsPerTick = TickMinutes / (MinutesPerRealSecond * TimeMultiplier);
                if (_pendingRealSeconds + 1e-9 < secondsPerTick) break;
                _pendingRealSeconds = Math.Max(0, _pendingRealSeconds - secondsPerTick);
                Tick();
                ticks++;
            }
            if (_s.outcome != Outcome.Running) _pendingRealSeconds = 0;
            return ticks;
        }

        /// <summary>Runs until the day ends or fails (headless runs), at most <paramref name="maxTicks"/> ticks.</summary>
        public void RunToEnd(int maxTicks = 100000)
        {
            for (int i = 0; i < maxTicks && _s.outcome == Outcome.Running; i++) Tick();
        }

        /// <summary>One in-game minute.</summary>
        public void Tick()
        {
            if (_s.outcome != Outcome.Running) return;

            if ((int)Math.Round(_s.minute) % 60 == 0) _s.stats.energyByHour.Add(_s.parent.energy);

            var command = _controller?.Decide(this);
            if (command != null) Issue(command);

            ProgressAction();
            UpdateBaby();
            UpdateCrying();
            UpdateParent();
            UpdateStats();
            CheckFailure();

            _s.minute += TickMinutes;
            ArriveDeliveries();

            if (_s.outcome == Outcome.Running && _s.minute >= _t.dayLengthMinutes)
            {
                _s.outcome = Outcome.Survived;
                Emit("Morning! You made it through the day.");
            }
        }

        // ---------------------------------------------------------------- actions

        /// <summary>Whether a command could start now. Read-only: use it to grey out buttons.</summary>
        public ActionCheck Check(ParentCommand command)
        {
            if (command == null) return ActionCheck.No("No command");
            if (_s.outcome != Outcome.Running) return ActionCheck.No("The day is over");

            var b = _s.baby;
            var p = _s.parent;
            var supplies = _s.supplies;

            switch (command.type)
            {
                case ActionType.Stop:
                    return p.action == null ? ActionCheck.No("Nothing to stop") : ActionCheck.Ok;
                case ActionType.OrderSupplies:
                    if (command.packs < 1) return ActionCheck.No("Order at least one pack");
                    int cost = _config.prices.Cost(command.item, command.packs, command.express);
                    return cost > _s.money ? ActionCheck.No($"Not enough money ({cost} needed)") : ActionCheck.Ok;
            }

            if (p.action != null && p.action.type == command.type) return ActionCheck.No("Already doing that");

            switch (command.type)
            {
                case ActionType.Breastfeed:
                    if (_s.feeding == FeedingChoice.Formula) return ActionCheck.No("Formula feeding was chosen");
                    return CheckFeed(b);
                case ActionType.PrepareBottle:
                    if (_s.feeding == FeedingChoice.Breast) return ActionCheck.No("Breastfeeding was chosen");
                    if (supplies.formula < 1) return ActionCheck.No("Out of formula - order some");
                    if (supplies.bottlesReady >= _t.maxBottlesReady) return ActionCheck.No("Enough bottles are ready");
                    return ActionCheck.Ok;
                case ActionType.BottleFeed:
                    if (_s.feeding == FeedingChoice.Breast) return ActionCheck.No("Breastfeeding was chosen");
                    if (supplies.bottlesReady < 1) return ActionCheck.No("No bottle ready - prepare one");
                    return CheckFeed(b);
                case ActionType.Burp:
                    if (!b.needsBurp && !b.gas) return ActionCheck.No("Only needed after a feed");
                    if (b.asleep) return ActionCheck.No("The baby is asleep");
                    return ActionCheck.Ok;
                case ActionType.ChangeDiaper:
                    if (b.diaper == DiaperState.Clean) return ActionCheck.No("The diaper is clean");
                    if (supplies.diapers < 1) return ActionCheck.No("Out of diapers - order some");
                    if (supplies.wipes < 1) return ActionCheck.No("Out of wipes - order some");
                    return ActionCheck.Ok;
                case ActionType.Soothe:
                    return b.asleep ? ActionCheck.No("The baby is asleep") : ActionCheck.Ok;
                case ActionType.PutBabyToSleep:
                    if (b.asleep) return ActionCheck.No("The baby is already asleep");
                    if (b.tension >= _t.tensionBlocksSleep) return ActionCheck.No("Too tense to sleep - soothe first");
                    var otherCauses = b.cryCauses & ~CryCause.Overtired;
                    if (otherCauses != CryCause.None) return ActionCheck.No($"The baby is crying ({CryHints.Describe(otherCauses)})");
                    if (b.tiredness < _t.minTirednessToSleep) return ActionCheck.No("The baby isn't tired yet");
                    return ActionCheck.Ok;
                case ActionType.Play:
                    if (b.asleep) return ActionCheck.No("The baby is asleep");
                    if (b.Crying) return ActionCheck.No("The baby is crying");
                    return ActionCheck.Ok;
                case ActionType.Eat:
                    if (supplies.adultFood < 1) return ActionCheck.No("No food at home - order some");
                    if (p.hunger < _t.minParentHungerToEat) return ActionCheck.No("You're not hungry");
                    return ActionCheck.Ok;
                case ActionType.Rest:
                    if (p.stress >= _t.stressBlocksSleep) return ActionCheck.No("Too stressed to rest");
                    if (b.Crying) return ActionCheck.No("The baby is crying");
                    if (p.energy >= 100) return ActionCheck.No("You're fully rested");
                    return ActionCheck.Ok;
                case ActionType.Sleep:
                    if (p.stress >= _t.stressBlocksSleep) return ActionCheck.No("Too stressed to sleep");
                    if (!b.asleep) return ActionCheck.No("The baby is awake");
                    if (p.energy >= 100) return ActionCheck.No("You're fully rested");
                    return ActionCheck.Ok;
                default:
                    return ActionCheck.No($"Unknown action {command.type}");
            }
        }

        ActionCheck CheckFeed(BabyState b)
        {
            if (b.asleep) return ActionCheck.No("The baby is asleep");
            if (b.hunger < _t.minBabyHungerToFeed) return ActionCheck.No("The baby isn't hungry");
            return ActionCheck.Ok;
        }

        void Issue(ParentCommand command)
        {
            var check = Check(command);
            if (!check.Allowed)
            {
                LastRejection = $"{command}: {check.Reason}";
                Emit($"Can't {command}: {check.Reason}");
                return;
            }
            LastRejection = null;

            var p = _s.parent;
            if (command.type == ActionType.OrderSupplies)
            {
                PlaceOrder(command);
                return;
            }
            if (p.action != null) Emit($"Stopped {p.action.type}");
            if (command.type == ActionType.Stop)
            {
                p.action = null;
                return;
            }

            double duration = DurationOf(command.type);
            if (duration > 0 && p.energy <= _t.lowEnergyThreshold) duration *= _t.lowEnergyDurationMultiplier;
            p.action = new ActiveAction { type = command.type, durationMinutes = duration, startHunger = _s.baby.hunger };
            if (command.type == ActionType.BottleFeed) _s.supplies.bottlesReady--;
            Emit($"Started {command.type}");
        }

        double DurationOf(ActionType type)
        {
            switch (type)
            {
                case ActionType.Breastfeed: return _t.breastfeedMinutes;
                case ActionType.PrepareBottle: return _t.prepareBottleMinutes;
                case ActionType.BottleFeed: return _t.bottleFeedMinutes;
                case ActionType.Burp: return _t.burpMinutes;
                case ActionType.ChangeDiaper: return _t.changeDiaperMinutes;
                case ActionType.PutBabyToSleep: return _t.putToSleepMinutes;
                case ActionType.Play: return _t.playMinutes;
                case ActionType.Eat: return _t.eatMinutes;
                default: return 0; // Soothe, Rest, Sleep: continuous
            }
        }

        void PlaceOrder(ParentCommand command)
        {
            var price = _config.prices.For(command.item);
            int cost = _config.prices.Cost(command.item, command.packs, command.express);
            double arrives = _s.minute + (command.express ? _config.prices.expressDeliveryMinutes : _config.prices.deliveryMinutes);
            _s.money -= cost;
            _s.stats.moneySpent += cost;
            _s.stats.ordersPlaced++;
            _s.deliveries.Add(new Delivery { item = command.item, quantity = price.packSize * command.packs, arrivesAtMinute = arrives });
            Emit($"Ordered {command.packs}x {command.item} for {cost}; arrives {FormatClock(arrives, _t.dayStartHour)}");
        }

        void ArriveDeliveries()
        {
            for (int i = _s.deliveries.Count - 1; i >= 0; i--)
            {
                var d = _s.deliveries[i];
                if (d.arrivesAtMinute > _s.minute) continue;
                _s.supplies.Add(d.item, d.quantity);
                _s.deliveries.RemoveAt(i);
                Emit($"Delivery arrived: {d.quantity} {d.item}");
            }
        }

        void ProgressAction()
        {
            var a = _s.parent.action;
            if (a == null) return;
            var b = _s.baby;
            var p = _s.parent;
            a.elapsedMinutes += TickMinutes;

            switch (a.type)
            {
                case ActionType.Breastfeed:
                case ActionType.BottleFeed:
                    b.hunger = Math.Max(0, b.hunger - a.startHunger / a.durationMinutes);
                    if (a.type == ActionType.Breastfeed) p.hunger += _t.breastfeedParentHunger / a.durationMinutes;
                    break;
                case ActionType.Soothe:
                case ActionType.PutBabyToSleep:
                    b.tension = Math.Max(0, b.tension - _t.sootheTensionPerMinute);
                    break;
                case ActionType.Play:
                    b.boredom = Math.Max(0, b.boredom - _t.playBoredomPerMinute);
                    b.tiredness += _t.playExtraTirednessPerMinute;
                    p.joy += _t.joyFromPlay;
                    break;
                case ActionType.Eat:
                    p.stress = Math.Max(0, p.stress - _t.stressDecayEating);
                    break;
            }

            if (!a.Continuous && a.elapsedMinutes >= a.durationMinutes - 1e-9) Complete(a);
        }

        void Complete(ActiveAction a)
        {
            var b = _s.baby;
            var p = _s.parent;
            var supplies = _s.supplies;
            var stats = _s.stats;
            p.action = null;

            switch (a.type)
            {
                case ActionType.Breastfeed:
                case ActionType.BottleFeed:
                    b.hunger = 0;
                    b.needsBurp = true;
                    b.minutesSinceFeed = 0;
                    stats.feeds++;
                    Emit("The baby is fed - time to burp");
                    break;
                case ActionType.PrepareBottle:
                    supplies.formula--;
                    supplies.bottlesReady++;
                    stats.formulaUsed++;
                    Emit("A bottle is ready");
                    break;
                case ActionType.Burp:
                    b.needsBurp = false;
                    b.gas = false;
                    stats.burps++;
                    Emit("Burped!");
                    break;
                case ActionType.ChangeDiaper:
                    b.diaper = DiaperState.Clean;
                    b.soiledMinutes = 0;
                    supplies.diapers--;
                    supplies.wipes--;
                    stats.diapersUsed++;
                    stats.wipesUsed++;
                    stats.diaperChanges++;
                    if (b.asleep)
                    {
                        b.asleep = false;
                        Emit("Diaper changed - and the baby woke up");
                    }
                    else Emit("Diaper changed");
                    break;
                case ActionType.PutBabyToSleep:
                    if (b.tension < _t.tensionBlocksSleep && (b.cryCauses & ~CryCause.Overtired) == CryCause.None)
                    {
                        b.asleep = true;
                        stats.babyNaps++;
                        Emit("The baby is asleep");
                    }
                    else Emit("The baby wouldn't settle");
                    break;
                case ActionType.Play:
                    Emit("Playtime done");
                    break;
                case ActionType.Eat:
                    p.hunger = 0;
                    supplies.adultFood--;
                    stats.mealsEaten++;
                    Emit("You ate");
                    break;
            }
        }

        bool ActionIs(ActionType type) => _s.parent.action != null && _s.parent.action.type == type;

        bool IsFeeding => ActionIs(ActionType.Breastfeed) || ActionIs(ActionType.BottleFeed);

        // ---------------------------------------------------------------- rules

        void UpdateBaby()
        {
            var b = _s.baby;
            bool playing = ActionIs(ActionType.Play);
            bool engaged = IsFeeding || ActionIs(ActionType.Soothe) || ActionIs(ActionType.PutBabyToSleep);

            b.hunger += _t.hungerCryThreshold / _band.hungerTo70Minutes;

            // Diaper events.
            if (b.diaper != DiaperState.Clean) b.soiledMinutes += TickMinutes;
            if (_random.NextDouble() < _band.diapersPerDay / _t.dayLengthMinutes)
            {
                bool dirty = _random.NextDouble() < _band.dirtyShare;
                if (dirty && b.diaper != DiaperState.Dirty) SetDiaper(DiaperState.Dirty);
                else if (!dirty && b.diaper == DiaperState.Clean) SetDiaper(DiaperState.Wet);
            }
            if (b.diaper == DiaperState.Dirty && b.soiledMinutes >= _t.rashAfterDirtyMinutes && !b.rash)
            {
                b.rash = true;
                Emit("Diaper rash! The baby will be uncomfortable all day");
            }

            // Burping and gas.
            b.minutesSinceFeed += TickMinutes;
            if (b.needsBurp && b.minutesSinceFeed >= _t.burpWindowMinutes)
            {
                b.needsBurp = false;
                b.gas = true;
                b.gasMinutes = 0;
                Emit("No burp after the feed - the baby has gas");
            }
            if (b.gas)
            {
                b.gasMinutes += TickMinutes;
                if (b.gasMinutes >= _t.gasDurationMinutes) b.gas = false;
            }

            // Tiredness and sleep.
            if (b.asleep)
            {
                b.tiredness = Math.Max(0, b.tiredness - _t.overtiredThreshold / _band.sleepRecoveryMinutes);
                b.boredom -= _t.boredomDecayAsleep;
                if (b.tiredness <= 0 && b.hunger >= _t.restedWakeHunger)
                {
                    b.asleep = false;
                    Emit("The baby woke up");
                    WakeParent("The baby woke up - so did you");
                }
            }
            else b.tiredness += _t.overtiredThreshold / _band.awakeWindowMinutes;

            // Boredom (from month 4).
            if (_s.month < _t.boredomFromMonth || _band.boredomTo70Minutes <= 0) b.boredom = 0;
            else if (!b.asleep && !playing)
                b.boredom += _t.boredomCryThreshold / _band.boredomTo70Minutes * (engaged ? _t.engagedBoredomMultiplier : 1);

            // Tension.
            if (!b.asleep && b.tiredness >= _t.overtiredThreshold) b.tension += _t.tensionFromOvertired;
            if (b.cryStreakMinutes >= _t.longCryMinutes) b.tension += _t.tensionFromLongCrying;
            if (b.gas) b.tension += _t.tensionFromGas;
            if (b.rash) b.tension += _t.tensionFromRash;
            if (b.asleep) b.tension -= _t.tensionDecayAsleep;
            else if (!b.Crying) b.tension -= _t.tensionDecayCalm;

            b.hunger = Clamp(b.hunger);
            b.tiredness = Clamp(b.tiredness);
            b.tension = Clamp(b.tension);
            b.boredom = Clamp(b.boredom);

            // Self-soothing (from month 6): a calm, tired baby falls asleep alone.
            if (!b.asleep && _s.month >= _t.selfSootheFromMonth && _s.parent.action == null
                && b.tension < 20 && b.tiredness >= 70 && (b.cryCauses & ~CryCause.Overtired) == CryCause.None)
            {
                b.asleep = true;
                _s.stats.babyNaps++;
                Emit("The baby fell asleep on their own");
            }
        }

        void SetDiaper(DiaperState state)
        {
            _s.baby.diaper = state;
            _s.baby.soiledMinutes = 0;
            Emit(state == DiaperState.Dirty ? "Dirty diaper!" : "Wet diaper");
        }

        void UpdateCrying()
        {
            var b = _s.baby;
            var causes = CryCause.None;
            if (b.hunger >= _t.hungerCryThreshold && !IsFeeding) causes |= CryCause.Hunger;
            // A cause being dealt with (feed, change, burp) no longer makes the baby cry.
            if (((b.diaper == DiaperState.Wet && b.soiledMinutes >= _t.wetCryAfterMinutes)
                 || (b.diaper == DiaperState.Dirty && b.soiledMinutes >= _t.dirtyCryAfterMinutes))
                && !ActionIs(ActionType.ChangeDiaper)) causes |= CryCause.Diaper;
            if (b.gas && !ActionIs(ActionType.Burp)) causes |= CryCause.Gas;
            if (b.tension >= _t.tensionCryThreshold && !ActionIs(ActionType.Soothe)) causes |= CryCause.Upset;
            if (b.asleep && causes != CryCause.None)
            {
                b.asleep = false;
                Emit("The baby woke up");
            }
            // Being fed or settled calms an overtired baby.
            if (!b.asleep && b.tiredness >= _t.overtiredThreshold && !ActionIs(ActionType.PutBabyToSleep) && !IsFeeding) causes |= CryCause.Overtired;
            if (!b.asleep && _s.month >= _t.boredomFromMonth && b.boredom >= _t.boredomCryThreshold && !ActionIs(ActionType.Play)) causes |= CryCause.Bored;

            bool wasCrying = b.Crying;
            b.cryCauses = causes;
            if (causes != CryCause.None)
            {
                b.cryStreakMinutes += TickMinutes;
                _s.stats.cryingMinutes += TickMinutes;
                _s.stats.longestCryMinutes = Math.Max(_s.stats.longestCryMinutes, b.cryStreakMinutes);
                var st = _s.stats;
                if ((causes & CryCause.Hunger) != 0) st.cryHungerMinutes += TickMinutes;
                if ((causes & CryCause.Diaper) != 0) st.cryDiaperMinutes += TickMinutes;
                if ((causes & CryCause.Overtired) != 0) st.cryOvertiredMinutes += TickMinutes;
                if ((causes & CryCause.Gas) != 0) st.cryGasMinutes += TickMinutes;
                if ((causes & CryCause.Upset) != 0) st.cryUpsetMinutes += TickMinutes;
                if ((causes & CryCause.Bored) != 0) st.cryBoredMinutes += TickMinutes;
                if (!wasCrying) Emit($"The baby is crying: {CryHints.Describe(causes)}");

                WakeParent("The crying woke you up");
                if (ActionIs(ActionType.Rest))
                {
                    Emit("The crying interrupted your rest");
                    _s.parent.action = null;
                }
            }
            else
            {
                if (wasCrying) Emit("The baby stopped crying");
                b.cryStreakMinutes = 0;
            }
        }

        void WakeParent(string text)
        {
            if (!ActionIs(ActionType.Sleep)) return;
            _s.parent.action = null;
            Emit(text);
        }

        void UpdateParent()
        {
            var p = _s.parent;
            bool crying = _s.baby.Crying;
            bool sleeping = ActionIs(ActionType.Sleep);
            bool resting = ActionIs(ActionType.Rest);
            bool hungry = p.hunger >= _t.parentHungerThreshold;
            bool carrying = ActionIs(ActionType.Soothe) || ActionIs(ActionType.PutBabyToSleep);

            if (sleeping || resting)
            {
                p.energy += (sleeping ? _t.energySleepRecovery : _t.energyRestRecovery) * (hungry ? _t.hungryRecoveryMultiplier : 1);
                p.stress -= _t.stressDecayResting;
                if (sleeping) _s.stats.parentSleepMinutes += TickMinutes;
                if (p.energy >= 100)
                {
                    p.energy = 100;
                    p.action = null;
                    Emit("You feel fully rested");
                }
            }
            else
            {
                p.energy -= _t.energyDrainAwake
                            * (crying ? _t.cryingEnergyMultiplier : 1)
                            * (carrying ? _t.carryingEnergyMultiplier : 1)
                            * (hungry ? _t.hungryDrainMultiplier : 1);
            }

            if (!ActionIs(ActionType.Eat)) p.hunger += _t.parentHungerThreshold / _t.parentHungerTo70Minutes;

            if (crying) p.stress += _t.stressFromCrying;
            else p.stress -= _t.stressDecayCalm;
            if (LowOnSupplies) p.stress += _t.stressFromLowSupplies;
            if (_s.money < _t.lowMoney) p.stress += _t.stressFromLowMoney;

            p.joy -= _t.joyDecay;

            p.energy = Clamp(p.energy);
            p.hunger = Clamp(p.hunger);
            p.stress = Clamp(p.stress);
            p.joy = Clamp(p.joy);
        }

        /// <summary>True when an essential supply is nearly gone; raises Stress.</summary>
        public bool LowOnSupplies
        {
            get
            {
                var s = _s.supplies;
                int low = _t.lowSupplyCount;
                return s.diapers < low || s.wipes < low || s.adultFood < 1
                       || (_s.feeding != FeedingChoice.Breast && s.formula + s.bottlesReady < low);
            }
        }

        void UpdateStats()
        {
            var b = _s.baby;
            b.wellbeing = Wellbeing(b);
            _s.stats.wellbeingSum += b.wellbeing;
            _s.stats.wellbeingSamples++;
            _s.stats.minEnergy = Math.Min(_s.stats.minEnergy, _s.parent.energy);
        }

        /// <summary>How well the baby's needs are met right now, 0-100 (design Section 7.1).</summary>
        static double Wellbeing(BabyState b)
        {
            double penalty = Math.Max(0, b.hunger - 40) * 0.5
                             + (b.diaper == DiaperState.Dirty ? 20 : b.diaper == DiaperState.Wet ? 8 : 0)
                             + Math.Max(0, b.tiredness - 60) * 0.5
                             + b.tension * 0.25
                             + Math.Max(0, b.boredom - 40) * 0.3
                             + (b.Crying ? 15 : 0)
                             + (b.rash ? 10 : 0)
                             + (b.gas ? 10 : 0);
            return Clamp(100 - penalty);
        }

        void CheckFailure()
        {
            if (_s.parent.energy <= 0) Fail(FailReason.ParentCollapsed, "You collapsed from exhaustion.");
            else if (_s.baby.cryStreakMinutes > _t.maxContinuousCryMinutes) Fail(FailReason.ContinuousCrying, "The baby cried for too long without a break.");
            else if (_s.stats.cryingMinutes > _t.cryingBudgetMinutes) Fail(FailReason.CryingBudget, "Too much crying today.");
        }

        void Fail(FailReason reason, string text)
        {
            _s.outcome = Outcome.Failed;
            _s.failReason = reason;
            _s.parent.action = null;
            Emit(text);
        }

        void Emit(string text)
        {
            var e = new SimEvent(_s.minute, text);
            _recentEvents.Add(e);
            if (_recentEvents.Count > MaxRecentEvents) _recentEvents.RemoveAt(0);
            EventRaised?.Invoke(e);
        }

        static double Clamp(double value) => value < 0 ? 0 : value > 100 ? 100 : value;
    }
}
