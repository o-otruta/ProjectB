using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Core.BalanceBot;
using ProjectB.Core.Telemetry;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    public class BalanceAutomationTests
    {
        private readonly List<UnityEngine.Object> tracked = new List<UnityEngine.Object>();
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            BalanceBotSaveIsolationScope.RestorePendingBackupIfAny();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                if (tracked[i] != null) UnityEngine.Object.DestroyImmediate(tracked[i]);
            }
            tracked.Clear();

            BalanceBotSaveIsolationScope.RestorePendingBackupIfAny();
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

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) tracked.Add(obj);
            return obj;
        }

        [Test]
        public void CsvUtility_UsesInvariantCulture_EscapesSpecialChars_AndWritesUtf8WithoutBom()
        {
            CultureInfo prevCulture = Thread.CurrentThread.CurrentCulture;
            string tempFile = Path.Combine(Path.GetTempPath(), $"projectb_csv_{Guid.NewGuid():N}.csv");
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

                var sb = new StringBuilder();
                CsvUtility.AppendCell(sb, "simple");
                CsvUtility.AppendCell(sb, "with, comma");
                CsvUtility.AppendCell(sb, "with \"quotes\"");
                CsvUtility.AppendCell(sb, "line1\nline2");
                CsvUtility.AppendFloat(sb, 12.34f);
                CsvUtility.AppendDouble(sb, 56.78, "F2", isLast: true);

                string row = sb.ToString();
                Assert.AreEqual("simple,\"with, comma\",\"with \"\"quotes\"\"\",\"line1\nline2\",12.34,56.78", row);

                CsvUtility.EnsureHeaderAndAppendLine(tempFile, "a,b,c,d,e,f", row);
                CsvUtility.EnsureHeaderAndAppendLine(tempFile, "a,b,c,d,e,f", "1,2,3,4,5,6");

                byte[] bytes = File.ReadAllBytes(tempFile);
                Assert.GreaterOrEqual(bytes.Length, 3);
                Assert.IsFalse(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "CSV must be UTF-8 without BOM");

                string[] lines = File.ReadAllText(tempFile, Encoding.UTF8).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                Assert.AreEqual(4, lines.Length); // header + multiline row (2 physical lines) + second row
                Assert.AreEqual("a,b,c,d,e,f", lines[0]);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = prevCulture;
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Test]
        public void RandomCardPickStrategy_NeverPicksAdSlot_AndIsDeterministic()
        {
            var c0 = CreateStatCard("Stat0", StatType.MaxHP, 10f);
            var c1 = CreateStatCard("Stat1", StatType.Armor, 5f);
            var c2Ad = CreateStatCard("Stat2_Ad", StatType.GlobalDamage, 50f);

            var ctx = new CardPickContext
            {
                OfferedCards = new CardData[] { c0, c1, c2Ad },
                FreeSlotsCount = 2,
                CanFreeReroll = true
            };

            var s1 = new RandomCardPickStrategy(12345);
            var s2 = new RandomCardPickStrategy(12345);

            for (int i = 0; i < 20; i++)
            {
                CardPickDecision d1 = s1.Choose(ctx);
                CardPickDecision d2 = s2.Choose(ctx);
                Assert.IsFalse(d1.RequestFreeReroll);
                Assert.That(d1.SlotIndex, Is.InRange(0, 1));
                Assert.AreEqual(d1.SlotIndex, d2.SlotIndex);
            }
        }

        [Test]
        public void GreedyDamageCardPickStrategy_PrioritizesNewActive_ThenTopDamageSourceModifiers()
        {
            var activeData = CreateStubAbility("Fireball", AbilityType.Active);
            var passiveData = CreateStubAbility("IronSkin", AbilityType.Passive);

            var newActiveCard = CreateAbilityCard("Card_Fireball", activeData);
            var newPassiveCard = CreateAbilityCard("Card_IronSkin", passiveData);
            var adCard = CreateStatCard("Card_AdStat", StatType.GlobalDamage, 20f);

            var ctxOpenSlot = new CardPickContext
            {
                OfferedCards = new CardData[] { newPassiveCard, newActiveCard, adCard },
                FreeSlotsCount = 2,
                CanAddActive = true,
                CanAddPassive = true,
                CanFreeReroll = false
            };

            var greedy = new GreedyDamageCardPickStrategy();
            CardPickDecision d1 = greedy.Choose(ctxOpenSlot);
            Assert.IsFalse(d1.RequestFreeReroll);
            Assert.AreEqual(1, d1.SlotIndex, "Should prefer new Active ability when active slot is available");

            // Full active slots: prefer Damage/Count/Cooldown modifier of highest-damage active
            var modLowDmg = CreateModifierCard("Mod_Low", "OrbitalSword", ModifierType.Damage, 10f);
            var modTopDmg = CreateModifierCard("Mod_Top", "Fireball", ModifierType.Damage, 15f);
            var statCard = CreateStatCard("Card_Hp", StatType.MaxHP, 20f);

            var ctxFullSlot = new CardPickContext
            {
                OfferedCards = new CardData[] { modLowDmg, modTopDmg, statCard },
                FreeSlotsCount = 2,
                CanAddActive = false,
                CanAddPassive = true,
                CanFreeReroll = false,
                EquippedActiveIds = new[] { "OrbitalSword", "Fireball" },
                GetAbilityDamage = id => id == "Fireball" ? 5000L : 1000L,
                GetAbilityLevel = id => 3
            };

            CardPickDecision d2 = greedy.Choose(ctxFullSlot);
            Assert.IsFalse(d2.RequestFreeReroll);
            Assert.AreEqual(1, d2.SlotIndex, "Should prefer DPS modifier for top-damage ability");

            // Open slot with weak offers + free reroll available -> reroll
            var ctxReroll = new CardPickContext
            {
                OfferedCards = new CardData[] { statCard, CreateStatCard("Card_Mag", StatType.MagnetRadius, 1f) },
                FreeSlotsCount = 2,
                CanAddActive = true,
                CanFreeReroll = true
            };
            CardPickDecision d3 = greedy.Choose(ctxReroll);
            Assert.IsTrue(d3.RequestFreeReroll, "Should use free reroll when active slot is open and only weak stat cards offered");
        }

        [Test]
        public void FixedBuildCardPickStrategy_FollowsPresetOrder_AndFallsBackToGreedy()
        {
            var ab1 = CreateStubAbility("Fireball", AbilityType.Active);
            var ab2 = CreateStubAbility("IceAura", AbilityType.Active);
            var abOther = CreateStubAbility("Shooter", AbilityType.Active);

            var preset = Track(ScriptableObject.CreateInstance<BalanceBotBuildPreset>());
            preset.presetId = "TestPreset";
            preset.useFreeRerollForPreset = true;
            preset.priorityAbilities.Add(ab1);
            preset.priorityAbilities.Add(ab2);

            var strategy = new FixedBuildCardPickStrategy(preset);

            var cardOther = CreateAbilityCard("Card_Shooter", abOther);
            var cardAb2 = CreateAbilityCard("Card_IceAura", ab2);
            var cardAb1 = CreateAbilityCard("Card_Fireball", ab1);

            // Both priority 2 and priority 1 offered -> pick priority 1 (index 1)
            var ctx1 = new CardPickContext
            {
                OfferedCards = new CardData[] { cardAb2, cardAb1 },
                FreeSlotsCount = 2,
                CanAddActive = true,
                CanFreeReroll = false,
                HasAbility = _ => false
            };
            Assert.AreEqual(1, strategy.Choose(ctx1).SlotIndex);

            // Only non-preset offered while preset still missing & free reroll available -> reroll
            var ctxReroll = new CardPickContext
            {
                OfferedCards = new CardData[] { cardOther, CreateStatCard("Card_Hp", StatType.MaxHP, 10f) },
                FreeSlotsCount = 2,
                CanAddActive = true,
                CanFreeReroll = true,
                HasAbility = _ => false
            };
            Assert.IsTrue(strategy.Choose(ctxReroll).RequestFreeReroll);

            // No free reroll & no preset cards -> fallback to GreedyDamage (picks new active Shooter over stat)
            ctxReroll.CanFreeReroll = false;
            CardPickDecision fallbackDecision = strategy.Choose(ctxReroll);
            Assert.IsFalse(fallbackDecision.RequestFreeReroll);
            Assert.AreEqual(0, fallbackDecision.SlotIndex);
        }

        [Test]
        public void SaveIsolationScope_RestoresOriginalSaveData_AndHandlesMissingOriginalSave()
        {
            // Case 1: Existing save
            var initialData = new SaveData { coins = 777, bossShards = 19, totalRuns = 12, selectedHeroId = "mage" };
            string initialJson = JsonUtility.ToJson(initialData, false);
            PlayerPrefs.SetString(SaveManager.SAVE_KEY, initialJson);
            PlayerPrefs.Save();

            using (var saveManager = new SaveManager())
            {
                using (var isolation = new BalanceBotSaveIsolationScope(saveManager))
                {
                    var botData = new SaveData { coins = 999999, totalRuns = 50, selectedHeroId = "knight" };
                    saveManager.ReplaceDataInMemory(botData, saveToActiveStorage: true);
                    Assert.AreEqual(999999, saveManager.Data.coins);
                    // PlayerPrefs must NOT have been overwritten by InMemorySaveStorage
                    Assert.AreEqual(initialJson, PlayerPrefs.GetString(SaveManager.SAVE_KEY));
                }

                Assert.AreEqual(initialJson, PlayerPrefs.GetString(SaveManager.SAVE_KEY));
                Assert.AreEqual(777, saveManager.Data.coins);
                Assert.AreEqual("mage", saveManager.Data.selectedHeroId);
                Assert.IsFalse(BalanceBotSaveIsolationScope.HasPendingBackup());
            }

            // Case 2: No prior save + crash backup recovery
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            PlayerPrefs.Save();

            using (var saveManager2 = new SaveManager())
            {
                var scope = new BalanceBotSaveIsolationScope(saveManager2);
                PlayerPrefs.SetString(SaveManager.SAVE_KEY, "{\"coins\":123}"); // Simulate dirty write
                Assert.IsTrue(BalanceBotSaveIsolationScope.HasPendingBackup());
                Assert.IsTrue(BalanceBotSaveIsolationScope.RestorePendingBackupIfAny());
                Assert.IsFalse(PlayerPrefs.HasKey(SaveManager.SAVE_KEY));
                scope.Dispose();
            }
        }

        [Test]
        public void BalanceBotMetaProfile_BuildsFreshMidFullCorrectly_WithFtueDisabled()
        {
            var metaUpgrades = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
            var talentTree = Resources.Load<TalentTreeData>(TalentTreeData.RESOURCE_PATH);
            var heroCatalog = Resources.Load<HeroCatalog>(SelectedHeroService.CATALOG_RESOURCE_PATH);

            var freshProfile = AssetDatabase.LoadAssetAtPath<BalanceBotMetaProfile>(
                "Assets/_ScriptableObjects/BalanceBot/Profile_Fresh.asset");
            var midProfile = AssetDatabase.LoadAssetAtPath<BalanceBotMetaProfile>(
                "Assets/_ScriptableObjects/BalanceBot/Profile_Mid.asset");
            var fullProfile = AssetDatabase.LoadAssetAtPath<BalanceBotMetaProfile>(
                "Assets/_ScriptableObjects/BalanceBot/Profile_Full.asset");

            Assert.IsNotNull(freshProfile);
            Assert.IsNotNull(midProfile);
            Assert.IsNotNull(fullProfile);

            SaveData fresh = freshProfile.BuildSaveData("ranger", metaUpgrades, talentTree, null, heroCatalog);
            Assert.Greater(fresh.totalRuns, 0);
            Assert.AreEqual(0, fresh.metaUpgradeLevels.Count);
            Assert.AreEqual(0, fresh.unlockedTalentIds.Count);
            Assert.IsTrue(fresh.unlockedHeroIds.Contains("ranger"));

            SaveData mid = midProfile.BuildSaveData("knight", metaUpgrades, talentTree, null, heroCatalog);
            Assert.Greater(mid.totalRuns, 0);
            Assert.Greater(mid.metaUpgradeLevels.Count, 0);
            Assert.Greater(mid.unlockedTalentIds.Count, 0);
            Assert.Less(mid.unlockedTalentIds.Count, talentTree.nodes.Count - 1);

            SaveData full = fullProfile.BuildSaveData("necromancer", metaUpgrades, talentTree, null, heroCatalog);
            Assert.Greater(full.totalRuns, 0);
            Assert.AreEqual(metaUpgrades.Length, full.metaUpgradeLevels.Count);
            Assert.AreEqual(3, full.bonusActiveSlots);
            Assert.AreEqual(3, full.bonusPassiveSlots);
            foreach (var upg in metaUpgrades)
            {
                Assert.AreEqual(upg.maxLevel, full.metaUpgradeLevels[upg.id]);
            }
        }

        [Test]
        public void MoveInputService_SetOverride_OverridesAndRestoresNormalInput()
        {
            var svc = new MoveInputService();
            Assert.IsFalse(svc.HasOverride);
            Assert.AreEqual(Vector2.zero, svc.Direction);

            svc.SetOverride(new Vector2(3f, 4f));
            Assert.IsTrue(svc.HasOverride);
            Assert.AreEqual(1f, svc.Direction.magnitude, 0.001f);

            svc.SetOverride(null);
            Assert.IsFalse(svc.HasOverride);
            Assert.AreEqual(Vector2.zero, svc.Direction);
        }

        [Test]
        public void CardSelectionUI_SelectCardForAutomation_RejectsAdSlotAndBypassesLock()
        {
            var panel = Track(new GameObject("Panel"));
            var uiGo = Track(new GameObject("CardUIHost", typeof(CardSelectionUI)));
            var ui = uiGo.GetComponent<CardSelectionUI>();
            typeof(CardSelectionUI).GetField("selectionPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(ui, panel);
            ui.SetUnscaledTimeProviderForTest(() => 10f);

            var c0 = CreateStatCard("C0", StatType.MaxHP, 10f);
            var c1 = CreateStatCard("C1", StatType.Armor, 5f);
            var c2Ad = CreateStatCard("C2Ad", StatType.GlobalDamage, 50f);

            CardData picked = null;
            int rerollCount = 0;
            ui.ShowCards(
                new List<CardData> { c0, c1, c2Ad },
                c => picked = c,
                (prev, isFree) =>
                {
                    rerollCount++;
                    return new List<CardData> { c1, c0, c2Ad };
                },
                freeRerolls: 1);

            // Ad slot (index 2) must be rejected
            Assert.IsFalse(ui.SelectCardForAutomation(2));
            Assert.IsNull(picked);

            // Free reroll works even while input lock is active
            Assert.IsTrue(ui.TryFreeRerollForAutomation());
            Assert.AreEqual(1, rerollCount);
            Assert.IsFalse(ui.CanFreeReroll);
            Assert.IsFalse(ui.TryFreeRerollForAutomation());

            // Free slot (index 1) succeeds immediately
            Assert.IsTrue(ui.SelectCardForAutomation(1));
            Assert.AreSame(c0, picked);
            Assert.IsFalse(ui.IsActive);
        }

        private StatUpgradeCardData CreateStatCard(string name, StatType statType, float value)
        {
            var c = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            c.name = name;
            c.cardName = name;
            c.statType = statType;
            c.value = value;
            return c;
        }

        private AbilityData CreateStubAbility(string id, AbilityType type)
        {
            var ab = Track(ScriptableObject.CreateInstance<OrbitalCompanionAbilityData>());
            ab.name = id;
            ab.id = id;
            ab.abilityName = id;
            ab.type = type;
            return ab;
        }

        private AbilityCardData CreateAbilityCard(string name, AbilityData ability)
        {
            var c = Track(ScriptableObject.CreateInstance<AbilityCardData>());
            c.name = name;
            c.cardName = name;
            c.abilityData = ability;
            return c;
        }

        private AbilityModifierCardData CreateModifierCard(string name, string targetId, ModifierType modType, float val)
        {
            var c = Track(ScriptableObject.CreateInstance<AbilityModifierCardData>());
            c.name = name;
            c.cardName = name;
            c.targetAbilityId = targetId;
            c.modifierType = modType;
            c.value = val;
            return c;
        }
    }
}
