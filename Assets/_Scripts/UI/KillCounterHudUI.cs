using UnityEngine;
using TMPro;
using VContainer;
using ProjectB.Core;

namespace ProjectB.UI
{
    /// <summary>
    /// Kill counter pill of the top HUD row (Sprint 13 HUD revision). Polls one int per frame and rewrites the text
    /// only when it changes — Zero-GC.
    /// </summary>
    public sealed class KillCounterHudUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text killsText;

        private RunStatistics runStatistics;
        private readonly char[] buffer = new char[16];
        private int lastDisplayed = int.MinValue;

        [Inject]
        public void Construct(RunStatistics runStatistics)
        {
            this.runStatistics = runStatistics;
        }

        private void LateUpdate()
        {
            int kills = runStatistics != null ? runStatistics.EnemiesKilled : 0;
            if (kills == lastDisplayed || killsText == null) return;
            lastDisplayed = kills;
            int len = UiFormat.WriteGrouped(buffer, 0, kills);
            killsText.SetCharArray(buffer, 0, len);
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text text)
        {
            killsText = text;
        }
#endif
    }
}
