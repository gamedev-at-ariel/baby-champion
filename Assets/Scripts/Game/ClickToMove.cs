using UnityEngine;
using UnityEngine.InputSystem;

namespace BabyChampion.Game
{
    /// <summary>
    /// M0 test-scene behaviour: moves this object across the floor to the point the
    /// player clicks or taps. Mouse and touch share one path through the Pointer actions.
    /// </summary>
    public class ClickToMove : MonoBehaviour
    {
        [SerializeField] InputActionReference point;
        [SerializeField] InputActionReference click;
        [SerializeField] Camera viewCamera;
        [SerializeField] float speed = 5f;
        [SerializeField] LayerMask floorMask = ~0;

        Vector3? _target;

        public Vector3? Target => _target;

        void OnEnable()
        {
            point.action.Enable();
            click.action.Enable();
            click.action.performed += OnClick;
        }

        void OnDisable()
        {
            click.action.performed -= OnClick;
        }

        void OnClick(InputAction.CallbackContext context)
        {
            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(point.action.ReadValue<Vector2>());
            if (Physics.Raycast(ray, out var hit, 500f, floorMask, QueryTriggerInteraction.Ignore)
                && hit.transform != transform)
            {
                _target = new Vector3(hit.point.x, transform.position.y, hit.point.z);
            }
        }

        void Update()
        {
            if (_target is not Vector3 target) return;

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude < 0.0001f) _target = null;
        }
    }
}
