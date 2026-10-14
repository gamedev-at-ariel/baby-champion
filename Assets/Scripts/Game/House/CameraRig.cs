using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>
    /// 3/4 top-down camera (design Section 15.2): follows the parent with room-based framing,
    /// zooms (scroll, pinch) and rotates in 90-degree steps (right-drag, two-finger twist, Q/E).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] HouseGame game;
        [SerializeField] float pitch = 52f;
        [SerializeField] float minDistance = 6f;
        [SerializeField] float maxDistance = 22f;
        [SerializeField] float distance = 13f;
        /// <summary>0 frames the parent's room, 1 follows the parent exactly.</summary>
        [SerializeField, Range(0f, 1f)] float followParent = 0.5f;
        [SerializeField] float smoothTime = 0.25f;

        Vector3 _focus, _focusVelocity;
        float _yaw, _yawVelocity;
        Room _framedRoom;

        public Camera Camera => viewCamera;
        public int YawSteps { get; private set; }
        public float TargetYaw => YawSteps * 90f;
        public float Distance => distance;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;

        void Start() => SnapToTarget();

        public void Zoom(float factor) => distance = Mathf.Clamp(distance * factor, minDistance, maxDistance);

        public void SetDistance(float value) => distance = Mathf.Clamp(value, minDistance, maxDistance);

        /// <summary>Rotates by whole 90-degree steps; positive is clockwise seen from above.</summary>
        public void Rotate(int steps) => YawSteps = ((YawSteps + steps) % 4 + 4) % 4;

        /// <summary>Frames a room exactly (screenshots); null returns to following the parent.</summary>
        public void FrameRoom(Room room)
        {
            _framedRoom = room;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            _focus = Focus();
            _focusVelocity = Vector3.zero;
            _yaw = TargetYaw;
            _yawVelocity = 0;
            Place();
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _focus = Vector3.SmoothDamp(_focus, Focus(), ref _focusVelocity, smoothTime, Mathf.Infinity, dt);
            _yaw = Mathf.SmoothDampAngle(_yaw, TargetYaw, ref _yawVelocity, smoothTime, Mathf.Infinity, dt);
            Place();
        }

        Vector3 Focus()
        {
            if (_framedRoom != null) return _framedRoom.Center;
            if (game == null || game.Parent == null) return Vector3.zero;
            var parentPosition = game.Parent.transform.position;
            var room = game.RoomAt(parentPosition);
            var focus = room == null ? parentPosition : Vector3.Lerp(room.Center, parentPosition, followParent);
            focus.y = 0;
            return focus;
        }

        void Place()
        {
            if (viewCamera == null) return;
            var rotation = Quaternion.Euler(pitch, _yaw, 0f);
            viewCamera.transform.SetPositionAndRotation(_focus + rotation * new Vector3(0f, 0f, -distance), rotation);
        }
    }
}
