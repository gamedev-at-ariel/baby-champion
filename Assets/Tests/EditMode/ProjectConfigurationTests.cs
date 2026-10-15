using System.IO;
using System.Linq;
using BabyChampion.Editor;
using BabyChampion.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BabyChampion.Tests.EditMode
{
    /// <summary>Guards the M0 project rules (design Section 19).</summary>
    public class ProjectConfigurationTests
    {
        [Test]
        public void LegacyInputManager_IsDisabled()
        {
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            int handler = new SerializedObject(playerSettings).FindProperty("activeInputHandler").intValue;
            Assert.AreEqual(1, handler, "Active Input Handling must be Input System Package (New): 0 = legacy, 2 = both.");
        }

        [Test]
        public void SimulationAssembly_HasNoUnityReferences()
        {
            var referenced = typeof(SeededRandom).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
            var unity = referenced.Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor") || n.StartsWith("Unity.")).ToList();
            CollectionAssert.IsEmpty(unity, "BabyChampion.Simulation must be plain C#.");
        }

        [Test]
        public void SimulationAsmdef_DisablesEngineReferences()
        {
            string guid = AssetDatabase.FindAssets("BabyChampion.Simulation t:AssemblyDefinitionAsset").Single();
            var asmdef = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(AssetDatabase.GUIDToAssetPath(guid)));
            Assert.IsTrue(asmdef.noEngineReferences);
            CollectionAssert.IsEmpty(asmdef.references);
        }

        [Test]
        public void MenuScene_IsTheFirstBuildScene_AndTheOthersAreIncluded()
        {
            Assert.That(EditorBuildSettings.scenes.Length, Is.GreaterThan(3));
            Assert.AreEqual(MenuSetup.ScenePath, EditorBuildSettings.scenes[0].path);
            var paths = EditorBuildSettings.scenes.Select(s => s.path).ToList();
            CollectionAssert.Contains(paths, HouseSetup.ScenePath);
            CollectionAssert.Contains(paths, DebugDaySetup.ScenePath);
            CollectionAssert.Contains(paths, ProjectSetup.TestScenePath);
        }

        [Test]
        public void HouseUi_UsesTheInputSystemUiModule_AndScalesWithTheScreen()
        {
            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(HouseSetup.PanelSettingsPath);
            Assert.IsNotNull(settings, "no HUD panel settings");
            Assert.IsNotNull(settings.themeStyleSheet, "the HUD panel has no theme");
            Assert.AreEqual(UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize, settings.scaleMode);
            Assert.IsNull(settings.targetTexture, "the HUD panel was left rendering into a texture (screenshot tool)");
            Assert.IsFalse(settings.clearColor, "an overlay panel that clears the screen hides the 3-D view (renders black)");
            StringAssert.Contains("InputSystemUIInputModule", File.ReadAllText(HouseSetup.ScenePath));
        }

        [Test]
        public void QualityTiers_Exist()
        {
            CollectionAssert.AreEquivalent(
                new[] { ProjectSetup.QualityAndroidLow, ProjectSetup.QualityAndroidHigh, ProjectSetup.QualityWeb },
                QualitySettings.names);
        }

        [Test]
        public void BuildProfiles_Exist()
        {
            foreach (var name in new[] { "Web Dev", "Web Release", "Android Dev", "Android Release" })
            {
                string path = $"{ProjectSetup.BuildProfilesFolder}/{name}.asset";
                Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(path), $"Missing build profile {path}");
            }
        }

        [Test]
        public void DataValidator_ReportsNoErrors()
        {
            CollectionAssert.IsEmpty(DataValidator.Validate());
        }

        [Test]
        public void VersionCode_IsMonotonicFromSemver()
        {
            Assert.AreEqual(1, VersionStamp.VersionCode("0.0.1"));
            Assert.AreEqual(100, VersionStamp.VersionCode("0.1.0"));
            Assert.AreEqual(10203, VersionStamp.VersionCode("1.2.3"));
        }

        [System.Serializable]
        class AsmdefJson
        {
            public bool noEngineReferences;
            public string[] references;
        }
    }
}
