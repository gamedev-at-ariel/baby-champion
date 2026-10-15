using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace BabyChampion.Game
{
    /// <summary>
    /// World input for the house (design Sections 15.1-15.2 and 19.2). Mouse and touch share the
    /// Pointer actions for taps; Enhanced Touch adds pinch (zoom) and two-finger twist (rotate).
    /// Desktop extras: scroll wheel zooms, right-drag rotates, Q/E rotate, Space pauses.
    /// Taps that start on the HUD are left to the HUD.
    /// </summary>
    public sealed class PointerInput : MonoBehaviour
    {
        [SerializeField] InputActionReference point;
        [SerializeField] InputActionReference click;
        [SerializeField] InputActionReference scroll;
        [SerializeField] InputActionReference drag;
        [SerializeField] InputActionReference rotateDrag;
        [SerializeField] InputActionReference rotateLeft;
        [SerializeField] InputActionReference rotateRight;
        [SerializeField] InputActionReference pause;
        [SerializeField] HouseGame game;
        [SerializeField] CameraRig rig;
        [SerializeField] Hud hud;
        [SerializeField] float tapMaxMovePixels = 24f;
        [SerializeField] float rotateDragPixels = 90f;
        [SerializeField] float twistDegrees = 35f;

        Vector2 _pressPosition;
        bool _pressed, _pressOverUi, _gesture;
        float _dragAccumulated;
        bool _twoFingers;
        float _lastPinchDistance, _lastTwistAngle, _twistAccumulated;

        InputActionReference[] All => new[] { point, click, scroll, drag, rotateDrag, rotateLeft, rotateRight, pause };

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            foreach (var reference in All)
                if (reference != null) reference.action.Enable();
            click.action.performed += OnPress;
            click.action.canceled += OnRelease;
        }

        void OnDisable()
        {
            click.action.performed -= OnPress;
            click.action.canceled -= OnRelease;
            if (EnhancedTouchSupport.enabled) EnhancedTouchSupport.Disable();
        }

        void Update()
        {
            if (game == null || rig == null) return;
            // The Input System can be reset under us (InputTestFixture); Enhanced Touch must be on to read touches.
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            HandleTouchGestures();
            HandleDesktop();
        }

        // Callbacks rather than polling: a touch press is reliably reported to performed/canceled.
        void OnPress(InputAction.CallbackContext context)
        {
            _pressed = true;
            _pressPosition = point.action.ReadValue<Vector2>();
            _pressOverUi = hud != null && hud.IsOverUi(_pressPosition);
            _gesture = EnhancedTouchSupport.enabled && Touch.activeTouches.Count > 1;
        }

        void OnRelease(InputAction.CallbackContext context)
        {
            if (!_pressed) return;
            _pressed = false;
            var position = point.action.ReadValue<Vector2>();
            bool still = (position - _pressPosition).magnitude <= tapMaxMovePixels;
            if (!_pressOverUi && !_gesture && still && game != null && rig != null && !game.Paused && !game.WaitingToStart) game.Tap(_pressPosition, rig.Camera);
        }

        void HandleTouchGestures()
        {
            var touches = Touch.activeTouches;
            if (touches.Count < 2)
            {
                _twoFingers = false;
                _twistAccumulated = 0;
                return;
            }

            _gesture = true;
            var a = touches[0].screenPosition;
            var b = touches[1].screenPosition;
            float pinch = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            if (_twoFingers)
            {
                if (pinch > 1f && _lastPinchDistance > 1f) rig.Zoom(_lastPinchDistance / pinch);
                _twistAccumulated += Mathf.DeltaAngle(_lastTwistAngle, angle);
                if (Mathf.Abs(_twistAccumulated) >= twistDegrees)
                {
                    rig.Rotate(_twistAccumulated > 0 ? 1 : -1);
                    _twistAccumulated = 0;
                }
            }
            _twoFingers = true;
            _lastPinchDistance = pinch;
            _lastTwistAngle = angle;
        }

        void HandleDesktop()
        {
            float wheel = scroll != null ? scroll.action.ReadValue<Vector2>().y : 0f;
            if (Mathf.Abs(wheel) > 0.01f && !(hud != null && hud.IsOverUi(point.action.ReadValue<Vector2>())))
                rig.Zoom(wheel > 0 ? 0.88f : 1f / 0.88f);

            if (rotateDrag != null && rotateDrag.action.IsPressed())
            {
                _dragAccumulated += drag.action.ReadValue<Vector2>().x;
                if (Mathf.Abs(_dragAccumulated) >= rotateDragPixels)
                {
                    rig.Rotate(_dragAccumulated > 0 ? 1 : -1);
                    _dragAccumulated = 0;
                }
            }
            else _dragAccumulated = 0;

            if (rotateLeft != null && rotateLeft.action.WasPressedThisFrame()) rig.Rotate(-1);
            if (rotateRight != null && rotateRight.action.WasPressedThisFrame()) rig.Rotate(1);
            if (pause != null && pause.action.WasPressedThisFrame() && game.Sim != null && game.Sim.Outcome == Simulation.Outcome.Running && !game.WaitingToStart)
                game.Paused = !game.Paused;
        }
    }
}
