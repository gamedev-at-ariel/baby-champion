using BabyChampion.Simulation;
using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>Age-rate table (design Section 7.4) as an editable asset.</summary>
    [CreateAssetMenu(menuName = "Baby Champion/Age Rates", fileName = "AgeRates")]
    public sealed class AgeRatesAsset : ScriptableObject
    {
        public AgeRateTable table = AgeRateTable.CreateDefault();
    }
}
