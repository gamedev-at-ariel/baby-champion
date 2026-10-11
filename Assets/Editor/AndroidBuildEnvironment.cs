using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BabyChampion.Editor
{
    /// <summary>
    /// Android builds fail when the Gradle cache path contains non-ASCII characters (for example
    /// a Windows user folder with a Hebrew name): the Prefab tool's generated .bat file mangles
    /// the path and Java reports "Could not find or load main class com.google.prefab.cli.AppKt".
    /// When GRADLE_USER_HOME is not set and the default (~/.gradle) is not pure ASCII, this points
    /// it at Library/Gradle inside the project for this Editor process and the Gradle it launches.
    /// </summary>
    [InitializeOnLoad]
    static class AndroidBuildEnvironment
    {
        static AndroidBuildEnvironment()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GRADLE_USER_HOME"))) return;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (IsAscii(home)) return;

            string projectGradle = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "Gradle"));
            if (!IsAscii(projectGradle))
            {
                Debug.LogWarning($"[AndroidBuildEnvironment] Both the user folder and the project path contain non-ASCII characters; set GRADLE_USER_HOME to an ASCII path before building for Android.");
                return;
            }

            Directory.CreateDirectory(projectGradle);
            Environment.SetEnvironmentVariable("GRADLE_USER_HOME", projectGradle);
        }

        static bool IsAscii(string path) => path.All(c => c < 128);
    }
}
