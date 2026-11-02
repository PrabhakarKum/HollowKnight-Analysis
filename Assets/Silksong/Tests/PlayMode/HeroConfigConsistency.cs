using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hollowknight.Silksong.Tests
{
    // Guards the owner's rule: everything the hero does is configured ONLY by the single project asset
    // Assets/Silksong/Hero/HeroConfig.asset. No scene, prefab, component or helper may override, duplicate or
    // hard-code a value HeroConfig defines.
    //
    // Two checks, both reusable from any scene test (Task 11b calls them on the real room scene):
    //   1. FindValueMismatches / AssertAssetMatchesSilksong: the asset still holds the documented Silksong numbers
    //      (docs/study/02-hero-movement.md). Measured values hard-fail; PROVISIONAL ones only warn (see below).
    //   2. FindWiringProblems / AssertSceneUsesProjectConfig (ALWAYS hard-fails): in the loaded scene every hero's Config IS the
    //      project asset (same reference, not a copy), no other HeroConfig instance exists, and no component in
    //      the scene serializes a different HeroConfig.
    public static class HeroConfigConsistency
    {
        public const string ProjectAssetPath = "Assets/Silksong/Hero/HeroConfig.asset";
        const float Tolerance = 1e-5f;

        // Fields that are PROVISIONAL (not confirmed against the original, see HeroConfig comments). They are
        // compared and reported as warnings only: an owner retune must never block the suite.
        public static readonly string[] Provisional =
        {
            "WallSlideSpeed", "WallJumpSpeedY", "PogoBounceSpeed",
            "AttackSideSize", "AttackSideOffset", "AttackUpSize", "AttackUpOffset", "AttackDownSize", "AttackDownOffset",
            "AttackDamage", "HurtKnockbackX", "HurtKnockbackY"
        };

        static readonly Dictionary<string, object> Documented = new Dictionary<string, object>
        {
            // run and jump
            { "RunSpeed", 8.25f }, { "JumpSpeed", 18.6f }, { "JumpHoldMaxSeconds", 0.18f }, { "JumpHoldMinSeconds", 0.04f },
            { "JumpReleaseMultiplier", 0.5f }, { "Gravity", 60f }, { "FallSpeedMax", 30f },
            { "CoyoteSeconds", 0.08f }, { "JumpBufferSeconds", 0.04f },
            // input thresholds
            { "HorizontalPressThreshold", 0.3f }, { "VerticalPressThreshold", 0.5f },
            // dash: 28 u/s for 0.1 s
            { "DashSpeed", 28f }, { "DashSeconds", 0.1f }, { "DashCooldownSeconds", 0.425f },
            // wall
            { "WallSlideSpeed", 6f }, { "WallJumpSpeedX", 25f }, { "WallJumpSpeedY", 18.6f }, { "WallJumpLockSeconds", 0.3f },
            // attack
            { "AttackActiveSeconds", 0.35f }, { "AttackCooldownSeconds", 0.41f }, { "PogoBounceSpeed", 12f },
            { "AttackSideSize", new Vector2(1.1f, 1.2f) }, { "AttackSideOffset", new Vector2(0.8f, 0.15f) },
            { "AttackUpSize", new Vector2(1.4f, 0.8f) }, { "AttackUpOffset", new Vector2(0f, 1.15f) },
            { "AttackDownSize", new Vector2(1.4f, 0.5f) }, { "AttackDownOffset", new Vector2(0f, 0f) },
            { "AttackDamage", 1 },
            // hurt
            { "HurtInvulnerableSeconds", 1f }, { "HurtKnockbackX", 15f }, { "HurtKnockbackY", 0f }, { "HurtStunSeconds", 0.2f },
            // room transitions (docs/study/10-room-transitions.md section 3; added with the hero transition driver)
            { "TransitionWalkInSeconds", 0.33f }, { "TransitionDropSpeed", -12f }, { "TransitionDropSeconds", 0.33f },
            { "TransitionBottomLift", 3f }, { "TransitionBottomSeconds", 0.1f }, { "TransitionBottomSpeedX", 8f },
            { "TransitionBottomSpeedY", 9.4f }, { "TransitionLeaveUpSpeed", 3f },
        };

#if UNITY_EDITOR
        public static HeroConfig LoadProjectAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<HeroConfig>(ProjectAssetPath);
            Assert.IsNotNull(asset, "The project hero config is missing: " + ProjectAssetPath);
            return asset;
        }
#endif

        // ---------- check 1: values ----------
        // Why a value table exists at all: it is the one place that records what the asset held when it was
        // checked against Silksong, so a scene, prefab or stray edit cannot quietly change the hero. It is a
        // canary only; game code never reads it. Values MEASURED from Silksong fail the suite when they drift.
        // PROVISIONAL / owner-tuned values (the Provisional list) only warn, so an owner retune never blocks tests.

        // Hard failures: a measured value differs, a HeroConfig field has no documented value, or a documented
        // field no longer exists.
        public static List<string> FindValueMismatches(HeroConfig config)
        {
            var problems = new List<string>();
            if (config == null) { problems.Add("HeroConfig is null"); return problems; }

            foreach (var field in typeof(HeroConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!Documented.TryGetValue(field.Name, out var expected))
                {
                    problems.Add(field.Name + ": no documented Silksong value in HeroConfigConsistency (add one)");
                    continue;
                }
                if (Array.IndexOf(Provisional, field.Name) >= 0) continue;
                object actual = field.GetValue(config);
                if (!Same(expected, actual))
                    problems.Add(field.Name + ": expected " + expected + ", asset has " + actual);
            }
            foreach (var name in Documented.Keys)
                if (typeof(HeroConfig).GetField(name, BindingFlags.Public | BindingFlags.Instance) == null)
                    problems.Add(name + ": documented but HeroConfig no longer has this field");
            return problems;
        }

        // Warnings only: PROVISIONAL / owner-tuned fields whose value differs from the table.
        public static List<string> FindProvisionalDrift(HeroConfig config)
        {
            var drift = new List<string>();
            if (config == null) return drift;
            foreach (var name in Provisional)
            {
                var field = typeof(HeroConfig).GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field == null || !Documented.TryGetValue(name, out var expected)) continue;
                object actual = field.GetValue(config);
                if (!Same(expected, actual)) drift.Add(name + " (PROVISIONAL): table has " + expected + ", asset has " + actual);
            }
            return drift;
        }

        static bool Same(object expected, object actual)
        {
            switch (expected)
            {
                case float f: return actual is float af && Mathf.Abs(f - af) <= Tolerance;
                case int i: return actual is int ai && i == ai;
                case Vector2 v: return actual is Vector2 av && (v - av).sqrMagnitude <= Tolerance * Tolerance;
                default: return Equals(expected, actual);
            }
        }

        public static void AssertAssetMatchesSilksong(HeroConfig config)
        {
            var drift = FindProvisionalDrift(config);
            if (drift.Count > 0)
                Debug.LogWarning("[HeroConfigConsistency] Owner-tuned values changed (not a failure):\n  " + string.Join("\n  ", drift));
            var problems = FindValueMismatches(config);
            Assert.IsEmpty(problems, "HeroConfig differs from the documented Silksong values:\n  " + string.Join("\n  ", problems));
        }

        // ---------- check 2: wiring ----------

