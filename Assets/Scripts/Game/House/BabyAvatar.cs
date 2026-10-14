using BabyChampion.Simulation;
using UnityEngine;

namespace BabyChampion.Game
{
    public enum BabyDisplayPose { Lying, Sitting, Reclined, InArms, OnShoulder }

    /// <summary>
    /// The placeholder baby: follows its anchor (a furniture spot or the parent's arms) and shows
    /// crying, sleep and the body-language cause hints of design Section 7.1 with simple procedural
    /// animation: hand to mouth (hungry), rubbing eyes (tired), squirming (diaper or gas),
    /// fussing (upset or bored). Driven by <see cref="HouseGame"/>.
    /// </summary>
    public sealed class BabyAvatar : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Transform body;
        [SerializeField] Renderer head;
        [SerializeField] Transform mouth;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [SerializeField] Transform[] tears = new Transform[0];
        [SerializeField] Transform[] sleepBubbles = new Transform[0];
        /// <summary>Coloured disc under the baby: yellow, red while crying; hidden while carried.</summary>
        [SerializeField] Renderer ring;
        [SerializeField] Color ringColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color skin = new Color(1f, 0.82f, 0.68f);
        [SerializeField] Color cryingFlush = new Color(0.95f, 0.35f, 0.3f);

        MaterialPropertyBlock _block;
        Vector3 _leftRest, _rightRest, _mouthScale;

        public BabyDisplayPose Pose { get; private set; }
        public CryHint Hint { get; private set; }
        public bool Crying { get; private set; }
        public bool Asleep { get; private set; }

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (leftHand != null) _leftRest = leftHand.localPosition;
            if (rightHand != null) _rightRest = rightHand.localPosition;
            if (mouth != null) _mouthScale = mouth.localScale;
        }

        public void Show(Transform anchor, BabyDisplayPose pose, BabyView baby)
        {
            Pose = pose;
            Crying = baby.Crying;
            Asleep = baby.Asleep;
            Hint = baby.Crying ? baby.Hint : CryHint.None;
            float t = Time.time;

            if (anchor != null) transform.SetPositionAndRotation(anchor.position, anchor.rotation);

            var rotation = Quaternion.identity;
            switch (pose)
            {
                case BabyDisplayPose.Lying: rotation = Quaternion.Euler(-90f, 0f, 0f); break;
                case BabyDisplayPose.Reclined: rotation = Quaternion.Euler(-35f, 0f, 0f); break;
                case BabyDisplayPose.InArms: rotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-70f, 0f, 0f); break;
            }
            if (Hint == CryHint.Squirming) rotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 25f);
            if (Hint == CryHint.Fussing) rotation *= Quaternion.Euler(Mathf.Sin(t * 6f) * 8f, 0f, 0f);
            if (body != null)
            {
                body.localRotation = rotation;
                body.localPosition = Hint == CryHint.Fussing ? Vector3.up * Mathf.Abs(Mathf.Sin(t * 7f)) * 0.04f : Vector3.zero;
            }

            AnimateHands(t);
            AnimateFace(t);

            if (ring != null)
            {
                ring.enabled = pose != BabyDisplayPose.InArms && pose != BabyDisplayPose.OnShoulder;
                _block.SetColor(BaseColor, Crying ? Color.Lerp(cryingFlush, Color.white, Mathf.Abs(Mathf.Sin(t * 5f)) * 0.4f) : ringColor);
                ring.SetPropertyBlock(_block);
            }
        }

        void AnimateHands(float t)
        {
            if (leftHand == null || rightHand == null) return;
            var left = _leftRest;
            var right = _rightRest;
            switch (Hint)
            {
                case CryHint.HandToMouth:
                    right = new Vector3(0.03f, 0.37f + Mathf.Sin(t * 8f) * 0.015f, 0.13f);
                    break;
                case CryHint.RubbingEyes:
                    left = new Vector3(-0.06f + Mathf.Cos(t * 10f) * 0.02f, 0.45f + Mathf.Sin(t * 10f) * 0.02f, 0.12f);
                    right = new Vector3(0.06f - Mathf.Cos(t * 10f) * 0.02f, 0.45f + Mathf.Sin(t * 10f) * 0.02f, 0.12f);
                    break;
                case CryHint.Fussing:
                    left = new Vector3(-0.16f, 0.27f + Mathf.Sin(t * 7f) * 0.07f, 0.05f);
                    right = new Vector3(0.16f, 0.27f + Mathf.Sin(t * 7f + 1.5f) * 0.07f, 0.05f);
                    break;
            }
            leftHand.localPosition = left;
            rightHand.localPosition = right;
        }

        void AnimateFace(float t)
        {
            if (mouth != null)
                mouth.localScale = Crying ? new Vector3(_mouthScale.x, _mouthScale.y * (1.6f + Mathf.Sin(t * 12f) * 0.8f), _mouthScale.z) : _mouthScale;

            if (head != null)
            {
                float flush = Crying ? 0.45f + Mathf.Sin(t * 6f) * 0.15f : 0f;
                _block.SetColor(BaseColor, Color.Lerp(skin, cryingFlush, flush));
                head.SetPropertyBlock(_block);
            }

            for (int i = 0; i < tears.Length; i++)
            {
                tears[i].gameObject.SetActive(Crying);
                if (!Crying) continue;
                float phase = Mathf.Repeat(t * 1.5f + i * 0.5f, 1f);
                var p = tears[i].localPosition;
                tears[i].localPosition = new Vector3(p.x, 0.44f - phase * 0.2f, p.z);
            }

            for (int i = 0; i < sleepBubbles.Length; i++)
            {
                sleepBubbles[i].gameObject.SetActive(Asleep);
                if (!Asleep) continue;
                float phase = Mathf.Repeat(t * 0.4f + i / (float)sleepBubbles.Length, 1f);
                sleepBubbles[i].position = head.transform.position + new Vector3(0.1f + phase * 0.15f, 0.15f + phase * 0.45f, 0f);
                sleepBubbles[i].localScale = Vector3.one * (0.03f + phase * 0.06f);
            }
        }
    }
}
