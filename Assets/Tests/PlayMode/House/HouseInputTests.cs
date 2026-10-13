using System.Collections;
using BabyChampion.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace BabyChampion.Tests.PlayMode
{
    /// <summary>
    /// Simulated mouse and touch input in the house: tap to walk, tap the baby for the radial menu
    /// and tap one of its buttons, pinch to zoom, two-finger twist to rotate, scroll and right-drag.
    /// </summary>
    public class HouseInputTests : InputTestFixture
    {
        readonly HouseFixture _house = new HouseFixture();

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        // Devices are added after the scene load: in batch mode, a device added before it can lose its state.
        static TDevice AddDevice<TDevice>() where TDevice : InputDevice
        {
            var device = InputSystem.AddDevice<TDevice>();
            if (!device.enabled) InputSystem.EnableDevice(device);
            return device;
        }

        Vector2 ScreenPoint(Vector3 world)
        {
            var p = _house.Rig.Camera.WorldToScreenPoint(world);
            return new Vector2(p.x, p.y);
        }

        IEnumerator ClickAt(Mouse mouse, Vector2 position)
        {
            Set(mouse.position, position);
            yield return null;
            Click(mouse.leftButton);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseClick_OnTheFloor_WalksThere()
        {
            yield return _house.Load();
            _house.Start();
            var mouse = AddDevice<Mouse>();
            yield return null;

            // Open floor between the parent's start and the kitchen, clear of the play mat and the bouncer.
            var target = _house.Game.Parent.transform.position + new Vector3(1.5f, 0f, -0.6f);
            target.y = 0f;
            yield return ClickAt(mouse, ScreenPoint(target));
            Assert.IsTrue(_house.Game.Parent.Walking || Vector3.Distance(_house.Game.Parent.transform.position, target) < 0.5f, "the click did not start a walk");
            var destination = _house.Game.Parent.Destination;
            Assert.Less(new Vector2(destination.x - target.x, destination.z - target.z).magnitude, 0.3f);
        }

        [UnityTest]
        public IEnumerator Tap_OnTheBaby_OpensTheRadial_AndTappingSootheStartsIt()
        {
            yield return _house.Load();
            _house.Start();
            var touchscreen = AddDevice<Touchscreen>();
            yield return null;

            var baby = ScreenPoint(_house.Game.Baby.transform.position + Vector3.up * 0.3f);
            BeginTouch(1, baby, screen: touchscreen);
            yield return null;
            EndTouch(1, baby, screen: touchscreen);
            yield return null;
            Assert.IsTrue(_house.Hud.RadialOpen, "tapping the baby did not open the radial menu");
            Assert.AreEqual("Baby", _house.Hud.RadialTitle);

            int soothe = _house.Hud.RadialIndexOf("Soothe");
            Assert.GreaterOrEqual(soothe, 0);
            yield return null; // let the panel lay the buttons out
            var button = _house.Hud.ElementScreenCenter(_house.Hud.RadialButtons[soothe]);
            Assert.IsTrue(_house.Hud.IsOverUi(button), "the Soothe button is not under its own centre");
            BeginTouch(2, button, screen: touchscreen);
            yield return null;
            EndTouch(2, button, screen: touchscreen);
            yield return null;
            Assert.IsFalse(_house.Hud.RadialOpen, "tapping a button did not close the menu");

            Time.timeScale = 3f;
            yield return _house.WaitUntilSettled();
            Assert.IsTrue(_house.HasEvent("Started Soothe"), "Soothe did not start: " + string.Join(" | ", _house.Events));
            Assert.IsTrue(_house.Game.BabyCarried);
        }

        [UnityTest]
        public IEnumerator Tap_OnTheHud_DoesNotWalk()
        {
            yield return _house.Load();
            _house.Start();
            var mouse = AddDevice<Mouse>();
            yield return null;
            yield return null;

            var pause = _house.Hud.ElementScreenCenter(_house.Hud.PauseButton);
            Assert.IsTrue(_house.Hud.IsOverUi(pause));
            yield return ClickAt(mouse, pause);
            yield return null;
            Assert.IsFalse(_house.Game.Parent.Walking, "a click on the HUD walked the parent");
            Assert.IsTrue(_house.Game.Paused, "the Pause button did not pause");
            Assert.IsTrue(_house.Hud.PauseMenuOpen);
            _house.Hud.SetPaused(false);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Pinch_ZoomsInAndOut()
        {
            yield return _house.Load();
            _house.Start();
            var touchscreen = AddDevice<Touchscreen>();
            yield return null;

            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float start = _house.Rig.Distance;
            yield return TwoFingers(touchscreen, center, 60f, 220f, 0f, 0f);
            float zoomedIn = _house.Rig.Distance;
            Assert.Less(zoomedIn, start * 0.75f, "spreading two fingers did not zoom in");

            yield return TwoFingers(touchscreen, center, 220f, 60f, 0f, 0f);
            Assert.Greater(_house.Rig.Distance, zoomedIn * 1.5f, "pinching two fingers did not zoom out");
            Assert.IsFalse(_house.Hud.RadialOpen);
            Assert.IsFalse(_house.Game.Parent.Walking, "a gesture was taken as a tap");
        }

        [UnityTest]
        public IEnumerator TwoFingerTwist_RotatesInNinetyDegreeSteps()
        {
            yield return _house.Load();
            _house.Start();
            var touchscreen = AddDevice<Touchscreen>();
            yield return null;

            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Assert.AreEqual(0, _house.Rig.YawSteps);
            yield return TwoFingers(touchscreen, center, 120f, 120f, 0f, 60f);
            Assert.AreEqual(1, _house.Rig.YawSteps, "a 60-degree counter-clockwise twist did not rotate one step");
            yield return TwoFingers(touchscreen, center, 120f, 120f, 0f, -60f);
            Assert.AreEqual(0, _house.Rig.YawSteps, "twisting back did not rotate back");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(_house.Rig.Camera.transform.eulerAngles.y, 0f), 2f, "the camera did not settle on the step");
        }

        /// <summary>Two touches around a centre, from one spread and angle to another, in small steps.</summary>
        IEnumerator TwoFingers(Touchscreen screen, Vector2 center, float fromSpread, float toSpread, float fromAngle, float toAngle)
        {
            const int steps = 8;
            Vector2 Finger(float spread, float angle, int sign)
            {
                float rad = angle * Mathf.Deg2Rad;
                return center + sign * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * spread / 2f;
            }
            BeginTouch(1, Finger(fromSpread, fromAngle, -1), screen: screen);
            BeginTouch(2, Finger(fromSpread, fromAngle, 1), screen: screen);
            yield return null;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float spread = Mathf.Lerp(fromSpread, toSpread, t);
                float angle = Mathf.Lerp(fromAngle, toAngle, t);
                MoveTouch(1, Finger(spread, angle, -1), screen: screen);
                MoveTouch(2, Finger(spread, angle, 1), screen: screen);
                yield return null;
            }
            EndTouch(1, Finger(toSpread, toAngle, -1), screen: screen);
            EndTouch(2, Finger(toSpread, toAngle, 1), screen: screen);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseScroll_Zooms_And_RightDrag_Rotates()
        {
            yield return _house.Load();
            _house.Start();
            var mouse = AddDevice<Mouse>();
            yield return null;

            Set(mouse.position, new Vector2(Screen.width / 2f, Screen.height / 2f));
            float start = _house.Rig.Distance;
            Set(mouse.scroll, new Vector2(0, 120));
            yield return null;
            yield return null;
            Assert.Less(_house.Rig.Distance, start, "scrolling up did not zoom in");

            Press(mouse.rightButton);
            yield return null;
            for (int i = 0; i < 3; i++)
            {
                Set(mouse.delta, new Vector2(40, 0));
                yield return null;
            }
            Release(mouse.rightButton);
            yield return null;
            Assert.AreEqual(1, _house.Rig.YawSteps, "right-dragging did not rotate one step");
        }
    }
}
