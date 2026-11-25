using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Ability level cap: base Lv 5 from <see cref="RunRulesConfig"/>, raised by the Constellation Path of Mastery
    /// (Lv 10 / 20 / 50, highest unlocked star wins) and the <see cref="TalentNodeData.minUnlockedStars"/> gate.
    /// </summary>
    public class AbilityLevelCapTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly List<IDisposable> disposables = new List<IDisposable>();
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = disposables.Count - 1; i >= 0; i--) disposables[i]?.Dispose();
            disposables.Clear();
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
            created.Clear();

            if (hadOriginalSave) PlayerPrefs.SetString(SaveManager.SAVE_KEY, originalSaveJson);
            else PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            PlayerPrefs.Save();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) created.Add(obj);
            return obj;
        }

        private SaveManager NewSave()
        {
            var save = new SaveManager();
            save.ResetAll();
            disposables.Add(save);
            return save;
        }

        private TalentTreeData BuildMasteryTree()
        {
            var tree = Track(ScriptableObject.CreateInstance<TalentTreeData>());
            TalentNodeData Make(string id, TalentNodeType type, int shards, int minStars, string[] prereq, params TalentEffect[] effects)
            {
                var n = Track(ScriptableObject.CreateInstance<TalentNodeData>());
                n.id = id;
                n.nodeType = type;
                n.shardCost = shards;
                n.minUnlockedStars = minStars;
                n.prerequisiteIds = new List<string>(prereq ?? new string[0]);
                n.effects = new List<TalentEffect>(effects);
                tree.nodes.Add(n);
                return n;
            }

            Make("origin", TalentNodeType.Origin, 0, 0, null);
            Make("lv10", TalentNodeType.Mastery, 1, 0, null, new TalentEffect(TalentEffectType.AbilityMaxLevel, 10));
            Make("lv20", TalentNodeType.Mastery, 1, 0, new[] { "lv10" }, new TalentEffect(TalentEffectType.AbilityMaxLevel, 20));
            Make("crit", TalentNodeType.Stat, 1, 0, null, new TalentEffect(TalentEffectType.CritChancePercent, 5));
            Make("lv50", TalentNodeType.Mastery, 1, 3, new[] { "lv20" }, new TalentEffect(TalentEffectType.AbilityMaxLevel, 50));
            return tree;
        }

        private HeroAbilities CreateHeroAbilities(HeroRuntimeStats stats, RunRulesConfig rules = null)
        {
            var go = Track(new GameObject("LevelCapHero"));
            var ha = go.AddComponent<HeroAbilities>();
            ha.Construct(stats, new GameEventBus(), null, null, null, null, new DefaultSynergyUnlockProvider(), rules);
            ha.SetAvailableSynergies(new List<SynergyData>());
            return ha;
        }

        private StatPassiveAbilityData MakePassive(string id)
        {
            var data = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            data.id = id;
            data.abilityName = id;
            data.type = AbilityType.Passive;
            data.effectType = PassiveEffectType.DamagePercent;
            data.baseValue = 5f;
            return data;
        }

        [Test]
        public void BaseCap_IsFiveFromRunRules_AndUpgradesPastItAreIgnored()
        {
            var rules = Resources.Load<RunRulesConfig>(RunRulesConfig.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(rules);
            Assert.AreEqual(5, rules.baseAbilityMaxLevel, "Base ability cap is Lv 5");

            var ha = CreateHeroAbilities(new HeroRuntimeStats(), rules);
            Assert.AreEqual(5, ha.MaxAbilityLevel);

            Assert.IsTrue(ha.AddAbility(MakePassive("BattleFury")));
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(ha.UpgradeAbility("BattleFury", ModifierType.Damage, 1f));
            }
            Assert.AreEqual(5, ha.GetAbilityLevel("BattleFury"));
            Assert.IsTrue(ha.IsAbilityAtMaxLevel("BattleFury"));
            Assert.IsFalse(ha.UpgradeAbility("BattleFury", ModifierType.Damage, 1f));
            Assert.AreEqual(5, ha.GetAbilityLevel("BattleFury"));

            // Fallback without config uses the same default.
            Assert.AreEqual(RunRulesConfig.DEFAULT_BASE_ABILITY_MAX_LEVEL, CreateHeroAbilities(new HeroRuntimeStats()).MaxAbilityLevel);
        }

        [Test]
        public void MasteryStars_RaiseCap_HighestUnlockedWins_NotSummed()
        {
            var save = NewSave();
            var tree = BuildMasteryTree();
            var mgr = new TalentTreeManager(save, tree, null);
            disposables.Add(mgr);
            save.AddBossShards(10);

            var stats = new HeroRuntimeStats(null, null, mgr);
            Assert.AreEqual(0, stats.TalentAbilityLevelCap);
            var ha = CreateHeroAbilities(stats);
            Assert.AreEqual(5, ha.MaxAbilityLevel);

            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("lv10"));
            stats.ApplyTalentBonuses(mgr);
            Assert.AreEqual(10, ha.MaxAbilityLevel);

            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("lv20"));
            stats.ApplyTalentBonuses(mgr);
            Assert.AreEqual(20, ha.MaxAbilityLevel, "Lv 10 + Lv 20 stars give a Lv 20 cap, not Lv 30");
            Assert.AreEqual(30f, mgr.GetTotalEffect(TalentEffectType.AbilityMaxLevel), 0.001f, "Raw sum stays available, cap uses the max");
        }

        [Test]
        public void MinUnlockedStars_GatesEndGameStar_UntilEnoughStarsAreLit()
        {
            var save = NewSave();
            var tree = BuildMasteryTree();
            var mgr = new TalentTreeManager(save, tree, null);
            disposables.Add(mgr);
            save.AddBossShards(10);

            mgr.TryUnlock("lv10");
            mgr.TryUnlock("lv20");
            Assert.IsTrue(mgr.IsConnected(tree.GetById("lv50")));
            Assert.AreEqual(TalentUnlockResult.NotEnoughStars, mgr.CanUnlock("lv50"));
            Assert.AreEqual(TalentNodeState.Locked, mgr.GetState("lv50"));

            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("crit"));
            Assert.AreEqual(TalentNodeState.Available, mgr.GetState("lv50"));
            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("lv50"));
            Assert.AreEqual(50, mgr.GetAbilityLevelCapBonus());
        }

        [Test]
        public void UpgradeManager_DropsModifiersOfCappedAbilities_StatCardsRemain()
        {
            var ha = CreateHeroAbilities(new HeroRuntimeStats());
            Assert.IsTrue(ha.AddAbility(MakePassive("IronSkin")));

            var mod = Track(ScriptableObject.CreateInstance<AbilityModifierCardData>());
            mod.targetAbilityId = "IronSkin";
            mod.modifierType = ModifierType.Damage;
            mod.value = 1f;
            var stat = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            stat.statType = StatType.GlobalDamage;
            stat.value = 5f;

            var applier = new UpgradeApplier(null, null, ha);
            var mgr = Track(new GameObject("LevelCapUpgradeManager")).AddComponent<UpgradeManager>();
            mgr.cardPool = new List<CardData> { mod, stat };
            mgr.Construct(null, null, null, ha, applier, null);
            mgr.InitializeRunPool();
            Assert.AreEqual(2, mgr.GetRandomCards(3).Count);

            for (int i = 0; i < 4; i++) applier.ApplyCard(mod, mgr);
            Assert.AreEqual(5, ha.GetAbilityLevel("IronSkin"));

            var offered = mgr.GetRandomCards(3);
            Assert.AreEqual(1, offered.Count);
            Assert.AreSame(stat, offered[0], "Only the stat card survives once every owned ability is capped");
        }

        [Test]
        public void CardBadge_MarksLastUpgradeBeforeCap_AsMax()
        {
            var ha = CreateHeroAbilities(new HeroRuntimeStats());
            Assert.IsTrue(ha.AddAbility(MakePassive("SwiftBoots")));
            var mod = Track(ScriptableObject.CreateInstance<AbilityModifierCardData>());
            mod.targetAbilityId = "SwiftBoots";

            for (int i = 0; i < 3; i++) ha.UpgradeAbility("SwiftBoots", ModifierType.Damage, 1f);
            Assert.AreEqual(4, ha.GetAbilityLevel("SwiftBoots"));
            string badge = CardSelectionUI.FormatLevelBadge(mod, ha, out _);
            StringAssert.Contains("MAX", badge);
            StringAssert.Contains("5", badge);
        }

        [Test]
        public void TalentTreeAsset_HasPathOfMastery_10_20_50()
        {
            var tree = Resources.Load<TalentTreeData>(TalentTreeData.RESOURCE_PATH);
            Assert.IsNotNull(tree);

            TalentNodeData lv10 = tree.GetById("mastery_lv10");
            TalentNodeData lv20 = tree.GetById("mastery_lv20");
            TalentNodeData lv50 = tree.GetById("mastery_lv50");
            Assert.IsNotNull(lv10);
            Assert.IsNotNull(lv20);
            Assert.IsNotNull(lv50);

            Assert.AreEqual(10f, lv10.GetEffectValue(TalentEffectType.AbilityMaxLevel));
            Assert.AreEqual(20f, lv20.GetEffectValue(TalentEffectType.AbilityMaxLevel));
            Assert.AreEqual(50f, lv50.GetEffectValue(TalentEffectType.AbilityMaxLevel));

            // Lv 10 is a cheap root star (also for coins), Lv 20 sits mid-tree, Lv 50 is the end-game star.
            Assert.AreEqual(0, lv10.prerequisiteIds.Count);
            Assert.Greater(lv10.coinCost, 0);
            Assert.LessOrEqual(lv10.shardCost, 2);
            Assert.Greater(lv20.shardCost, lv10.shardCost);
            Assert.Greater(lv50.shardCost, lv20.shardCost);
            Assert.Greater(lv50.minUnlockedStars, (tree.nodes.Count - 1) / 2, "Mythic Ascension needs most of the tree");
            Assert.Less(lv50.minUnlockedStars, tree.nodes.Count - 1, "…but never the whole tree");
            foreach (TalentNodeData n in tree.nodes)
            {
                Assert.LessOrEqual(n.shardCost, lv50.shardCost, $"{n.id}: Mythic Ascension is the priciest star");
            }
        }
    }
}
