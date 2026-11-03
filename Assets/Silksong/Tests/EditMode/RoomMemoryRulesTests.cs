using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Hollowknight.Silksong.Editor;

namespace Hollowknight.Silksong.Tests
{
    // Milestone 3 task 3: the pure rules behind rooms that remember, the PersistentBool storage lookup (fallback
    // PlayerData, current-data-at-use-time) and the Tut_01 wall and pickup added by the room builder.
    public class RoomMemoryRulesTests
    {
        readonly List<GameObject> made = new List<GameObject>();
        readonly List<Scene> previews = new List<Scene>();

        [SetUp]
        public void SetUp()
        {
            PersistentBool.ResetStatics();
            Stubs.StubsDocPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rm-edit-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var g in made) if (g != null) Object.DestroyImmediate(g);
            made.Clear();
            foreach (var s in previews) if (s.IsValid()) EditorSceneManager.ClosePreviewScene(s);
            previews.Clear();
            PersistentBool.ResetStatics();
            if (Stubs.StubsDocPath != null && System.IO.File.Exists(Stubs.StubsDocPath)) System.IO.File.Delete(Stubs.StubsDocPath);
            Stubs.StubsDocPath = null;
        }

        PersistentBool Make(string room, string id)
        {
            var go = new GameObject("obj");
            made.Add(go);
            var p = go.AddComponent<PersistentBool>();
            p.Configure(null, room, id);   // null data: use the current PlayerData at use time
            return p;
        }

        // ---------- pure rules ----------

        [Test]
        public void StartsDone_OnlyWhenStoredAndTrue()
        {
            Assert.IsFalse(PersistentRules.StartsDone(false, false));
            Assert.IsFalse(PersistentRules.StartsDone(false, true), "no entry cannot mean done");
            Assert.IsFalse(PersistentRules.StartsDone(true, false));
            Assert.IsTrue(PersistentRules.StartsDone(true, true));
        }

        [Test]
        public void IdProblem_FlagsEmptyIds()
        {
            Assert.IsNotNull(PersistentRules.IdProblem(null));
            Assert.IsNotNull(PersistentRules.IdProblem(""));
            Assert.IsNull(PersistentRules.IdProblem("Tut_01_BreakableWall_1"));
        }

        // ---------- storage lookup ----------

        [Test]
        public void Set_WritesToFallbackSceneData_UnderSceneAndId()
        {
            var p = Make("Room_A", "wall_1");
            Assert.IsFalse(p.IsSet);
            p.Set(true);
            Assert.IsTrue(p.IsSet);
            Assert.IsTrue(PersistentBool.Fallback.sceneData.GetBool("Room_A", "wall_1"));
            Assert.IsFalse(PersistentBool.Fallback.sceneData.GetBool("Room_B", "wall_1"), "other scene, same id");
        }

        [Test]
        public void IsSet_ReadsTheCurrentPlayerData_NotACachedOne()
        {
            var p = Make("Room_A", "wall_1");
            p.Set(true);
            Assert.IsTrue(p.IsSet);

            // Simulated load: a different PlayerData becomes current.
            var loaded = PlayerData.CreateNew();
            PersistentBool.Fallback = loaded;
            Assert.IsFalse(p.IsSet, "the new data has no entry");

            loaded.sceneData.SetBool("Room_A", "wall_1", true);
            Assert.IsTrue(p.IsSet, "the new data says done");
        }

        [Test]
        public void ExplicitConfigure_PinsItsOwnSceneData()
        {
            var d = new SceneData();
            var go = new GameObject("pinned");
            made.Add(go);
            var p = go.AddComponent<PersistentBool>();
            p.Configure(d, "Room_A", "x");
            p.Set(true);
            Assert.IsTrue(d.GetBool("Room_A", "x"));
            Assert.IsFalse(PersistentBool.Fallback.sceneData.HasBool("Room_A", "x"));
        }

        [Test]
        public void HasExplicitId_FalseWhenEmpty_AndItemIdFallsBackToName()
        {
            var go = new GameObject("Some Name");
            made.Add(go);
            var p = go.AddComponent<PersistentBool>();
            Assert.IsFalse(p.HasExplicitId);
            Assert.AreEqual("Some Name", p.ItemId);
            p.SetId("explicit_1");
            Assert.IsTrue(p.HasExplicitId);
            Assert.AreEqual("explicit_1", p.ItemId);
        }

        [Test]
        public void TwoObjectsSameSceneAndId_WarnOnSet()
        {
            var a = Make("Room_A", "dup");
            var b = Make("Room_A", "dup");
            a.Set(true);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[PersistentBool\] 'dup'"));
            b.Set(true);
        }

        // ---------- the Tut_01 additions ----------

