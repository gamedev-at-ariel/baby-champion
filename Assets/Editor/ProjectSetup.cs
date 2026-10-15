using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BabyChampion.Game;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BabyChampion.Editor
{
    /// <summary>
    /// Idempotent project-configuration commands (milestone M0). Run them all with
    /// <c>unity command bc_setup_all</c>, or one at a time by name.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ControlsPath = "Assets/Input/BabyChampionControls.inputactions";
        public const string TestScenePath = "Assets/Scenes/TestScene.unity";
        public const string BuildProfilesFolder = "Assets/Settings/Build Profiles";
        const string SettingsFolder = "Assets/Settings";
        const string MaterialsFolder = "Assets/Art/Materials";

        public const string QualityAndroidLow = "Android Low";
        public const string QualityAndroidHigh = "Android High";
        public const string QualityWeb = "Web";

        [CliCommand("bc_setup_all", "Apply every Baby Champion M0 project setting (player, quality, input, test scene, build profiles)")]
        public static string SetupAll()
        {
            var log = new StringBuilder();
            log.AppendLine(SetupPlayer());
            log.AppendLine(SetupQuality());
            log.AppendLine(SetupInput());
            log.AppendLine(SetupTestScene());
            log.AppendLine(SetupBuildProfiles());
            return log.ToString();
        }

        [CliCommand("bc_setup_player", "Player settings: names, app id, Android IL2CPP/ARM64/landscape, Web compression with decompression fallback")]
        public static string SetupPlayer()
        {
            PlayerSettings.companyName = "Erel Segal-Halevi";
            PlayerSettings.productName = "Baby Champion";
            if (File.Exists("version.txt")) PlayerSettings.bundleVersion = File.ReadAllText("version.txt").Trim();

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.erelsegalhalevi.babychampion");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Landscape only, either way up.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // itch.io does not send Content-Encoding headers, so the loader must decompress itself.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            AssetDatabase.SaveAssets();
            return "player: ok";
        }

        [CliCommand("bc_setup_quality", "Create the URP quality tiers Web, Android Low and Android High")]
        public static string SetupQuality()
        {
            var template = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"{SettingsFolder}/Mobile_RPAsset.asset");
            var web = EnsureRpAsset("Web_RPAsset", template);
            var low = EnsureRpAsset("AndroidLow_RPAsset", template);
            var high = EnsureRpAsset("AndroidHigh_RPAsset", template);

            ConfigureRpAsset(web, renderScale: 1f, msaa: 2, hdr: false, shadows: true, shadowDistance: 30f);
            ConfigureRpAsset(low, renderScale: 0.75f, msaa: 1, hdr: false, shadows: false, shadowDistance: 0f);
            ConfigureRpAsset(high, renderScale: 1f, msaa: 2, hdr: false, shadows: true, shadowDistance: 30f);

            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qualityAsset);
            var levels = so.FindProperty("m_QualitySettings");
            while (levels.arraySize < 3) levels.InsertArrayElementAtIndex(levels.arraySize - 1);
            while (levels.arraySize > 3) levels.DeleteArrayElementAtIndex(levels.arraySize - 1);

            SetQualityLevel(levels.GetArrayElementAtIndex(0), QualityAndroidLow, low, new[] { "Standalone", "WebGL" });
            SetQualityLevel(levels.GetArrayElementAtIndex(1), QualityAndroidHigh, high, new[] { "Standalone", "WebGL" });
            SetQualityLevel(levels.GetArrayElementAtIndex(2), QualityWeb, web, new[] { "Android" });

            var defaults = so.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var entry = defaults.GetArrayElementAtIndex(i);
                string platform = entry.FindPropertyRelative("first").stringValue;
                entry.FindPropertyRelative("second").intValue = platform == "Android" ? 1 : 2;
            }
            so.FindProperty("m_CurrentQuality").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = web;

            // The template's desktop assets are no longer used by any tier.
            foreach (var unused in new[] { "PC_RPAsset", "PC_Renderer" })
            {
                string path = $"{SettingsFolder}/{unused}.asset";
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            return "quality: " + string.Join(", ", QualitySettings.names);
        }

        [CliCommand("bc_setup_input", "Make BabyChampionControls the project-wide Input Actions asset")]
        public static string SetupInput()
        {
            AssetDatabase.ImportAsset(ControlsPath);
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            if (controls == null) return $"input: ERROR, {ControlsPath} not found";

            InputSystem.actions = controls;

            const string templateActions = "Assets/InputSystem_Actions.inputactions";
            if (AssetDatabase.LoadMainAssetAtPath(templateActions) != null) AssetDatabase.DeleteAsset(templateActions);

            AssetDatabase.SaveAssets();
            return "input: project-wide actions = " + AssetDatabase.GetAssetPath(InputSystem.actions);
        }

        [CliCommand("bc_setup_test_scene", "(Re)create the M0 test scene: floor plane, camera, light and a click-to-move cube")]
        public static string SetupTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 12f, -10f), Quaternion.Euler(50f, 0f, 0f));
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.98f, 0.92f, 0.84f);
            camera.fieldOfView = 45f;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Directional Light", typeof(Light));
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.96f, 0.88f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<Renderer>().sharedMaterial = EnsureMaterial("Floor", new Color(0.62f, 0.78f, 0.55f));

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Mover";
            cube.transform.position = new Vector3(0f, 0.5f, 0f);
            cube.GetComponent<Renderer>().sharedMaterial = EnsureMaterial("Mover", new Color(0.95f, 0.55f, 0.45f));
            var mover = cube.AddComponent<ClickToMove>();

            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            var moverSo = new SerializedObject(mover);
            moverSo.FindProperty("point").objectReferenceValue = InputActionReference.Create(controls.FindAction("Gameplay/Point", true));
            moverSo.FindProperty("click").objectReferenceValue = InputActionReference.Create(controls.FindAction("Gameplay/Click", true));
            moverSo.FindProperty("viewCamera").objectReferenceValue = camera;
            moverSo.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(TestScenePath));
            EditorSceneManager.SaveScene(scene, TestScenePath);

            AddBuildScene(TestScenePath);
            return "test scene: saved " + TestScenePath;
        }

        /// <summary>
        /// Adds a scene to the build list (once), optionally as the first scene, keeping the others.
        /// A scene already in the list keeps its place unless it is to be first.
        /// </summary>
        public static void AddBuildScene(string path, bool first = false)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var scene = new EditorBuildSettingsScene(path, true);
            int existing = scenes.FindIndex(s => s.path == path);
            if (existing >= 0 && !first) scenes[existing] = scene;
            else
            {
                if (existing >= 0) scenes.RemoveAt(existing);
                if (first) scenes.Insert(0, scene);
                else scenes.Add(scene);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [CliCommand("bc_setup_build_profiles", "Create the Build Profiles Web Dev, Web Release, Android Dev and Android Release")]
        public static string SetupBuildProfiles()
        {
            var log = new List<string>();
            log.Add(EnsureProfile("Web Dev", BuildTarget.WebGL, development: true, appBundle: false));
            log.Add(EnsureProfile("Web Release", BuildTarget.WebGL, development: false, appBundle: false));
            log.Add(EnsureProfile("Android Dev", BuildTarget.Android, development: true, appBundle: false));
            log.Add(EnsureProfile("Android Release", BuildTarget.Android, development: false, appBundle: true));
            AssetDatabase.SaveAssets();
            return "build profiles: " + string.Join(", ", log);
        }

        static UniversalRenderPipelineAsset EnsureRpAsset(string name, UniversalRenderPipelineAsset template)
        {
            string path = $"{SettingsFolder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset != null) return asset;
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(template), path);
            return AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        }

        static void ConfigureRpAsset(UniversalRenderPipelineAsset asset, float renderScale, int msaa, bool hdr, bool shadows, float shadowDistance)
        {
            var so = new SerializedObject(asset);
            so.FindProperty("m_RenderScale").floatValue = renderScale;
            so.FindProperty("m_MSAA").intValue = msaa;
            so.FindProperty("m_SupportsHDR").boolValue = hdr;
            so.FindProperty("m_MainLightShadowsSupported").boolValue = shadows;
            so.FindProperty("m_ShadowDistance").floatValue = shadowDistance;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void SetQualityLevel(SerializedProperty level, string name, RenderPipelineAsset pipeline, string[] excludedPlatforms)
        {
            level.FindPropertyRelative("name").stringValue = name;
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
            excluded.ClearArray();
            for (int i = 0; i < excludedPlatforms.Length; i++)
            {
                excluded.InsertArrayElementAtIndex(i);
                excluded.GetArrayElementAtIndex(i).stringValue = excludedPlatforms[i];
            }
        }

        internal static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(MaterialsFolder);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        const BindingFlags AnyMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static string EnsureProfile(string name, BuildTarget target, bool development, bool appBundle)
        {
            string path = $"{BuildProfilesFolder}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
            if (profile == null)
            {
                // BuildProfile.CreateInstance(BuildTarget, StandaloneBuildSubtarget) is internal in 6000.3.
                var create = typeof(BuildProfile).GetMethod("CreateInstance", AnyMember, null,
                    new[] { typeof(BuildTarget), typeof(StandaloneBuildSubtarget) }, null);
                profile = (BuildProfile)create.Invoke(null, new object[] { target, StandaloneBuildSubtarget.Default });
                Directory.CreateDirectory(BuildProfilesFolder);
                AssetDatabase.CreateAsset(profile, path);
            }

            var settings = typeof(BuildProfile).GetProperty("platformBuildProfile", AnyMember).GetValue(profile);
            SetProperty(settings, "development", development);
            if (target == BuildTarget.Android) SetProperty(settings, "buildAppBundle", appBundle);

            EditorUtility.SetDirty(profile);
            return name;
        }

        static void SetProperty(object target, string property, object value)
        {
            var prop = target.GetType().GetProperties(AnyMember).First(p => p.Name == property && p.CanWrite);
            prop.SetValue(target, value);
        }
    }
}
