using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace BabyChampion.Game
{
    /// <summary>
    /// Captures every room of the house with the HUD, plus the radial menu, at several resolutions,
    /// into PNG files. The world camera and the HUD panel each render into a texture of the target
    /// size, so the result does not depend on the Game view's size. Started by the editor's
    /// screenshot tool in Play mode.
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

        public bool Done { get; private set; }
        public string Error { get; private set; }
        public readonly List<string> Files = new List<string>();

        public void Begin(string folder, Size[] sizes) => StartCoroutine(Run(folder, sizes));

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
            Done = true;
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
