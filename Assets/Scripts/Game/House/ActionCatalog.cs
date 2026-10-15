using BabyChampion.Simulation;

namespace BabyChampion.Game
{
    /// <summary>How an action uses the house: where it happens and what it needs from the parent's hands.</summary>
    public sealed class ActionInfo
    {
        public ActionType Type;
        public string Label;
        /// <summary>The parent must hold the baby (they pick the baby up first if needed).</summary>
        public bool NeedsBaby;
        /// <summary>The parent's hands must be free: the baby has to be put down first.</summary>
        public bool HandsFree;
        /// <summary>Happens at a piece of furniture (design Section 9 "Where"); otherwise wherever the baby is.</summary>
        public bool AtStation;
        /// <summary>The baby is put down at the station before the action starts (play mat).</summary>
        public bool PlaceBabyFirst;
        /// <summary>The baby stays at the station when the action ends with the baby asleep (crib).</summary>
        public bool PlaceBabyWhenAsleep;
    }

    /// <summary>House rules for every parent action (design Section 9).</summary>
    public static class ActionCatalog
    {
        static readonly ActionInfo[] Infos =
        {
            new ActionInfo { Type = ActionType.Stop, Label = "Stop" },
            new ActionInfo { Type = ActionType.Breastfeed, Label = "Breastfeed", NeedsBaby = true, AtStation = true },
            new ActionInfo { Type = ActionType.PrepareBottle, Label = "Prepare bottle", HandsFree = true, AtStation = true },
            new ActionInfo { Type = ActionType.BottleFeed, Label = "Bottle-feed", NeedsBaby = true, AtStation = true },
            new ActionInfo { Type = ActionType.Burp, Label = "Burp", NeedsBaby = true },
            new ActionInfo { Type = ActionType.ChangeDiaper, Label = "Change diaper", NeedsBaby = true, AtStation = true },
            new ActionInfo { Type = ActionType.Soothe, Label = "Soothe", NeedsBaby = true },
            new ActionInfo { Type = ActionType.PutBabyToSleep, Label = "Put to sleep", NeedsBaby = true, AtStation = true, PlaceBabyWhenAsleep = true },
            new ActionInfo { Type = ActionType.Play, Label = "Play", NeedsBaby = true, AtStation = true, PlaceBabyFirst = true },
            new ActionInfo { Type = ActionType.Eat, Label = "Eat", HandsFree = true, AtStation = true },
            new ActionInfo { Type = ActionType.Rest, Label = "Rest", HandsFree = true, AtStation = true },
            new ActionInfo { Type = ActionType.Sleep, Label = "Sleep", HandsFree = true, AtStation = true },
            new ActionInfo { Type = ActionType.OrderSupplies, Label = "Order" },
            new ActionInfo { Type = ActionType.CallForHelp, Label = "Call Grandma" },
        };

        public static ActionInfo For(ActionType type)
        {
            foreach (var info in Infos)
                if (info.Type == type) return info;
            throw new System.ArgumentOutOfRangeException(nameof(type));
        }

        /// <summary>Actions offered when the baby is tapped, in radial order.</summary>
        public static readonly ActionType[] BabyActions =
        {
            ActionType.Breastfeed, ActionType.BottleFeed, ActionType.Burp, ActionType.ChangeDiaper,
            ActionType.Soothe, ActionType.PutBabyToSleep, ActionType.Play,
        };

        /// <summary>Feeding actions that the chosen feeding method rules out are hidden rather than greyed out.</summary>
        public static bool Offered(ActionType type, FeedingChoice feeding)
        {
            switch (type)
            {
                case ActionType.Breastfeed: return feeding != FeedingChoice.Formula;
                case ActionType.PrepareBottle:
                case ActionType.BottleFeed: return feeding != FeedingChoice.Breast;
                default: return true;
            }
        }
    }
}
