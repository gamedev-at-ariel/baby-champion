using System.Collections;
using BabyChampion.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BabyChampion.Tests.PlayMode
{
    /// <summary>Simulated pointer input in the M0 test scene moves the cube to the tapped point.</summary>
    public class ClickToMoveTests : InputTestFixture
    {
        static readonly Vector3 Offset = new Vector3(3f, 0f, 2f);

        ClickToMove _mover;
        Camera _camera;

        IEnumerator LoadTestScene()
        {
            yield return SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Single);
            _mover = Object.FindAnyObjectByType<ClickToMove>();
            _camera = Camera.main;
            Assert.IsNotNull(_mover, "TestScene has no ClickToMove");
            Assert.IsNotNull(_camera, "TestScene has no main camera");
        }

        // Devices are added after the scene load: in batch mode, a device added before it can lose its state.
        static TDevice AddDevice<TDevice>() where TDevice : InputDevice
        {
            var device = InputSystem.AddDevice<TDevice>();
            if (!device.enabled) InputSystem.EnableDevice(device);
            return device;
        }

        Vector2 ScreenPointOnFloor(Vector3 world) => _camera.WorldToScreenPoint(new Vector3(world.x, 0f, world.z));

        IEnumerator AssertMovesTo(Vector3 target)
        {
            Assert.IsTrue(_mover.Target.HasValue, "The tap did not set a move target");
            yield return new WaitForSeconds(2f);
            var position = _mover.transform.position;
            Assert.AreEqual(target.x, position.x, 0.05f);
            Assert.AreEqual(target.z, position.z, 0.05f);
        }

        [UnityTest]
        public IEnumerator Tap_OnTouchscreen_MovesCube()
        {
            yield return LoadTestScene();
            var touchscreen = AddDevice<Touchscreen>();
            yield return null;

            var target = _mover.transform.position + Offset;
            var screen = ScreenPointOnFloor(target);
            BeginTouch(1, screen, screen: touchscreen);
            yield return null;
            EndTouch(1, screen, screen: touchscreen);
            yield return null;

            yield return AssertMovesTo(target);
        }

        [UnityTest]
        public IEnumerator Click_WithMouse_MovesCube()
        {
            yield return LoadTestScene();
            var mouse = AddDevice<Mouse>();
            yield return null;

            var target = _mover.transform.position - Offset;
            Set(mouse.position, ScreenPointOnFloor(target));
            yield return null;
            Click(mouse.leftButton);
            yield return null;

            yield return AssertMovesTo(target);
        }
    }
}
