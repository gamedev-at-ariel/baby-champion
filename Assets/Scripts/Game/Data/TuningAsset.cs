using BabyChampion.Simulation;
using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>Rule thresholds, rates and day-start values as an editable asset.</summary>
    [CreateAssetMenu(menuName = "Baby Champion/Tuning", fileName = "Tuning")]
    public sealed class TuningAsset : ScriptableObject
    {
        public Tuning tuning = Tuning.CreateDefault();
    }
}
