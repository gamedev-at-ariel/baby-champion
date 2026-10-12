using System.IO;
using BabyChampion.Game;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BabyChampion.Editor
{
    /// <summary>Builds the M1 debug scene: one simulated day shown as text, bars and buttons.</summary>
    public static class DebugDaySetup
    {
        public const string ScenePath = "Assets/Scenes/DebugDay.unity";

        [CliCommand("bc_setup_debug_scene", "(Re)create the M1 debug scene (simulation day with text, bars and action buttons) and make it the first build scene")]
        public static string SetupDebugScene()
        {
            DataAssets.SetupData();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.17f);
            cameraGo.AddComponent<UniversalAdditionalCameraData>();

            var runnerGo = new GameObject("DebugDay");
            var runner = runnerGo.AddComponent<DebugDayRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("ageRates").objectReferenceValue = DataAssets.AgeRates;
            so.FindProperty("prices").objectReferenceValue = DataAssets.Prices;
            so.FindProperty("tuning").objectReferenceValue = DataAssets.Tuning;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectSetup.AddBuildScene(ScenePath, first: true);
            return "debug scene: saved " + ScenePath + " (first build scene)";
        }
    }
}