        Scene Preview(string room)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            previews.Add(scene);
            TransitionRoomsBuilder.Populate(scene, TransitionRoomsBuilder.GetSpec(room), TransitionRoomsBuilder.LoadAssets());
            return scene;
        }

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

        [Test]
        public void Tut01_HasOneWallAndOnePickup_WithTheExplicitIds()
        {
            var scene = Preview("Tut_01");
            var walls = All<BreakableWall>(scene);
            var pickups = All<Pickup>(scene);
            Assert.AreEqual(1, walls.Count);
            Assert.AreEqual(1, pickups.Count);
            Assert.AreEqual("Tut_01_BreakableWall_1", walls[0].GetComponent<PersistentBool>().ItemId);
            Assert.IsTrue(walls[0].GetComponent<PersistentBool>().HasExplicitId);
            Assert.AreEqual("Tut_01_Pickup_1", pickups[0].GetComponent<PersistentBool>().ItemId);
            Assert.IsTrue(pickups[0].GetComponent<PersistentBool>().HasExplicitId);
        }

        [Test]
        public void OtherRooms_HaveNoWallOrPickup()
        {
            foreach (var name in new[] { "Tut_01b", "Tut_02" })
            {
                var scene = Preview(name);
                Assert.AreEqual(0, All<BreakableWall>(scene).Count, name);
                Assert.AreEqual(0, All<Pickup>(scene).Count, name);
            }
        }

        [Test]
        public void Tut01_WallIsSolid_PickupIsATrigger()
        {
            var scene = Preview("Tut_01");
            Assert.IsFalse(All<BreakableWall>(scene)[0].GetComponent<BoxCollider2D>().isTrigger);
            Assert.IsTrue(All<Pickup>(scene)[0].GetComponent<BoxCollider2D>().isTrigger);
        }

        [Test]
        public void Tut01_WallAndPickup_DoNotOverlapGatesBenchOrHazards()
        {
            var scene = Preview("Tut_01");
            var wall = WorldRect(All<BreakableWall>(scene)[0].GetComponent<BoxCollider2D>());
            var pick = WorldRect(All<Pickup>(scene)[0].GetComponent<BoxCollider2D>());
            var keepClear = new List<Rect>();
            foreach (var g in All<TransitionPoint>(scene)) keepClear.Add(WorldRect(g.GetComponent<BoxCollider2D>()));
            foreach (var z in All<HazardZone>(scene)) keepClear.Add(WorldRect(z.GetComponent<BoxCollider2D>()));
            foreach (var t in All<HazardRespawnTrigger>(scene)) keepClear.Add(WorldRect(t.GetComponent<BoxCollider2D>()));
            foreach (var r in keepClear)
            {
                Assert.IsFalse(wall.Overlaps(r), "wall overlaps " + r);
                Assert.IsFalse(pick.Overlaps(r), "pickup overlaps " + r);
            }
            Vector2 bench = All<RoomWorldStarter>(scene)[0].StartPoint.transform.position;
            Assert.IsFalse(wall.Contains(bench));
            Assert.IsFalse(pick.Contains(bench));
        }

        [Test]
        public void Tut01_WallSealsAnAlcoveTheHeroFits_AndTheShelfMeetsPlatformB()
        {
            var scene = Preview("Tut_01");
            var wall = WorldRect(All<BreakableWall>(scene)[0].GetComponent<BoxCollider2D>());
            var others = All<BoxCollider2D>(scene).Where(c => !c.isTrigger).Select(WorldRect).Where(s => s != wall).ToList();

            // The wall stands on a floor and runs up to a roof: that is what seals the alcove.
            Assert.IsTrue(others.Any(s => Mathf.Abs(s.yMax - wall.yMin) < 0.01f && s.xMin < wall.xMax && s.xMax > wall.xMin), "no floor under the wall");
            Assert.IsTrue(others.Any(s => Mathf.Abs(s.yMin - wall.yMax) < 0.01f && s.xMin <= wall.xMin && s.xMax >= wall.xMin), "no roof meeting the wall");
            // The hero is about 2.1 high; the alcove must let it stand.
            Assert.GreaterOrEqual(wall.height, SilksongUnits.HeroColliderSize.y + 0.5f);

            // The floor in front of the wall is the same surface as PlatformB, so the hero walks from there to the wall.
            var platformB = others.First(s => Mathf.Abs(s.yMax - 12.5f) < 0.01f && s.xMin >= 11.99f && s.xMax <= 17.01f);
            var shelf = others.First(s => Mathf.Abs(s.yMax - wall.yMin) < 0.01f && s.xMin < wall.xMax && s.xMax > wall.xMax);
            Assert.GreaterOrEqual(shelf.xMax, platformB.xMin - 0.01f, "the shelf must meet PlatformB");
        }

        [Test]
        public void Tut01_NoExistingBlockMoved()
        {
            // Spot checks of coordinates that existed before this task.
            var spec = TransitionRoomsBuilder.GetSpec("Tut_01");
            var step = spec.Blocks.First(b => b.Name == "StepLeft");
            Assert.AreEqual(8f, step.MinX);
            Assert.AreEqual(12f, step.MaxX);
            Assert.AreEqual(2.5f, step.Top);
            var pb = spec.Blocks.First(b => b.Name == "PlatformB");
            Assert.AreEqual(12f, pb.MinX);
            Assert.AreEqual(17f, pb.MaxX);
            Assert.AreEqual(12.5f, pb.Top);
            Assert.AreEqual(14f, spec.StartX);
        }
    }
}
