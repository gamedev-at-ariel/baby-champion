using BabyChampion.Simulation;

namespace BabyChampion.Game
{
    /// <summary>
    /// What the menus hand to the house across the scene load: the save slot being played and the
    /// level to start. The house plays a free day when nothing is pending (tests, the Editor).
    /// </summary>
    public static class GameSession
    {
        public const string MenuScene = "Menu";
        public const string HouseScene = "House";

        /// <summary>The save slot (0-2) being played, or -1 for none (nothing is saved).</summary>
        public static int SlotIndex { get; private set; } = -1;
        public static SaveSlot Slot { get; private set; }
        /// <summary>The level the house starts when it loads; 0 for none. Taken once.</summary>
        public static int PendingLevel { get; set; }
        /// <summary>The menu opens on the level map instead of the title.</summary>
        public static bool ReturnToMap { get; set; }

        public static void Use(int slotIndex, SaveSlot slot)
        {
            SlotIndex = slot == null ? -1 : slotIndex;
            Slot = slot;
        }

        public static void Clear()
        {
            Use(-1, null);
            PendingLevel = 0;
            ReturnToMap = false;
        }

        public static int TakePendingLevel()
        {
            int level = PendingLevel;
            PendingLevel = 0;
            return level;
        }

        /// <summary>Writes the slot back to storage (after a level ends).</summary>
        public static void Save()
        {
            if (Slot != null && SlotIndex >= 0) SaveStore.Save(SlotIndex, Slot);
        }
    }
}
