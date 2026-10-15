using System;

namespace BabyChampion.Simulation
{
    /// <summary>Parent actions of design Section 9 available in the simulation core.</summary>
    public enum ActionType
    {
        /// <summary>Cancel the action in progress.</summary>
        Stop,
        Breastfeed,
        PrepareBottle,
        BottleFeed,
        Burp,
        ChangeDiaper,
        Soothe,
        PutBabyToSleep,
        Play,
        Eat,
        Rest,
        Sleep,
        OrderSupplies,
        /// <summary>A grandparent looks after the baby for a while; once per level; caps the stars (design Section 9).</summary>
        CallForHelp,
    }

    /// <summary>A request from a parent controller to start an action.</summary>
    [Serializable]
    public sealed class ParentCommand : IEquatable<ParentCommand>
    {
        public ActionType type;
        // OrderSupplies only.
        public SupplyType item;
        public int packs;
        public bool express;

        public static ParentCommand Of(ActionType type) => new ParentCommand { type = type };

        public static ParentCommand Order(SupplyType item, int packs = 1, bool express = false) =>
            new ParentCommand { type = ActionType.OrderSupplies, item = item, packs = packs, express = express };

        public bool Equals(ParentCommand other) =>
            other != null && type == other.type
            && (type != ActionType.OrderSupplies || (item == other.item && packs == other.packs && express == other.express));

        public override bool Equals(object obj) => Equals(obj as ParentCommand);
        public override int GetHashCode() => (int)type * 397 ^ (type == ActionType.OrderSupplies ? (int)item * 31 + packs + (express ? 1000 : 0) : 0);

        public override string ToString() => type == ActionType.OrderSupplies
            ? $"Order {packs}x {item}{(express ? " (express)" : "")}"
            : type.ToString();
    }

    /// <summary>Whether an action is possible now, and if not, why (shown on greyed-out buttons).</summary>
    public readonly struct ActionCheck
    {
        public readonly bool Allowed;
        public readonly string Reason;

        ActionCheck(bool allowed, string reason)
        {
            Allowed = allowed;
            Reason = reason;
        }

        public static readonly ActionCheck Ok = new ActionCheck(true, null);
        public static ActionCheck No(string reason) => new ActionCheck(false, reason);

        public override string ToString() => Allowed ? "allowed" : Reason;
    }

    /// <summary>
    /// Drives one parent. The simulation asks its controller for a decision every in-game minute;
    /// this is the only way actions enter the simulation (design Section 19.4). The player, test
    /// bots, and later the partner AI and a second player are all controllers.
    /// </summary>
    public interface IParentController
    {
        /// <summary>
        /// Returns the command to start now, or null to keep doing the current action (or nothing).
        /// A command replaces the action in progress.
        /// </summary>
        ParentCommand Decide(CareSimulation sim);
    }

    /// <summary>Controller for a human player: the UI enqueues commands, the simulation takes them in order.</summary>
    public sealed class QueuedController : IParentController
    {
        readonly System.Collections.Generic.Queue<ParentCommand> _queue = new System.Collections.Generic.Queue<ParentCommand>();

        public void Enqueue(ParentCommand command) => _queue.Enqueue(command);

        public int Pending => _queue.Count;

        public ParentCommand Decide(CareSimulation sim) => _queue.Count > 0 ? _queue.Dequeue() : null;
    }
}
