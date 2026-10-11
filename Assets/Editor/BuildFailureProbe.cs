using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BabyChampion.Editor
{
    /// <summary>
    /// Fails a player build on purpose when BABYCHAMPION_FAIL_BUILD=1, so tools/ci.sh
    /// --verify-build-failure can prove that a failed -build returns a non-zero exit code.
    /// </summary>
    public class BuildFailureProbe : IPreprocessBuildWithReport
    {
        public const string EnvironmentVariable = "BABYCHAMPION_FAIL_BUILD";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (Environment.GetEnvironmentVariable(EnvironmentVariable) == "1")
                throw new BuildFailedException($"{EnvironmentVariable}=1: failing this build on purpose.");
        }
    }
}
