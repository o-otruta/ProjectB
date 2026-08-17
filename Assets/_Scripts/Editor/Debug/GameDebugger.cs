using UnityEditor;
using UnityEngine;
using ProjectB.Player;

namespace ProjectB.Editor
{
    public class GameDebugger : UnityEditor.EditorWindow
    {
        [MenuItem("ProjectB/Debug/Kill Hero")]
        public static void KillHero()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[GameDebugger] Cannot kill hero while game is not playing!");
                return;
            }

            var hero = FindAnyObjectByType<HeroHealth>();
            if (hero != null)
            {
                hero.DebugKill();
            }
            else
            {
                Debug.LogWarning("[GameDebugger] HeroHealth not found in scene!");
            }
        }
    }
}
