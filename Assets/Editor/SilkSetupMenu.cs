#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.EditorTools
{
    public static class SilkSetupMenu
    {
        [MenuItem("Hollowknight/Add Silk + Bind")]
        public static void Apply()
        {
            var player = Object.FindFirstObjectByType<PlayerController2D>();
            if (player == null) { Debug.LogError("No PlayerController2D in the scene."); return; }
            if (player.GetComponent<PlayerHealth2D>() == null) Undo.AddComponent<PlayerHealth2D>(player.gameObject);
            if (player.GetComponent<PlayerSilk2D>() == null) Undo.AddComponent<PlayerSilk2D>(player.gameObject);
            if (player.GetComponent<BindEffect2D>() == null) Undo.AddComponent<BindEffect2D>(player.gameObject);
            if (player.GetComponent<SprintFx2D>() == null) Undo.AddComponent<SprintFx2D>(player.gameObject);
            Debug.Log("Silk + bind added (and sprint dust: hold the dash key after a dash to sprint). Hit the dummies / enemy with the nail to fill the 9 silk pips; once you are hurt and the pips pulse, HOLD F (gamepad North) to bind and heal.");
        }
    }
}
#endif
