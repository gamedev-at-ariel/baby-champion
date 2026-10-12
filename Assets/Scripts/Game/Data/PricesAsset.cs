using BabyChampion.Simulation;
using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>Supply prices and delivery times as an editable asset.</summary>
    [CreateAssetMenu(menuName = "Baby Champion/Prices", fileName = "Prices")]
    public sealed class PricesAsset : ScriptableObject
    {
        public PriceList prices = PriceList.CreateDefault();
    }
}
