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
        public void ChangedMeasuredValue_FailsByName_ButProvisionalDriftOnlyWarns()
        {
            copy = ScriptableObject.CreateInstance<HeroConfig>();
            copy.RunSpeed = 9f;
            copy.AttackDownSize = new Vector2(2f, 2f);
            copy.HurtKnockbackX = 1f;
            var problems = HeroConfigConsistency.FindValueMismatches(copy);
            Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
            StringAssert.StartsWith("RunSpeed", problems[0]);
            var drift = HeroConfigConsistency.FindProvisionalDrift(copy);
            Assert.AreEqual(2, drift.Count, string.Join("\n", drift));
            Assert.IsTrue(drift.Exists(p => p.StartsWith("AttackDownSize")));
            Assert.IsTrue(drift.Exists(p => p.StartsWith("HurtKnockbackX")));
        }

        [Test]
        public void OwnerRetuneOfProvisionalValue_DoesNotFailTheAssert()
        {
            copy = ScriptableObject.CreateInstance<HeroConfig>();
            copy.AttackSideSize = new Vector2(3f, 3f);
            copy.PogoBounceSpeed = 20f;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Owner-tuned values changed"));
            Assert.DoesNotThrow(() => HeroConfigConsistency.AssertAssetMatchesSilksong(copy));
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
