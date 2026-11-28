#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.Meta;

namespace ProjectB.Core.BalanceBot
{
    public enum BalanceBotMetaTier
    {
        Fresh = 0,
        Mid = 1,
        Full = 2,
        Custom = 3
    }

    [CreateAssetMenu(fileName = "BalanceBotMetaProfile", menuName = "ProjectB/Debug/Balance Bot Meta Profile")]
    public class BalanceBotMetaProfile : ScriptableObject
    {
        public string profileId = "Fresh";
        public BalanceBotMetaTier tier = BalanceBotMetaTier.Fresh;

        [Header("FTUE Suppression")]
        [Min(1)] public int initialTotalRuns = 10;

        [Header("Custom Overrides (used when tier == Custom)")]
        [Range(0f, 1f)] public float customMetaUpgradeFraction = 0.5f;
        [Range(0, 3)] public int customBonusActiveSlots;
        [Range(0, 3)] public int customBonusPassiveSlots;
        public List<string> customUnlockedTalentIds = new List<string>();
        public List<string> customUnlockedAbilityIds = new List<string>();

        public string ResolvedProfileId => !string.IsNullOrEmpty(profileId) ? profileId : tier.ToString();

        public SaveData BuildSaveData(
            string selectedHeroId,
            MetaUpgradeData[] metaUpgrades = null,
            TalentTreeData talentTree = null,
            IReadOnlyList<AbilityData> allAbilities = null,
            HeroCatalog heroCatalog = null)
        {
            var data = new SaveData
            {
                selectedHeroId = string.IsNullOrEmpty(selectedHeroId) ? "knight" : selectedHeroId,
                totalRuns = Mathf.Max(1, initialTotalRuns),
                ftueStarterCoinsGranted = true,
                ftueMenuFlowCompleted = true,
                ftueShardHintShown = true,
                ftueConstellationsHintDismissed = true,
                coins = 0,
                bossShards = 0,
                musicVolume = 0f,
                sfxVolume = 0f
            };

            // Always ensure the requested test hero is unlocked so SelectedHeroService can select it
            if (!data.unlockedHeroIds.Contains(data.selectedHeroId))
            {
                data.unlockedHeroIds.Add(data.selectedHeroId);
            }

            if (metaUpgrades == null || metaUpgrades.Length == 0)
            {
                metaUpgrades = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
            }
            if (talentTree == null)
            {
                talentTree = Resources.Load<TalentTreeData>(TalentTreeData.RESOURCE_PATH);
            }
            if (allAbilities == null || allAbilities.Count == 0)
            {
                allAbilities = LoadAbilitiesFromCardCatalog();
            }
            if (heroCatalog == null)
            {
                heroCatalog = Resources.Load<HeroCatalog>(SelectedHeroService.CATALOG_RESOURCE_PATH);
            }

            switch (tier)
            {
                case BalanceBotMetaTier.Fresh:
                    // Zero meta upgrades, zero talents, zero extra slots, only default-unlocked abilities
                    break;

                case BalanceBotMetaTier.Mid:
                    ApplyMetaFraction(data, metaUpgrades, 0.5f);
                    ApplyMidConstellation(data, talentTree);
                    ApplyAbilityUnlocks(data, allAbilities, 0.5f);
                    data.bonusActiveSlots = 1;
                    data.bonusPassiveSlots = 1;
                    UnlockAllHeroes(data, heroCatalog);
                    break;

                case BalanceBotMetaTier.Full:
                    ApplyMetaFraction(data, metaUpgrades, 1f);
                    ApplyAllConstellation(data, talentTree);
                    ApplyAbilityUnlocks(data, allAbilities, 1f);
                    data.bonusActiveSlots = 3;
                    data.bonusPassiveSlots = 3;
                    UnlockAllHeroes(data, heroCatalog);
                    break;

                case BalanceBotMetaTier.Custom:
                    ApplyMetaFraction(data, metaUpgrades, customMetaUpgradeFraction);
                    data.bonusActiveSlots = Mathf.Clamp(customBonusActiveSlots, 0, 3);
                    data.bonusPassiveSlots = Mathf.Clamp(customBonusPassiveSlots, 0, 3);
                    if (customUnlockedTalentIds != null)
                    {
                        for (int i = 0; i < customUnlockedTalentIds.Count; i++)
                        {
                            string tid = customUnlockedTalentIds[i];
                            if (!string.IsNullOrEmpty(tid) && !data.unlockedTalentIds.Contains(tid))
                            {
                                data.unlockedTalentIds.Add(tid);
                            }
                        }
                    }
                    if (customUnlockedAbilityIds != null)
                    {
                        for (int i = 0; i < customUnlockedAbilityIds.Count; i++)
                        {
                            string aid = customUnlockedAbilityIds[i];
                            if (!string.IsNullOrEmpty(aid) && !data.unlockedAbilityIds.Contains(aid))
                            {
                                data.unlockedAbilityIds.Add(aid);
                            }
                        }
                    }
                    UnlockAllHeroes(data, heroCatalog);
                    break;
            }

            return data;
        }

        private static void ApplyMetaFraction(SaveData data, MetaUpgradeData[] metaUpgrades, float fraction)
        {
            if (metaUpgrades == null || fraction <= 0f) return;
            float clamped = Mathf.Clamp01(fraction);
            for (int i = 0; i < metaUpgrades.Length; i++)
            {
                MetaUpgradeData upg = metaUpgrades[i];
                if (upg == null || string.IsNullOrEmpty(upg.id) || upg.maxLevel <= 0) continue;
                int level = Mathf.Clamp(Mathf.RoundToInt(upg.maxLevel * clamped), 0, upg.maxLevel);
                if (level > 0)
                {
                    data.metaUpgradeLevels[upg.id] = level;
                }
            }
        }

