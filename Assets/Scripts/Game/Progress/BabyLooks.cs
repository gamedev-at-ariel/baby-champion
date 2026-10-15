using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>The appearance choices of the baby setup screen (placeholder palettes until the art pass).</summary>
    public static class BabyLooks
    {
        public static readonly Color[] SkinTones =
        {
            new Color(1f, 0.85f, 0.73f),
            new Color(0.93f, 0.74f, 0.58f),
            new Color(0.76f, 0.55f, 0.4f),
            new Color(0.5f, 0.34f, 0.24f),
        };

        public static readonly string[] OutfitNames = { "Sunny", "Mint", "Sky", "Rose" };

        public static readonly Color[] Outfits =
        {
            new Color(0.98f, 0.9f, 0.5f),
            new Color(0.6f, 0.88f, 0.7f),
            new Color(0.62f, 0.78f, 0.98f),
            new Color(0.98f, 0.7f, 0.78f),
        };

        public static Color Skin(int index) => SkinTones[Mathf.Clamp(index, 0, SkinTones.Length - 1)];
        public static Color Outfit(int index) => Outfits[Mathf.Clamp(index, 0, Outfits.Length - 1)];
    }
}
