using BabyChampion.Simulation;

namespace BabyChampion.Game
{
    /// <summary>
    /// The tutorial of Level 1 (design Section 14): one short hint at a time, chosen from what is
    /// happening right now, most urgent first. Empty when there is nothing to say.
    /// </summary>
    public static class TutorialCoach
    {
        public static string Hint(CareSimulation sim, bool babyCarried)
        {
            if (sim == null || sim.Outcome != Outcome.Running) return "";
            var b = sim.Baby;
            var p = sim.Parent;
            var t = sim.Config.tuning;

            if (sim.HelpActive) return "Grandma is looking after the baby for a while - a good moment to eat or sleep (tap the bed).";
            if (b.Crying)
            {
                string why = (b.CryCauses & CryCause.Hunger) != 0 ? "hand to mouth means hungry: feed them"
                    : (b.CryCauses & CryCause.Gas) != 0 ? "squirming after a feed means tummy ache: burp them"
                    : (b.CryCauses & CryCause.Diaper) != 0 ? "squirming means the diaper needs changing"
                    : (b.CryCauses & CryCause.Overtired) != 0 ? "rubbing eyes means tired: put them to sleep in the crib"
                    : "fussing means upset: hold and soothe them";
                return $"Crying! Look at the body language - {why}. Tap the baby to act.";
            }
            if (b.NeedsBurp) return "Fed! Now burp the baby (tap the baby, then Burp), or a tummy ache follows.";
            if (sim.FeedsToday == 0 && sim.Minute < 20)
                return "Welcome home! Tap the floor to walk, and tap the baby to see what you can do. The icons at the top show what the baby needs.";
            if (b.Diaper != DiaperState.Clean)
                return "The diaper needs changing (the diaper icon is not a circle). Tap the baby and choose Change diaper.";
            if (!b.Asleep && b.Hunger >= t.hungerCryThreshold * 0.7)
                return "The hunger icon turned into a triangle: feed the baby now, before the crying starts.";
            if (!b.Asleep && b.Tiredness >= t.overtiredThreshold * 0.6)
                return "Sleepy! Put the baby to sleep in the crib (tap the baby, then Put to sleep).";
            if (b.Asleep && !babyCarried && p.Energy < 80 && !(p.Busy && (p.Action == ActionType.Sleep || p.Action == ActionType.Rest)))
                return "The baby is asleep. Sleep when the baby sleeps: tap the bed - time runs faster while you sleep.";
            if (p.Hunger >= t.parentHungerThreshold * 0.8) return "You're getting hungry: eat at the kitchen table (put the baby down first).";
            if (sim.LowOnSupplies) return "Running low on supplies: tap Phone to order more. Deliveries take 2 hours.";
            bool allDone = true;
            foreach (var goal in sim.Goals) allDone &= goal.Met;
            if (allDone) return "All tutorial goals done! Keep the baby happy until 07:00 tomorrow.";
            return "";
        }
    }
}
