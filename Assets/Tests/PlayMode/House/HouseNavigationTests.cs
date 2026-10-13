using System.Collections;
using System.Collections.Generic;
using BabyChampion.Game;
using BabyChampion.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace BabyChampion.Tests.PlayMode
{
    /// <summary>The parent can reach every piece of furniture, and every action is offered somewhere.</summary>
    public class HouseNavigationTests
    {
        readonly HouseFixture _house = new HouseFixture();

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator EveryInteractable_HasAPathFromTheStart()
        {
            yield return _house.Load();
            Assert.That(_house.Game.Interactables.Length, Is.GreaterThanOrEqualTo(9));
            var start = _house.Game.Parent.transform.position;
            var path = new NavMeshPath();
            var failures = new List<string>();
            foreach (var target in _house.Game.Interactables)
            {
                bool sampled = NavMesh.SamplePosition(target.StandPosition, out var hit, 0.6f, NavMesh.AllAreas);
                if (!sampled) failures.Add($"{target.name}: stand point is not on the NavMesh");
                else if (!NavMesh.CalculatePath(start, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    failures.Add($"{target.name}: no complete path ({path.status})");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator Parent_WalksToEveryInteractable()
        {
            yield return _house.Load();
            _house.Start(minutesPerSecond: 0.01);
            Time.timeScale = 4f;
            var failures = new List<string>();
            foreach (var target in _house.Game.Interactables)
            {
                _house.Game.WalkTo(target.StandPosition);
                float end = Time.realtimeSinceStartup + 15f;
                yield return null;
                while (_house.Game.Parent.Walking && Time.realtimeSinceStartup < end) yield return null;
                var p = _house.Game.Parent.transform.position;
                float distance = new Vector2(p.x - target.StandPosition.x, p.z - target.StandPosition.z).magnitude;
                if (_house.Game.Parent.WalkFailed || distance > 0.6f) failures.Add($"{target.name}: stopped {distance:0.00} m away");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator EveryParentAction_IsOfferedByFurnitureOrTheBaby()
        {
            yield return _house.Load();
            var offered = new HashSet<ActionType>();
            foreach (var target in _house.Game.Interactables)
                foreach (var a in _house.Game.ActionsFor(target))
                    if (a.Verb == HouseVerb.Sim) offered.Add(a.Command.type);
            foreach (var a in _house.Game.ActionsForBaby())
                if (a.Verb == HouseVerb.Sim) offered.Add(a.Command.type);

            foreach (ActionType type in System.Enum.GetValues(typeof(ActionType)))
            {
                if (type == ActionType.Stop || type == ActionType.OrderSupplies) continue; // HUD: Stop button and phone
                Assert.IsTrue(offered.Contains(type), $"{type} is not offered anywhere in the house");
                var info = ActionCatalog.For(type);
                if (info.AtStation)
                    Assert.IsNotNull(_house.Game.NearestStation(type, Vector3.zero), $"{type} has no furniture");
            }
        }

        [UnityTest]
        public IEnumerator Rooms_CoverEveryRoomOfTheDesign()
        {
            yield return _house.Load();
            var names = new HashSet<string>();
            foreach (var room in _house.Game.Rooms) names.Add(room.name);
            foreach (var expected in new[] { "Living", "Kitchen", "Nursery", "Bedroom", "Bathroom", "Office", "Entrance" })
                Assert.IsTrue(names.Contains(expected), $"no {expected} room");
            foreach (var target in _house.Game.Interactables)
                Assert.IsNotNull(_house.Game.RoomAt(target.StandPosition), $"{target.name} stands outside every room");
        }
    }
}
