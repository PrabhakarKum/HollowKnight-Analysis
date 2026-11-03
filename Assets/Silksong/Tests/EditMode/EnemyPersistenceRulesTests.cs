using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    public class EnemyPersistenceRulesTests
    {
        [Test]
        public void StaysDead_WithTrueFlag_StartsDead() =>
            Assert.IsTrue(EnemyPersistenceRules.StartsDead(true, true, true));

        [Test]
        public void StaysDead_WithNoEntry_StartsAlive() =>
            Assert.IsFalse(EnemyPersistenceRules.StartsDead(true, false, false));

        [Test]
        public void StaysDead_WithFalseFlag_StartsAlive() =>
            Assert.IsFalse(EnemyPersistenceRules.StartsDead(true, true, false));

        [Test]
        public void StaysDeadOff_IgnoresAStoredTrueFlag() =>
            Assert.IsFalse(EnemyPersistenceRules.StartsDead(false, true, true));

        [Test]
        public void OnlyStaysDeadEnemies_ReadAndWrite()
        {
            Assert.IsTrue(EnemyPersistenceRules.ReadsStorage(true));
            Assert.IsTrue(EnemyPersistenceRules.WritesOnDeath(true));
            Assert.IsFalse(EnemyPersistenceRules.ReadsStorage(false));
            Assert.IsFalse(EnemyPersistenceRules.WritesOnDeath(false));
        }

        [Test]
        public void IdProblem_MatchesThePersistentRule()
        {
            Assert.IsNotNull(EnemyPersistenceRules.IdProblem(""));
            Assert.IsNotNull(EnemyPersistenceRules.IdProblem(null));
            Assert.IsNull(EnemyPersistenceRules.IdProblem("Crawler_1"));
        }
    }
}
