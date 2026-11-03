using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hollowknight.Silksong.Editor;

namespace Hollowknight.Silksong.Tests
{
    // Task 7: the grey-box transition rooms. The room content is checked in preview scenes (the builder's Populate
    // works on any scene), so the tests need no scene files and do not touch the open scenes. One test builds real
    // files into a temp folder. Preview scenes are used because EditMode additive scenes fail while the active scene is
    // untitled.
    public class TransitionRoomsBuilderTests
    {
        const string TempFolder = "Assets/Silksong/Tests/Temp";
        const string TempRooms = TempFolder + "/Rooms";

        // The study's gate table (docs/study/10-room-transitions.md, section 6) for the three-room set: gate -> target/gate.
        // Kept here as its own copy on purpose, so a wrong builder table cannot pass by agreeing with itself.
        static readonly Dictionary<string, Dictionary<string, string>> Table = new Dictionary<string, Dictionary<string, string>>
        {
            { "Tut_01", new Dictionary<string, string>
                { { "right1", "Tut_01b/left1" }, { "right2", "Tut_01b/left2" }, { "left2", "Tut_02/right1" }, { "left3", "Tut_02/right2" } } },
            { "Tut_01b", new Dictionary<string, string>
                { { "left1", "Tut_01/right1" }, { "left2", "Tut_01/right2" } } },
            { "Tut_02", new Dictionary<string, string>
                { { "right1", "Tut_01/left2" }, { "right2", "Tut_01/left3" } } },
        };

