using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.Meta;

namespace ProjectB.Core
{
    /// <summary>
    /// Lives in the tiny <c>Boot</c> scene (build index 0). Until the first run is finished
    /// (<see cref="SaveManager.IsFirstRun"/>) the player lands straight in <c>Gameplay</c>, skipping the main menu;
    /// afterwards the game opens on <c>MainMenu</c>. The heavy menu scene is not loaded on the very first launch.
    /// </summary>
    public sealed class BootRouter : MonoBehaviour
    {
        public const string MAIN_MENU_SCENE = "MainMenu";
        public const string GAMEPLAY_SCENE = "Gameplay";

        private void Start()
        {
            RootServices.TryResolve(out SaveManager saveManager);
            SceneManager.LoadScene(ResolveStartScene(saveManager));
        }

        public static string ResolveStartScene(SaveManager saveManager)
        {
            return saveManager != null && saveManager.IsFirstRun ? GAMEPLAY_SCENE : MAIN_MENU_SCENE;
        }
    }
}
