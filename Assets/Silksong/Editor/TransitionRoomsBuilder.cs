using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong.Editor
{
    // One solid box of a grey-box room (world units, the top edge is the walkable surface).
    public sealed class BlockSpec
    {
        public readonly string Name;
        public readonly float MinX, MaxX, Top, Bottom;

        public BlockSpec(string name, float minX, float maxX, float top, float bottom)
        {
            Name = name;
            MinX = minX;
            MaxX = maxX;
            Top = top;
            Bottom = bottom;
        }
    }

    // A gate of a grey-box room. The edge comes from the name ("left2" is on the left edge, "right1" on the right),
    // GroundTop is the floor height the hero walks on at that gate.
    public sealed class GateSpec
    {
        public readonly string Name;
        public readonly float GroundTop;
        public readonly string TargetScene;
        public readonly string EntryGate;

        public GateSpec(string name, float groundTop, string targetScene, string entryGate)
        {
            Name = name;
            GroundTop = groundTop;
            TargetScene = targetScene;
            EntryGate = entryGate;
        }

        public bool OnLeftEdge => Name.StartsWith("left");
    }

    // A spike patch standing on a floor (x range, GroundTop = the floor height under it; the patch is 1 high).
    public sealed class SpikeSpec
    {
        public readonly float MinX, MaxX, GroundTop;
        public SpikeSpec(float minX, float maxX, float groundTop) { MinX = minX; MaxX = maxX; GroundTop = groundTop; }
    }

    // A hazard respawn pair: the trigger box (x range) and the marker it stores (MarkerX), both on a floor.
    public sealed class HazardSpotSpec
    {
        public readonly string Name;
        public readonly float TriggerMinX, TriggerMaxX, MarkerX, GroundTop;
        public readonly bool FacingRight;
        public HazardSpotSpec(string name, float triggerMinX, float triggerMaxX, float markerX, float groundTop, bool facingRight)
        {
            Name = name; TriggerMinX = triggerMinX; TriggerMaxX = triggerMaxX; MarkerX = markerX; GroundTop = groundTop; FacingRight = facingRight;
        }
    }

    // A wall the nail breaks (Task 3 of Milestone 3). Id is the explicit PersistentBool id, unique within the room.
    public sealed class BreakableWallSpec
    {
        public readonly string Id;
        public readonly float MinX, MaxX, Top, Bottom;
        public BreakableWallSpec(string id, float minX, float maxX, float top, float bottom) { Id = id; MinX = minX; MaxX = maxX; Top = top; Bottom = bottom; }
    }

    // A pickup standing on a floor: X is its centre, GroundTop the floor under it. It is a PickupSize square.
    public sealed class PickupSpec
    {
        public readonly string Id;
        public readonly float X, GroundTop;
        public PickupSpec(string id, float x, float groundTop) { Id = id; X = x; GroundTop = groundTop; }
    }

    // A Moss Creep standing on a floor: X is the centre of the walking stretch, FloorTop the surface it walks on,
    // StretchMinX/MaxX the floor's ends (a ledge or wall stops it there). Id is the explicit EnemyPersistence id.
    public sealed class EnemySpec
    {
        public readonly string Id;
        public readonly float X, FloorTop, StretchMinX, StretchMaxX;
        public readonly bool FacingRight;
        public EnemySpec(string id, float x, float floorTop, float stretchMinX, float stretchMaxX, bool facingRight)
        {
            Id = id; X = x; FloorTop = floorTop; StretchMinX = stretchMinX; StretchMaxX = stretchMaxX; FacingRight = facingRight;
        }
    }

    public sealed class RoomSpec
    {
        public readonly List<EnemySpec> Enemies = new List<EnemySpec>();
        public readonly List<BreakableWallSpec> BreakableWalls = new List<BreakableWallSpec>();
        public readonly List<PickupSpec> Pickups = new List<PickupSpec>();
        // True: the Start marker is the bench (the death respawn). Only the start room, Tut_01.
        public bool StartIsBench;
        public readonly List<SpikeSpec> Spikes = new List<SpikeSpec>();
        public readonly List<HazardSpotSpec> HazardSpots = new List<HazardSpotSpec>();
        public string Name;
        public string Title;
        public float Width, Height;
        public float StartX;
        public Color Tint;
        public readonly List<BlockSpec> Blocks = new List<BlockSpec>();
        public readonly List<GateSpec> Gates = new List<GateSpec>();
        // Text put in the room where an original exit was left out (name, x, y).
        public readonly List<KeyValuePair<string, Vector2>> Notes = new List<KeyValuePair<string, Vector2>>();
    }

    // The assets every generated room refers to (loaded once per room scene; see Populate).
    public sealed class RoomAssets
    {
        public HeroConfig Config;
        public InputActionAsset Actions;
        public Sprite WhiteSprite;
        public Sprite HeroSprite;
        public AnimationClip[] HeroClips;
        public Font LabelFont;
        // The Moss Creep config (created on demand, see MossCreepConfigAsset).
        public SilksongEnemyConfig MossCreepConfig;
    }

    // Builds the three grey-box rooms of Milestone 2 (Tut_01, Tut_01b, Tut_02) with Silksong's gate names and links
    // (study 10, section 6), small enough to walk through in a minute. Real room art and geometry come later.
    //
    // Rooms hold floors, walls, gates, RoomInfo, RoomBounds and one RoomWorldStarter and nothing else: the hero,
    // camera, fader and manager are the persistent world (RoomWorldBootstrap), created by the starter when a room is
    // opened and played directly. Every hero number still comes from the one HeroConfig.asset (the starter holds a
    // reference); camera numbers from GameCameraSettings, timings from RoomTransitionSettings.
    //
    // The scenes are generated into Assets/Scenes/Rooms and are gitignored (they point at gitignored hero art by id
    // when it is imported). Scene file name == scene name == the target name the gates use. They are NOT added to
    // Build Settings (ProjectSettings is the owner's); in the Editor the world loads them by path instead.
    //
    // Not built (rooms outside the first three): Tut_01 left1 -> Tut_03 and top1 -> Bonetown, Tut_01b right1 ->
    // Weave_04. Notes in the rooms say so and the menu command logs a stub.
    public static class TransitionRoomsBuilder
    {
        public const string Folder = RoomWorldStarter.RoomsFolder;
        public static readonly string[] RoomNames = { "Tut_01", "Tut_01b", "Tut_02" };

        // Gate trigger box: 1 wide, 4 tall, standing on the floor at the room edge, half outside the room like the
        // original's. The wall has a 5 high opening there and a floor stub carries the hero out and in.
        public const float GateWidth = 1f, GateHeight = 4f, OpeningHeight = 5f, StubLength = 4f;

        // The explicit save ids of the Tut_01 wall and pickup (scene name + id is the save key; never rename these).
        public const string Tut01WallId = "Tut_01_BreakableWall_1";
        public const string Tut01PickupId = "Tut_01_Pickup_1";
        public const float PickupSize = 1f;

        // The explicit save id of the Tut_01b Moss Creep (scene name + id is the save key; never rename it).
        public const string Tut01bMossCreepId = "Tut_01b_MossCreep_1";

        const string MissingAssetMessage = "Required asset missing or not imported: ";

        // ---------- menu ----------

        [MenuItem("Silksong/Build Transition Rooms")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                var paths = BuildAll(Folder, true);
                LogStubs();
                EditorSceneManager.OpenScene(paths[0]);
                Debug.Log("[TransitionRoomsBuilder] Built " + string.Join(", ", paths) + ". Opened " + paths[0] + "; press Play.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[TransitionRoomsBuilder] " + e.Message);
            }
        }

        // -executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch
        public static void BuildBatch()
        {
            try
            {
                var paths = BuildAll(Folder, true);
                LogStubs();
                Debug.Log("[TransitionRoomsBuilder] Built " + string.Join(", ", paths));
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[TransitionRoomsBuilder] FAILED: " + e.Message);
                EditorApplication.Exit(1);
            }
        }

        static void LogStubs()
        {
            Stubs.NotImplemented("Transition rooms: exits Tut_01 left1 -> Tut_03, Tut_01 top1 -> Bonetown and Tut_01b right1 -> Weave_04 are not built");
        }

        // ---------- build ----------

        // Builds all three scenes into the folder and returns their paths. Replaces whatever scenes are open.
        public static string[] BuildAll(string folder, bool discardOpenScenes)
        {
            if (!discardOpenScenes)
                throw new System.InvalidOperationException("Build replaces all open scenes without saving; pass discardOpenScenes: true to confirm.");

            // Fail before touching any scene, with the instruction the owner needs.
            LoadAssets();

            EnsureFolder(folder);
            var paths = new List<string>();
            try
            {
                foreach (var name in RoomNames)
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    // Loaded after the scene switch: switching can unload assets fetched earlier.
                    Populate(scene, GetSpec(name), LoadAssets());
                    string path = folder + "/" + name + ".unity";
                    if (!EditorSceneManager.SaveScene(scene, path))
                        throw new System.InvalidOperationException("Could not save " + path);
                    paths.Add(path);
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            AssetDatabase.Refresh();
            return paths.ToArray();
        }

        // Config and input actions are required. Hero art is optional: without the imported clips the rooms are
        // built anyway and the starter draws the hero as a plain box.
        public static RoomAssets LoadAssets()
        {
            var assets = new RoomAssets
            {
                Config = Require<HeroConfig>(HeroTestSceneBuilder.ConfigPath),
                Actions = Require<InputActionAsset>(HeroTestSceneBuilder.InputActionsPath),
                WhiteSprite = Require<Sprite>(HeroTestSceneBuilder.SpritePath),
                LabelFont = ResolveLabelFont(),
                MossCreepConfig = MossCreepConfigAsset.EnsureAsset(),
            };
            assets.HeroSprite = assets.WhiteSprite;
            assets.HeroClips = new AnimationClip[0];

            var clips = new List<AnimationClip>();
            foreach (var group in new[] { HeroClipNames.PlayedByHeroCode, HeroClipNames.ImportedForLater })
                foreach (var clipName in group)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroClipNames.ClipPath(clipName));
                    if (clip == null) return assets;   // incomplete import: plain box hero
                    clips.Add(clip);
                    var loopPart = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroClipNames.LoopClipPath(clipName));
                    if (loopPart != null) clips.Add(loopPart);
                }
            assets.HeroClips = clips.ToArray();

            // The first Idle frame, so the hero is visible before the animation plays.
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroClipNames.ClipPath("Idle"));
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(idle);
            if (bindings.Length > 0)
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(idle, bindings[0]);
                if (keys.Length > 0 && keys[0].value is Sprite first) assets.HeroSprite = first;
            }
            return assets;
        }

        static T Require<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new System.InvalidOperationException(MissingAssetMessage + path);
            return asset;
        }

        static Font ResolveLabelFont()
        {
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try { var f = Resources.GetBuiltinResource<Font>(name); if (f != null) return f; }
                catch (System.Exception) { /* name not valid in this Unity version; try the next */ }
            }
            try { return Font.CreateDynamicFontFromOSFont("Arial", 16); }
            catch (System.Exception) { return null; }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ---------- room data ----------

        // Heights come from the study's gate heights, scaled down (original 14 / 33 / 38 / 91 become 0 / 5 / 7.5 / 15).
        // Steps are 2.5 units high, well inside the hero's jump, so every gate floor is reachable by walking and jumping.
        public static RoomSpec GetSpec(string room)
        {
            switch (room)
            {
                case "Tut_01": return Tut01();
                case "Tut_01b": return Tut01b();
                case "Tut_02": return Tut02();
                default: throw new System.ArgumentException("Unknown transition room: " + room);
            }
        }

        static RoomSpec Tut01()
        {
            var r = new RoomSpec { Name = "Tut_01", Title = "hub: Tut_01b to the right, Tut_02 to the left", Width = 40f, Height = 24f, StartX = 14f, Tint = new Color(0.35f, 0.42f, 0.6f) };
            r.Blocks.Add(new BlockSpec("Floor", 0f, 40f, 0f, -2f));
            r.Blocks.Add(new BlockSpec("LedgeLeft", 0f, 6f, 5f, 4f));          // left2 stands on this ledge
            r.Blocks.Add(new BlockSpec("StepLeft", 8f, 12f, 2.5f, 0f));
            r.Blocks.Add(new BlockSpec("StepRight1", 16f, 20f, 2.5f, 0f));
            r.Blocks.Add(new BlockSpec("StepRight2", 20f, 24f, 5f, 0f));
            r.Blocks.Add(new BlockSpec("LedgeRightLow", 24f, 40f, 7.5f, 0f));  // right2
            r.Blocks.Add(new BlockSpec("PlatformA", 18f, 22f, 10f, 9.5f));
            r.Blocks.Add(new BlockSpec("PlatformB", 12f, 17f, 12.5f, 12f));
            r.Blocks.Add(new BlockSpec("LedgeRightHigh", 20f, 40f, 15f, 14f)); // right1
            r.Gates.Add(new GateSpec("right1", 15f, "Tut_01b", "left1"));
            r.Gates.Add(new GateSpec("right2", 7.5f, "Tut_01b", "left2"));
            r.Gates.Add(new GateSpec("left2", 5f, "Tut_02", "right1"));
            r.Gates.Add(new GateSpec("left3", 0f, "Tut_02", "right2"));
            // Rooms that remember (Milestone 3 task 3). A shelf continues PlatformB to the left wall, and a roof closes
            // its far end into a nook (3 high inside, the hero is 2.1). The breakable wall seals the nook; the hero
            // reaches the shelf the same way it reaches PlatformB, and breaks the wall with the nail. The pickup sits on
            // StepLeft, which the hero walks over anyway. No existing coordinate is changed.
            r.Blocks.Add(new BlockSpec("ShelfLeft", 0f, 12f, 12.5f, 12f));
            r.Blocks.Add(new BlockSpec("NookRoof", 0f, 6f, 16f, 15.5f));
            r.BreakableWalls.Add(new BreakableWallSpec(Tut01WallId, 6f, 7f, 15.5f, 12.5f));
            r.Pickups.Add(new PickupSpec(Tut01PickupId, 10f, 2.5f));
            r.StartIsBench = true;
            // Walk right along the ledge: touch the yellow trigger, then fall into the spikes and reappear at its marker.
            r.HazardSpots.Add(new HazardSpotSpec("HazardA", 25.5f, 28.5f, 27f, 7.5f, true));
            r.Spikes.Add(new SpikeSpec(30f, 32f, 7.5f));
            r.Notes.Add(new KeyValuePair<string, Vector2>("left1 -> Tut_03 and top1 -> Bonetown: not built", new Vector2(20f, 19f)));
            return r;
        }

        static RoomSpec Tut01b()
        {
            var r = new RoomSpec { Name = "Tut_01b", Title = "left gates lead back to Tut_01", Width = 56f, Height = 24f, StartX = 34f, Tint = new Color(0.3f, 0.55f, 0.35f) };
            r.Blocks.Add(new BlockSpec("Floor", 0f, 56f, 0f, -2f));
            r.Blocks.Add(new BlockSpec("StepRight1", 20f, 24f, 2.5f, 0f));
            r.Blocks.Add(new BlockSpec("StepRight2", 16f, 20f, 5f, 0f));
            r.Blocks.Add(new BlockSpec("LedgeLeftLow", 0f, 16f, 7.5f, 0f));    // left2
            r.Blocks.Add(new BlockSpec("PlatformA", 18f, 22f, 10f, 9.5f));
            r.Blocks.Add(new BlockSpec("PlatformB", 23f, 28f, 12.5f, 12f));
            r.Blocks.Add(new BlockSpec("LedgeLeftHigh", 0f, 20f, 15f, 14f));   // left1
            r.Blocks.Add(new BlockSpec("Bump", 40f, 46f, 2f, 0f));
            r.Gates.Add(new GateSpec("left1", 15f, "Tut_01", "right1"));
            r.Gates.Add(new GateSpec("left2", 7.5f, "Tut_01", "right2"));
            r.HazardSpots.Add(new HazardSpotSpec("HazardLeft", 29.5f, 32.5f, 31f, 0f, false));
            r.Spikes.Add(new SpikeSpec(28f, 29.5f, 0f));
            r.HazardSpots.Add(new HazardSpotSpec("HazardRight", 47f, 49f, 48f, 0f, true));
            r.Spikes.Add(new SpikeSpec(50f, 52f, 0f));
            r.Notes.Add(new KeyValuePair<string, Vector2>("right1 -> Weave_04: not built (wall)", new Vector2(48f, 4f)));
            // The first enemy (Milestone 4 task 6). It walks on top of the Bump (x 40..46, top 2): a ledge at both ends
            // turns it round, so it stays on the Bump. That stretch is clear of every gate (the nearest, left2, is 40 units
            // away), the hazard triggers (29.5..32.5 and 47..49), the spikes (28..29.5 and 50..52) and the Start marker
            // (x 34). The hero jumps up onto the Bump to meet it. No existing coordinate is changed.
            r.Enemies.Add(new EnemySpec(Tut01bMossCreepId, 43f, 2f, 40f, 46f, true));
            return r;
        }

        static RoomSpec Tut02()
        {
            var r = new RoomSpec { Name = "Tut_02", Title = "right gates lead back to Tut_01", Width = 48f, Height = 20f, StartX = 20f, Tint = new Color(0.62f, 0.45f, 0.3f) };
            r.Blocks.Add(new BlockSpec("Floor", 0f, 48f, 0f, -2f));
            r.Blocks.Add(new BlockSpec("LedgeRight", 42f, 48f, 5f, 4f));       // right1 stands on this ledge
            r.Blocks.Add(new BlockSpec("StepRight", 36f, 40f, 2.5f, 0f));
            r.Blocks.Add(new BlockSpec("Bump", 8f, 14f, 2.5f, 0f));
            r.Gates.Add(new GateSpec("right1", 5f, "Tut_01", "left2"));
            r.Gates.Add(new GateSpec("right2", 0f, "Tut_01", "left3"));
            r.HazardSpots.Add(new HazardSpotSpec("HazardA", 21f, 23f, 22f, 0f, true));
            r.Spikes.Add(new SpikeSpec(24f, 26f, 0f));
            r.HazardSpots.Add(new HazardSpotSpec("HazardB", 28f, 30.5f, 29f, 0f, true));
            r.Spikes.Add(new SpikeSpec(32f, 34f, 0f));
            return r;
        }

        // ---------- room objects ----------

        // Height of the hero root above the feet, from the original collider (same as HeroSceneBuilder).
        static float RootAboveFeet => SilksongUnits.HeroColliderSize.y * 0.5f - SilksongUnits.HeroColliderOffset.y;

        // Fills a scene with one room. Used by the builder and, with a preview scene, by the tests.
        public static void Populate(Scene scene, RoomSpec spec, RoomAssets assets)
        {
            var root = new GameObject("Room");
            SceneManager.MoveGameObjectToScene(root, scene);
            var info = root.AddComponent<RoomInfo>();

            var world = Child(root.transform, "Geometry");
            var gates = Child(root.transform, "Gates");
            var labels = Child(root.transform, "Labels");

            // Backdrop: the room's own dark tint, so rooms look different even where the camera sees no floor.
            var backdrop = Visual(world, "Backdrop", new Vector2(spec.Width * 0.5f, spec.Height * 0.5f - 0.5f),
                new Vector2(spec.Width + 2f, spec.Height + 3f), Shade(spec.Tint, 0.3f), assets.WhiteSprite);
            backdrop.sortingOrder = -10;

            foreach (var b in spec.Blocks) Block(world, b.Name, b.MinX, b.MaxX, b.Top, b.Bottom, spec.Tint, assets.WhiteSprite);
            var wallColor = Shade(spec.Tint, 0.6f);
            BuildWalls(world, spec, wallColor, assets.WhiteSprite);

            var hazards = Child(root.transform, "Hazards");
            foreach (var g in spec.Gates)
            {
                var gateComponent = BuildGate(gates, g, spec, assets.WhiteSprite);
                // Arriving here sets the hazard respawn spot to a marker a few units inside the room, on the same floor.
                float markerX = g.OnLeftEdge ? GateMarkerInset : spec.Width - GateMarkerInset;
                var gateMarker = BuildHazardMarker(hazards, "HazardMarker_" + g.Name, markerX, g.GroundTop, g.OnLeftEdge, assets.WhiteSprite);
                gateComponent.ConfigureHazard(gateMarker, false);
                // Floor stub outside the room: the hero walks out over it, and arrives on it.
                float x0 = g.OnLeftEdge ? -StubLength : spec.Width;
                Block(world, "Stub_" + g.Name, x0, x0 + StubLength, g.GroundTop, g.GroundTop - 1f, spec.Tint, assets.WhiteSprite);
                Label(labels, g.Name + "\n-> " + g.TargetScene + " / " + g.EntryGate,
                    new Vector3(g.OnLeftEdge ? 4f : spec.Width - 4f, g.GroundTop + 4.6f, 0f), assets.LabelFont, 0.1f);
            }
            foreach (var note in spec.Notes) Label(labels, note.Key, new Vector3(note.Value.x, note.Value.y, 0f), assets.LabelFont, 0.1f);
            Label(labels, spec.Name + "\n" + spec.Title, new Vector3(spec.Width * 0.5f, spec.Height - 3f, 0f), assets.LabelFont, 0.2f);

            foreach (var h in spec.HazardSpots) BuildHazardSpot(hazards, h, assets.WhiteSprite);
            foreach (var s in spec.Spikes) BuildSpikes(hazards, s, assets.WhiteSprite);

            var memory = Child(root.transform, "Memory");
            foreach (var w in spec.BreakableWalls) BuildBreakableWall(memory, w, spec.Tint, assets.WhiteSprite);
            foreach (var p in spec.Pickups) BuildPickup(memory, p, assets.WhiteSprite);

            if (spec.Enemies.Count > 0)
            {
                if (assets.MossCreepConfig == null) throw new System.InvalidOperationException("Moss Creep config missing: run Silksong/Build Moss Creep Config");
                var enemies = Child(root.transform, "Enemies");
                foreach (var e in spec.Enemies) BuildEnemy(enemies, e, assets.MossCreepConfig);
            }

            var bounds = RoomBounds.Create(new Rect(0f, 0f, spec.Width, spec.Height));
            bounds.transform.SetParent(root.transform, false);
            info.Configure(spec.Name, 13, "Moss Cave", bounds);

            var marker = BuildStart(root.transform, spec, assets.WhiteSprite);
            var starterGo = new GameObject("RoomWorldStarter");
            starterGo.transform.SetParent(root.transform, false);
            starterGo.AddComponent<RoomWorldStarter>().Configure(assets.Config, assets.Actions, marker, assets.HeroSprite, assets.HeroClips);
        }

        // A darker copy of a colour (Color * float would scale the alpha too).
        static Color Shade(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, 1f);

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // How far inside the room the gate's hazard marker stands.
        public const float GateMarkerInset = 3f;
        public const float SpikeHeight = 1f, HazardTriggerHeight = 3f;

        // A marker on a floor: the transform is a hair above the floor, so it finds the floor below (and still works
        // if the search finds nothing). A small yellow strip shows it.
        static HazardRespawnMarker BuildHazardMarker(Transform parent, string name, float x, float groundTop, bool facingRight, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, groundTop + 0.05f, 0f);
            var marker = go.AddComponent<HazardRespawnMarker>();
            marker.Configure(facingRight);
            var strip = Visual(go.transform, "MarkerStrip", new Vector2(x, groundTop + 0.05f), new Vector2(0.6f, 0.1f), new Color(1f, 0.85f, 0.2f), sprite);
            strip.sortingOrder = 1;
            return marker;
        }

        static void BuildHazardSpot(Transform parent, HazardSpotSpec h, Sprite sprite)
        {
            var marker = BuildHazardMarker(parent, "HazardMarker_" + h.Name, h.MarkerX, h.GroundTop, h.FacingRight, sprite);
            float cx = (h.TriggerMinX + h.TriggerMaxX) * 0.5f;
            var vis = Visual(parent, "HazardTrigger_" + h.Name, new Vector2(cx, h.GroundTop + HazardTriggerHeight * 0.5f),
                new Vector2(h.TriggerMaxX - h.TriggerMinX, HazardTriggerHeight), new Color(1f, 0.85f, 0.2f, 0.15f), sprite);
            vis.sortingOrder = 1;
            var box = vis.gameObject.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;   // the object's scale carries the real size
            box.isTrigger = true;
            vis.gameObject.AddComponent<HazardRespawnTrigger>().Configure(marker);
        }

        static void BuildSpikes(Transform parent, SpikeSpec s, Sprite sprite)
        {
            var vis = Visual(parent, "Spikes", new Vector2((s.MinX + s.MaxX) * 0.5f, s.GroundTop + SpikeHeight * 0.5f),
                new Vector2(s.MaxX - s.MinX, SpikeHeight), new Color(0.85f, 0.15f, 0.15f), sprite);
            vis.sortingOrder = 2;
            var box = vis.gameObject.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.isTrigger = true;
            vis.gameObject.AddComponent<HazardZone>().SetRespawnsHero(true);
        }

        // A lighter, solid box so it reads as breakable; the object name is the id so the hierarchy shows it.
        static void BuildBreakableWall(Transform parent, BreakableWallSpec w, Color tint, Sprite sprite)
        {
            var center = new Vector2((w.MinX + w.MaxX) * 0.5f, (w.Top + w.Bottom) * 0.5f);
            var vis = Visual(parent, w.Id, center, new Vector2(w.MaxX - w.MinX, w.Top - w.Bottom), Color.Lerp(tint, Color.white, 0.5f), sprite);
            vis.sortingOrder = 1;
            vis.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;   // solid; the scale carries the size
            vis.gameObject.AddComponent<BreakableWall>().Configure(w.Id);
        }

        static void BuildPickup(Transform parent, PickupSpec p, Sprite sprite)
        {
            var vis = Visual(parent, p.Id, new Vector2(p.X, p.GroundTop + PickupSize * 0.5f), new Vector2(PickupSize, PickupSize), new Color(1f, 0.9f, 0.3f), sprite);
            vis.sortingOrder = 1;
            var box = vis.gameObject.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.isTrigger = true;
            vis.gameObject.AddComponent<Pickup>().Configure(p.Id);
        }

        // Only the MossCreep component goes into the room (config, save id, facing); it builds the rest when the room
        // loads (it cannot run in the Editor). The object is named after the id so the hierarchy shows it.
        static void BuildEnemy(Transform parent, EnemySpec e, SilksongEnemyConfig config)
        {
            var go = new GameObject(e.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(e.X, MossCreep.RootYOnFloor(config, e.FloorTop), 0f);
            go.AddComponent<MossCreep>().Configure(config, e.Id, e.FacingRight);
        }

        static RespawnMarker BuildStart(Transform parent, RoomSpec spec, Sprite sprite)
        {
            var go = new GameObject("Start");
            go.transform.SetParent(parent, false);
            // Root a hair above where the feet touch the floor, so the hero settles instead of starting inside it.
            go.transform.position = new Vector3(spec.StartX, 0f + RootAboveFeet + 0.01f, 0f);
            var marker = go.AddComponent<RespawnMarker>();
            marker.Configure("Start", true);
            // A green strip at the feet, world-positioned.
            var strip = Visual(go.transform, "StartStrip", new Vector2(spec.StartX, 0.05f), new Vector2(1f, 0.1f), new Color(0.3f, 0.9f, 0.4f), sprite);
            strip.sortingOrder = 1;
            if (spec.StartIsBench)
            {
                marker.SetBench(true);
                // A bench-like seat beside the strip: where the hero comes back after dying in any room.
                var seat = Visual(go.transform, "BenchSeat", new Vector2(spec.StartX + 1.6f, 0.3f), new Vector2(2f, 0.6f), new Color(0.55f, 0.35f, 0.2f), sprite);
                seat.sortingOrder = 1;
                // The resting spot: a trigger around the strip and seat. Standing in it and pressing up rests (Bench).
                var area = go.AddComponent<BoxCollider2D>();
                area.isTrigger = true;
                area.offset = new Vector2(1f, 0.5f);
                area.size = new Vector2(4f, 2.5f);
                go.AddComponent<Bench>();
            }
            return marker;
        }

        static TransitionPoint BuildGate(Transform parent, GateSpec g, RoomSpec spec, Sprite sprite)
        {
            var go = new GameObject(g.Name);
            go.transform.SetParent(parent, false);
            float x = g.OnLeftEdge ? -GateWidth * 0.5f : spec.Width + GateWidth * 0.5f;
            go.transform.position = new Vector3(x, g.GroundTop + GateHeight * 0.5f, 0f);
            // The collider first: TransitionPoint requires one.
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(GateWidth, GateHeight);
            box.isTrigger = true;
            var point = go.AddComponent<TransitionPoint>();
            point.Configure(g.TargetScene, g.EntryGate, Vector2.zero);
            var marker = Visual(go.transform, "GateMarker", go.transform.position, new Vector2(GateWidth, GateHeight), new Color(0.2f, 0.9f, 0.9f, 0.35f), sprite);
            marker.sortingOrder = 2;
            return point;
        }

        // Room walls and ceiling. The side walls leave an opening at every gate.
        static void BuildWalls(Transform parent, RoomSpec spec, Color color, Sprite sprite)
        {
            var leftOpenings = new List<Vector2>();
            var rightOpenings = new List<Vector2>();
            foreach (var g in spec.Gates)
                (g.OnLeftEdge ? leftOpenings : rightOpenings).Add(new Vector2(g.GroundTop, g.GroundTop + OpeningHeight));

            float bottom = -2f, top = spec.Height;
            WallSide(parent, "WallLeft", -1f, 0f, bottom, top, leftOpenings, color, sprite);
            WallSide(parent, "WallRight", spec.Width, spec.Width + 1f, bottom, top, rightOpenings, color, sprite);
            Block(parent, "Ceiling", -1f, spec.Width + 1f, top + 1f, top, color, sprite);
        }

        // The wall between bottom and top except the openings (each a y range).
        public static List<Vector2> WallSegments(float bottom, float top, List<Vector2> openings)
        {
            var sorted = new List<Vector2>(openings);
            sorted.Sort((a, b) => a.x.CompareTo(b.x));
            var segments = new List<Vector2>();
            float y = bottom;
            foreach (var o in sorted)
            {
                if (o.x > y) segments.Add(new Vector2(y, o.x));
                if (o.y > y) y = o.y;
            }
            if (top > y) segments.Add(new Vector2(y, top));
            return segments;
        }

        static void WallSide(Transform parent, string name, float minX, float maxX, float bottom, float top, List<Vector2> openings, Color color, Sprite sprite)
        {
            int i = 0;
            foreach (var seg in WallSegments(bottom, top, openings))
                Block(parent, name + (i++), minX, maxX, seg.y, seg.x, color, sprite);
        }

        static SpriteRenderer Visual(Transform parent, string name, Vector2 center, Vector2 size, Color color, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            return sr;
        }

        // A solid box on the default layer, which HeroController.GroundLayers (everything) treats as floor.
        static void Block(Transform parent, string name, float minX, float maxX, float top, float bottom, Color color, Sprite sprite)
        {
            var center = new Vector2((minX + maxX) * 0.5f, (top + bottom) * 0.5f);
            var sr = Visual(parent, name, center, new Vector2(maxX - minX, top - bottom), color, sprite);
            // Unit size: the object's scale already carries the real dimensions.
            sr.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        static void Label(Transform parent, string text, Vector3 position, Font font, float characterSize)
        {
            if (font == null) return;
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 48;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            var renderer = go.GetComponent<MeshRenderer>();
            // A TextMesh draws nothing without the font's own material.
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 10;
        }
    }
}