        readonly List<Scene> previews = new List<Scene>();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in previews) if (s.IsValid()) EditorSceneManager.ClosePreviewScene(s);
            previews.Clear();
            AssetDatabase.DeleteAsset(TempFolder);
            if (Directory.Exists(TempFolder)) Directory.Delete(TempFolder, true);
            if (File.Exists(TempFolder + ".meta")) File.Delete(TempFolder + ".meta");
            AssetDatabase.Refresh();
        }

        Scene Preview(string room)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            previews.Add(scene);
            TransitionRoomsBuilder.Populate(scene, TransitionRoomsBuilder.GetSpec(room), TransitionRoomsBuilder.LoadAssets());
            return scene;
        }

        Dictionary<string, Scene> AllRooms() => TransitionRoomsBuilder.RoomNames.ToDictionary(n => n, Preview);

        static List<T> All<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (var root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        static Rect WorldRect(BoxCollider2D c)
        {
            Vector3 s = c.transform.lossyScale;
            var size = new Vector2(c.size.x * Mathf.Abs(s.x), c.size.y * Mathf.Abs(s.y));
            Vector2 centre = c.transform.TransformPoint(c.offset);
            return new Rect(centre - size * 0.5f, size);
        }

        static List<Rect> Solids(Scene scene) =>
            All<BoxCollider2D>(scene).Where(c => !c.isTrigger).Select(WorldRect).ToList();

        // ---------- the room set ----------

        [Test]
        public void RoomNames_AreTheThreeStudyRooms()
        {
            CollectionAssert.AreEquivalent(new[] { "Tut_01", "Tut_01b", "Tut_02" }, TransitionRoomsBuilder.RoomNames);
            Assert.AreEqual("Assets/Scenes/Rooms", TransitionRoomsBuilder.Folder);
        }

        [Test]
        public void EachRoom_HasExactlyOneRoomInfo_NamedAfterTheRoom_WithBoundsAroundTheFloor()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var scene = Preview(name);
                var infos = All<RoomInfo>(scene);
                Assert.AreEqual(1, infos.Count, name + ": RoomInfo count");
                Assert.AreEqual(name, infos[0].SceneName);
                Assert.AreEqual(1, All<RoomBounds>(scene).Count, name + ": RoomBounds count");
                Assert.IsNotNull(infos[0].Bounds, name + ": RoomInfo has no bounds");
                var area = infos[0].Bounds.Area;
                Assert.Greater(area.width, 31f, name + ": room narrower than the camera view");
                foreach (var gate in All<TransitionPoint>(scene))
                    Assert.IsTrue(gate.transform.position.y >= area.yMin && gate.transform.position.y <= area.yMax,
                        name + "/" + gate.name + " is outside the bounds vertically");
            }
        }

        [Test]
        public void Gates_PerRoom_EqualTheStudyTable()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var gates = All<TransitionPoint>(Preview(name));
                CollectionAssert.AreEquivalent(Table[name].Keys, gates.Select(g => g.name).ToList(), name + ": gate names");
                foreach (var g in gates)
                {
                    Assert.AreEqual(Table[name][g.name], g.TargetScene + "/" + g.EntryGate, name + "/" + g.name);
                    Assert.AreEqual(Vector2.zero, g.EntryOffset, name + "/" + g.name + ": entry offset");
                    Assert.IsTrue(g.HasTarget);
                }
            }
        }

        [Test]
        public void EveryGateTarget_ExistsInTheTargetRoom_AndLinksBack()
        {
            var rooms = AllRooms();
            foreach (var pair in rooms)
                foreach (var gate in All<TransitionPoint>(pair.Value))
                {
                    Assert.IsTrue(rooms.ContainsKey(gate.TargetScene), pair.Key + "/" + gate.name + " leads to a room that is not built: " + gate.TargetScene);
                    var arrival = All<TransitionPoint>(rooms[gate.TargetScene]).FirstOrDefault(g => g.name == gate.EntryGate);
                    Assert.IsNotNull(arrival, gate.TargetScene + " has no gate " + gate.EntryGate);
                    Assert.AreEqual(pair.Key, arrival.TargetScene, "the way back from " + gate.TargetScene + "/" + arrival.name);
                    Assert.AreEqual(gate.name, arrival.EntryGate, "the way back from " + gate.TargetScene + "/" + arrival.name);
                }
        }

        [Test]
        public void GateNames_AreUniquePerRoom_AndEdgeMatchesTheName()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var scene = Preview(name);
                var area = All<RoomInfo>(scene)[0].Bounds.Area;
                var gates = All<TransitionPoint>(scene);
                Assert.AreEqual(gates.Count, gates.Select(g => g.name).Distinct().Count(), name + ": duplicate gate names");
                foreach (var g in gates)
                {
                    var box = g.GetComponent<BoxCollider2D>();
                    Assert.IsNotNull(box);
                    Assert.IsTrue(box.isTrigger, name + "/" + g.name + " must be a trigger");
                    if (g.Position == GatePosition.Left) Assert.Less(g.transform.position.x, area.xMin, name + "/" + g.name);
                    else if (g.Position == GatePosition.Right) Assert.Greater(g.transform.position.x, area.xMax, name + "/" + g.name);
                    else Assert.Fail(name + "/" + g.name + ": only left and right gates are built");
                }
            }
        }

        // ---------- walking ----------

        [Test]
        public void EveryGate_StandsOnGround_AndNothingSolidBlocksIt()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var scene = Preview(name);
                var solids = Solids(scene);
                foreach (var g in All<TransitionPoint>(scene))
                {
                    var rect = WorldRect(g.GetComponent<BoxCollider2D>());
                    bool onGround = solids.Any(s => Mathf.Abs(s.yMax - rect.yMin) < 0.01f && s.xMin < rect.xMax && s.xMax > rect.xMin);
                    Assert.IsTrue(onGround, name + "/" + g.name + ": no floor under the gate");

                    var inner = new Rect(rect.x + 0.01f, rect.y + 0.01f, rect.width - 0.02f, rect.height - 0.02f);
                    Assert.IsFalse(solids.Any(s => s.Overlaps(inner)), name + "/" + g.name + ": a solid box blocks the gate");
                }
            }
        }

        // A coarse walk model: from one floor to another is possible when the gap is at most 5 units and the rise at most
        // 4 (the hero's jump is about 6 high). Checks the layout, not the physics.
        [Test]
        public void EveryGate_IsReachableFromTheStartPoint()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var scene = Preview(name);
                var solids = Solids(scene);
                var start = All<RoomWorldStarter>(scene)[0].StartPoint;
                Assert.IsNotNull(start, name + ": no start point");
                Vector2 p = start.transform.position;

                int first = -1;
                float best = float.NegativeInfinity;
                for (int i = 0; i < solids.Count; i++)
                {
                    var s = solids[i];
                    if (s.xMin <= p.x && s.xMax >= p.x && s.yMax <= p.y && s.yMax > best) { best = s.yMax; first = i; }
                }
                Assert.GreaterOrEqual(first, 0, name + ": nothing under the start point");
                Assert.Less(p.y - best, 2f, name + ": the start point floats above its floor");

                var reached = new HashSet<int>();
                var queue = new Queue<int>();
                reached.Add(first);
                queue.Enqueue(first);
                while (queue.Count > 0)
                {
                    var a = solids[queue.Dequeue()];
                    for (int i = 0; i < solids.Count; i++)
                    {
                        if (reached.Contains(i)) continue;
                        var b = solids[i];
                        float gap = Mathf.Max(0f, Mathf.Max(b.xMin - a.xMax, a.xMin - b.xMax));
                        if (gap <= 5f && b.yMax - a.yMax <= 4f) { reached.Add(i); queue.Enqueue(i); }
                    }
                }

                foreach (var g in All<TransitionPoint>(scene))
                {
                    var rect = WorldRect(g.GetComponent<BoxCollider2D>());
                    bool reachable = reached.Any(i => Mathf.Abs(solids[i].yMax - rect.yMin) < 0.01f && solids[i].xMin < rect.xMax && solids[i].xMax > rect.xMin);
                    Assert.IsTrue(reachable, name + "/" + g.name + " cannot be reached by walking and jumping from the start point");
                }
            }
        }

        // ---------- hazard respawn (task 8) ----------

        [Test]
        public void EachRoom_HasHazardSpotsAndSpikes_AtLeastTwoInTut01bAndTut02()
        {
            Assert.GreaterOrEqual(All<HazardRespawnTrigger>(Preview("Tut_01")).Count, 1);
            Assert.GreaterOrEqual(All<HazardZone>(Preview("Tut_01")).Count, 1);
            foreach (var name in new[] { "Tut_01b", "Tut_02" })
            {
                var scene = Preview(name);
                Assert.GreaterOrEqual(All<HazardRespawnTrigger>(scene).Count, 2, name + ": triggers");
                Assert.GreaterOrEqual(All<HazardZone>(scene).Count, 2, name + ": spikes");
            }
        }

        [Test]
        public void HazardTriggers_LinkAMarkerOfTheSameRoom_StandingOnTheFloor()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var scene = Preview(name);
                var solids = Solids(scene);
                var markers = All<HazardRespawnMarker>(scene);
                foreach (var trig in All<HazardRespawnTrigger>(scene))
                {
                    Assert.IsNotNull(trig.Marker, name + "/" + trig.name + ": no marker");
                    Assert.AreEqual(scene, trig.Marker.gameObject.scene, name + "/" + trig.name + ": marker is in another scene");
                    Assert.IsTrue(trig.GetComponent<BoxCollider2D>().isTrigger, name + "/" + trig.name);
                }
                foreach (var m in markers)
                {
                    Vector2 p = m.transform.position;
                    Assert.IsTrue(solids.Any(s => s.xMin < p.x && s.xMax > p.x && Mathf.Abs(s.yMax - (p.y - 0.05f)) < 0.01f), name + "/" + m.name + ": not on a floor");
                    Assert.IsFalse(solids.Any(s => s.Contains(p)), name + "/" + m.name + ": inside a solid");
                }
            }
        }

        [Test]
        public void Spikes_StandOnFloors_AreTriggers_AndKeepClearOfStartAndGates()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var scene = Preview(name);
                var solids = Solids(scene);
                Vector2 start = All<RoomWorldStarter>(scene)[0].StartPoint.transform.position;
                foreach (var zone in All<HazardZone>(scene))
                {
                    Assert.IsTrue(zone.RespawnsHero, name + ": spikes must respawn the hero");
                    var box = zone.GetComponent<BoxCollider2D>();
                    Assert.IsTrue(box.isTrigger);
                    var rect = WorldRect(box);
                    Assert.IsTrue(solids.Any(s => Mathf.Abs(s.yMax - rect.yMin) < 0.01f && s.xMin < rect.xMax && s.xMax > rect.xMin), name + ": spikes float");
                    Assert.IsFalse(rect.Contains(start), name + ": spikes on the start point");
                    foreach (var g in All<TransitionPoint>(scene))
                        Assert.IsFalse(rect.Overlaps(WorldRect(g.GetComponent<BoxCollider2D>())), name + ": spikes block gate " + g.name);
                }
            }
        }

        [Test]
        public void EveryGate_HasAHazardMarkerInsideTheRoom_AndIsNotANonHazardGate()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var scene = Preview(name);
                var area = All<RoomInfo>(scene)[0].Bounds.Area;
                foreach (var g in All<TransitionPoint>(scene))
                {
                    Assert.IsFalse(g.NonHazardGate, name + "/" + g.name);
                    var m = g.HazardMarker;
                    Assert.IsNotNull(m, name + "/" + g.name + ": no hazard marker");
                    Assert.IsTrue(area.Contains(m.transform.position), name + "/" + g.name + ": marker outside the room");
                }
            }
        }

        [Test]
        public void OnlyTut01_StartMarkerIsTheBench()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var start = All<RoomWorldStarter>(Preview(name))[0].StartPoint;
                Assert.AreEqual("Start", start.MarkerName);
                Assert.AreEqual(name == "Tut_01", start.SetsBenchRespawn, name);
            }
        }

        [Test]
        public void OnlyTheBenchMarker_HasABenchWithATrigger()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var scene = Preview(name);
                var benches = All<Bench>(scene);
                var start = All<RoomWorldStarter>(scene)[0].StartPoint;
                if (name != "Tut_01") { Assert.AreEqual(0, benches.Count, name); continue; }
                Assert.AreEqual(1, benches.Count);
                Assert.AreSame(start.gameObject, benches[0].gameObject, "the bench sits on the bench marker");
                Assert.IsTrue(benches[0].GetComponent<Collider2D>().isTrigger);
                Assert.IsTrue(benches[0].GetComponent<RespawnMarker>().SetsBenchRespawn);
            }
        }

        // ---------- what a room must not hold ----------

        [Test]
        public void Rooms_HoldNoHeroCameraFaderOrManager()
        {
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var scene = Preview(name);
                Assert.AreEqual(0, All<HeroController>(scene).Count, name + ": hero");
                Assert.AreEqual(0, All<Camera>(scene).Count, name + ": camera");
                Assert.AreEqual(0, All<AudioListener>(scene).Count, name + ": audio listener");
                Assert.AreEqual(0, All<GameCamera>(scene).Count, name + ": game camera");
                Assert.AreEqual(0, All<ScreenFader>(scene).Count, name + ": fader");
                Assert.AreEqual(0, All<RoomTransitionManager>(scene).Count, name + ": manager");
                Assert.AreEqual(0, All<RoomWorldBootstrap>(scene).Count, name + ": world");
                Assert.AreEqual(0, All<HeroSceneBootstrap>(scene).Count, name + ": single-room hero bootstrap");
                Assert.AreEqual(1, All<RoomWorldStarter>(scene).Count, name + ": starter");
            }
        }

        [Test]
        public void Starter_HoldsOnlyReferences_AndTheProjectHeroConfig()
        {
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var fi in typeof(RoomWorldStarter).GetFields(all))
            {
                bool serialized = fi.IsPublic || fi.GetCustomAttributes(typeof(SerializeField), true).Length > 0;
                if (!serialized || fi.IsNotSerialized) continue;
                if (fi.Name == "artFacesLeft") continue;   // art direction, as on HeroSceneBootstrap
                Assert.IsTrue(typeof(UnityEngine.Object).IsAssignableFrom(fi.FieldType) || typeof(UnityEngine.Object[]).IsAssignableFrom(fi.FieldType),
                    "RoomWorldStarter." + fi.Name + " is serialized but is not an object reference (a copied value?)");
            }

            var projectConfig = AssetDatabase.LoadAssetAtPath<HeroConfig>(HeroTestSceneBuilder.ConfigPath);
            foreach (var name in TransitionRoomsBuilder.RoomNames)
            {
                var so = new SerializedObject(All<RoomWorldStarter>(Preview(name))[0]);
                Assert.AreSame(projectConfig, so.FindProperty("config").objectReferenceValue, name + ": hero config must be the project asset");
                Assert.IsNotNull(so.FindProperty("actionsAsset").objectReferenceValue, name + ": input actions");
            }
        }

        [Test]
        public void Rooms_AreDistinguishable_ByTint()
        {
            var tints = TransitionRoomsBuilder.RoomNames.Select(n => TransitionRoomsBuilder.GetSpec(n).Tint).ToList();
            Assert.AreEqual(tints.Count, tints.Distinct().Count());
        }

        // ---------- helpers and files ----------

        [Test]
        public void WallSegments_SkipTheOpenings()
        {
            var segments = TransitionRoomsBuilder.WallSegments(-2f, 24f, new List<Vector2> { new Vector2(5f, 10f), new Vector2(0f, 5f) });
            Assert.AreEqual(2, segments.Count);
            Assert.AreEqual(new Vector2(-2f, 0f), segments[0]);
            Assert.AreEqual(new Vector2(10f, 24f), segments[1]);

            var solid = TransitionRoomsBuilder.WallSegments(-2f, 24f, new List<Vector2>());
            Assert.AreEqual(1, solid.Count);
            Assert.AreEqual(new Vector2(-2f, 24f), solid[0]);
        }

        sealed class FakeSource : IRoomSceneSource
        {
            public readonly HashSet<string> Names = new HashSet<string>();
            public readonly List<string> Loaded = new List<string>();
            public bool Exists(string sceneName) => Names.Contains(sceneName);
            public AsyncOperation BeginLoad(string sceneName) { Loaded.Add(sceneName); return null; }
        }

        [Test]
        public void FallbackSource_UsesFirstWhenItHasTheScene_ElseTheSecond_ElseReportsMissing()
        {
            var first = new FakeSource();
            var second = new FakeSource();
            first.Names.Add("Both");
            second.Names.Add("Both");
            second.Names.Add("OnlySecond");
            var source = new FallbackRoomSceneSource(first, second);

            Assert.IsTrue(source.Exists("Both"));
            Assert.IsTrue(source.Exists("OnlySecond"));
            Assert.IsFalse(source.Exists("Nowhere"));

            source.BeginLoad("Both");
            source.BeginLoad("OnlySecond");
            CollectionAssert.AreEqual(new[] { "Both" }, first.Loaded);
            CollectionAssert.AreEqual(new[] { "OnlySecond" }, second.Loaded);
        }

        [Test]
        public void StarterScenePath_IsTheBuilderFolder_AndMatchesPlayModeTests()
        {
            Assert.AreEqual("Assets/Scenes/Rooms/Tut_01.unity", RoomWorldStarter.ScenePath("Tut_01"));
            StringAssert.Contains("\"Assets/Scenes/Rooms\"", File.ReadAllText(Path.Combine(Application.dataPath, "Silksong/Tests/PlayMode/TransitionRoomsWalkTests.cs")));
        }

        [Test]
        public void BuildAll_WithoutDiscardFlag_Throws_AndWritesNothing()
        {
            Assert.Throws<System.InvalidOperationException>(() => TransitionRoomsBuilder.BuildAll(TempRooms, false));
            Assert.IsFalse(Directory.Exists(TempRooms));
        }

        [Test]
        public void BuildAll_WritesTheThreeScenes_NamedAfterTheRooms()
        {
            var paths = TransitionRoomsBuilder.BuildAll(TempRooms, true);
            CollectionAssert.AreEqual(new[] { TempRooms + "/Tut_01.unity", TempRooms + "/Tut_01b.unity", TempRooms + "/Tut_02.unity" }, paths);
            foreach (var path in paths) Assert.IsTrue(File.Exists(path), path);

            try
            {
                foreach (var path in paths)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    Assert.AreEqual(Path.GetFileNameWithoutExtension(path), scene.name);
                    Assert.AreEqual(scene.name, All<RoomInfo>(scene).Single().SceneName);
                    Assert.AreEqual(Table[scene.name].Count, All<TransitionPoint>(scene).Count, scene.name + ": gates after saving");
                    Assert.AreEqual(1, All<RoomWorldStarter>(scene).Count);
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void GitIgnore_ListsTheGeneratedRoomsFolder()
        {
            string ignore = File.ReadAllText(Path.Combine(Application.dataPath, "..", ".gitignore"));
            StringAssert.Contains("/Assets/Scenes/Rooms/", ignore);
            StringAssert.Contains("/Assets/Scenes/Rooms.meta", ignore);
        }
    }
}
