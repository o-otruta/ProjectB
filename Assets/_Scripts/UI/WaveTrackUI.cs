using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ProjectB.Core.Events;
using ProjectB.Enemies;

namespace ProjectB.UI
{
    /// <summary>
    /// HUD wave track (UI redesign, variant A): "WAVE ① ② ③ ④ ☠" — the current block of waves up to the next
    /// mini-boss / boss, so the player sees how far the next big fight is. Done waves green, current gold and
    /// slightly bigger, upcoming dark; the last node is the mini-boss (orange) or boss (red) wave.
    /// Updates only on <see cref="WaveCompletedEvent"/>; Zero-GC (preallocated nodes, char buffers).
    /// </summary>
    public class WaveTrackUI : MonoBehaviour
    {
        public enum NodeState
        {
            Done = 0,
            Current = 1,
            Upcoming = 2
        }

        [SerializeField] private Image[] nodes = new Image[0];
        [Tooltip("Number labels of nodes[0..n-2]; the last node shows the boss icon instead.")]
        [SerializeField] private TMP_Text[] nodeLabels = new TMP_Text[0];
        [SerializeField] private Image[] connectors = new Image[0];
        [SerializeField] private Image bossIcon;
        [SerializeField, Range(1f, 1.5f)] private float currentScale = 1.22f;

        private WaveManager waveManager;
        private GameEventBus eventBus;
        private bool isSubscribed;
        private int lastWave = int.MinValue;
        private readonly char[] buffer = new char[8];

        [Inject]
        public void Construct(WaveManager waveManager, GameEventBus eventBus)
        {
            Unsubscribe();
            this.waveManager = waveManager;
            this.eventBus = eventBus;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (eventBus != null && !isSubscribed)
            {
                eventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
                isSubscribed = true;
            }
            lastWave = int.MinValue;
            Refresh(waveManager != null ? waveManager.CurrentWave : 1);
        }

        private void Unsubscribe()
        {
            if (eventBus != null && isSubscribed)
            {
                eventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
                isSubscribed = false;
            }
        }

        private void OnWaveCompleted(WaveCompletedEvent evt) => Refresh(evt.NextWave);

        /// <summary>First wave of the block that ends with the next mini-boss / boss wave.</summary>
        public static int GetBlockStart(int wave, int blockSize)
        {
            blockSize = Mathf.Max(1, blockSize);
            wave = Mathf.Max(1, wave);
            return (wave - 1) / blockSize * blockSize + 1;
        }

        public static NodeState GetNodeState(int nodeWave, int currentWave)
        {
            if (nodeWave < currentWave) return NodeState.Done;
            return nodeWave == currentWave ? NodeState.Current : NodeState.Upcoming;
        }

        public void Refresh(int wave)
        {
            if (wave == lastWave || nodes == null || nodes.Length == 0) return;
            lastWave = wave;

            int miniInterval = waveManager != null && waveManager.Config != null ? waveManager.Config.miniBossInterval : nodes.Length;
            int bossInterval = waveManager != null && waveManager.Config != null ? waveManager.Config.bossInterval : 0;
            int blockSize = Mathf.Max(1, miniInterval);
            int start = GetBlockStart(wave, blockSize);
            UiTheme theme = UiTheme.Current;

            for (int i = 0; i < nodes.Length; i++)
            {
                Image node = nodes[i];
                if (node == null) continue;
                bool last = i == nodes.Length - 1;
                // The last node always stands for the block's final (mini-)boss wave, even if the block is longer.
                int nodeWave = last ? start + blockSize - 1 : start + i;
                bool visible = last || i < blockSize - 1;
                node.gameObject.SetActive(visible);
                if (!visible) continue;

                NodeState state = GetNodeState(nodeWave, wave);
                bool isBoss = last && WaveManager.IsBossWave(nodeWave, bossInterval);
                Color fill;
                if (last) fill = state == NodeState.Upcoming ? (isBoss ? theme.danger : theme.warning) : (state == NodeState.Current ? theme.gold : theme.xpFill);
                else fill = state == NodeState.Done ? theme.xpFill : state == NodeState.Current ? theme.gold : theme.surfaceRaised;
                node.color = fill;
                float s = state == NodeState.Current ? currentScale : 1f;
                node.rectTransform.localScale = new Vector3(s, s, 1f);

                if (!last && i < nodeLabels.Length && nodeLabels[i] != null)
                {
                    int len = UiFormat.WriteInt(buffer, 0, nodeWave);
                    nodeLabels[i].SetCharArray(buffer, 0, len);
                    nodeLabels[i].color = state == NodeState.Upcoming ? theme.textSecondary : theme.background;
                }
                if (last && bossIcon != null) bossIcon.color = state == NodeState.Upcoming ? theme.white : theme.background;
            }

            for (int i = 0; i < connectors.Length; i++)
            {
                if (connectors[i] == null) continue;
                bool visible = i < Mathf.Min(blockSize, nodes.Length) - 1;
                connectors[i].gameObject.SetActive(visible);
                connectors[i].color = start + i < wave ? theme.xpFill : theme.outline;
            }

            if (Application.isPlaying && wave > 1) UiTween.Punch(transform, 0.08f, 0.3f);
        }

#if UNITY_EDITOR
        public void EditorSetup(Image[] trackNodes, TMP_Text[] labels, Image[] links, Image boss)
        {
            nodes = trackNodes;
            nodeLabels = labels;
            connectors = links;
            bossIcon = boss;
        }
#endif
    }
}
