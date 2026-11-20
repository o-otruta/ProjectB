using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Arena;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Tests.EditMode
{
    public class PassivesSynergiesSecretsTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        [Test]
        public void StatPassiveAbility_AppliesAndStacksStatsOnHeroRuntimeStats()
        {
            var stats = new HeroRuntimeStats();
            var heroGo = Track(new GameObject("TestHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, new GameEventBus(), null, null);

            var furyData = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            furyData.id = "BattleFury";
            furyData.abilityName = "Battle Fury";
            furyData.effectType = PassiveEffectType.DamagePercent;
            furyData.baseValue = 15f;

            var armorData = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            armorData.id = "IronSkin";
            armorData.abilityName = "Iron Skin";
            armorData.effectType = PassiveEffectType.ArmorFlat;
            armorData.baseValue = 20f;

            Assert.IsTrue(heroAbilities.AddAbility(furyData));
            Assert.IsTrue(heroAbilities.AddAbility(armorData));
            Assert.AreEqual(2, heroAbilities.PassiveAbilities.Count);
            Assert.AreEqual(1.15f, stats.GlobalDamageMultiplier, 0.001f);
            Assert.AreEqual(20f, stats.Armor, 0.001f);

            heroAbilities.UpgradeAbility("BattleFury", ModifierType.Damage, 10f);
            heroAbilities.UpgradeAbility("IronSkin", ModifierType.Area, 15f);

            Assert.AreEqual(1.25f, stats.GlobalDamageMultiplier, 0.001f);
            Assert.AreEqual(35f, stats.Armor, 0.001f);
        }

        [Test]
        public void SynergyCard_AppearsOnlyWhenBothRequiredAbilitiesEquipped_AndDisappearsAfterClaim()
        {
            var stats = new HeroRuntimeStats();
            var heroGo = Track(new GameObject("TestHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, new GameEventBus(), null, null);

            var ab1 = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            ab1.id = "Fireball";
            ab1.effectType = PassiveEffectType.DamagePercent;
            ab1.baseValue = 5f;

            var ab2 = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            ab2.id = "IceAura";
            ab2.effectType = PassiveEffectType.ArmorFlat;
            ab2.baseValue = 5f;

            var synergyCard = Track(ScriptableObject.CreateInstance<SynergyCardData>());
            synergyCard.synergyId = "steam_blast";
            synergyCard.cardName = "Steam Blast";
            synergyCard.rarity = CardRarity.Legendary;
            synergyCard.globalDamageBonusPercent = 25f;

            var synergy = Track(ScriptableObject.CreateInstance<SynergyData>());
            synergy.id = "steam_blast";
            synergy.requiredAbilityIds = new List<string> { "Fireball", "IceAura" };
            synergy.rewardCard = synergyCard;

            var applier = new UpgradeApplier(null, stats, heroAbilities);
            var mgrGo = Track(new GameObject("TestUpgradeMgr"));
            var mgr = mgrGo.AddComponent<UpgradeManager>();
            mgr.cardPool = new List<CardData>();
            mgr.synergies = new List<SynergyData> { synergy };
            mgr.Construct(null, null, null, heroAbilities, applier, null);
            mgr.InitializeRunPool();

            // Neither ability equipped -> no synergy card
            Assert.AreEqual(0, mgr.GetRandomCards(5).Count);

            // Only 1 ability equipped -> still no synergy card
            heroAbilities.AddAbility(ab1);
            Assert.AreEqual(0, mgr.GetRandomCards(5).Count);

            // Both abilities equipped -> synergy card appears!
            heroAbilities.AddAbility(ab2);
            var offered = mgr.GetRandomCards(5);
            Assert.AreEqual(1, offered.Count);
            Assert.AreSame(synergyCard, offered[0]);

            // Applying synergy grants its stat bonuses
            applier.ApplyCard(synergyCard, mgr);
            Assert.AreEqual(1.30f, stats.GlobalDamageMultiplier, 0.001f); // 1.0 + 0.05 + 0.25
        }

        [Test]
        public void SecretItemSpawner_ChestAndCrystalGrantExpectedRewards()
        {
            var stats = new HeroRuntimeStats();
            var runStats = new RunStatistics();
            var bus = new GameEventBus();

            var heroGo = Track(new GameObject("TestHero"));
            var economy = heroGo.AddComponent<HeroEconomy>();
            economy.Construct(runStats, bus);
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, null);

            var rareAbility = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            rareAbility.id = "SwiftBoots";
            rareAbility.abilityName = "Swift Boots";
            rareAbility.rarity = CardRarity.Rare;
            rareAbility.effectType = PassiveEffectType.MoveSpeedFlat;
            rareAbility.baseValue = 1f;

            var rareCard = Track(ScriptableObject.CreateInstance<AbilityCardData>());
            rareCard.abilityData = rareAbility;
            rareCard.rarity = CardRarity.Rare;
            rareCard.isConsumable = true;

            var applier = new UpgradeApplier(null, stats, heroAbilities);
            var mgrGo = Track(new GameObject("TestUpgradeMgr"));
            var mgr = mgrGo.AddComponent<UpgradeManager>();
            mgr.cardPool = new List<CardData> { rareCard };
            mgr.Construct(null, null, null, heroAbilities, applier, null);
            mgr.InitializeRunPool();

            var spawnerGo = Track(new GameObject("TestSecretSpawner"));
            var spawner = spawnerGo.AddComponent<SecretItemSpawner>();
            spawner.Construct(null, null, economy, null, mgr, null, bus);

            // Treasure Chest -> +100 coins
            string chestMsg = spawner.ApplySecretReward(SecretItemType.TreasureChest);
            Assert.AreEqual(100, runStats.CoinsEarned);
            Assert.IsTrue(chestMsg.Contains("100"));

            // Mystery Crystal -> grants SwiftBoots rare ability directly
            string crystalMsg = spawner.ApplySecretReward(SecretItemType.MysteryCrystal);
            Assert.IsTrue(heroAbilities.HasAbility("SwiftBoots"));
            Assert.IsTrue(crystalMsg.Contains("Swift Boots"));
        }
    }
}
