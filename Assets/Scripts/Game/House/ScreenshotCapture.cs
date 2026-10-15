using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BabyChampion.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BabyChampion.Game
{
    /// <summary>
    /// Captures every room of the house with the HUD, plus the radial menu, the phone, the level
    /// briefing, the success and failure screens, and then the menus, at several resolutions, into
    /// PNG files. The world camera and the UI panel each render into a texture of the target size,
    /// so the result does not depend on the Game view's size. Started by the editor's screenshot
    /// tool in Play mode. Saves made for the menu shots stay in memory.
    /// </summary>
    public sealed class ScreenshotCapture : MonoBehaviour
    {
        public struct Size
        {
            public string Name;
            public int Width;
            public int Height;

            public Size(string name, int width, int height)
            {
                Name = name;
                Width = width;
                Height = height;
            }
        }

        public static readonly Size[] DefaultSizes =
        {
            new Size("phone", 2400, 1080),
            new Size("desktop", 1920, 1080),
        };

        /// <summary>Screens per size besides the rooms: radial, phone, briefing, success, failure; title, slots, setup, map.</summary>
        public const int ExtraScreens = 9;

        public bool Done { get; private set; }
        /// <summary>How many images a complete run writes.</summary>
        public int Expected { get; private set; } = -1;
        public string Error { get; private set; }
        public readonly List<string> Files = new List<string>();

        public void Begin(string folder, Size[] sizes)
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run(folder, sizes));
        }

        IEnumerator Run(string folder, Size[] sizes)
        {
            var game = FindAnyObjectByType<HouseGame>();
            var rig = FindAnyObjectByType<CameraRig>();
            var hud = FindAnyObjectByType<Hud>();
            if (game == null || rig == null || hud == null)
            {
                Error = "The House scene is not loaded (no HouseGame, CameraRig or Hud)";
                Done = true;
                yield break;
            }
            Directory.CreateDirectory(folder);
            var document = hud.GetComponent<UIDocument>();
            var settings = document.panelSettings;
            var oldTarget = settings.targetTexture;
            bool oldClear = settings.clearColor;
            double oldSpeed = game.Sim.MinutesPerRealSecond;
            game.Sim.MinutesPerRealSecond = 0;
            var rooms = new List<Room>(game.Rooms);
            rooms.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            Expected = sizes.Length * (rooms.Count + ExtraScreens);
            var storage = SaveStore.Storage;
            SaveStore.Storage = new MemorySaveStorage();

            foreach (var size in sizes)
            {
                var world = new RenderTexture(size.Width, size.Height, 24, RenderTextureFormat.ARGB32);
                var ui = new RenderTexture(size.Width, size.Height, 24, RenderTextureFormat.ARGB32);
                rig.Camera.targetTexture = world;
                settings.targetTexture = ui;
                settings.clearColor = true; // a transparent texture, composited over the world image
                try
                {
                    foreach (var room in rooms)
                    {
                        rig.FrameRoom(room);
                        rig.SetDistance(Mathf.Max(room.area.width * 0.95f, room.area.height * 1.5f));
                        rig.SnapToTarget();
                        yield return Settle();
                        Save(world, ui, Path.Combine(folder, $"{size.Name}-room-{room.name.ToLowerInvariant()}.png"));
                    }

                    // The radial menu around the baby, from the default follow camera.
                    rig.FrameRoom(null);
                    rig.SetDistance(11f);
                    rig.SnapToTarget();
                    yield return Settle();
                    var babyScreen = rig.Camera.WorldToScreenPoint(game.Baby.transform.position);
                    hud.CloseRadial();
                    game.Tap(new Vector2(babyScreen.x, babyScreen.y), rig.Camera);
                    if (!hud.RadialOpen) hud.Toast("(the baby was not under the tap point)");
                    yield return Settle();
                    Save(world, ui, Path.Combine(folder, $"{size.Name}-radial-baby.png"));
                    hud.CloseRadial();

                    hud.OpenPhone();
                    yield return Settle();
                    Save(world, ui, Path.Combine(folder, $"{size.Name}-phone.png"));
                    hud.ClosePhone();

                    game.StartLevel(1, briefing: true, seed: 1);
                    game.Sim.MinutesPerRealSecond = 0;
                    yield return Settle();
                    Save(world, ui, Path.Combine(folder, $"{size.Name}-briefing.png"));

                    // A level that ends at once with full Joy: the success screen with 3 stars.
                    // No random diaper events, so the quick level reliably earns all 3 stars.
                    var config = game.CreateConfig();
                    foreach (var band in config.ageRates.bands) band.diapersPerDay = 0;
                    var quick = config.levels.Find(1);
                    quick.lengthMinutes = 30;
                    quick.goals.Clear();
                    quick.start = new LevelStart { babyHunger = 10, babyTiredness = 30, babyAsleep = true, parentEnergy = 90, parentJoy = 100 };
                    yield return EndLevel(game, CareSimulation.StartLevel(config, quick, game.Feeding, 1).CaptureState(), config);
                    Save(world, ui, Path.Combine(folder, $"{size.Name}-success.png"));

                    var tired = CareSimulation.StartLevel(config, config.levels.Find(2), game.Feeding, 1).CaptureState();
                    tired.parent.energy = 0.05;
                    yield return EndLevel(game, tired, config);
                    Save(world, ui, Path.Combine(folder, $"{size.Name}-failure.png"));
                    game.Restart(1);
                    game.Sim.MinutesPerRealSecond = 0;
                }
                finally
                {
                    rig.Camera.targetTexture = null;
                    settings.targetTexture = oldTarget;
                    settings.clearColor = oldClear;
                    world.Release();
                    ui.Release();
                    Destroy(world);
                    Destroy(ui);
                }
            }

            rig.FrameRoom(null);
            game.Sim.MinutesPerRealSecond = oldSpeed;

            yield return CaptureMenus(folder, sizes, settings);
            SaveStore.Storage = storage;
            GameSession.Clear();
            settings.targetTexture = oldTarget;
            settings.clearColor = oldClear;
            Done = true;
        }

        /// <summary>Plays a state until it ends (a level that is over at once), then holds the result screen.</summary>
        static IEnumerator EndLevel(HouseGame game, SimState state, Simulation.SimConfig config)
        {
            game.StartFromState(state, config, seed: 1);
            game.Sim.MinutesPerRealSecond = 600;
            for (int i = 0; i < 200 && game.Sim.Outcome == Outcome.Running; i++) yield return null;
            game.Sim.MinutesPerRealSecond = 0;
            yield return Settle();
        }

        /// <summary>The Menu scene: title, save slots with one baby, baby setup and the level map.</summary>
        IEnumerator CaptureMenus(string folder, Size[] sizes, PanelSettings settings)
        {
            MenuScreens.LoadScenes = false;
            GameSession.Clear();
            var load = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (load != null && !load.isDone) yield return null;
            yield return Settle();
            var menu = FindAnyObjectByType<MenuScreens>();
            var camera = Camera.main;
            if (menu == null || camera == null)
            {
                Error = "The Menu scene has no MenuScreens or camera";
                MenuScreens.LoadScenes = true;
                yield break;
            }
            var save = SaveSlot.Create("Noa", BabySex.Girl, 1, 1, FeedingChoice.Both, new Simulation.Tuning());
            save.RecordResult(new LevelResult { Level = 1, Completed = true, Stars = 2 }, save.household);
            SaveStore.Save(0, save);

            foreach (var size in sizes)
            {
                var world = new RenderTexture(size.Width, size.Height, 24, RenderTextureFormat.ARGB32);
                var ui = new RenderTexture(size.Width, size.Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = world;
                settings.targetTexture = ui;
                settings.clearColor = true;
                try
                {
                    var pages = new[] { MenuScreens.Page.Title, MenuScreens.Page.Slots, MenuScreens.Page.Setup, MenuScreens.Page.Map };
                    foreach (var page in pages)
                    {
                        if (page == MenuScreens.Page.Map) menu.ChooseSlot(0);
                        else menu.Show(page);
                        yield return Settle();
                        Save(world, ui, Path.Combine(folder, $"{size.Name}-menu-{page.ToString().ToLowerInvariant()}.png"));
                    }
                }
                finally
                {
                    camera.targetTexture = null;
                    world.Release();
                    ui.Release();
                    Destroy(world);
                    Destroy(ui);
                }
            }
            MenuScreens.LoadScenes = true;
        }

        // Plain frames, not WaitForEndOfFrame: that never resumes in batch mode (no Game view renders).
        // The camera and the panel render into their textures during each frame either way.
        static IEnumerator Settle()
        {
            for (int i = 0; i < 3; i++) yield return null;
        }

        void Save(RenderTexture world, RenderTexture ui, string path)
        {
            try
            {
                var worldPixels = Read(world);
                var uiPixels = Read(ui);
                var result = new Texture2D(world.width, world.height, TextureFormat.RGB24, false);
                var output = new Color32[worldPixels.Length];
                for (int i = 0; i < output.Length; i++)
                {
                    var w = worldPixels[i];
                    var u = uiPixels[i];
                    int a = u.a;
                    // The panel renders premultiplied colour over a transparent texture.
                    output[i] = new Color32(
                        (byte)Mathf.Min(255, u.r + w.r * (255 - a) / 255),
                        (byte)Mathf.Min(255, u.g + w.g * (255 - a) / 255),
                        (byte)Mathf.Min(255, u.b + w.b * (255 - a) / 255),
                        255);
                }
                result.SetPixels32(output);
                File.WriteAllBytes(path, result.EncodeToPNG());
                Destroy(result);
                Files.Add(path);
            }
            catch (Exception e)
            {
                Error = $"{path}: {e.Message}";
            }
        }

        static Color32[] Read(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            var pixels = texture.GetPixels32();
            Destroy(texture);
            return pixels;
        }
    }
}
