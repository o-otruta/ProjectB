using NUnit.Framework;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Tests.EditMode
{
    public class HeroRuntimeStatsTests
    {
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
            if (hadOriginalSave)
            {
                PlayerPrefs.SetString(SaveManager.SAVE_KEY, originalSaveJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void GlobalDamage_StacksMultiplicativelyWithMetaUpgrades()
        {
            var saveManager = new SaveManager();
            try
            {
                // hero_damage: +8%/lvl -> lvl 10 = +80% (1.8x)
                // ability_damage: +8%/lvl -> lvl 5 = +40% (1.4x)
                saveManager.SetMetaUpgradeLevel("hero_damage", 10, saveImmediately: false);
                saveManager.SetMetaUpgradeLevel("ability_damage", 5, saveImmediately: false);

                var meta = new MetaUpgradeManager(saveManager);
                var stats = new HeroRuntimeStats(meta);

                Assert.AreEqual(1.8f, stats.WeaponDamageMultiplier, 0.001f);
                Assert.AreEqual(1.4f, stats.AbilityDamageMultiplier, 0.001f);

                stats.AddGlobalDamagePercent(10f);

                // 1.8 * 1.1 = 1.98, 1.4 * 1.1 = 1.54
                Assert.AreEqual(1.98f, stats.WeaponDamageMultiplier, 0.001f);
                Assert.AreEqual(1.54f, stats.AbilityDamageMultiplier, 0.001f);
            }
            finally
            {
                saveManager.Dispose();
            }
        }

        [Test]
        public void Armor_AppliesDiminishingReturnsWithMinimumOneDamage()
        {
            var stats = new HeroRuntimeStats();

            Assert.AreEqual(50, stats.CalculateMitigatedDamage(50));
            Assert.AreEqual(0, stats.CalculateMitigatedDamage(0));

            stats.AddArmor(100f); // 100 / (100 + 100) = 0.5x
            Assert.AreEqual(50, stats.CalculateMitigatedDamage(100));
            Assert.AreEqual(1, stats.CalculateMitigatedDamage(1));

            stats.AddArmor(900f); // 1000 armor -> 100 / 1100 ≈ 0.0909x
            Assert.AreEqual(9, stats.CalculateMitigatedDamage(100));
            Assert.AreEqual(1, stats.CalculateMitigatedDamage(2));
        }

        [Test]
        public void ModifyingStats_RaisesOnStatsChangedEvent()
        {
            var stats = new HeroRuntimeStats();
            int notifications = 0;
            stats.OnStatsChanged += () => notifications++;

            stats.AddGlobalDamagePercent(10f);
            stats.AddArmor(20f);
            stats.AddMoveSpeed(1f);
            stats.AddMagnetRadius(1.5f);
            stats.AddMaxHp(20);

            Assert.AreEqual(5, notifications);
            Assert.AreEqual(6f, stats.GetMoveSpeed(5f), 0.001f);
            Assert.AreEqual(4.5f, stats.GetMagnetRadius(3f), 0.001f);
            Assert.AreEqual(120, stats.GetMaxHp(100));
        }

        [Test]
        public void KnightPassive_StacksMultiplicativelyWithMetaHpUpgrade()
        {
            var saveManager = new SaveManager();
            var knight = ScriptableObject.CreateInstance<HeroData>();
            knight.id = "knight";
            knight.maxHp = 100;
            knight.passiveType = HeroPassiveType.MaxHpPercent;
            knight.passiveValue = 100f; // +100% Max HP

            try
            {
                // hero_hp: +10%/lvl -> lvl 5 = +50% (1.5x)
                saveManager.SetMetaUpgradeLevel("hero_hp", 5, saveImmediately: false);
                var meta = new MetaUpgradeManager(saveManager);
                var stats = new HeroRuntimeStats(meta);
                stats.ApplyHeroPassive(knight, notify: false);

                // 100 * 2.0 (Knight passive) * 1.5 (Meta HP) = 300
                Assert.AreEqual(300, stats.GetMaxHp(knight.maxHp));

                stats.AddMaxHp(25);
                Assert.AreEqual(325, stats.GetMaxHp(knight.maxHp));
            }
            finally
            {
                Object.DestroyImmediate(knight);
                saveManager.Dispose();
            }
        }

        [Test]
        public void HeroHealth_Invulnerable_BlocksDamageAndDoesNotFireDamageEvent()
        {
            var go = new GameObject("TestHeroHealth");
            var health = go.AddComponent<HeroHealth>();
            var stats = new HeroRuntimeStats();
            health.Construct(stats, null, null);

            // Manually initialize via IncreaseMaxHealth / reflection-free path or Start equivalent:
            // Set invulnerable and verify TakeDamage does not trigger OnDamageTaken
            int damageEvents = 0;
            health.OnDamageTaken += _ => damageEvents++;

            health.SetInvulnerable(true);
            health.TakeDamage(50);

            Assert.IsTrue(health.IsInvulnerable);
            Assert.AreEqual(0, damageEvents);
            Assert.IsFalse(health.IsDead);

            Object.DestroyImmediate(go);
        }
    }
}
