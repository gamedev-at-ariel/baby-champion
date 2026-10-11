using System;
using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>Logs the build version and applies <see cref="DevArgs"/> in development builds.</summary>
    static class DevBootstrap
    {
        public static DevArgs Args { get; private set; } = new DevArgs();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            Debug.Log($"Baby Champion {Application.version}");

            if (Application.isEditor || !Debug.isDebugBuild) return;

            Args = Application.platform == RuntimePlatform.WebGLPlayer
                ? DevArgs.FromUrl(Application.absoluteURL)
                : DevArgs.FromCommandLine(Environment.GetCommandLineArgs());

            if (Args.TimeScale is float scale)
            {
                Time.timeScale = scale;
                Debug.Log($"[DevArgs] timescale = {scale}");
            }
        }
    }
}
