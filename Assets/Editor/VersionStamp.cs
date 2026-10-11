using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace BabyChampion.Editor
{
    /// <summary>
    /// Stamps every player build with the version from <c>version.txt</c> plus the git commit
    /// (e.g. <c>0.1.0+a1b2c3d</c>), visible at runtime as Application.version. The original
    /// PlayerSettings values are restored after the build, so builds never dirty
    /// ProjectSettings; if a build aborts, they are restored the next time the Editor loads.
    /// </summary>
    [InitializeOnLoad]
    public class VersionStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        const string VersionFile = "version.txt";
        const string BackupFile = "Library/BabyChampion/version-stamp-backup.txt";

        public int callbackOrder => 0;

        static VersionStamp() => EditorApplication.delayCall += Restore;

        public void OnPreprocessBuild(BuildReport report)
        {
            Restore();
            string baseVersion = File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : PlayerSettings.bundleVersion;
            string commit = GitShortHash();
            string version = commit == null ? baseVersion : $"{baseVersion}+{commit}";

            Directory.CreateDirectory(Path.GetDirectoryName(BackupFile));
            File.WriteAllLines(BackupFile, new[] { PlayerSettings.bundleVersion, PlayerSettings.Android.bundleVersionCode.ToString() });

            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = VersionCode(baseVersion);
            Debug.Log($"[VersionStamp] {report.summary.platform}: version {version}, Android versionCode {PlayerSettings.Android.bundleVersionCode}");
        }

        public void OnPostprocessBuild(BuildReport report) => Restore();

        /// <summary>major.minor.patch -> major*10000 + minor*100 + patch (Android needs a growing integer).</summary>
        public static int VersionCode(string version)
        {
            var parts = version.Split('.');
            int Part(int i) => i < parts.Length && int.TryParse(parts[i], out int v) ? v : 0;
            return Math.Max(1, Part(0) * 10000 + Part(1) * 100 + Part(2));
        }

        static void Restore()
        {
            if (!File.Exists(BackupFile)) return;
            var lines = File.ReadAllLines(BackupFile);
            if (lines.Length >= 2)
            {
                PlayerSettings.bundleVersion = lines[0];
                if (int.TryParse(lines[1], out int code)) PlayerSettings.Android.bundleVersionCode = code;
            }
            File.Delete(BackupFile);
            // Batch mode (-quit) exits without saving, which would leave the stamp in ProjectSettings.asset.
            AssetDatabase.SaveAssets();
        }

        static string GitShortHash()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return process.ExitCode == 0 && output.Length > 0 ? output : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