        private static void ApplyMidConstellation(SaveData data, TalentTreeData talentTree)
        {
            if (talentTree == null || talentTree.nodes == null) return;

            var byId = new Dictionary<string, TalentNodeData>(StringComparer.Ordinal);
            var nonOrigin = new List<TalentNodeData>();
            for (int i = 0; i < talentTree.nodes.Count; i++)
            {
                TalentNodeData n = talentTree.nodes[i];
                if (n == null || string.IsNullOrEmpty(n.id)) continue;
                byId[n.id] = n;
                if (n.nodeType != TalentNodeType.Origin)
                {
                    nonOrigin.Add(n);
                }
            }

            // Deterministic unlock of ~50% nodes prioritizing lower shard cost & non-keystones (inner/mid rings)
            nonOrigin.Sort((a, b) =>
            {
                int keyCmp = (a.nodeType == TalentNodeType.Keystone ? 1 : 0).CompareTo(b.nodeType == TalentNodeType.Keystone ? 1 : 0);
                if (keyCmp != 0) return keyCmp;
                int costCmp = a.shardCost.CompareTo(b.shardCost);
                if (costCmp != 0) return costCmp;
                return string.CompareOrdinal(a.id, b.id);
            });

            int targetCount = nonOrigin.Count / 2;
            var unlocked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in byId)
            {
                if (kv.Value.nodeType == TalentNodeType.Origin) unlocked.Add(kv.Key);
            }

            bool progress = true;
            while (unlocked.Count - 1 < targetCount && progress)
            {
                progress = false;
                for (int i = 0; i < nonOrigin.Count && unlocked.Count - 1 < targetCount; i++)
                {
                    TalentNodeData node = nonOrigin[i];
                    if (unlocked.Contains(node.id)) continue;

                    bool connected = node.prerequisiteIds == null || node.prerequisiteIds.Count == 0;
                    if (!connected)
                    {
                        for (int p = 0; p < node.prerequisiteIds.Count; p++)
                        {
                            if (unlocked.Contains(node.prerequisiteIds[p]))
                            {
                                connected = true;
                                break;
                            }
                        }
                    }

                    if (connected && node.minUnlockedStars > 0 && unlocked.Count - 1 < node.minUnlockedStars)
                    {
                        connected = false;
                    }

                    if (connected)
                    {
                        unlocked.Add(node.id);
                        data.unlockedTalentIds.Add(node.id);
                        progress = true;
                    }
                }
            }
        }

        private static void ApplyAllConstellation(SaveData data, TalentTreeData talentTree)
        {
            if (talentTree == null || talentTree.nodes == null) return;
            for (int i = 0; i < talentTree.nodes.Count; i++)
            {
                TalentNodeData node = talentTree.nodes[i];
                if (node == null || node.nodeType == TalentNodeType.Origin || string.IsNullOrEmpty(node.id)) continue;
                if (!data.unlockedTalentIds.Contains(node.id))
                {
                    data.unlockedTalentIds.Add(node.id);
                }
            }
        }

        private static void ApplyAbilityUnlocks(SaveData data, IReadOnlyList<AbilityData> allAbilities, float fraction)
        {
            if (allAbilities == null || fraction <= 0f) return;
            var locked = new List<AbilityData>();
            for (int i = 0; i < allAbilities.Count; i++)
            {
                AbilityData ab = allAbilities[i];
                if (ab != null && !ab.unlockedByDefault && !string.IsNullOrEmpty(ab.id))
                {
                    locked.Add(ab);
                }
            }

            locked.Sort((a, b) =>
            {
                int rarityCmp = a.rarity.CompareTo(b.rarity);
                return rarityCmp != 0 ? rarityCmp : string.CompareOrdinal(a.id, b.id);
            });

            int count = Mathf.Clamp(Mathf.RoundToInt(locked.Count * Mathf.Clamp01(fraction)), 0, locked.Count);
            for (int i = 0; i < count; i++)
            {
                if (!data.unlockedAbilityIds.Contains(locked[i].id))
                {
                    data.unlockedAbilityIds.Add(locked[i].id);
                }
            }
        }

        private static void UnlockAllHeroes(SaveData data, HeroCatalog heroCatalog)
        {
            if (heroCatalog == null || heroCatalog.heroes == null) return;
            for (int i = 0; i < heroCatalog.heroes.Count; i++)
            {
                HeroData hero = heroCatalog.heroes[i];
                if (hero != null && !string.IsNullOrEmpty(hero.id) && !data.unlockedHeroIds.Contains(hero.id))
                {
                    data.unlockedHeroIds.Add(hero.id);
                }
            }
        }

        private static IReadOnlyList<AbilityData> LoadAbilitiesFromCardCatalog()
        {
            var catalog = Resources.Load<CardCatalog>(CardCatalog.DEFAULT_RESOURCE_PATH);
            if (catalog == null || catalog.cards == null) return Array.Empty<AbilityData>();

            var list = new List<AbilityData>(32);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.cards.Count; i++)
            {
                if (catalog.cards[i] is AbilityCardData ac && ac.abilityData != null && !string.IsNullOrEmpty(ac.abilityData.id))
                {
                    if (seen.Add(ac.abilityData.id))
                    {
                        list.Add(ac.abilityData);
                    }
                }
            }
            return list;
        }
    }
}
#endif
