using UnityEngine;

namespace Hollowknight.Silksong
{
    // Everything a target needs to know about one hit. Like the original's hit instance it is a plain value
    // handed from the attacker to the target. Left and right are both "Normal"; the target finds the side
    // from the Source position.
    public struct HitInstance
    {
        public int Damage;
        public AttackDirection Direction;
        public GameObject Source;
    }
}
