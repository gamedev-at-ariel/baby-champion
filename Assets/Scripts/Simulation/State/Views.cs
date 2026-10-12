namespace BabyChampion.Simulation
{
    /// <summary>Read-only view of the baby, so controllers and UI can look but not change state.</summary>
    public readonly struct BabyView
    {
        readonly BabyState _b;
        public BabyView(BabyState b) => _b = b;

        public double Hunger => _b.hunger;
        public double Tiredness => _b.tiredness;
        public double Tension => _b.tension;
        public double Boredom => _b.boredom;
        public double Wellbeing => _b.wellbeing;
        public DiaperState Diaper => _b.diaper;
        public double SoiledMinutes => _b.soiledMinutes;
        public bool Rash => _b.rash;
        public bool Asleep => _b.asleep;
        public bool NeedsBurp => _b.needsBurp;
        public bool Gas => _b.gas;
        public bool Crying => _b.Crying;
        public CryCause CryCauses => _b.cryCauses;
        public CryHint Hint => CryHints.For(_b.cryCauses);
        public double CryStreakMinutes => _b.cryStreakMinutes;
    }

    public readonly struct ParentView
    {
        readonly ParentState _p;
        public ParentView(ParentState p) => _p = p;

        public double Energy => _p.energy;
        public double Hunger => _p.hunger;
        public double Stress => _p.stress;
        public double Joy => _p.joy;
        public bool Busy => _p.action != null;
        /// <summary>The action in progress; meaningful only when <see cref="Busy"/>.</summary>
        public ActionType Action => _p.action?.type ?? ActionType.Stop;
        public bool ActionIsContinuous => _p.action != null && _p.action.Continuous;
        public double ActionProgress => _p.action?.Progress ?? 0;
        public double ActionElapsedMinutes => _p.action?.elapsedMinutes ?? 0;
        public double ActionDurationMinutes => _p.action?.durationMinutes ?? 0;
    }

    public readonly struct SupplyView
    {
        readonly SupplyStock _s;
        public SupplyView(SupplyStock s) => _s = s;

        public int Diapers => _s.diapers;
        public int Wipes => _s.wipes;
        public int Formula => _s.formula;
        public int BabyFood => _s.babyFood;
        public int AdultFood => _s.adultFood;
        public int BottlesReady => _s.bottlesReady;
        public int Get(SupplyType item) => _s.Get(item);
    }

    public readonly struct DeliveryInfo
    {
        public readonly SupplyType Item;
        public readonly int Quantity;
        public readonly double ArrivesAtMinute;

        public DeliveryInfo(SupplyType item, int quantity, double arrivesAtMinute)
        {
            Item = item;
            Quantity = quantity;
            ArrivesAtMinute = arrivesAtMinute;
        }
    }

    /// <summary>Maps crying causes to the visible hints of design Section 7.1.</summary>
    public static class CryHints
    {
        /// <summary>The most urgent cause's hint.</summary>
        public static CryHint For(CryCause causes)
        {
            if ((causes & CryCause.Hunger) != 0) return CryHint.HandToMouth;
            if ((causes & (CryCause.Diaper | CryCause.Gas)) != 0) return CryHint.Squirming;
            if ((causes & CryCause.Overtired) != 0) return CryHint.RubbingEyes;
            if ((causes & (CryCause.Upset | CryCause.Bored)) != 0) return CryHint.Fussing;
            return CryHint.None;
        }

        public static string HintText(CryHint hint)
        {
            switch (hint)
            {
                case CryHint.HandToMouth: return "hand to mouth";
                case CryHint.Squirming: return "squirming";
                case CryHint.RubbingEyes: return "rubbing eyes";
                case CryHint.Fussing: return "fussing";
                default: return "";
            }
        }

        /// <summary>Hint plus the causes, for logs and the debug scene.</summary>
        public static string Describe(CryCause causes) => $"{HintText(For(causes))} ({causes})";
    }
}
