using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Tests of the HeroConfigConsistency helper itself, so a broken guard cannot silently pass a scene.
    public class HeroConfigConsistencyTests
    {
        HeroConfig copy;
        GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (copy != null) Object.DestroyImmediate(copy);
            if (go != null) Object.DestroyImmediate(go);
        }

#if UNITY_EDITOR
        [Test]
        public void ProjectAsset_MatchesTheDocumentedSilksongValues()
        {
            HeroConfigConsistency.AssertAssetMatchesSilksong(HeroConfigConsistency.LoadProjectAsset());
        }

        [Test]
        public void CodeDefaults_MatchTheDocumentedValues()
        {
            // A fresh HeroConfig (field initialisers) must equal the asset too, so neither can drift alone.
            copy = ScriptableObject.CreateInstance<HeroConfig>();
            Assert.IsEmpty(HeroConfigConsistency.FindValueMismatches(copy));
        }

        [Test]
        public void ChangedValue_IsReportedByName()
        {
            copy = ScriptableObject.CreateInstance<HeroConfig>();
            copy.RunSpeed = 9f;
            copy.AttackDownSize = new Vector2(2f, 2f);
            var problems = HeroConfigConsistency.FindValueMismatches(copy);
            Assert.AreEqual(2, problems.Count, string.Join("\n", problems));
            Assert.IsTrue(problems.Exists(p => p.StartsWith("RunSpeed")));
            Assert.IsTrue(problems.Exists(p => p.StartsWith("AttackDownSize")));
        }

        [Test]
        public void EveryHeroConfigField_HasADocumentedValue()
        {
            // FindValueMismatches reports any field without a documented value, so a clean result proves coverage.
            copy = ScriptableObject.CreateInstance<HeroConfig>();
            Assert.IsEmpty(HeroConfigConsistency.FindValueMismatches(copy).FindAll(p => p.Contains("no documented")));
        }

        [Test]
        public void HeroUsingACopyOfTheConfig_IsReported()
        {
            var asset = HeroConfigConsistency.LoadProjectAsset();
            copy = Object.Instantiate(asset);
            go = new GameObject("Hero", typeof(BoxCollider2D));
            var hero = go.AddComponent<HeroController>();
            hero.Config = copy;
            var problems = HeroConfigConsistency.FindWiringProblems(asset);
            Assert.IsTrue(problems.Exists(p => p.Contains("not the project asset")), string.Join("\n", problems));
            Assert.IsTrue(problems.Exists(p => p.Contains("second HeroConfig")), string.Join("\n", problems));
        }

        [Test]
        public void HeroUsingTheProjectAsset_HasNoWiringProblems()
        {
            var asset = HeroConfigConsistency.LoadProjectAsset();
            go = new GameObject("Hero", typeof(BoxCollider2D));
            go.AddComponent<HeroController>().Config = asset;
            Assert.IsEmpty(HeroConfigConsistency.FindWiringProblems(asset));
        }

        [Test]
        public void SceneWithNoHero_IsReported()
        {
            var problems = HeroConfigConsistency.FindWiringProblems(HeroConfigConsistency.LoadProjectAsset());
            Assert.IsTrue(problems.Exists(p => p.Contains("no HeroController")));
        }
#endif
    }
}
