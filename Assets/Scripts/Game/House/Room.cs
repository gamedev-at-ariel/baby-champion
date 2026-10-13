using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>A room of the house (design Section 12): its floor area frames the camera.</summary>
    public sealed class Room : MonoBehaviour
    {
        public string displayName;
        /// <summary>Floor area in world space (x, z); y is ignored.</summary>
        public Rect area;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public Vector3 Center => new Vector3(area.center.x, 0f, area.center.y);

        public bool Contains(Vector3 world) => area.Contains(new Vector2(world.x, world.z));
    }
}