#if UNITY_EDITOR
        // Inspects the currently loaded scene(s). Call after the scene has run at least one frame, because the
        // config is injected at Start (HeroDebugHud sets hero.Config from its serialized reference).
        public static List<string> FindWiringProblems(HeroConfig projectAsset)
        {
            var problems = new List<string>();
            if (projectAsset == null) { problems.Add("project HeroConfig asset not supplied"); return problems; }

            var heroes = UnityEngine.Object.FindObjectsByType<HeroController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (heroes.Length == 0) problems.Add("no HeroController in the loaded scene");
            foreach (var hero in heroes)
            {
                if (hero.Config == null) problems.Add(hero.name + ": Config is not set");
                else if (!ReferenceEquals(hero.Config, projectAsset)) problems.Add(hero.name + ": Config is not the project asset (a copy or another asset)");
            }

            foreach (var any in Resources.FindObjectsOfTypeAll<HeroConfig>())
                if (!ReferenceEquals(any, projectAsset))
                    problems.Add("a second HeroConfig instance exists: '" + any.name + "' (persistent: " + EditorUtility.IsPersistent(any) + ")");

            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null) continue;
                var so = new SerializedObject(behaviour);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue is HeroConfig cfg && !ReferenceEquals(cfg, projectAsset))
                        problems.Add(behaviour.GetType().Name + " on " + behaviour.name + " (" + it.propertyPath + ") references a HeroConfig that is not the project asset");
                }
            }
            return problems;
        }

        public static void AssertSceneUsesProjectConfig()
        {
            var asset = LoadProjectAsset();
            var problems = FindWiringProblems(asset);
            Assert.IsEmpty(problems, "Hero is not configured only by " + ProjectAssetPath + ":\n  " + string.Join("\n  ", problems));
            AssertAssetMatchesSilksong(asset);
        }
#endif
    }
}
