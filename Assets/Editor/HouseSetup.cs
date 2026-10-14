using System.Collections.Generic;
using System.IO;
using BabyChampion.Game;
using BabyChampion.Simulation;
using Unity.AI.Navigation;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace BabyChampion.Editor
{
    /// <summary>
    /// Builds the M2 greybox house scene from primitives: the rooms of design Section 12, furniture
    /// that offers actions, the placeholder parent and baby, camera, HUD and a baked NavMesh.
    /// Re-run it after changing the layout: <c>unity command bc_setup_house</c>.
    /// </summary>
    public static class HouseSetup
    {
        public const string ScenePath = "Assets/Scenes/House.unity";
        public const string NavMeshPath = "Assets/Scenes/House/NavMesh.asset";
        public const string UiFolder = "Assets/UI";
        public const string PanelSettingsPath = UiFolder + "/HudPanelSettings.asset";
        public const string ThemePath = UiFolder + "/BabyChampionTheme.tss";
        public const string HudStylePath = UiFolder + "/Hud.uss";

        const float WallHeight = 1.2f;
        const float WallThickness = 0.2f;
        const int NotWalkable = 1;

        static Transform _house;

        [CliCommand("bc_setup_house", "(Re)create the M2 greybox house scene (rooms, furniture, parent, baby, camera, HUD, NavMesh) and make it the first build scene")]
        public static string SetupHouse()
        {
            DataAssets.SetupData();
            var panelSettings = EnsurePanelSettings();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.6f, 0.58f);
            var lightGo = new GameObject("Sun", typeof(Light));
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.88f);

            _house = new GameObject("House").transform;
            var rooms = BuildRooms();
            BuildWalls();
            var furniture = BuildFurniture();

            var surface = _house.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            Directory.CreateDirectory(Path.GetDirectoryName(NavMeshPath));
            if (AssetDatabase.LoadMainAssetAtPath(NavMeshPath) != null) AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);

            var parentStart = new GameObject("ParentStart").transform;
            parentStart.SetPositionAndRotation(new Vector3(-0.2f, 0f, -3.6f), Quaternion.Euler(0, 0, 0));
            var parent = BuildParent(parentStart.position);
            var baby = BuildBaby();

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.36f, 0.33f, 0.32f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 120f;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();

            var gameGo = new GameObject("HouseGame");
            var game = gameGo.AddComponent<HouseGame>();
            Set(game, "ageRates", DataAssets.AgeRates);
            Set(game, "prices", DataAssets.Prices);
            Set(game, "tuning", DataAssets.Tuning);
            Set(game, "parent", parent);
            Set(game, "baby", baby);
            Set(game, "startBabySpot", furniture["Bouncer"]);
            Set(game, "parentStart", parentStart);
            Set(game, "frontDoor", furniture["FrontDoor"]);

            var rigGo = new GameObject("CameraRig");
            var rig = rigGo.AddComponent<CameraRig>();
            Set(rig, "viewCamera", camera);
            Set(rig, "game", game);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetAsLastSibling();

            var hudGo = new GameObject("HUD");
            var document = hudGo.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var hud = hudGo.AddComponent<Hud>();
            Set(hud, "game", game);
            Set(hud, "rig", rig);
            Set(hud, "styleSheet", AssetDatabase.LoadAssetAtPath<StyleSheet>(HudStylePath));

            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProjectSetup.ControlsPath);
            var inputGo = new GameObject("Input");
            var input = inputGo.AddComponent<PointerInput>();
            SetAction(input, "point", controls, "Point");
            SetAction(input, "click", controls, "Click");
            SetAction(input, "scroll", controls, "Scroll");
            SetAction(input, "drag", controls, "Drag");
            SetAction(input, "rotateDrag", controls, "RotateDrag");
            SetAction(input, "rotateLeft", controls, "RotateLeft");
            SetAction(input, "rotateRight", controls, "RotateRight");
            SetAction(input, "pause", controls, "Pause");
            Set(input, "game", game);
            Set(input, "rig", rig);
            Set(input, "hud", hud);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectSetup.AddBuildScene(ScenePath, first: true);
            AssetDatabase.SaveAssets();

            var tri = NavMesh.CalculateTriangulation();
            return $"house: saved {ScenePath} (first build scene); {rooms.Count} rooms, {furniture.Count} pieces of furniture, NavMesh {tri.vertices.Length} vertices";
        }

        // ---------------------------------------------------------------- UI assets

        static PanelSettings EnsurePanelSettings()
        {
            AssetDatabase.ImportAsset(ThemePath);
            AssetDatabase.ImportAsset(HudStylePath);
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f;
            // Clearing would wipe the 3-D view under the overlay; only screenshots (render textures) clear.
            settings.clearColor = false;
            settings.colorClearValue = new Color(0, 0, 0, 0);
            settings.targetTexture = null;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // ---------------------------------------------------------------- rooms and walls

        static readonly (string name, string label, Rect area, Color color)[] RoomDefs =
        {
            ("Bedroom", "Bedroom", new Rect(-10, 1, 7, 6), new Color(0.80f, 0.74f, 0.88f)),
            ("Nursery", "Nursery", new Rect(-3, 1, 7, 6), new Color(0.74f, 0.86f, 0.93f)),
            ("Bathroom", "Bathroom", new Rect(4, 1, 6, 6), new Color(0.80f, 0.91f, 0.90f)),
            ("Office", "Home office", new Rect(-10, -6, 4, 7), new Color(0.78f, 0.74f, 0.66f)),
            ("Living", "Living room", new Rect(-6, -6, 10, 7), new Color(0.93f, 0.84f, 0.69f)),
            ("Kitchen", "Kitchen", new Rect(4, -6, 6, 7), new Color(0.91f, 0.89f, 0.78f)),
            ("Entrance", "Entrance", new Rect(-2, -9, 3, 3), new Color(0.72f, 0.68f, 0.62f)),
        };

        static List<Room> BuildRooms()
        {
            var rooms = new List<Room>();
            var parent = new GameObject("Rooms").transform;
            parent.SetParent(_house, false);
            foreach (var (name, label, area, color) in RoomDefs)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var room = go.AddComponent<Room>();
                room.displayName = label;
                room.area = area;
                var floor = Cube("Floor", go.transform, new Vector3(area.center.x, -0.05f, area.center.y), new Vector3(area.width, 0.1f, area.height), "Floor" + name, color);
                floor.isStatic = true;
                rooms.Add(room);
            }
            return rooms;
        }

        static void BuildWalls()
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(_house, false);
            AddModifier(walls.gameObject, NotWalkable);
            // Outer walls.
            WallX(walls, 7f, -10f, 10f);
            WallZ(walls, -10f, -6f, 7f);
            WallZ(walls, 10f, -6f, 7f);
            WallX(walls, -6f, -10f, 10f, (-2f, 1f));
            // Entrance vestibule; the front door is in its south wall.
            WallZ(walls, -2f, -9f, -6f);
            WallZ(walls, 1f, -9f, -6f);
            WallX(walls, -9f, -2f, 1f);
            // Between the north rooms and the living area: doors to the bedroom and the nursery.
            WallX(walls, 1f, -10f, 10f, (-5.4f, -3.8f), (-0.8f, 0.8f));
            // Bedroom | nursery, nursery | bathroom (door), living | kitchen (wide opening).
            WallZ(walls, -3f, 1f, 7f);
            WallZ(walls, 4f, 1f, 7f, (2.2f, 3.8f));
            WallZ(walls, 4f, -6f, 1f, (-4f, 0.2f));
        }

        /// <summary>A wall along x at the given z, from x0 to x1, with door gaps.</summary>
        static void WallX(Transform parent, float z, float x0, float x1, params (float from, float to)[] gaps)
        {
            foreach (var (a, b) in Segments(x0, x1, gaps))
                Cube("Wall", parent, new Vector3((a + b) / 2, WallHeight / 2, z), new Vector3(b - a + WallThickness, WallHeight, WallThickness), "Wall", WallColor).isStatic = true;
        }

        /// <summary>A wall along z at the given x, from z0 to z1, with door gaps.</summary>
        static void WallZ(Transform parent, float x, float z0, float z1, params (float from, float to)[] gaps)
        {
            foreach (var (a, b) in Segments(z0, z1, gaps))
                Cube("Wall", parent, new Vector3(x, WallHeight / 2, (a + b) / 2), new Vector3(WallThickness, WallHeight, b - a + WallThickness), "Wall", WallColor).isStatic = true;
        }

        static IEnumerable<(float, float)> Segments(float from, float to, (float from, float to)[] gaps)
        {
            float start = from;
            foreach (var gap in gaps)
            {
                if (gap.from > start) yield return (start, gap.from - WallThickness / 2);
                start = gap.to + WallThickness / 2;
            }
            if (to > start) yield return (start, to);
        }

        static readonly Color WallColor = new Color(0.97f, 0.95f, 0.9f);
        static readonly Color Wood = new Color(0.72f, 0.55f, 0.4f);
        static readonly Color White = new Color(0.95f, 0.95f, 0.97f);
        static readonly Color Appliance = new Color(0.84f, 0.86f, 0.9f);

        // ---------------------------------------------------------------- furniture

        static Dictionary<string, Interactable> BuildFurniture()
        {
            var all = new Dictionary<string, Interactable>();
            var root = new GameObject("Furniture").transform;
            root.SetParent(_house, false);

            // Bedroom: the parents' bed, head against the north wall.
            var bed = Furniture(all, root, "Bed", "Bed", new Vector3(-7.5f, 0f, 5.8f));
            Cube("Frame", bed.transform, new Vector3(-7.5f, 0.25f, 5.8f), new Vector3(2.0f, 0.5f, 2.2f), "Bed", White);
            Cube("Blanket", bed.transform, new Vector3(-7.5f, 0.52f, 5.3f), new Vector3(2.0f, 0.06f, 1.2f), "Blanket", new Color(0.55f, 0.62f, 0.85f));
            Cube("Pillow", bed.transform, new Vector3(-7.5f, 0.56f, 6.55f), new Vector3(1.6f, 0.12f, 0.45f), "Pillow", Color.white);
            bed.actions = new[] { ActionType.Sleep, ActionType.Rest, ActionType.Breastfeed };
            bed.seatPose = ParentPose.Lie;
            Points(bed, stand: new Vector3(-5.7f, 0f, 5.0f), use: new Vector3(-7.0f, 0.5f, 4.8f), useYaw: 180f);
            Prop(root, "Wardrobe", new Vector3(-9.55f, 0.9f, 2.4f), new Vector3(0.7f, 1.8f, 1.6f), Wood);

            // Nursery: crib, changing table, nursing chair.
            var crib = Furniture(all, root, "Crib", "Crib", new Vector3(2.6f, 0f, 6.1f));
            Cube("Mattress", crib.transform, new Vector3(2.6f, 0.22f, 6.1f), new Vector3(1.4f, 0.44f, 0.8f), "Crib", White);
            Cube("RailN", crib.transform, new Vector3(2.6f, 0.62f, 6.47f), new Vector3(1.4f, 0.36f, 0.06f), "Crib", White);
            Cube("RailS", crib.transform, new Vector3(2.6f, 0.62f, 5.73f), new Vector3(1.4f, 0.36f, 0.06f), "Crib", White);
            crib.actions = new[] { ActionType.PutBabyToSleep };
            crib.babySpot = true;
            crib.babyPose = BabyPose.Lying;
            Points(crib, stand: new Vector3(2.6f, 0f, 4.9f), baby: new Vector3(2.9f, 0.45f, 6.1f), babyYaw: 90f);

            var changing = Furniture(all, root, "ChangingTable", "Changing table", new Vector3(-2.2f, 0f, 6.25f));
            Cube("Table", changing.transform, new Vector3(-2.2f, 0.45f, 6.25f), new Vector3(1.2f, 0.9f, 0.7f), "ChangingTable", new Color(0.95f, 0.85f, 0.75f));
            Cube("Pad", changing.transform, new Vector3(-2.2f, 0.93f, 6.25f), new Vector3(0.9f, 0.06f, 0.55f), "Pad", new Color(0.6f, 0.82f, 0.75f));
            changing.actions = new[] { ActionType.ChangeDiaper };
            Points(changing, stand: new Vector3(-2.2f, 0f, 5.2f), baby: new Vector3(-1.95f, 0.96f, 6.25f), babyYaw: 90f);

            var chair = Furniture(all, root, "NursingChair", "Nursing chair", new Vector3(-2.1f, 0f, 2.0f));
            Cube("Seat", chair.transform, new Vector3(-2.1f, 0.25f, 2.0f), new Vector3(0.9f, 0.5f, 0.9f), "Armchair", new Color(0.85f, 0.6f, 0.65f));
            Cube("Back", chair.transform, new Vector3(-2.1f, 0.75f, 1.6f), new Vector3(0.9f, 0.6f, 0.2f), "Armchair", new Color(0.85f, 0.6f, 0.65f));
            chair.actions = new[] { ActionType.Breastfeed, ActionType.BottleFeed };
            chair.seatPose = ParentPose.Sit;
            Points(chair, stand: new Vector3(-1.0f, 0f, 2.4f), use: new Vector3(-2.1f, 0.5f, 2.05f), useYaw: 0f);

            // Bathroom: props until bathing and showering arrive.
            Prop(root, "Bathtub", new Vector3(8.5f, 0.3f, 6.2f), new Vector3(2.6f, 0.6f, 1.1f), White);
            Prop(root, "Sink", new Vector3(9.5f, 0.45f, 2.6f), new Vector3(0.6f, 0.9f, 0.8f), White);
            Prop(root, "Toilet", new Vector3(5.6f, 0.25f, 6.4f), new Vector3(0.5f, 0.5f, 0.7f), White);

            // Home office corner: a prop until Work arrives (month 6+).
            Prop(root, "Desk", new Vector3(-9.35f, 0.38f, -2.5f), new Vector3(1.1f, 0.76f, 1.8f), Wood);
            Prop(root, "DeskChair", new Vector3(-8.4f, 0.25f, -2.5f), new Vector3(0.5f, 0.5f, 0.5f), new Color(0.3f, 0.3f, 0.35f));

            // Living room: sofa, play mat, bouncer.
            var sofa = Furniture(all, root, "Sofa", "Sofa", new Vector3(-4.2f, 0f, -5.3f));
            Cube("Seat", sofa.transform, new Vector3(-4.2f, 0.25f, -5.3f), new Vector3(2.4f, 0.5f, 0.9f), "Sofa", new Color(0.45f, 0.6f, 0.78f));
            Cube("Back", sofa.transform, new Vector3(-4.2f, 0.75f, -5.68f), new Vector3(2.4f, 0.5f, 0.25f), "Sofa", new Color(0.45f, 0.6f, 0.78f));
            sofa.actions = new[] { ActionType.Breastfeed, ActionType.BottleFeed, ActionType.Rest };
            sofa.seatPose = ParentPose.Sit;
            Points(sofa, stand: new Vector3(-4.2f, 0f, -4.0f), use: new Vector3(-4.6f, 0.5f, -5.2f), useYaw: 0f);

            var mat = Furniture(all, root, "PlayMat", "Play mat", new Vector3(1.6f, 0f, -2.0f), walkable: true);
            Cube("Mat", mat.transform, new Vector3(1.6f, 0.02f, -2.0f), new Vector3(1.8f, 0.04f, 1.8f), "PlayMat", new Color(0.55f, 0.8f, 0.55f));
            Cube("Arch", mat.transform, new Vector3(1.6f, 0.5f, -1.2f), new Vector3(1.4f, 0.08f, 0.08f), "Bouncer", new Color(0.95f, 0.6f, 0.5f)).GetComponent<Collider>().enabled = false;
            mat.actions = new[] { ActionType.Play };
            mat.babySpot = true;
            mat.babyPose = BabyPose.Sitting;
            Points(mat, stand: new Vector3(1.6f, 0f, -3.0f), baby: new Vector3(1.6f, 0.04f, -1.8f), babyYaw: 180f);

            var bouncer = Furniture(all, root, "Bouncer", "Bouncer", new Vector3(-1.4f, 0f, -2.6f));
            Cube("Seat", bouncer.transform, new Vector3(-1.4f, 0.12f, -2.6f), new Vector3(0.5f, 0.24f, 0.7f), "Bouncer", new Color(0.95f, 0.6f, 0.5f));
            bouncer.babySpot = true;
            bouncer.babyPose = BabyPose.Reclined;
            Points(bouncer, stand: new Vector3(-1.4f, 0f, -3.7f), baby: new Vector3(-1.4f, 0.22f, -2.5f), babyYaw: 180f);

            // Kitchen: counter (bottles) and table (meals).
            var counter = Furniture(all, root, "Counter", "Kitchen counter", new Vector3(9.4f, 0f, -2.6f));
            Cube("Counter", counter.transform, new Vector3(9.4f, 0.45f, -2.6f), new Vector3(1.0f, 0.9f, 3.6f), "Counter", Appliance);
            counter.actions = new[] { ActionType.PrepareBottle };
            Points(counter, stand: new Vector3(8.3f, 0f, -2.6f), use: new Vector3(9.4f, 0.9f, -2.6f));
            Prop(root, "Fridge", new Vector3(9.45f, 0.9f, 0.25f), new Vector3(0.9f, 1.8f, 1.0f), Appliance);

            var table = Furniture(all, root, "KitchenTable", "Kitchen table", new Vector3(6.6f, 0f, -4.0f));
            Cube("Top", table.transform, new Vector3(6.6f, 0.4f, -4.0f), new Vector3(1.4f, 0.8f, 1.0f), "Table", Wood);
            Cube("Chair", table.transform, new Vector3(6.6f, 0.22f, -5.15f), new Vector3(0.5f, 0.45f, 0.5f), "Table", Wood);
            table.actions = new[] { ActionType.Eat };
            table.seatPose = ParentPose.Sit;
            Points(table, stand: new Vector3(5.3f, 0f, -4.6f), use: new Vector3(6.6f, 0.45f, -5.1f), useYaw: 0f);

            // Entrance: the front door, where deliveries arrive.
            var door = Furniture(all, root, "FrontDoor", "Front door", new Vector3(-0.5f, 0f, -8.85f));
            Cube("Door", door.transform, new Vector3(-0.5f, 1.0f, -8.85f), new Vector3(1.0f, 2.0f, 0.12f), "Door", new Color(0.55f, 0.35f, 0.25f));
            Points(door, stand: new Vector3(-0.5f, 0f, -7.8f));
            var doormat = Cube("Doormat", root, new Vector3(-0.5f, 0.01f, -7.9f), new Vector3(1.2f, 0.02f, 0.7f), "Doormat", new Color(0.5f, 0.4f, 0.3f));
            doormat.isStatic = true;

            return all;
        }

        static Interactable Furniture(Dictionary<string, Interactable> all, Transform parent, string name, string label, Vector3 position, bool walkable = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.isStatic = true;
            var interactable = go.AddComponent<Interactable>();
            interactable.displayName = label;
            if (!walkable) AddModifier(go, NotWalkable);
            all[name] = interactable;
            return interactable;
        }

        static void Points(Interactable target, Vector3 stand, Vector3? use = null, float useYaw = 0f, Vector3? baby = null, float babyYaw = 0f)
        {
            target.standPoint = Point(target.transform, "Stand", stand, 0f);
            if (use is Vector3 u) target.usePoint = Point(target.transform, "Use", u, useYaw);
            if (baby is Vector3 b) target.babyPoint = Point(target.transform, "BabySpot", b, babyYaw);
            EditorUtility.SetDirty(target);
        }

        static Transform Point(Transform parent, string name, Vector3 position, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static void Prop(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        {
            var go = Cube(name, parent, center, size, name, color);
            go.isStatic = true;
            AddModifier(go, NotWalkable);
        }

        static void AddModifier(GameObject go, int area)
        {
            var modifier = go.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = area;
        }

        // ---------------------------------------------------------------- characters

        static ParentAvatar BuildParent(Vector3 position)
        {
            var root = new GameObject("Parent");
            root.transform.position = position;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.speed = 9f;
            agent.angularSpeed = 900f;
            agent.acceleration = 80f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 0.9f, 0);
            collider.height = 1.8f;
            collider.radius = 0.35f;

            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var shirt = new Color(0.35f, 0.5f, 0.8f);
            var skin = new Color(0.95f, 0.78f, 0.65f);
            var torso = Primitive(PrimitiveType.Capsule, "Torso", body, new Vector3(0, 0.75f, 0), new Vector3(0.5f, 0.75f, 0.5f), "Parent", shirt);
            Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0, 1.68f, 0), Vector3.one * 0.4f, "ParentSkin", skin);
            Primitive(PrimitiveType.Cube, "Nose", body, new Vector3(0, 1.68f, 0.2f), Vector3.one * 0.08f, "ParentSkin", skin);
            var hand = Primitive(PrimitiveType.Sphere, "Hand", body, new Vector3(0.3f, 1.0f, 0.22f), Vector3.one * 0.14f, "ParentSkin", skin);
            Primitive(PrimitiveType.Cylinder, "Ring", root.transform, new Vector3(0, 0.02f, 0), new Vector3(1.0f, 0.01f, 1.0f), "ParentRing", new Color(0.25f, 0.45f, 0.95f));
            var arms = new GameObject("Arms").transform;
            arms.SetParent(body, false);
            arms.localPosition = new Vector3(0, 0.95f, 0.38f);
            var shoulder = new GameObject("Shoulder").transform;
            shoulder.SetParent(body, false);
            shoulder.localPosition = new Vector3(0.12f, 1.25f, 0.05f);

            var avatar = root.AddComponent<ParentAvatar>();
            Set(avatar, "body", body);
            Set(avatar, "torso", torso.transform);
            Set(avatar, "hand", hand.transform);
            Set(avatar, "arms", arms);
            Set(avatar, "shoulder", shoulder);
            return avatar;
        }

        static BabyAvatar BuildBaby()
        {
            var root = new GameObject("Baby");
            root.transform.localScale = Vector3.one * 1.5f;
            var collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0, 0.25f, 0);
            collider.radius = 0.45f;

            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var onesie = new Color(0.98f, 0.9f, 0.5f);
            var skin = new Color(1f, 0.82f, 0.68f);
            var dark = new Color(0.18f, 0.1f, 0.1f);
            Primitive(PrimitiveType.Capsule, "Torso", body, new Vector3(0, 0.18f, 0), new Vector3(0.22f, 0.18f, 0.2f), "Onesie", onesie);
            var head = Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0, 0.42f, 0), Vector3.one * 0.25f, "BabySkin", skin);
            var mouth = Primitive(PrimitiveType.Sphere, "Mouth", body, new Vector3(0, 0.375f, 0.115f), new Vector3(0.07f, 0.03f, 0.03f), "BabyDark", dark);
            Primitive(PrimitiveType.Sphere, "EyeL", body, new Vector3(-0.05f, 0.45f, 0.11f), Vector3.one * 0.035f, "BabyDark", dark);
            Primitive(PrimitiveType.Sphere, "EyeR", body, new Vector3(0.05f, 0.45f, 0.11f), Vector3.one * 0.035f, "BabyDark", dark);
            var left = Primitive(PrimitiveType.Sphere, "HandL", body, new Vector3(-0.13f, 0.2f, 0.08f), Vector3.one * 0.07f, "BabySkin", skin);
            var right = Primitive(PrimitiveType.Sphere, "HandR", body, new Vector3(0.13f, 0.2f, 0.08f), Vector3.one * 0.07f, "BabySkin", skin);
            var tearColor = new Color(0.4f, 0.7f, 1f);
            var tears = new[]
            {
                Primitive(PrimitiveType.Sphere, "TearL", body, new Vector3(-0.06f, 0.44f, 0.115f), Vector3.one * 0.03f, "Tear", tearColor).transform,
                Primitive(PrimitiveType.Sphere, "TearR", body, new Vector3(0.06f, 0.44f, 0.115f), Vector3.one * 0.03f, "Tear", tearColor).transform,
            };
            var bubbles = new Transform[3];
            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i] = Primitive(PrimitiveType.Sphere, "Zzz" + i, root.transform, new Vector3(0.1f, 0.6f, 0), Vector3.one * 0.05f, "SleepBubble", Color.white).transform;

            var ring = Primitive(PrimitiveType.Cylinder, "Ring", root.transform, new Vector3(0, 0.015f, 0), new Vector3(0.7f, 0.006f, 0.7f), "BabyRing", new Color(1f, 0.85f, 0.2f));

            var avatar = root.AddComponent<BabyAvatar>();
            Set(avatar, "ring", ring.GetComponent<Renderer>());
            Set(avatar, "body", body);
            Set(avatar, "head", head.GetComponent<Renderer>());
            Set(avatar, "mouth", mouth.transform);
            Set(avatar, "leftHand", left.transform);
            Set(avatar, "rightHand", right.transform);
            SetArray(avatar, "tears", tears);
            SetArray(avatar, "sleepBubbles", bubbles);
            return avatar;
        }

        // ---------------------------------------------------------------- helpers

        static GameObject Cube(string name, Transform parent, Vector3 center, Vector3 size, string material, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = ProjectSetup.EnsureMaterial(material, color);
            return go;
        }

        /// <summary>A primitive without a collider (character parts).</summary>
        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, string material, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = ProjectSetup.EnsureMaterial(material, color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Transform[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetAction(Object target, string field, InputActionAsset controls, string action) =>
            Set(target, field, InputActionReference.Create(controls.FindAction("Gameplay/" + action, true)));
    }
}
