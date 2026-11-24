using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;

namespace ProjectB.EditorScripts
{
    public class BalanceCurveSimulator : EditorWindow
    {
        public static readonly int[] DefaultSampleWaves = { 1, 5, 10, 25, 50, 75, 100 };

        private WaveConfig waveConfig;
        private Vector2 scrollPosition;
        private string cachedReport = string.Empty;

        [MenuItem("ProjectB/Debug/Balance Curve Simulator")]
        public static void OpenWindow()
        {
            var window = GetWindow<BalanceCurveSimulator>("Balance Curve Simulator");
            window.minSize = new Vector2(640f, 480f);
            window.RefreshReport();
            window.Show();
        }

        [MenuItem("ProjectB/Debug/Log Balance Curve Report")]
        public static void LogReportToConsole()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Debug.Log(BuildSimulationReport(config));
        }

        private void OnEnable()
        {
            if (waveConfig == null)
            {
                waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            }
            RefreshReport();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUI.BeginChangeCheck();
            waveConfig = (WaveConfig)EditorGUILayout.ObjectField("Wave Config", waveConfig, typeof(WaveConfig), false);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshReport();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(26f)))
            {
                RefreshReport();
            }
            if (GUILayout.Button("Log to Console", GUILayout.Height(26f)))
            {
                RefreshReport();
                Debug.Log(cachedReport);
            }
            if (GUILayout.Button("Copy Report", GUILayout.Height(26f)))
            {
                RefreshReport();
                EditorGUIUtility.systemCopyBuffer = cachedReport;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.TextArea(cachedReport, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RefreshReport()
        {
            cachedReport = BuildSimulationReport(waveConfig);
        }

        public static string BuildSimulationReport(WaveConfig config, int[] waves = null)
        {
            if (config == null)
            {
                return "WaveConfig is not assigned.";
            }

            int[] sampleWaves = (waves != null && waves.Length > 0) ? waves : DefaultSampleWaves;
            var sb = new StringBuilder(2048);
            sb.AppendLine("=== ProjectB Balance Curve Simulation ===");
            sb.AppendLine(
                $"HpLinear={config.hpLinearPerWave:F3}, HpQuad={config.hpQuadraticPerWave:F4}, " +
                $"DmgLinear={config.damageLinearPerWave:F3}, MaxDmgMult={config.maxDamageMultiplier:F2}");
            sb.AppendLine();

            for (int i = 0; i < sampleWaves.Length; i++)
            {
                int wave = Mathf.Max(1, sampleWaves[i]);
                float hpMult = config.GetHpDifficultyMultiplier(wave);
                float dmgMult = config.GetDamageDifficultyMultiplier(wave);
                int enemyCount = Mathf.Min(
                    Mathf.RoundToInt(config.baseEnemyCount * Mathf.Pow(config.enemiesPerWaveMultiplier, wave - 1)),
                    config.maxEnemiesPerWave);

                bool isBoss = config.bosses != null && config.bosses.Count > 0 && WaveManager.IsBossWave(wave, config.bossInterval);
                bool isMiniBoss = WaveManager.IsMiniBossWave(wave, config.miniBossInterval, config.bossInterval);
                EnemyWaveEntry intro = config.GetIntroEntryForWave(wave);

                sb.Append($"Wave {wave,3} | HP x{hpMult:F2} | Dmg x{dmgMult:F2} | Count {enemyCount,2}");
                if (intro != null && intro.enemy != null)
                {
                    sb.Append($" | INTRO: {intro.enemy.name} ({Mathf.RoundToInt(intro.introSpawnShare * 100f)}%)");
                }
                if (isMiniBoss) sb.Append(" | MINI-BOSS");
                if (isBoss) sb.Append(" | BOSS");
                sb.AppendLine();

                EnemyData highestNonExploder = null;
                if (config.enemySchedule != null)
                {
                    for (int e = 0; e < config.enemySchedule.Count; e++)
                    {
                        EnemyWaveEntry entry = config.enemySchedule[e];
                        if (entry == null || !entry.IsEligibleForWave(wave)) continue;

                        EnemyData ed = entry.enemy;
                        int scaledHp = Mathf.Max(1, Mathf.RoundToInt(ed.hp * hpMult));
                        int scaledDmg = Mathf.Max(1, Mathf.RoundToInt(ed.contactDamage * dmgMult));
                        sb.AppendLine($"    - {ed.name,-18}: HP {scaledHp,6} | ContactDmg {scaledDmg,4}");

                        if (!(ed is ExploderEnemyData) &&
                            (highestNonExploder == null || ed.hp > highestNonExploder.hp))
                        {
                            highestNonExploder = ed;
                        }
                    }
                }

                if (wave >= config.minWaveForElites && highestNonExploder != null)
                {
                    int berserkerHp = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.hp * hpMult * config.eliteHpMultiplier));
                    int berserkerDmg = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.contactDamage * dmgMult * config.eliteDamageMultiplier));
                    int juggHp = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.hp * hpMult * config.juggernautHpMultiplier));
                    int juggDmg = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.contactDamage * dmgMult * config.juggernautDamageMultiplier));
                    sb.AppendLine($"    * Elite ({highestNonExploder.name}): Berserker HP {berserkerHp} / Dmg {berserkerDmg} | Juggernaut HP {juggHp} / Dmg {juggDmg}");
                }

                if (isMiniBoss && highestNonExploder != null)
                {
                    int mbHp = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.hp * hpMult * config.miniBossHpMultiplier));
                    int mbDmg = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.contactDamage * dmgMult * config.miniBossDamageMultiplier));
                    sb.AppendLine($"    * Mini-Boss ({highestNonExploder.name}): HP {mbHp} | Dmg {mbDmg}");
                }

                if (isBoss)
                {
                    int bossIdx = WaveManager.GetBossIndexForWave(wave, config.bossInterval, config.bosses.Count);
                    BossData boss = config.bosses[bossIdx];
                    if (boss != null)
                    {
                        int bossHp = Mathf.Max(1, Mathf.RoundToInt(boss.hp * hpMult));
                        int bossDmg = Mathf.Max(1, Mathf.RoundToInt(boss.contactDamage * dmgMult));
                        sb.AppendLine($"    # BOSS [{boss.bossDisplayName}]: HP {bossHp} | ContactDmg {bossDmg}");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
