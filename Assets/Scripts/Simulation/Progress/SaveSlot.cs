using System;
using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    public enum BabySex { Girl, Boy }

    /// <summary>Money and supplies, which carry over from level to level (design Section 10).</summary>
    [Serializable]
    public class Household
    {
        public int money;
        public SupplyStock supplies = new SupplyStock();

        public static Household FromTuning(Tuning tuning)
        {
            var s = tuning.start;
            return new Household
            {
                money = s.money,
                supplies = new SupplyStock { diapers = s.diapers, wipes = s.wipes, formula = s.formula, babyFood = s.babyFood, adultFood = s.adultFood },
            };
        }

        public Household Copy() => JsonCodec.Deserialize<Household>(JsonCodec.Serialize(this));
    }

    /// <summary>Progress in one level of one save slot.</summary>
    [Serializable]
    public class LevelRecord
    {
        public int level;
        public int bestStars;
        public int attempts;
        public int completions;
    }

    /// <summary>
    /// One baby's playthrough (design Section 19.6): the setup choices, progress per level, and the
    /// money and supplies the next level starts with. Saved as JSON at the end of every level.
    /// </summary>
    [Serializable]
    public class SaveSlot
    {
        public const int CurrentVersion = 1;
        public const int MaxNameLength = 20;

        public int version = CurrentVersion;
        public string babyName;
        public BabySex sex;
        /// <summary>Index into the game's skin-tone palette.</summary>
        public int skinTone;
        /// <summary>Index into the game's outfit palette.</summary>
        public int outfit;
        public FeedingChoice feeding;
        /// <summary>What the next level starts with. Updated only when a level is completed, so a retry starts the same.</summary>
        public Household household = new Household();
        public List<LevelRecord> levels = new List<LevelRecord>();
        /// <summary>The level played last, for "Continue".</summary>
        public int lastLevel;

        public static SaveSlot Create(string name, BabySex sex, int skinTone, int outfit, FeedingChoice feeding, Tuning tuning) => new SaveSlot
        {
            babyName = CleanName(name),
            sex = sex,
            skinTone = skinTone,
            outfit = outfit,
            feeding = feeding,
            household = Household.FromTuning(tuning),
        };

        /// <summary>Trimmed, at most <see cref="MaxNameLength"/> characters, "Baby" when empty.</summary>
        public static string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength).Trim();
            return name.Length == 0 ? "Baby" : name;
        }

        public LevelRecord Record(int level) => levels.Find(r => r.level == level);

        public int BestStars(int level) => Record(level)?.bestStars ?? 0;

        public bool Completed(int level) => (Record(level)?.completions ?? 0) > 0;

        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (var r in levels) total += r.bestStars;
                return total;
            }
        }

        /// <summary>Level 1 is always open; every other level opens when the one before it is completed.</summary>
        public bool IsUnlocked(int level) => level == 1 || (level > 1 && Completed(level - 1));

        /// <summary>The first built level that is unlocked but not completed; otherwise the last built level that is unlocked.</summary>
        public int NextLevel(LevelTable table)
        {
            int best = 1;
            foreach (var level in table.levels)
            {
                if (!IsUnlocked(level.number)) continue;
                if (!Completed(level.number)) return level.number;
                best = Math.Max(best, level.number);
            }
            return best;
        }

        /// <summary>Records a finished level. A completed level also carries its end money and supplies to the next one.</summary>
        public void RecordResult(LevelResult result, Household atEnd)
        {
            var record = Record(result.Level);
            if (record == null)
            {
                record = new LevelRecord { level = result.Level };
                levels.Add(record);
                levels.Sort((a, b) => a.level.CompareTo(b.level));
            }
            record.attempts++;
            lastLevel = result.Level;
            if (!result.Completed) return;
            record.completions++;
            record.bestStars = Math.Max(record.bestStars, result.Stars);
            household = atEnd.Copy();
        }

        public string ToJson() => JsonCodec.Serialize(this, pretty: true);

        public static SaveSlot FromJson(string json)
        {
            var slot = JsonCodec.Deserialize<SaveSlot>(json);
            if (slot.version != CurrentVersion) throw new FormatException($"Unsupported save version {slot.version}");
            slot.babyName = CleanName(slot.babyName);
            slot.household = slot.household ?? new Household();
            slot.household.supplies = slot.household.supplies ?? new SupplyStock();
            slot.levels = slot.levels ?? new List<LevelRecord>();
            return slot;
        }
    }
}
