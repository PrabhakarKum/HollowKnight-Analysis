using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    // Pure decisions behind GameSession (no scene).
    public class GameSessionRulesTests
    {
        [Test]
        public void ContinueRoom_UsesTheSavedRespawnScene()
        {
            var d = PlayerData.CreateNew();
            d.respawnScene = "Tut_02";
            Assert.AreEqual("Tut_02", GameSessionRules.ContinueRoom(d));
        }

        [Test]
        public void ContinueRoom_FallsBackToTheFirstRoom_WhenUnknown()
        {
            var d = PlayerData.CreateNew();
            d.respawnScene = "";
            Assert.AreEqual(GameSession.NewGameRoom, GameSessionRules.ContinueRoom(d));
            Assert.AreEqual(GameSession.NewGameRoom, GameSessionRules.ContinueRoom(null));
        }

        [Test]
        public void SanitizeHealth_RepairsZeroAndTooMuch_AndLeavesGoodValuesAlone()
        {
            var d = PlayerData.CreateNew();
            d.health = 3;
            Assert.IsFalse(GameSessionRules.SanitizeHealth(d));
            Assert.AreEqual(3, d.health, "health stays as saved");

            d.health = 0;
            Assert.IsTrue(GameSessionRules.SanitizeHealth(d));
            Assert.AreEqual(d.maxHealth, d.health);

            d.health = d.maxHealth + 4;
            Assert.IsTrue(GameSessionRules.SanitizeHealth(d));
            Assert.AreEqual(d.maxHealth, d.health);
        }

        [Test]
        public void IsBenchLike_AcceptsBenchOrFlaggedMarker()
        {
            Assert.IsTrue(GameSessionRules.IsBenchLike(true, false));
            Assert.IsTrue(GameSessionRules.IsBenchLike(false, true));
            Assert.IsFalse(GameSessionRules.IsBenchLike(false, false));
        }
    }
}
