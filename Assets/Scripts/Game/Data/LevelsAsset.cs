using BabyChampion.Simulation;
using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>Chapters and level definitions (design Section 14) as an editable asset.</summary>
    [CreateAssetMenu(menuName = "Baby Champion/Levels", fileName = "Levels")]
    public sealed class LevelsAsset : ScriptableObject
    {
        public LevelTable table = LevelTable.CreateDefault();
    }
}
