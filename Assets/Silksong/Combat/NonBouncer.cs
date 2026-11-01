using UnityEngine;

namespace Hollowknight.Silksong
{
    // Marker, named after the original's component: a target carrying an active one still takes down-slash
    // hits but does not bounce the hero. Put it on the hittable or one of its parents.
    public class NonBouncer : MonoBehaviour
    {
        public bool Active = true;
    }
}
