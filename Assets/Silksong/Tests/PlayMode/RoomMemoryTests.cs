using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // Milestone 3 task 3: rooms that remember. A small room is built in code (one wall, one pickup), "left" by
    // unloading its scene, and built again under the same scene name, which is what re-entering a room does. The
    // memory lives in PlayerData.sceneData, so a replaced PlayerData (a simulated load) must be honoured.
    // Nothing here needs hero art or the real room files.
    public class RoomMemoryTests
    {
        const string WallId = "Test_Wall_1";
        const string PickupId = "Test_Pickup_1";

        string roomName;
        HeroConfig config;
        RoomWorldBootstrap world;
        SimulationMode2D savedMode;
        readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            PersistentBool.ResetStatics();
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = Path.Combine(Path.GetTempPath(), "room-memory-stubs-" + Guid.NewGuid() + ".md");
            Stubs.ResetForTests();
            roomName = "MemRoom_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            config = ScriptableObject.CreateInstance<HeroConfig>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (world != null) UnityEngine.Object.Destroy(world.gameObject);
            world = null;
            foreach (var o in spawned) if (o != null) UnityEngine.Object.Destroy(o);
            spawned.Clear();
            yield return null;
            var scene = SceneManager.GetSceneByName(roomName);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            Physics2D.simulationMode = savedMode;
            PersistentBool.ResetStatics();
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            if (File.Exists(Stubs.StubsDocPath)) File.Delete(Stubs.StubsDocPath);
            Stubs.StubsDocPath = null;
        }

        // ---------- the fake room ----------

        sealed class Room
        {
            public Scene Scene;
            public BreakableWall Wall;
            public Pickup Pickup;
        }

        // Builds the room in a fresh scene with the test's scene name. Start has not run yet; callers yield a frame.
        Room BuildRoom()
        {
            var scene = SceneManager.CreateScene(roomName);
            var room = new Room { Scene = scene };

            var wallGo = new GameObject("Wall");
            wallGo.AddComponent<BoxCollider2D>();   // solid
            wallGo.AddComponent<SpriteRenderer>();
            room.Wall = wallGo.AddComponent<BreakableWall>();
            room.Wall.Configure(WallId);
            SceneManager.MoveGameObjectToScene(wallGo, scene);

            var pickGo = new GameObject("Pickup");
            pickGo.transform.position = new Vector3(10f, 0f, 0f);
            pickGo.AddComponent<BoxCollider2D>().isTrigger = true;
            pickGo.AddComponent<SpriteRenderer>();
            room.Pickup = pickGo.AddComponent<Pickup>();
            room.Pickup.Configure(PickupId);
            SceneManager.MoveGameObjectToScene(pickGo, scene);
            return room;
        }

        // Leaves the room: its scene is unloaded, so every object in it is gone.
        IEnumerator Leave(Room room)
        {
            yield return SceneManager.UnloadSceneAsync(room.Scene);
        }

        static bool WallSolid(Room r) => r.Wall.GetComponent<Collider2D>().enabled && r.Wall.GetComponent<Renderer>().enabled;
        static bool WallGone(Room r) => !r.Wall.GetComponent<Collider2D>().enabled && !r.Wall.GetComponent<Renderer>().enabled;

        static HitInstance NailHit() => new HitInstance { Damage = 1, Direction = AttackDirection.Normal, Source = null };

        // ---------- wall ----------

        [UnityTest]
        public IEnumerator Wall_StartsIntact_WhenNothingWasStored()
        {
            var room = BuildRoom();
            yield return null;
            Assert.IsFalse(room.Wall.IsBroken);
            Assert.IsTrue(WallSolid(room));
            Assert.IsFalse(PersistentBool.Fallback.sceneData.HasBool(roomName, WallId), "reading must not create an entry");
        }

        [UnityTest]
        public IEnumerator Wall_BreaksOnAHit_AndWritesSceneData()
        {
            var room = BuildRoom();
            yield return null;

            IHittable target = room.Wall;
            Assert.IsTrue(target.TakeHit(NailHit()), "the first hit lands");

            Assert.IsTrue(room.Wall.IsBroken);
            Assert.IsTrue(WallGone(room));
            Assert.IsTrue(PersistentBool.Fallback.sceneData.GetBool(roomName, WallId), "written at once, not on leaving");
            Assert.IsFalse(target.TakeHit(NailHit()), "a broken wall refuses further hits");
            CollectionAssert.Contains(Stubs.Reported, "Breakable wall: break effect and sound");
        }

        [UnityTest]
        public IEnumerator Wall_StaysBroken_AfterLeavingAndReEnteringTheRoom()
        {
            var first = BuildRoom();
            yield return null;
            first.Wall.TakeHit(NailHit());
            yield return Leave(first);

            var second = BuildRoom();
            yield return null;
            Assert.IsTrue(second.Wall.IsBroken, "the new room object starts broken");
            Assert.IsTrue(WallGone(second));
        }

        [UnityTest]
        public IEnumerator Wall_InAnotherScene_IsNotAffected_SameIdDifferentRoom()
        {
            var room = BuildRoom();
            yield return null;
            PersistentBool.Fallback.sceneData.SetBool("SomeOtherRoom", WallId, true);
            room.Wall.Reapply();
            Assert.IsFalse(room.Wall.IsBroken, "the key is scene name + id");
        }

        // ---------- pickup ----------

        [UnityTest]
        public IEnumerator Pickup_StartsPresent_WhenNothingWasStored()
        {
            var room = BuildRoom();
            yield return null;
            Assert.IsFalse(room.Pickup.IsCollected);
            Assert.IsTrue(room.Pickup.GetComponent<Collider2D>().enabled);
            Assert.IsTrue(room.Pickup.GetComponent<Renderer>().enabled);
        }

        [UnityTest]
        public IEnumerator Pickup_CollectedOnce_WritesSceneData_AndIsGoneAfterReEntry()
        {
            var first = BuildRoom();
            yield return null;
            first.Pickup.Collect();
            first.Pickup.Collect();   // a second touch does nothing
            Assert.IsTrue(first.Pickup.IsCollected);
            Assert.IsFalse(first.Pickup.GetComponent<Renderer>().enabled);
            Assert.IsFalse(first.Pickup.GetComponent<Collider2D>().enabled);
            Assert.IsTrue(PersistentBool.Fallback.sceneData.GetBool(roomName, PickupId));
            CollectionAssert.Contains(Stubs.Reported, "Pickup effect: item is not granted yet");
            yield return Leave(first);

            var second = BuildRoom();
            yield return null;
            Assert.IsTrue(second.Pickup.IsCollected);
            Assert.IsFalse(second.Pickup.GetComponent<Renderer>().enabled);
        }

        [UnityTest]
        public IEnumerator Pickup_IsCollectedByTheHeroTouchingIt_NotByOtherThings()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            var room = BuildRoom();
            yield return null;

            // Something that is not the hero overlaps it: nothing happens.
            var other = new GameObject("NotTheHero");
            spawned.Add(other);
            other.transform.position = new Vector3(10f, 0f, 0f);
            other.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 0.5f);
            other.AddComponent<Rigidbody2D>().gravityScale = 0f;
            Physics2D.SyncTransforms();
            Physics2D.Simulate(0.02f);
            Assert.IsFalse(room.Pickup.IsCollected, "only the hero collects");
            UnityEngine.Object.Destroy(other);
            yield return null;

            var heroGo = new GameObject("Hero");
            spawned.Add(heroGo);
            heroGo.transform.position = new Vector3(10f, 0f, 0f);
            heroGo.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            var hero = heroGo.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = new FakeHeroInput();
            hero.GetComponent<Rigidbody2D>().gravityScale = 0f;
            Physics2D.SyncTransforms();
            Physics2D.Simulate(0.02f);

            Assert.IsTrue(room.Pickup.IsCollected, "the hero overlapping the trigger collects it");
            Assert.IsTrue(PersistentBool.Fallback.sceneData.GetBool(roomName, PickupId));
        }

        // ---------- the 'current PlayerData at use time' rule ----------

        [UnityTest]
        public IEnumerator ReplacedPlayerData_WithTheFlagsSet_MakesTheObjectsStartBrokenAndCollected()
        {
            // A load replaces the PlayerData before the room is built.
            var loaded = PlayerData.CreateNew();
            loaded.sceneData.SetBool(roomName, WallId, true);
            loaded.sceneData.SetBool(roomName, PickupId, true);
            PersistentBool.Fallback = loaded;

            var room = BuildRoom();
            yield return null;
            Assert.IsTrue(room.Wall.IsBroken);
            Assert.IsTrue(WallGone(room));
            Assert.IsTrue(room.Pickup.IsCollected);
        }

        [UnityTest]
        public IEnumerator ReplacedPlayerData_WithNoEntry_MakesTheObjectsStartIntact()
        {
            var first = BuildRoom();
            yield return null;
            first.Wall.Break();
            first.Pickup.Collect();
            yield return Leave(first);

            // A different save is loaded: it never broke or took anything.
            PersistentBool.Fallback = PlayerData.CreateNew();

            var second = BuildRoom();
            yield return null;
            Assert.IsFalse(second.Wall.IsBroken, "the new data has no entry");
            Assert.IsTrue(WallSolid(second));
            Assert.IsFalse(second.Pickup.IsCollected);
        }

        [UnityTest]
        public IEnumerator Reapply_FollowsAPlayerDataReplacedWhileTheRoomIsLive()
        {
            var room = BuildRoom();
            yield return null;
            room.Wall.Break();
            Assert.IsTrue(room.Wall.IsBroken);

            PersistentBool.Fallback = PlayerData.CreateNew();   // load a save that never broke it
            room.Wall.Reapply();
            Assert.IsFalse(room.Wall.IsBroken);
            Assert.IsTrue(WallSolid(room));
        }

        [UnityTest]
        public IEnumerator WithAWorld_TheObjectsUseTheBootstrapsCurrentPlayerData_EvenAfterItIsReplaced()
        {
            world = RoomWorldBootstrap.Create(config, new FakeHeroInput());
            var room = BuildRoom();
            yield return null;
            room.Wall.Break();
            Assert.IsTrue(world.PlayerData.sceneData.GetBool(roomName, WallId), "written into the world's PlayerData");
            Assert.IsFalse(PersistentBool.Fallback.sceneData.HasBool(roomName, WallId), "not into the fallback");
            yield return Leave(room);

            // Same world, new PlayerData (a load into a running world) that has the flag set.
            var loaded = PlayerData.CreateNew();
            loaded.sceneData.SetBool(roomName, PickupId, true);
            world.PlayerData = loaded;

            var again = BuildRoom();
            yield return null;
            Assert.IsFalse(again.Wall.IsBroken, "the replaced data never recorded the wall");
            Assert.IsTrue(again.Pickup.IsCollected, "the replaced data recorded the pickup");
        }

        // ---------- id checks ----------

        [UnityTest]
        public IEnumerator EmptyId_LogsAWarning_OnStart()
        {
            var scene = SceneManager.CreateScene(roomName);
            var go = new GameObject("NoId");
            go.AddComponent<PersistentBool>();
            SceneManager.MoveGameObjectToScene(go, scene);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[PersistentBool\] 'NoId' has no Id"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DuplicateIdsInOneScene_LogAWarning()
        {
            var scene = SceneManager.CreateScene(roomName);
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Dup" + i);
                go.AddComponent<PersistentBool>().SetId("same");
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[PersistentBool\] 'same' in scene '" + roomName + "' is used by two objects"));
            yield return null;
        }
    }
}
