using UnityEngine;

namespace Hollowknight.Silksong
{
    // Pure decisions behind EnemyPersistence, testable without a scene.
    public static class EnemyPersistenceRules
    {
        // The enemy never appears only when it is a stays-dead enemy AND a stored 'dead' flag exists and is true.
        // A stays-dead=false enemy ignores storage completely.
        public static bool StartsDead(bool staysDead, bool hasEntry, bool value) =>
            staysDead && PersistentRules.StartsDone(hasEntry, value);

        // Death is written only for stays-dead enemies.
        public static bool WritesOnDeath(bool staysDead) => staysDead;

        // Storage is read at all only for stays-dead enemies.
        public static bool ReadsStorage(bool staysDead) => staysDead;

        // Why the id cannot be used (same rule as every persistent object), or null.
        public static string IdProblem(string id) => PersistentRules.IdProblem(id);
    }

    // Put next to EnemyHealth on an enemy that must stay dead once killed. The dead flag lives in the CURRENT
    // PlayerData.sceneData keyed by scene name + Id, stored by a PersistentBool on the same object (same id rule,
    // same warnings for empty or shared ids, same fallback when no world runs).
    //
    // Start (and Reapply, which GameSession calls on a loaded room) hides a stays-dead enemy whose flag is set: its
    // walker stops, its contact damage is zeroed and the object is deactivated, so it can neither hurt nor be hit.
    // When EnemyHealth raises Died, the flag is written at once. StaysDead=false: nothing is read or written.
    //
    // Reapply only hides; it does not bring an enemy back (the brain is stopped for good).
    [RequireComponent(typeof(PersistentBool))]
    public class EnemyPersistence : MonoBehaviour
    {
        [SerializeField] string id;
        [SerializeField] bool staysDead = true;

        PersistentBool persistent;
        EnemyHealth health;

        public bool StaysDead { get => staysDead; set => staysDead = value; }
        public string Id => id;
        public bool IsHiddenAsDead { get; private set; }

        PersistentBool Persistent => persistent != null ? persistent : (persistent = GetComponent<PersistentBool>());

        // For enemies built from code (and the room builder): explicit id and the per-enemy setting.
        public void Configure(string explicitId, bool staysDeadValue)
        {
            id = explicitId;
            staysDead = staysDeadValue;
            Persistent.SetId(id);
        }

        void Awake()
        {
            Persistent.SetId(id);
            health = GetComponent<EnemyHealth>();
            if (health != null) health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        void Start() => Reapply();

        void OnDied()
        {
            if (!EnemyPersistenceRules.WritesOnDeath(staysDead)) return;
            Persistent.Set(true);
        }

        // Re-reads the current PlayerData (load, replaced data) and hides the enemy when it is flagged dead.
        public void Reapply()
        {
            if (!EnemyPersistenceRules.ReadsStorage(staysDead)) return;
            // IsSet already means an entry exists and is true.
            if (EnemyPersistenceRules.StartsDead(staysDead, true, Persistent.IsSet)) HideAsDead();
        }

        void HideAsDead()
        {
            IsHiddenAsDead = true;
            var contact = GetComponent<EnemyContactDamage>();
            if (contact != null) contact.ZeroDamage();
            var walker = GetComponent<EnemyWalker>();
            if (walker != null) { walker.Stop(); walker.enabled = false; }
            gameObject.SetActive(false);
        }
    }
}
