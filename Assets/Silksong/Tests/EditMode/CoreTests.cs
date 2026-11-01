using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    public class CoreTests
    {
        [System.Flags]
        enum Sample { None = 0, A = 1, B = 2, C = 4 }

        class PoolProbe : MonoBehaviour, IPoolable
        {
            public int Spawned;
            public int Despawned;
            public void OnSpawn() { Spawned++; }
            public void OnDespawn() { Despawned++; }
        }

        string _stubsPath;

        [SetUp]
        public void SetUp()
        {
            // Point the Editor doc writer at a temp file so tests never touch the real STUBS.md.
            _stubsPath = Path.Combine(Path.GetTempPath(), "stubs-test-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = _stubsPath;
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Stubs.StubsDocPath = null;
            if (File.Exists(_stubsPath)) File.Delete(_stubsPath);
        }

        [Test]
        public void Stubs_NotImplemented_LogsOncePerFeature()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("tools"));
            Stubs.NotImplemented("tools");
            Stubs.NotImplemented("tools"); // second call must stay silent; an unexpected log would fail the test

            Assert.AreEqual(1, Stubs.Reported.Count(f => f == "tools"));
            Assert.AreEqual(1, File.ReadAllText(_stubsPath).Split(new[] { "- tools" }, System.StringSplitOptions.None).Length - 1);
        }

        [Test]
        public void ObjectPool_Despawn_ThenSpawn_ReusesInstance()
        {
            var prefabGo = new GameObject("prefab");
            var prefab = prefabGo.AddComponent<PoolProbe>();

            var first = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            ObjectPool.Despawn(first.gameObject);
            var second = ObjectPool.Spawn(prefab, Vector3.one, Quaternion.identity);

            Assert.AreSame(first, second);
            Assert.AreEqual(2, second.Spawned);
            Assert.AreEqual(1, second.Despawned);
            Assert.AreEqual(Vector3.one, second.transform.position);

            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(prefabGo);
        }

        [Test]
        public void EnumFlags_Has_ReturnsTrueOnlyForSetFlag()
        {
            var v = Sample.A | Sample.C;
            Assert.IsTrue(v.Has(Sample.A));
            Assert.IsTrue(v.Has(Sample.C));
            Assert.IsFalse(v.Has(Sample.B));
        }
    }
}
