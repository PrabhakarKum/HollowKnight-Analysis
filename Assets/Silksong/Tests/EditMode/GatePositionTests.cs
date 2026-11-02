using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    // Source: TransitionPoint.GetGatePosition (TransitionPoint.cs ~line 422). The position comes from the
    // object's NAME using a case-sensitive Contains, checked in the order top, right, left, bot, door.
    public class GatePositionTests
    {
        [TestCase("left1", GatePosition.Left)]
        [TestCase("left3", GatePosition.Left)]
        [TestCase("right1", GatePosition.Right)]
        [TestCase("right2", GatePosition.Right)]
        [TestCase("top1", GatePosition.Top)]
        [TestCase("bot1", GatePosition.Bottom)]
        [TestCase("bot2", GatePosition.Bottom)]
        [TestCase("door1", GatePosition.Door)]
        // Contains, not StartsWith: prefixes and suffixes around the keyword still match.
        [TestCase("Gate_left1", GatePosition.Left)]
        [TestCase("right1 (2)", GatePosition.Right)]
        [TestCase("bottom1", GatePosition.Bottom)]   // "bot" is contained in "bottom"
        // First match in the check order wins when a name holds several keywords.
        [TestCase("top_left1", GatePosition.Top)]
        [TestCase("left_door", GatePosition.Left)]
        // Case-sensitive in the original, so capitalised names are unknown.
        [TestCase("Left1", GatePosition.Unknown)]
        [TestCase("RIGHT1", GatePosition.Unknown)]
        [TestCase("Door1", GatePosition.Unknown)]
        // Anything else, including empty and null, is unknown rather than an error.
        [TestCase("", GatePosition.Unknown)]
        [TestCase(null, GatePosition.Unknown)]
        [TestCase("exit1", GatePosition.Unknown)]
        [TestCase("1", GatePosition.Unknown)]
        public void FromName_FollowsTheOriginalRules(string name, GatePosition expected)
        {
            Assert.AreEqual(expected, GatePositions.FromName(name));
        }

        // Order precedence top > right > left > bot > door, each pair checked in both textual orders.
        [TestCase("right_top", GatePosition.Top)]
        [TestCase("top_right", GatePosition.Top)]
        [TestCase("left_right", GatePosition.Right)]
        [TestCase("right_left", GatePosition.Right)]
        [TestCase("bot_left", GatePosition.Left)]
        [TestCase("left_bot", GatePosition.Left)]
        [TestCase("door_bot", GatePosition.Bottom)]
        [TestCase("bot_door", GatePosition.Bottom)]
        public void FromName_OrderPrecedenceIsTopRightLeftBotDoor(string name, GatePosition expected)
        {
            Assert.AreEqual(expected, GatePositions.FromName(name));
        }
    }
}
