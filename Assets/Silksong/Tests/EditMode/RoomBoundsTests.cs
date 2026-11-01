using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    public class RoomBoundsTests
    {
        static readonly Rect Room = new Rect(0f, 0f, 100f, 50f);
        static readonly Vector2 Inset = new Vector2(14.6f, 8.3f);

        [Test]
        public void ClampCenter_InsideTheRange_IsUnchanged()
        {
            Assert.AreEqual(new Vector2(40f, 20f), RoomBounds.ClampCenter(Room, new Vector2(40f, 20f), Inset));
        }

        [Test]
        public void ClampCenter_KeepsTheInsetFromEveryEdge()
        {
            Assert.AreEqual(new Vector2(14.6f, 8.3f), RoomBounds.ClampCenter(Room, new Vector2(-5f, -5f), Inset));
            Assert.AreEqual(new Vector2(85.4f, 41.7f), RoomBounds.ClampCenter(Room, new Vector2(500f, 500f), Inset));
        }

        [Test]
        public void ClampCenter_RoomSmallerThanTheView_CentresOnThatAxis()
        {
            var narrow = new Rect(10f, 0f, 20f, 50f);   // 20 wide < 2 * 14.6
            var p = RoomBounds.ClampCenter(narrow, new Vector2(500f, 3f), Inset);
            Assert.AreEqual(20f, p.x, 1e-5f);
            Assert.AreEqual(8.3f, p.y, 1e-5f, "the other axis still clamps normally");
        }

        [Test]
        public void ClampCenter_ZeroInset_ClampsToTheEdges()
        {
            Assert.AreEqual(new Vector2(0f, 50f), RoomBounds.ClampCenter(Room, new Vector2(-3f, 80f), Vector2.zero));
        }

        [Test]
        public void Create_PlacesTheAreaInWorldSpace()
        {
            var rb = RoomBounds.Create(new Rect(-5f, 2f, 30f, 20f));
            try
            {
                Assert.AreEqual(new Rect(-5f, 2f, 30f, 20f), rb.Area);
                Assert.AreEqual(new Vector2(-5f + 14.6f, 2f + 8.3f), rb.ClampCameraCenter(new Vector2(-100f, -100f), new Vector2(14.6f, 8.3f)));
            }
            finally { Object.DestroyImmediate(rb.gameObject); }
        }

        [Test]
        public void Area_FollowsTheObjectWhenItMoves()
        {
            var rb = RoomBounds.Create(new Rect(0f, 0f, 10f, 10f));
            try
            {
                rb.transform.position = new Vector3(100f, 0f, 0f);
                Assert.AreEqual(new Rect(100f, 0f, 10f, 10f), rb.Area);
            }
            finally { Object.DestroyImmediate(rb.gameObject); }
        }
    }
}
