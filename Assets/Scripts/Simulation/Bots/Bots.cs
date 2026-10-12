using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    /// <summary>
    /// Base for test bots: subclasses list the commands they want, most important first; the
    /// first one the simulation allows is issued. Bots act only through <see cref="IParentController"/>,
    /// exactly like the player.
    /// </summary>
    public abstract class BotController : IParentController
    {
        public ParentCommand Decide(CareSimulation sim)
        {
            var parent = sim.Parent;
            foreach (var command in Wishes(sim))
            {
                if (command == null) continue;
                // Keep going with the continuous action already in progress.
                if (parent.Busy && command.type == parent.Action) return null;
                // Orders are instant and never interrupt; other commands never interrupt a timed action.
                if (command.type != ActionType.OrderSupplies && parent.Busy && !parent.ActionIsContinuous) continue;
                if (sim.Check(command).Allowed) return command;
            }
            return null;
        }

        /// <summary>Wanted commands for this minute, most important first.</summary>
        protected abstract IEnumerable<ParentCommand> Wishes(CareSimulation sim);

        protected static ParentCommand Do(ActionType type) => ParentCommand.Of(type);

        /// <summary>The feed that fits the feeding choice: breast if possible, else a bottle (preparing one first).</summary>
        protected static ParentCommand Feed(CareSimulation sim)
        {
            if (sim.Feeding != FeedingChoice.Formula) return Do(ActionType.Breastfeed);
            return sim.Supplies.BottlesReady > 0 ? Do(ActionType.BottleFeed) : Do(ActionType.PrepareBottle);
        }

        /// <summary>Orders one pack of a supply that is below its threshold and not already on the way.</summary>
        protected static ParentCommand Restock(CareSimulation sim, int diapers, int wipes, int food, int formula)
        {
            var s = sim.Supplies;
            ParentCommand Need(SupplyType item, int stock, int threshold) =>
                stock < threshold && !sim.HasPendingDelivery(item) ? ParentCommand.Order(item, 1, express: stock == 0) : null;

            return Need(SupplyType.Diapers, s.Diapers, diapers)
                   ?? Need(SupplyType.Wipes, s.Wipes, wipes)
                   ?? Need(SupplyType.AdultFood, s.AdultFood, food)
                   ?? (sim.Feeding != FeedingChoice.Breast ? Need(SupplyType.Formula, s.Formula + s.BottlesReady, formula) : null);
        }
    }

    /// <summary>Does nothing: a sanity check that neglect always fails.</summary>
    public sealed class IdleBot : IParentController
    {
        public ParentCommand Decide(CareSimulation sim) => null;
    }

    /// <summary>Always handles the most urgent need, and acts before needs turn into crying.</summary>
    public sealed class GreedyBot : BotController
    {
        protected override IEnumerable<ParentCommand> Wishes(CareSimulation sim)
        {
            var b = sim.Baby;
            var p = sim.Parent;
            var t = sim.Config.tuning;

            yield return Restock(sim, diapers: 5, wipes: 5, food: 2, formula: 3);

            if (b.NeedsBurp && !b.Asleep) yield return Do(ActionType.Burp);
            if (b.Crying)
            {
                var c = b.CryCauses;
                if ((c & CryCause.Hunger) != 0) yield return Feed(sim);
                if ((c & CryCause.Gas) != 0) yield return Do(ActionType.Burp);
                if ((c & CryCause.Diaper) != 0) yield return Do(ActionType.ChangeDiaper);
                if ((c & CryCause.Upset) != 0) yield return Do(ActionType.Soothe);
                if ((c & CryCause.Overtired) != 0)
                    yield return b.Tension >= t.tensionBlocksSleep ? Do(ActionType.Soothe) : Do(ActionType.PutBabyToSleep);
                if ((c & CryCause.Bored) != 0) yield return Do(ActionType.Play);
                yield return Do(ActionType.Soothe);
            }

            if (!b.Asleep)
            {
                if (b.Hunger >= 55) yield return Feed(sim);
                if (b.Diaper != DiaperState.Clean) yield return Do(ActionType.ChangeDiaper);
                if (b.Tiredness >= 60)
                    yield return b.Tension >= t.tensionBlocksSleep - 5 ? Do(ActionType.Soothe) : Do(ActionType.PutBabyToSleep);
                if (b.Tension >= 40) yield return Do(ActionType.Soothe);
                if (b.Boredom >= 45) yield return Do(ActionType.Play);
            }
            else if (b.Diaper == DiaperState.Dirty || b.SoiledMinutes >= 45) yield return Do(ActionType.ChangeDiaper);

            if (p.Hunger >= 60) yield return Do(ActionType.Eat);
            if (sim.Feeding == FeedingChoice.Formula && sim.Supplies.BottlesReady == 0) yield return Do(ActionType.PrepareBottle);
            // Keep a rest going until full; start one only when it is worth it.
            bool keepResting = p.Busy && (p.Action == ActionType.Sleep || p.Action == ActionType.Rest);
            if (b.Asleep && (p.Energy < 90 || keepResting)) yield return Do(ActionType.Sleep);
            if (!b.Crying && (p.Energy < 80 || keepResting)) yield return Do(ActionType.Rest);
        }
    }

    /// <summary>
    /// A distracted parent: reacts only once the baby cries, and late; sometimes soothes when
    /// something else is needed; often forgets to burp; restocks only when nearly out.
    /// </summary>
    public sealed class SloppyBot : BotController
    {
        readonly SeededRandom _random;
        readonly int _minDelay;
        readonly int _maxDelay;
        double _reactAt = -1;
        double _mistakeUntil = -1;
        bool _willBurp;
        bool _wasCrying;
        int _feedsSeen;

        readonly double _attention;

        /// <param name="attention">Chance per idle minute of noticing a need before the baby cries.</param>
        public SloppyBot(int seed, int minDelay = 1, int maxDelay = 5, double attention = 0.25)
        {
            _random = new SeededRandom(seed);
            _minDelay = minDelay;
            _maxDelay = maxDelay;
            _attention = attention;
        }

        protected override IEnumerable<ParentCommand> Wishes(CareSimulation sim)
        {
            var b = sim.Baby;
            var p = sim.Parent;
            double now = sim.Minute;

            if (b.Crying && !_wasCrying)
            {
                _reactAt = now + _random.Range(_minDelay, _maxDelay + 1);
                _mistakeUntil = _random.NextDouble() < 0.2 ? _reactAt + 5 : -1;
            }
            _wasCrying = b.Crying;

            // Decide once per feed whether this one gets burped.
            int feeds = sim.FeedsToday;
            if (feeds != _feedsSeen)
            {
                _feedsSeen = feeds;
                _willBurp = _random.NextDouble() < 0.7;
            }

            yield return Restock(sim, diapers: 2, wipes: 2, food: 1, formula: 2);

            if (b.Crying)
            {
                if (now < _reactAt) yield break;
                if (now < _mistakeUntil) yield return Do(ActionType.Soothe);

                var c = b.CryCauses;
                if ((c & CryCause.Hunger) != 0) yield return Feed(sim);
                if ((c & CryCause.Diaper) != 0) yield return Do(ActionType.ChangeDiaper);
                if ((c & CryCause.Gas) != 0) yield return Do(ActionType.Burp);
                if ((c & CryCause.Overtired) != 0) yield return Do(ActionType.PutBabyToSleep);
                if ((c & CryCause.Bored) != 0) yield return Do(ActionType.Play);
                yield return Do(ActionType.Soothe);
                yield break;
            }

            if (b.NeedsBurp && _willBurp) yield return Do(ActionType.Burp);

            // Now and then glances at the baby and handles an obvious need before it turns into crying.
            if (!p.Busy && _random.NextDouble() < _attention)
            {
                if (!b.Asleep)
                {
                    if (b.Hunger >= 60) yield return Feed(sim);
                    if (b.Diaper == DiaperState.Dirty) yield return Do(ActionType.ChangeDiaper);
                    if (b.Tiredness >= 70) yield return Do(ActionType.PutBabyToSleep);
                }
                if (sim.Feeding == FeedingChoice.Formula && sim.Supplies.BottlesReady == 0 && b.Hunger >= 55)
                    yield return Do(ActionType.PrepareBottle);
            }

            if (p.Hunger >= 75) yield return Do(ActionType.Eat);
            if (b.Asleep && p.Energy < 70) yield return Do(ActionType.Sleep);
        }
    }
}
