using BabyChampion.Simulation;
using UnityEngine;

namespace BabyChampion.Game
{
    public enum ParentPose { Stand, Sit, Lie, Kneel }
    public enum BabyPose { Lying, Sitting, Reclined }

    /// <summary>
    /// A piece of furniture the player can tap: it offers actions in the radial menu, and the parent
    /// walks to its stand point to use it.
    /// </summary>
    public sealed class Interactable : MonoBehaviour
    {
        public string displayName;
        public ActionType[] actions = new ActionType[0];
        /// <summary>A safe place to leave the baby (crib, bouncer, play mat).</summary>
        public bool babySpot;
        public BabyPose babyPose = BabyPose.Lying;
        /// <summary>The parent's pose while using it for a seated action (feeding, eating, resting).</summary>
        public ParentPose seatPose = ParentPose.Stand;
        /// <summary>Where the parent stands to use it; on the NavMesh.</summary>
        public Transform standPoint;
        /// <summary>Where the parent's body goes when sitting or lying on it.</summary>
        public Transform usePoint;
        /// <summary>Where the baby lies or sits on it.</summary>
        public Transform babyPoint;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public bool Offers(ActionType type) => System.Array.IndexOf(actions, type) >= 0;

        public Vector3 StandPosition => standPoint != null ? standPoint.position : transform.position;

        /// <summary>The direction the parent faces while using it.</summary>
        public Vector3 FacePosition => usePoint != null ? usePoint.position : transform.position;
    }
}
