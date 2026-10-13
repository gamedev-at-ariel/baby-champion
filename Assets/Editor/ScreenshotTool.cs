using System;
using System.IO;
using BabyChampion.Game;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BabyChampion.Editor
{
    /// <summary>
    /// Captures every room of the house with the HUD, the radial menu and the phone, at phone
    /// (2400x1080) and desktop (1920x1080) resolutions, into artifacts/screens/ (3-plan.md Section 2).
    /// It needs graphics and Play mode, so it opens the House scene, enters Play mode, captures,
    /// and then exits (batch) or leaves Play mode (open Editor).
    /// CLI:    tools/unity.sh -executeMethod BabyChampion.Editor.ScreenshotTool.Run   (no -quit, no -nographics)
    /// Editor: unity command bc_screenshots, then unity command bc_screenshots_status
    /// </summary>
    [InitializeOnLoad]
    public static class ScreenshotTool
    {
        public const string OutputFolder = "artifacts/screens";
        const string StateKey = "BabyChampion.Screenshots.State";
        const string BatchKey = "BabyChampion.Screenshots.Batch";
        const string StartedKey = "BabyChampion.Screenshots.Started";
        const string ResultKey = "BabyChampion.Screenshots.Result";
        const double TimeoutSeconds = 180;

        static ScreenshotCapture _capture;

        static ScreenshotTool()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Watch;
        }

        static string State
        {
            get => SessionState.GetString(StateKey, "idle");
            set => SessionState.SetString(StateKey, value);
        }

        /// <summary>Batch-mode entry point. Exits with code 0 when every image was written, 1 otherwise.</summary>
        public static void Run()
        {
            try
            {
                Start(batch: true);
            }
            catch (Exception e)
            {
                Debug.LogError("[Screenshots] " + e);
                EditorApplication.Exit(1);
            }
        }

        [CliCommand("bc_screenshots", "Capture every room, the HUD, the radial menu and the phone at phone and desktop resolutions into artifacts/screens/ (enters and leaves Play mode; poll bc_screenshots_status)")]
        public static string RunInEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode first");
            Start(batch: false);
            return "started: the Editor enters Play mode, captures, and leaves it. Poll: unity command bc_screenshots_status";
        }

        [CliCommand("bc_screenshots_status", "State of the last bc_screenshots run: running, or the list of files written")]
        public static string Status() => State == "running" ? "running" : SessionState.GetString(ResultKey, "no run yet");

        static void Start(bool batch)
        {
            if (!File.Exists(HouseSetup.ScenePath)) throw new FileNotFoundException("No house scene; run unity command bc_setup_house", HouseSetup.ScenePath);
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != HouseSetup.ScenePath)
            {
                if (active.isDirty && !batch) throw new InvalidOperationException($"The open scene {active.path} has unsaved changes; save it first");
                EditorSceneManager.OpenScene(HouseSetup.ScenePath, OpenSceneMode.Single);
            }
            if (Directory.Exists(OutputFolder))
                foreach (var file in Directory.GetFiles(OutputFolder, "*.png")) File.Delete(file);
            Directory.CreateDirectory(OutputFolder);
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetString(ResultKey, "");
            State = "entering";
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || State != "entering") return;
            State = "running";
            var go = new GameObject("ScreenshotCapture");
            _capture = go.AddComponent<ScreenshotCapture>();
            _capture.Begin(OutputFolder, ScreenshotCapture.DefaultSizes);
        }

        static void Watch()
        {
            string state = State;
            if (state == "idle" || state == "done") return;
            bool timedOut = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, 0f) > TimeoutSeconds;
            if (state == "running" && _capture != null && _capture.Done) Finish(_capture.Error, _capture.Files.Count);
            else if (timedOut) Finish("timed out", _capture != null ? _capture.Files.Count : 0);
        }

        static void Finish(string error, int count)
        {
            State = "done";
            int expected = ExpectedCount();
            bool ok = error == null && count == expected;
            string result = ok
                ? $"ok: {count} images in {OutputFolder}/"
                : $"FAILED: {count} of {expected} images written{(error != null ? "; " + error : "")}";
            SessionState.SetString(ResultKey, result);
            if (ok) Debug.Log("[Screenshots] " + result);
            else Debug.LogError("[Screenshots] " + result);
            _capture = null;

            if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        static int ExpectedCount()
        {
            int rooms = UnityEngine.Object.FindObjectsByType<Room>(FindObjectsSortMode.None).Length;
            return ScreenshotCapture.DefaultSizes.Length * (rooms + 2);
        }
    }
}
