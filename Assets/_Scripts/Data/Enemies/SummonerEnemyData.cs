using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "SummonerEnemyData", menuName = "ProjectB/Data/Enemies/SummonerEnemyData")]
    public class SummonerEnemyData : EnemyData
    {
        [Header("Summoner Positioning")]
        [Tooltip("Distance at which the summoner stops advancing toward the hero")]
        public float preferredDistance = 8f;

        [Tooltip("Distance below which the summoner backs away from the hero")]
        public float retreatDistance = 4.8f;

        [Header("Summon & Heal Pulse")]
        [Tooltip("Seconds between minion summons and ally heal pulses")]
        public float summonInterval = 6f;

        [Min(1)]
        [Tooltip("Number of minions summoned per pulse")]
        public int summonCount = 2;

        [Tooltip("Minion EnemyData spawned by this summoner")]
        public EnemyData minionData;

        [Tooltip("Radius within which allied enemies are healed on pulse")]
        public float healRadius = 6f;

        [Range(0f, 1f)]
        [Tooltip("Fraction of MaxHp restored to nearby allies on pulse (0.15 = 15%)")]
        public float healPercent = 0.15f;

        [Range(0f, 1f)]
        [Tooltip("Multiplier on healPercent for bosses and mini-bosses (0.2 = they get 20% of the normal heal)")]
        public float bossHealMultiplier = 0.2f;

        [Header("Heal Telegraph")]
        [Min(0f)]
        [Tooltip("Seconds the summoner glows green before each heal pulse, so players can spot the healer")]
        public float healTelegraphDuration = 1.0f;

        [Header("Healer Marker (floating pluses above the head)")]
        [Tooltip("Material for the green pluses (emissive VoxelLit, no pickup motion: the marker bobs and faces the camera in code). Empty = no marker")]
        public Material healerMarkerMaterial;

        [Range(0, 5)]
        [Tooltip("Number of pluses stacked vertically above the healer's head")]
        public int healerMarkerCount = 3;

        [Min(0.05f)]
        [Tooltip("Size of one plus in world units")]
        public float healerMarkerSize = 0.25f;

        [Min(0f)]
        [Tooltip("Gap between the top of the model and the pluses")]
        public float healerMarkerHeight = 0.45f;

        [Min(0f)]
        [Tooltip("Seconds before the heal pulse at which the pluses start to appear (small = right at the heal)")]
        public float healerMarkerLeadTime = 0.15f;

        [Min(0.01f)]
        [Tooltip("Seconds for the pluses to grow in")]
        public float healerMarkerFadeTime = 0.12f;

        [Min(0.01f)]
        [Tooltip("Seconds for the pluses to float up and shrink away once the heal pulse is done")]
        public float healerMarkerFadeOutTime = 0.22f;

        [Min(0f)]
        [Tooltip("Seconds the pluses stay visible after the heal pulse before floating away")]
        public float healerMarkerHoldAfterPulse = 0.05f;

        [Min(0f)]
        [Tooltip("How far (world units) the pluses drift upward while disappearing")]
        public float healerMarkerRiseDistance = 0.45f;
    }
}
