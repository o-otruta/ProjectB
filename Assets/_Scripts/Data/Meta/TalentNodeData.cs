using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Data
{
    public enum TalentNodeType
    {
        Origin = 0,
        Stat = 1,
        Synergy = 2,
        Keystone = 3
    }

    public enum TalentConstellation
    {
        Core = 0,
        Fury = 1,       // Offense
        Bastion = 2,    // Defense
        Fortune = 3     // Fortune & Arcane
    }

    /// <summary>
    /// Effects granted permanently by an unlocked Constellation node. Values are additive across nodes.
    /// Percent effects use whole numbers (10 = +10%).
    /// </summary>
    public enum TalentEffectType
    {
        None = 0,
        CritChancePercent = 1,
        GlobalAreaPercent = 2,
        BossDamagePercent = 3,
        CooldownReductionPercent = 4,
        ArmorFlat = 5,
        HealingPercent = 6,
        IFrameSeconds = 7,
        UltimateCooldownPercent = 8,
        CoinGainPercent = 9,
        FreeRerolls = 10,
        BarrelRespawnBonus = 11,
        SecretAndShrineRewardPercent = 12,
        BonusActiveSlots = 13,
        BonusPassiveSlots = 14,
        SecondWind = 15,
        CrystalCoinBonus = 16,
        MaxHpPercent = 17,
        MoveSpeedPercent = 18,
        XpGainPercent = 19,
        GlobalDamagePercent = 20
    }

    [Serializable]
    public struct TalentEffect
    {
        public TalentEffectType type;
        public float value;

        public TalentEffect(TalentEffectType type, float value)
        {
            this.type = type;
            this.value = value;
        }
    }

    [CreateAssetMenu(fileName = "TalentNode", menuName = "ProjectB/Meta/Talent Node")]
    public class TalentNodeData : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        [Header("Localization")]
        public string nameKey;
        public string descriptionKey;
        public Sprite icon;
        public TalentNodeType nodeType = TalentNodeType.Stat;
        public TalentConstellation constellation = TalentConstellation.Core;

        [Header("Layout (canvas units, Origin at 0,0)")]
        public Vector2 position;

        [Header("Graph")]
        [Tooltip("Node becomes available when ANY of these nodes is unlocked. Empty = root-adjacent (always available).")]
        public List<string> prerequisiteIds = new List<string>();

        [Header("Cost")]
        [Min(0)] public int shardCost = 1;
        [Tooltip("Optional alternative coin price (inner ring only). 0 = shards only.")]
        [Min(0)] public int coinCost;

        [Header("Effects")]
        public List<TalentEffect> effects = new List<TalentEffect>();

        [Tooltip("Reactive synergy unlocked by this node (SynergyData.id). Only for Synergy nodes.")]
        public string synergyId;

        public bool IsKeystone => nodeType == TalentNodeType.Keystone;
        public bool IsOrigin => nodeType == TalentNodeType.Origin;

        public float GetEffectValue(TalentEffectType type)
        {
            if (effects == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].type == type) sum += effects[i].value;
            }
            return sum;
        }
    }
}
