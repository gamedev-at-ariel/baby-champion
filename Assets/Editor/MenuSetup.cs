using System.IO;
using BabyChampion.Game;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace BabyChampion.Editor
{
    /// <summary>
    /// Builds the Menu scene (title, save slots, baby setup, level map) and makes it the first build
    /// scene, so the game starts there. Re-run it with <c>unity command bc_setup_menu</c>.
    /// </summary>
    public static class MenuSetup
    {
        public const string ScenePath = "Assets/Scenes/Menu.unity";
        public const string MenuStylePath = HouseSetup.UiFolder + "/Menu.uss";

        [CliCommand("bc_setup_menu", "(Re)create the Menu scene (title, save slots, baby setup, level map) and make it the first build scene")]
        public static string SetupMenu()
        {
            DataAssets.SetupData();
            AssetDatabase.ImportAsset(MenuStylePath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load after NewScene: opening a new scene unloads assets nothing references.
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(HouseSetup.PanelSettingsPath);
            if (panelSettings == null) throw new FileNotFoundException("No HUD panel settings; run unity command bc_setup_house first", HouseSetup.PanelSettingsPath);
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.98f, 0.93f, 0.84f);
            cameraGo.AddComponent<UniversalAdditionalCameraData>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var menuGo = new GameObject("Menu");
            var document = menuGo.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var menu = menuGo.AddComponent<MenuScreens>();
            var so = new SerializedObject(menu);
            so.FindProperty("levels").objectReferenceValue = DataAssets.Levels;
            so.FindProperty("tuning").objectReferenceValue = DataAssets.Tuning;
            so.FindProperty("styleSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(MenuStylePath);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (document.panelSettings == null) throw new System.InvalidOperationException("The menu's UIDocument has no panel settings");

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectSetup.AddBuildScene(ScenePath, first: true);
            ProjectSetup.AddBuildScene(HouseSetup.ScenePath);
            AssetDatabase.SaveAssets();
            return $"menu: saved {ScenePath} (first build scene); build scenes: {string.Join(", ", System.Array.ConvertAll(EditorBuildSettings.scenes, s => Path.GetFileNameWithoutExtension(s.path)))}";
        }
    }
}
