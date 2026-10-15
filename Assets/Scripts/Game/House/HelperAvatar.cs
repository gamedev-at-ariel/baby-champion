using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>The placeholder grandparent who comes after a Call for help: stands by the baby while helping.</summary>
    public sealed class HelperAvatar : MonoBehaviour
    {
        [SerializeField] Transform body;

        public bool Visible => gameObject.activeSelf;

        /// <summary>Stands at <paramref name="position"/>, facing <paramref name="lookAt"/>, with a little bob while busy.</summary>
        public void Show(Vector3 position, Vector3 lookAt, bool busy)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            var flat = new Vector3(lookAt.x - position.x, 0f, lookAt.z - position.z);
            transform.SetPositionAndRotation(position, flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat) : transform.rotation);
            if (body != null)
                body.localRotation = busy ? Quaternion.Euler(18f + Mathf.Sin(Time.time * 4f) * 6f, 0f, 0f) : Quaternion.identity;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
