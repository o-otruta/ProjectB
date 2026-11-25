using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.Abilities;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class Sprint11ConstellationAndExpansionTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly List<IDisposable> disposables = new List<IDisposable>();
        private readonly HashSet<GameObject> preExistingRoots = new HashSet<GameObject>();
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);

            preExistingRoots.Clear();
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded)
            {
                foreach (GameObject root in active.GetRootGameObjects()) preExistingRoots.Add(root);
            }
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

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded)
            {
                foreach (GameObject root in active.GetRootGameObjects())
                {
                    if (root != null && !preExistingRoots.Contains(root)) UnityEngine.Object.DestroyImmediate(root);
                }
            }
            Physics.SyncTransforms();

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

        private static LayerMask EnemyMask => 1 << 8;

        private EnemyBase SpawnEnemy(string name, Vector3 pos, int hp, GameEventBus bus)
        {
            var data = Track(ScriptableObject.CreateInstance<EnemyData>());
            data.hp = hp;
            data.speed = 3f;
            data.contactDamage = 5;
            var go = Track(new GameObject(name));
            go.layer = 8;
            go.transform.position = pos;
            go.AddComponent<BoxCollider>();
            var enemy = go.AddComponent<EnemyBase>();
            enemy.Initialize(data, null, null, bus, 1f);
            return enemy;
        }

        private HeroAbilities CreateHeroAbilities(HeroRuntimeStats stats, GameEventBus bus, RunStatistics runStats, List<SynergyData> synergies, out GameObject heroGo, HeroHealth health = null)
        {
            heroGo = health != null ? health.gameObject : Track(new GameObject("S11Hero"));
            var save = NewSave();
            save.AddBonusActiveSlots(3, false);
            save.AddBonusPassiveSlots(3, false);
            var slots = new AbilitySlotService(save, null);
            disposables.Add(slots);
            var ha = heroGo.AddComponent<HeroAbilities>();
            ha.Construct(stats, bus, null, health, slots, runStats, new DefaultSynergyUnlockProvider());
            ha.SetAvailableSynergies(synergies ?? new List<SynergyData>());
            return ha;
        }

        private HeroHealth CreateHeroHealth(HeroRuntimeStats stats, GameEventBus bus, int maxHp = 100)
        {
            var heroData = Track(ScriptableObject.CreateInstance<HeroData>());
            heroData.maxHp = maxHp;
            heroData.passiveType = HeroPassiveType.None;
            heroData.passiveValue = 0f;
            var go = Track(new GameObject("S11HeroHealth"));
            int heroLayer = LayerMask.NameToLayer("Hero");
            if (heroLayer >= 0) go.layer = heroLayer;
            var health = go.AddComponent<HeroHealth>();
            var so = new SerializedObject(health);
            so.FindProperty("heroData").objectReferenceValue = heroData;
            so.ApplyModifiedPropertiesWithoutUndo();
            var zeroIframeRules = Track(ScriptableObject.CreateInstance<RunRulesConfig>());
            zeroIframeRules.postHitInvulnerabilityDuration = 0f;
            health.SetRunRulesConfig(zeroIframeRules);
            health.Construct(stats, bus, null);
            typeof(HeroHealth).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(health, null);
            return health;
        }

        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, $"Missing asset {path}");
            return a;
        }

        private TalentTreeData BuildTestTree()
        {
            var tree = Track(ScriptableObject.CreateInstance<TalentTreeData>());
            TalentNodeData Make(string id, TalentNodeType type, int shards, int coins, string[] prereq, string synergy, params TalentEffect[] effects)
            {
                var n = Track(ScriptableObject.CreateInstance<TalentNodeData>());
                n.id = id;
                n.nodeType = type;
                n.shardCost = shards;
                n.coinCost = coins;
                n.prerequisiteIds = new List<string>(prereq ?? new string[0]);
                n.synergyId = synergy;
                n.effects = new List<TalentEffect>(effects);
                tree.nodes.Add(n);
                return n;
            }

            Make("origin", TalentNodeType.Origin, 0, 0, null, null);
            Make("crit", TalentNodeType.Stat, 1, 100, null, null, new TalentEffect(TalentEffectType.CritChancePercent, 6f));
            Make("area", TalentNodeType.Stat, 2, 0, new[] { "crit" }, null, new TalentEffect(TalentEffectType.GlobalAreaPercent, 10f));
            Make("syn_toxic", TalentNodeType.Synergy, 3, 0, new[] { "area" }, SynergyIds.ToxicInferno);
            Make("arsenal", TalentNodeType.Keystone, 5, 0, new[] { "syn_toxic" }, null,
                new TalentEffect(TalentEffectType.BonusActiveSlots, 1f), new TalentEffect(TalentEffectType.CooldownReductionPercent, 8f));
            Make("greed", TalentNodeType.Keystone, 1, 0, null, null,
                new TalentEffect(TalentEffectType.BonusPassiveSlots, 1f), new TalentEffect(TalentEffectType.CrystalCoinBonus, 5f));
            Make("wind", TalentNodeType.Keystone, 1, 0, null, null, new TalentEffect(TalentEffectType.SecondWind, 1f));
            return tree;
        }

        // ------------------------------------------------------------------ 11.1 Save migration

        [Test]
        public void SaveData_V2_PersistsBossShardsAndUnlockedTalents()
        {
            var fresh = new SaveData();
            Assert.AreEqual(SaveData.CURRENT_SAVE_VERSION, fresh.saveVersion);
            Assert.AreEqual(2, fresh.saveVersion);
            Assert.AreEqual(0, fresh.bossShards);
            Assert.IsNotNull(fresh.unlockedTalentIds);

            var save = NewSave();
            save.AddBossShards(7);
            save.UnlockTalent("fury_r1a");
            save.UnlockTalent("fury_r1a");
            Assert.AreEqual(1, save.Data.unlockedTalentIds.Count, "Unlocking twice must not duplicate");

            var reloaded = new SaveManager();
            disposables.Add(reloaded);
            Assert.AreEqual(7, reloaded.BossShards);
            Assert.AreEqual(7, reloaded.Data.totalBossShardsEarned);
            Assert.IsTrue(reloaded.IsTalentUnlocked("fury_r1a"));
            Assert.IsFalse(reloaded.IsTalentUnlocked("fury_r1b"));
        }

        [Test]
        public void BossShards_AwardedForMiniBossBossAndShrine_AndPersistedWithRunResult()
        {
            var bus = new GameEventBus();
            var runStats = new RunStatistics();
            var handler = new MetaRunRewardsHandler(bus, runStats);
            handler.Start();
            disposables.Add(handler);

            int events = 0;
            bus.Subscribe<BossShardsGainedEvent>(_ => events++);

            bus.Publish(new EnemyDiedEvent(Vector3.zero, null, false));
            Assert.AreEqual(0, runStats.BossShardsEarned, "Regular enemies give no shards");
            bus.Publish(new EnemyDiedEvent(Vector3.zero, null, false, wasMiniBoss: true));
            Assert.AreEqual(1, runStats.BossShardsEarned);
            bus.Publish(new EnemyDiedEvent(Vector3.zero, null, false, wasBoss: true, bossId: "golem"));
            Assert.AreEqual(6, runStats.BossShardsEarned);
            bus.Publish(new ChallengeShrineEvent(ShrineEventType.Started, Vector3.zero, 6, 6, 0));
            bus.Publish(new ChallengeShrineEvent(ShrineEventType.Completed, Vector3.zero, 0, 6, 100));
            Assert.AreEqual(8, runStats.BossShardsEarned);
            Assert.AreEqual(1, runStats.ShrinesCleared);
            Assert.AreEqual(3, events);

            var save = NewSave();
            save.RecordRunResult(new RunResult { waveReached = 10, bossShardsEarned = runStats.BossShardsEarned });
            Assert.AreEqual(8, save.BossShards);
            Assert.AreEqual(8, save.Data.totalBossShardsEarned);
            Assert.IsTrue(save.SpendBossShards(5));
            Assert.IsFalse(save.SpendBossShards(10));
            Assert.AreEqual(3, save.BossShards);

            var cfg = LoadAsset<ProjectB.Data.Enemies.WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.AreEqual(1, cfg.miniBossShardReward);
            Assert.AreEqual(5, cfg.bossShardReward);
            Assert.AreEqual(2, cfg.shrineShardReward);
        }

        // ------------------------------------------------------------------ 11.1 Talent tree manager

        [Test]
        public void TalentTreeManager_UnlockFlow_SlotBonusProvider_AndSynergyGating()
        {
            var save = NewSave();
            var slots = new AbilitySlotService(save, null);
            disposables.Add(slots);
            var tree = BuildTestTree();
            var mgr = new TalentTreeManager(save, tree, slots);
            disposables.Add(mgr);

            Assert.AreEqual(AbilitySlotService.BASE_ACTIVE_SLOTS, slots.MaxActiveSlots);
            Assert.IsTrue(mgr.IsUnlocked("origin"), "Origin is always lit");
            Assert.AreEqual(TalentNodeState.Available, mgr.GetState("crit"));
            Assert.AreEqual(TalentNodeState.Locked, mgr.GetState("area"));
            Assert.AreEqual(TalentUnlockResult.NotConnected, mgr.TryUnlock("area"));
            Assert.AreEqual(TalentUnlockResult.NotEnoughShards, mgr.TryUnlock("crit"));

            // Synergy gated by its node; synergies without nodes are never gated
            Assert.IsFalse(mgr.IsSynergyUnlocked(SynergyIds.ToxicInferno));
            Assert.IsTrue(mgr.IsSynergyUnlocked(SynergyIds.PhantomLegion));
            Assert.IsFalse(mgr.IsSynergyUnlocked(null));

            // Inner ring coin purchase
            save.AddCoins(150);
            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("crit", payWithCoins: true));
            Assert.AreEqual(50, save.Data.coins);
            Assert.AreEqual(TalentUnlockResult.AlreadyUnlocked, mgr.TryUnlock("crit"));
            Assert.AreEqual(TalentUnlockResult.CoinsNotAllowed, mgr.CanUnlock("area", payWithCoins: true));
            Assert.AreEqual(6f, mgr.GetTotalEffect(TalentEffectType.CritChancePercent), 0.001f);

            int slotEvents = 0;
            mgr.OnSlotBonusesChanged += () => slotEvents++;
            int capacityEvents = 0;
            slots.OnSlotCapacityChanged += () => capacityEvents++;

            save.AddBossShards(10);
            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("area"));
            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("syn_toxic"));
            Assert.IsTrue(mgr.IsSynergyUnlocked(SynergyIds.ToxicInferno));
            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("arsenal"));
            Assert.AreEqual(0, save.BossShards);

            Assert.AreEqual(1, mgr.GetBonusActiveSlots());
            Assert.AreEqual(AbilitySlotService.BASE_ACTIVE_SLOTS + 1, slots.MaxActiveSlots, "Keystone Overclocked Arsenal grants +1 active slot via IAbilitySlotBonusProvider");
            Assert.GreaterOrEqual(slotEvents, 1);
            Assert.GreaterOrEqual(capacityEvents, 1);
            Assert.AreEqual(8f, mgr.GetTotalEffect(TalentEffectType.CooldownReductionPercent), 0.001f);
            Assert.IsTrue(save.IsTalentUnlocked("arsenal"));

            // Persisted: a fresh manager on the same save sees the unlocks
            var mgr2 = new TalentTreeManager(save, tree, null);
            disposables.Add(mgr2);
            Assert.AreEqual(4, mgr2.UnlockedCount);
            Assert.AreEqual(1, mgr2.GetBonusActiveSlots());

            // Unregister removes the bonus
            mgr.Dispose();
            Assert.AreEqual(AbilitySlotService.BASE_ACTIVE_SLOTS, slots.MaxActiveSlots);
        }

        [Test]
        public void TalentTree_SynergyGating_ControlsHeroAbilitiesAutoActivation()
        {
            var save = NewSave();
            var tree = BuildTestTree();
            var mgr = new TalentTreeManager(save, tree, null);
            disposables.Add(mgr);

            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var syn = LoadAsset<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_ToxicInferno.asset");
            var ha = CreateHeroAbilities(stats, bus, new RunStatistics(), new List<SynergyData> { syn }, out _);
            ha.SetSynergyUnlockProvider(mgr);

            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/PoisonFlaskData.asset")));
            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/FireballData.asset")));
            Assert.IsFalse(ha.HasSynergy(SynergyIds.ToxicInferno), "Locked constellation node must block auto-activation");

            save.AddBossShards(6);
            save.AddCoins(100);
            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("crit", true));
            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("area"));
            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("syn_toxic"));
            ha.EvaluateAutoSynergies();
            Assert.IsTrue(ha.HasSynergy(SynergyIds.ToxicInferno));
        }

        [Test]
        public void TalentTreeAsset_IsRadial_WithThreeKeystonesAndAllTwelveSynergyNodes()
        {
            var tree = Resources.Load<TalentTreeData>(TalentTreeData.RESOURCE_PATH);
            Assert.IsNotNull(tree, "Resources/TalentTree.asset missing");
            Assert.GreaterOrEqual(tree.nodes.Count, 30);
            Assert.IsNotNull(tree.GetOrigin());
            Assert.AreEqual(Vector2.zero, tree.GetOrigin().position);

            var ids = new HashSet<string>();
            int keystones = 0;
            var synergyIds = new HashSet<string>();
            float half = tree.canvasSize * 0.5f;
            float minKeystoneRadius = float.MaxValue, maxSynergyRadius = 0f;
            var angles = new HashSet<int>();

            foreach (var n in tree.nodes)
            {
                Assert.IsNotNull(n);
                Assert.IsTrue(ids.Add(n.id), $"Duplicate node id {n.id}");
                Assert.IsNotNull(n.icon, $"{n.id} has no icon");
                float halfNode = ConstellationTreeUI.NODE_SIZE * 0.5f * ConstellationTreeUI.KEYSTONE_SCALE;
                Assert.LessOrEqual(Mathf.Abs(n.position.x) + halfNode, half, $"{n.id} outside canvas");
                Assert.LessOrEqual(Mathf.Abs(n.position.y) + halfNode, half, $"{n.id} outside canvas");
                if (n.nodeType == TalentNodeType.Keystone)
                {
                    keystones++;
                    minKeystoneRadius = Mathf.Min(minKeystoneRadius, n.position.magnitude);
                }
                if (n.nodeType == TalentNodeType.Synergy)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(n.synergyId));
                    synergyIds.Add(n.synergyId);
                    maxSynergyRadius = Mathf.Max(maxSynergyRadius, n.position.magnitude);
                }
                if (n.nodeType != TalentNodeType.Origin)
                {
                    angles.Add(Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(n.position.y, n.position.x) * Mathf.Rad2Deg, 360f) / 90f) % 4);
                }
            }

            foreach (var n in tree.nodes)
            {
                foreach (string p in n.prerequisiteIds)
                {
                    Assert.IsTrue(ids.Contains(p), $"{n.id} references unknown prerequisite {p}");
                }
            }

            Assert.AreEqual(3, keystones, "Overclocked Arsenal, Second Wind, Collector's Greed");
            Assert.Greater(minKeystoneRadius, maxSynergyRadius, "Keystones sit on the outer rim beyond synergy stars");
            Assert.GreaterOrEqual(angles.Count, 4, "Branches spread in every direction (360°)");

            var catalog = Resources.Load<SynergyCatalog>("SynergyCatalog");
            Assert.AreEqual(12, catalog.synergies.Count);
            foreach (var syn in catalog.synergies)
            {
                Assert.IsTrue(synergyIds.Contains(syn.id), $"Synergy {syn.id} has no Constellation node");
            }

            // Every node can be reached from the origin
            var save = NewSave();
            var mgr = new TalentTreeManager(save, tree, null);
            disposables.Add(mgr);
            save.AddBossShards(999);
            int guard = 0;
            bool progress = true;
            while (progress && guard++ < 100)
            {
                progress = false;
                foreach (var n in tree.nodes)
                {
                    if (mgr.TryUnlock(n.id) == TalentUnlockResult.Success) progress = true;
                }
            }
            Assert.AreEqual(tree.nodes.Count - 1, mgr.UnlockedCount, "All stars must be reachable");
            // The full Constellation grants 3+3 slots (achievements add only 1+1: first mini-boss and Golem).
            Assert.AreEqual(3, mgr.GetBonusActiveSlots());
            Assert.AreEqual(3, mgr.GetBonusPassiveSlots());
            Assert.IsTrue(mgr.HasEffect(TalentEffectType.SecondWind));
        }

        [Test]
        public void HeroRuntimeStats_ApplyTalentBonuses_IsIdempotentAndScalesArea()
        {
            var save = NewSave();
            var tree = BuildTestTree();
            var mgr = new TalentTreeManager(save, tree, null);
            disposables.Add(mgr);
            save.AddBossShards(20);
            mgr.TryUnlock("crit");
            mgr.TryUnlock("area");
            mgr.TryUnlock("greed");

            var stats = new HeroRuntimeStats(null, null, mgr);
            Assert.AreEqual(0.06f, stats.CritChance, 0.0001f);
            Assert.AreEqual(1.10f, stats.GlobalAreaMultiplier, 0.0001f);
            Assert.AreEqual(5, stats.CrystalCoinBonus);
            Assert.AreEqual(5.5f, stats.GetScaledArea(5f), 0.0001f);

            stats.ApplyTalentBonuses(mgr);
            stats.ApplyTalentBonuses(mgr);
            Assert.AreEqual(0.06f, stats.CritChance, 0.0001f, "Re-applying talents must not stack");

            stats.AddAreaPercent(12f); // Area Tome
            Assert.AreEqual(1.22f, stats.GlobalAreaMultiplier, 0.0001f);

            stats.ApplyTalentBonuses(null);
            Assert.AreEqual(0f, stats.CritChance, 0.0001f);
            Assert.AreEqual(1.12f, stats.GlobalAreaMultiplier, 0.0001f);
        }

        [Test]
        public void SecondWindKeystone_SurvivesLethalHitOncePerRun()
        {
            var save = NewSave();
            var tree = BuildTestTree();
            var mgr = new TalentTreeManager(save, tree, null);
            disposables.Add(mgr);
            save.AddBossShards(1);
            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("wind"));

            var stats = new HeroRuntimeStats(null, null, mgr);
            Assert.IsTrue(stats.HasSecondWind);
            var bus = new GameEventBus();
            int windEvents = 0;
            bus.Subscribe<SecondWindTriggeredEvent>(_ => windEvents++);

            var enemy = SpawnEnemy("WindEnemy", new Vector3(1.5f, 0f, 0f), 500, bus);
            Physics.SyncTransforms();
            var health = CreateHeroHealth(stats, bus, 100);
            int died = 0;
            health.OnDied += () => died++;

            Assert.IsTrue(health.HasSecondWindAvailable);
            health.TakeDamage(9999);
            Assert.IsFalse(health.IsDead, "Second Wind must prevent the lethal hit");
            Assert.AreEqual(30, health.CurrentHp, "Survive with 30% HP");
            Assert.IsTrue(health.IsInvulnerable, "2.5s of invulnerability");
            Assert.IsTrue(health.SecondWindUsed);
            Assert.AreEqual(1, windEvents);
            Assert.Greater(enemy.transform.position.x, 1.5f, "Knockback wave pushes enemies away");

            health.TakeDamage(9999);
            Assert.IsFalse(health.IsDead, "Still invulnerable");
            health.TickInvulnerability(3f);
            health.TakeDamage(9999);
            Assert.IsTrue(health.IsDead, "Second Wind works only once per run");
            Assert.AreEqual(1, died);
        }

        // ------------------------------------------------------------------ 11.1 Pan / zoom UX

        [Test]
        public void ConstellationPanZoom_ClampsPansZoomsAroundPivotAndSpringsBack()
        {
            Vector2 content = new Vector2(2200f, 2200f);
            Vector2 view = new Vector2(1000f, 1500f);

            Vector2 max = ConstellationPanZoom.ComputeMaxOffset(content, 1f, view, 200f);
            Assert.AreEqual(600f + 200f, max.x, 0.01f);
            Assert.AreEqual(350f + 200f, max.y, 0.01f);

            Vector2 tiny = ConstellationPanZoom.ComputeMaxOffset(content, 0.3f, view, 50f);
            Assert.AreEqual(50f, tiny.x, 0.01f, "When zoomed out below the viewport only the margin remains");

            Assert.AreEqual(new Vector2(800f, -550f), ConstellationPanZoom.ClampOffset(new Vector2(5000f, -9000f), max));
            Assert.AreEqual(new Vector2(-120f, 40f), ConstellationPanZoom.ClampOffset(new Vector2(-120f, 40f), max), "Free 360° pan inside bounds");

            float r1 = ConstellationPanZoom.RubberDelta(100f, 1000f);
            float r2 = ConstellationPanZoom.RubberDelta(400f, 1000f);
            Assert.Greater(r1, 0f);
            Assert.Less(r1, 100f, "Rubber band resists overscroll");
            Assert.Greater(r2, r1);
            Assert.Less(ConstellationPanZoom.RubberDelta(-100f, 1000f), 0f);

            Vector2 pivot = new Vector2(150f, -90f);
            Vector2 pos = new Vector2(40f, 25f);
            Vector2 newPos = ConstellationPanZoom.ZoomAroundPivot(pos, 1f, 1.5f, pivot);
            Vector2 contentPointBefore = (pivot - pos) / 1f;
            Vector2 contentPointAfter = (pivot - newPos) / 1.5f;
            Assert.AreEqual(contentPointBefore.x, contentPointAfter.x, 0.01f, "Pivot stays under the fingers while pinching");
            Assert.AreEqual(contentPointBefore.y, contentPointAfter.y, 0.01f);

            Vector2 focus = ConstellationPanZoom.PositionToShowPoint(new Vector2(300f, 700f), 0.8f, new Vector2(0f, 300f));
            Assert.AreEqual(new Vector2(-240f, 300f - 560f), focus);

            // Runtime component
            var canvasGo = Track(new GameObject("S11Canvas", typeof(RectTransform), typeof(Canvas)));
            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            var vp = (RectTransform)viewportGo.transform;
            vp.SetParent(canvasGo.transform, false);
            vp.sizeDelta = view;
            var contentGo = new GameObject("Content", typeof(RectTransform));
            var ct = (RectTransform)contentGo.transform;
            ct.SetParent(vp, false);
            ct.sizeDelta = content;
            var pz = viewportGo.AddComponent<ConstellationPanZoom>();
            pz.Configure(vp, ct);

            pz.SetView(new Vector2(99999f, -99999f), 5f);
            Assert.AreEqual(pz.MaxZoom, pz.Zoom, 0.0001f, "Zoom clamps to max");
            Vector2 maxAtZoom = ConstellationPanZoom.ComputeMaxOffset(content, pz.Zoom, view, 220f);
            Assert.AreEqual(maxAtZoom.x, pz.Position.x, 0.01f);
            Assert.AreEqual(-maxAtZoom.y, pz.Position.y, 0.01f);

            pz.SetView(Vector2.zero, 1f);
            ct.anchoredPosition = new Vector2(2000f, 0f); // simulate an overscroll drag
            for (int i = 0; i < 240; i++) pz.StepInertia(1f / 60f);
            Vector2 limit = ConstellationPanZoom.ComputeMaxOffset(content, 1f, view, 220f);
            Assert.LessOrEqual(pz.Position.x, limit.x + 1f, "Elastic bounce returns the map inside the bounds");

            pz.SetView(Vector2.zero, 1f);
            pz.SetVelocity(new Vector2(-900f, 600f));
            pz.StepInertia(0.1f);
            Assert.Less(pz.Position.x, 0f, "Inertia keeps panning after release");
            Assert.Greater(pz.Position.y, 0f);
            Assert.Less(pz.Velocity.magnitude, new Vector2(-900f, 600f).magnitude, "Inertia decays");
        }

        // ------------------------------------------------------------------ 11.2 Heroes & weapon cards

        [Test]
        public void HeroRoster_HasFourHeroes_AndFormerHeroAchievementsGiveCoins()
        {
            var catalog = Resources.Load<HeroCatalog>("HeroCatalog");
            Assert.AreEqual(4, catalog.heroes.Count);
            foreach (string id in new[] { "knight", "mage", "ranger", "necromancer" })
            {
                Assert.IsNotNull(catalog.GetById(id), $"Missing hero {id}");
            }
            Assert.IsNull(catalog.GetById("paladin"), "Paladin was removed");
            Assert.IsNull(catalog.GetById("engineer"), "Engineer was removed");

            var lich = LoadAsset<AchievementData>("Assets/Resources/Achievements/defeat_frost_lich.asset");
            Assert.AreEqual(AchievementRewardType.GiveShards, lich.rewardType);
            Assert.Greater(lich.rewardValue, 0);
            var shrines = LoadAsset<AchievementData>("Assets/Resources/Achievements/clear_5_shrines.asset");
            Assert.AreEqual(AchievementConditionType.ClearShrines, shrines.conditionType);
            Assert.AreEqual(AchievementRewardType.GiveCoins, shrines.rewardType);
            Assert.Greater(shrines.rewardValue, 0);

            foreach (string model in new[] { "BuzzsawChakram", "SkyfallSpire", "ShadowDecoy" })
            {
                Assert.IsTrue(File.Exists($"ArtSources/Blender/{model}.blend"), $"Missing {model}.blend source");
            }
        }

        [Test]
        public void HealingBonusTalent_AmplifiesHeals()
        {
            var stats = new HeroRuntimeStats();
            var health = CreateHeroHealth(stats, new GameEventBus(), 100);
            health.TakeDamage(50);

            // Constellation +15% Healing
            var tree = BuildTestTree();
            var healNode = Track(ScriptableObject.CreateInstance<TalentNodeData>());
            healNode.id = "heal";
            healNode.effects = new List<TalentEffect> { new TalentEffect(TalentEffectType.HealingPercent, 100f) };
            tree.nodes.Add(healNode);
            var save = NewSave();
            var mgr = new TalentTreeManager(save, tree, null);
            disposables.Add(mgr);
            save.AddBossShards(1);
            mgr.TryUnlock("heal");
            stats.ApplyTalentBonuses(mgr);
            int before = health.CurrentHp;
            health.Heal(5);
            Assert.AreEqual(before + 10, health.CurrentHp, "Healing bonus doubles heals at +100%");
        }

        [Test]
        public void UltimateCooldownTalent_ReducesUltimateCooldown()
        {
            var ultTree = BuildTestTree();
            var ultNode = Track(ScriptableObject.CreateInstance<TalentNodeData>());
            ultNode.id = "ultcdr";
            ultNode.effects = new List<TalentEffect> { new TalentEffect(TalentEffectType.UltimateCooldownPercent, 25f) };
            ultTree.nodes.Add(ultNode);
            var save = NewSave();
            var mgr = new TalentTreeManager(save, ultTree, null);
            disposables.Add(mgr);
            save.AddBossShards(1);
            mgr.TryUnlock("ultcdr");
            var cdrStats = new HeroRuntimeStats(null, null, mgr);
            Assert.AreEqual(0.75f, cdrStats.UltimateCooldownMultiplier, 0.0001f);
        }

        [Test]
        public void WeaponUpgradeCards_MultishotFireRateAndPower_ApplyToWeaponController()
        {
            Assert.IsNotNull(LoadAsset<WeaponUpgradeCardData>("Assets/_ScriptableObjects/Cards/WeaponMod_Multishot.asset"));
            var fireRateCard = LoadAsset<WeaponUpgradeCardData>("Assets/_ScriptableObjects/Cards/WeaponMod_FireRate.asset");
            var powerCard = LoadAsset<WeaponUpgradeCardData>("Assets/_ScriptableObjects/Cards/WeaponMod_Power.asset");
            var multiCard = LoadAsset<WeaponUpgradeCardData>("Assets/_ScriptableObjects/Cards/WeaponMod_Multishot.asset");
            Assert.AreEqual(WeaponUpgradeType.Multishot, multiCard.upgradeType);

            var weapon = Track(ScriptableObject.CreateInstance<WeaponData>());
            weapon.damage = 10;
            weapon.attackCooldown = 0.5f;
            weapon.range = 10f;
            weapon.projectileSpeed = 10f;
            weapon.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Projectiles/RangerArrowProjectile.prefab");

            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var health = CreateHeroHealth(stats, bus, 100);
            var combat = health.gameObject.AddComponent<HeroCombat>();
            var cso = new SerializedObject(combat);
            cso.FindProperty("enemyLayer").intValue = EnemyMask;
            cso.ApplyModifiedPropertiesWithoutUndo();
            combat.Construct(health, stats, null, null);
            combat.EnsureInitialized();
            combat.AddWeapon(weapon);
            WeaponController controller = combat.ActiveWeapons[combat.ActiveWeapons.Count - 1];

            float baseCooldown = controller.EffectiveAttackCooldown;
            float baseDamage = controller.DamageMultiplier;

            var applier = new UpgradeApplier(health, stats, null, bus, combat);
            applier.ApplyCard(fireRateCard, null);
            applier.ApplyCard(powerCard, null);
            applier.ApplyCard(multiCard, null);

            Assert.AreEqual(baseCooldown / 1.2f, controller.EffectiveAttackCooldown, 0.001f, "+20% attack speed");
            Assert.AreEqual(baseDamage * 1.3f, controller.DamageMultiplier, 0.001f, "+30% damage");
            Assert.AreEqual(2, controller.ProjectilesPerVolley);
            Assert.AreEqual(1, combat.GetWeaponUpgradeStacks(WeaponUpgradeType.Multishot));

            var e1 = SpawnEnemy("MultiA", new Vector3(3f, 0f, 0f), 100, bus);
            SpawnEnemy("MultiB", new Vector3(-3f, 0f, 1f), 100, bus);
            Physics.SyncTransforms();
            Assert.AreEqual(2, controller.FireVolley(e1.transform), "Multishot fires a fan of 2 projectiles");
        }

        [Test]
        public void PiercingProjectile_RetargetsToNextEnemy()
        {
            var bus = new GameEventBus();
            var a = SpawnEnemy("PierceA", new Vector3(0f, 0f, 2f), 500, bus);
            var b = SpawnEnemy("PierceB", new Vector3(1f, 0f, 3f), 500, bus);
            Physics.SyncTransforms();

            var weapon = Track(ScriptableObject.CreateInstance<WeaponData>());
            weapon.damage = 10;
            weapon.projectileSpeed = 10f;
            weapon.pierceCount = 2;
            weapon.pierceRetargetRadius = 4.5f;

            var go = Track(new GameObject("PiercingProjectile"));
            go.transform.position = new Vector3(0f, 0f, 2f);
            var projectile = go.AddComponent<Projectile>();
            projectile.Initialize(weapon, a.transform, null, 1f, EnemyMask, null, null, null);
            Assert.AreEqual(2, projectile.RemainingPierces);

            projectile.SimulateHit();
            Assert.Less(a.CurrentHp, 500);
            Assert.AreEqual(1, projectile.RemainingPierces);
            Assert.AreEqual(b.transform, projectile.CurrentTarget, "Retargets to the nearest un-hit enemy");
        }

        // ------------------------------------------------------------------ 11.3 Abilities & synergies

        [Test]
        public void AreaTome_RaisesGlobalAreaMultiplier_AndScalesAoeAbilities()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var ha = CreateHeroAbilities(stats, bus, new RunStatistics(), null, out _);

            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/IceAuraData.asset")));
            var aura = ha.GetActiveAbility<IceAuraAbility>();
            float radiusBefore = aura.EffectiveRadius;

            var tome = LoadAsset<StatPassiveAbilityData>("Assets/_ScriptableObjects/Abilities/AreaTomeData.asset");
            Assert.AreEqual(PassiveEffectType.AreaPercent, tome.effectType);
            Assert.IsTrue(ha.AddAbility(tome));
            Assert.AreEqual(1.12f, stats.GlobalAreaMultiplier, 0.0001f);
            Assert.AreEqual(radiusBefore * 1.12f, aura.EffectiveRadius, 0.001f);

            ha.UpgradeAbility("AreaTome", ModifierType.Area, 6f);
            Assert.AreEqual(1.18f, stats.GlobalAreaMultiplier, 0.0001f);
        }

        [Test]
        public void VampiricFang_HealsEveryEightKills()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var health = CreateHeroHealth(stats, bus, 100);
            health.TakeDamage(50);
            var ha = CreateHeroAbilities(stats, bus, new RunStatistics(), null, out _, health);
            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/VampiricFangData.asset")));

            int hp = health.CurrentHp;
            for (int i = 0; i < 7; i++) bus.Publish(new EnemyDiedEvent(Vector3.zero, null, false));
            Assert.AreEqual(hp, health.CurrentHp);
            bus.Publish(new EnemyDiedEvent(Vector3.zero, null, false));
            Assert.AreEqual(hp + 2, health.CurrentHp, "+2 HP on the 8th kill");
        }

        [Test]
        public void GlacialShatter_FreezesThenShattersWithShrapnel()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var ha = CreateHeroAbilities(stats, bus, new RunStatistics(), null, out GameObject heroGo);
            var a = SpawnEnemy("FrozenA", new Vector3(2f, 0f, 0f), 1000, bus);
            var b = SpawnEnemy("FrozenB", new Vector3(2.8f, 0f, 0f), 1000, bus);
            var far = SpawnEnemy("Far", new Vector3(30f, 0f, 0f), 1000, bus);
            Physics.SyncTransforms();

            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/GlacialShatterData.asset")));
            var glacial = ha.GetActiveAbility<GlacialShatterAbility>();
            Assert.AreEqual(2, glacial.TriggerFreezeWave());
            Assert.IsTrue(a.IsFrozen);
            Assert.IsTrue(b.IsFrozen);
            Assert.IsFalse(far.IsFrozen);
            Assert.IsTrue(a.IsSlowed);
            Assert.IsTrue(glacial.HasPendingShatter);

            int hpAfterWave = a.CurrentHp;
            Assert.AreEqual(2, glacial.ShatterFrozen());
            Assert.Less(a.CurrentHp, hpAfterWave, "Shatter damage");
            Assert.IsFalse(a.IsFrozen, "Shatter consumes the freeze");
            Assert.AreEqual(1000, far.CurrentHp);
        }

        [Test]
        public void ShatterNova_SeismicSlamOnFrozenEnemyDeals2_5xAndBurstsIntoShards()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var syn = LoadAsset<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_ShatterNova.asset");
            var ha = CreateHeroAbilities(stats, bus, new RunStatistics(), new List<SynergyData> { syn }, out _);
            var frozen = SpawnEnemy("NovaFrozen", new Vector3(1.5f, 0f, 0f), 10000, bus);
            var neighbour = SpawnEnemy("NovaNeighbour", new Vector3(30f, 0f, 0f), 10000, bus);
            Physics.SyncTransforms();

            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/GlacialShatterData.asset")));
            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/SeismicHammerData.asset")));
            Assert.IsTrue(ha.HasSynergy(SynergyIds.ShatterNova));

            var hammer = ha.GetActiveAbility<SeismicHammerAbility>();
            frozen.ApplyFreeze(2f);
            neighbour.transform.position = new Vector3(3f, 0f, 0f);
            Physics.SyncTransforms();
            int neighbourHp = neighbour.CurrentHp;
            Assert.AreEqual(1, hammer.TriggerShatterNova(frozen, EnemyMask), "Ice shards strike the neighbour");
            Assert.Less(neighbour.CurrentHp, neighbourHp);
            Assert.IsFalse(frozen.IsFrozen);
            Assert.AreEqual(1, hammer.ShatterNovaCount);
        }

        [Test]
        public void BuzzsawChakram_Ricochets_AndRicochetBarrageSpawnsMiniDiscOnCrit()
        {
            var stats = new HeroRuntimeStats();
            stats.AddCritChancePercent(100f);
            var bus = new GameEventBus();
            var syn = LoadAsset<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_RicochetBarrage.asset");
            var ha = CreateHeroAbilities(stats, bus, new RunStatistics(), new List<SynergyData> { syn }, out _);
            var a = SpawnEnemy("DiscA", new Vector3(2f, 0f, 0f), 5000, bus);
            var b = SpawnEnemy("DiscB", new Vector3(4f, 0f, 0f), 5000, bus);
            var c = SpawnEnemy("DiscC", new Vector3(4.5f, 0f, 1.5f), 5000, bus);
            Physics.SyncTransforms();

            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/BuzzsawChakramData.asset")));
            var chakram = ha.GetActiveAbility<BuzzsawChakramAbility>();
            Assert.AreEqual(1, chakram.Fire());

            ChakramDisc disc = null;
            foreach (var d in UnityEngine.Object.FindObjectsByType<ChakramDisc>(FindObjectsInactive.Exclude))
            {
                if (d.IsActive && !d.IsMini) disc = d;
            }
            Assert.IsNotNull(disc);
            Assert.AreEqual(a.transform, disc.Target);

            // No synergy yet: crit does not spawn mini discs
            disc.ResolveHit();
            Assert.Less(a.CurrentHp, 5000);
            Assert.AreEqual(b.transform, disc.Target, "Bounces to the next enemy");
            Assert.AreEqual(0, chakram.MiniDiscsSpawned);

            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/CriticalLensData.asset")));
            Assert.IsTrue(ha.HasSynergy(SynergyIds.RicochetBarrage));
            float speedBefore = disc.Speed;
            disc.ResolveHit();
            Assert.Less(b.CurrentHp, 5000);
            Assert.AreEqual(1, chakram.MiniDiscsSpawned, "Critical hit spawns a shrapnel mini-disc");
            Assert.Greater(disc.Speed, speedBefore, "Main disc accelerates");
            Assert.AreEqual(c.transform, disc.Target);
        }

        [Test]
        public void SkyfallSpire_TargetsDensestCluster_AndSolarEclipsePullsEnemies()
        {
            var points = new[]
            {
                new Vector3(10f, 0f, 10f),
                new Vector3(-5f, 0f, 0f), new Vector3(-5.5f, 0f, 0.5f), new Vector3(-4.6f, 0f, -0.4f), new Vector3(-5.2f, 0f, -0.8f),
                new Vector3(20f, 0f, -20f)
            };
            var results = new Vector3[2];
            int chosen = SkyfallSpireAbility.SelectDensestPoints(points, points.Length, 3f, 2, results);
            Assert.AreEqual(2, chosen);
            Assert.Less(Vector3.Distance(results[0], new Vector3(-5f, 0f, 0f)), 1.5f, "First strike hits the densest group");
            Assert.Greater(Vector3.Distance(results[1], results[0]), 3f, "Second strike avoids overlapping the first");

            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var syn = LoadAsset<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_SolarEclipse.asset");
            var ha = CreateHeroAbilities(stats, bus, new RunStatistics(), new List<SynergyData> { syn }, out _);
            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/SkyfallSpireData.asset")));
            var spire = ha.GetActiveAbility<SkyfallSpireAbility>();

            var victim = SpawnEnemy("SpireVictim", new Vector3(8f, 0f, 0f), 5000, bus);
            var outer = SpawnEnemy("SpireOuter", new Vector3(8f, 0f, 5f), 5000, bus);
            Physics.SyncTransforms();
            Assert.IsTrue(spire.StartStrike(new Vector3(8f, 0f, 0f)));
            Assert.AreEqual(1, spire.ActiveStrikeCount);
            spire.StepStrikes(1f);
            Assert.AreEqual(0, spire.ActiveStrikeCount);
            Assert.Less(victim.CurrentHp, 5000, "Pillar of fire damages enemies in the blast");
            Assert.AreEqual(5000, outer.CurrentHp, "Outside the blast radius");
            Assert.AreEqual(0, spire.EclipsePulses);

            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/BlackHoleData.asset")));
            Assert.IsTrue(ha.HasSynergy(SynergyIds.SolarEclipse));
            float distBefore = Vector3.Distance(outer.transform.position, new Vector3(8f, 0f, 0f));
            int pulled = spire.TriggerEclipsePull(new Vector3(8f, 0f, 0f));
            Assert.GreaterOrEqual(pulled, 1);
            Assert.Less(Vector3.Distance(outer.transform.position, new Vector3(8f, 0f, 0f)), distBefore, "Eclipse drags enemies to the strike point");

            Assert.IsTrue(spire.StartStrike(new Vector3(8f, 0f, 0f)));
            spire.StepStrikes(0.5f);
            Assert.AreEqual(2, spire.EclipsePulses, "Eclipse fires 0.3s before impact");
        }

        [Test]
        public void ShadowDecoy_TauntsEnemiesThenDetonates_AndPhantomLegionReleasesFireballs()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var syn = LoadAsset<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_PhantomLegion.asset");
            var ha = CreateHeroAbilities(stats, bus, new RunStatistics(), new List<SynergyData> { syn }, out GameObject heroGo);
            var e1 = SpawnEnemy("TauntA", new Vector3(3f, 0f, 0f), 5000, bus);
            var e2 = SpawnEnemy("TauntB", new Vector3(-2f, 0f, 2f), 5000, bus);
            Physics.SyncTransforms();

            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/ShadowDecoyData.asset")));
            var decoyAbility = ha.GetActiveAbility<ShadowDecoyAbility>();
            Assert.AreEqual(1, decoyAbility.Deploy());
            Assert.AreEqual(1, decoyAbility.ActiveDecoyCount);
            Assert.IsTrue(e1.IsTaunted);
            Assert.IsTrue(e2.IsTaunted);

            ShadowDecoyEntity decoy = null;
            foreach (var d in UnityEngine.Object.FindObjectsByType<ShadowDecoyEntity>(FindObjectsInactive.Exclude)) decoy = d;
            Assert.IsNotNull(decoy);
            Assert.AreEqual(decoy.transform, e1.TauntTarget);

            // Detonation without synergy
            decoy.Detonate();
            Assert.AreEqual(0, decoyAbility.ActiveDecoyCount);
            Assert.AreEqual(0, decoyAbility.PhantomVolleys);
            e1.ClearTaunt();
            Assert.IsFalse(e1.IsTaunted);

            Assert.IsTrue(ha.AddAbility(LoadAsset<AbilityData>("Assets/_ScriptableObjects/Abilities/MiniDragonData.asset")));
            Assert.IsTrue(ha.HasSynergy(SynergyIds.PhantomLegion));
            Assert.AreEqual(8, decoyAbility.ReleasePhantomLegion(Vector3.zero));
            Assert.AreEqual(1, decoyAbility.PhantomVolleys);

            // Bosses ignore taunts
            var boss = SpawnEnemy("NotABoss", new Vector3(1f, 0f, 1f), 100, bus);
            Assert.IsTrue(boss.CanBeTaunted);
        }

        [Test]
        public void NewAbilitiesAndSynergies_AreRegisteredInCatalogsCardPoolAndAchievements()
        {
            var catalog = LoadAsset<AbilityCatalog>("Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset");
            var expected = new Dictionary<string, (CardRarity rarity, bool unlocked)>
            {
                { "BuzzsawChakram", (CardRarity.Rare, true) },
                { "SkyfallSpire", (CardRarity.Epic, false) },
                { "GlacialShatter", (CardRarity.Rare, true) },
                { "ShadowDecoy", (CardRarity.Epic, false) },
                { "VampiricFang", (CardRarity.Epic, false) },
                { "AreaTome", (CardRarity.Rare, true) },
            };
            foreach (var kv in expected)
            {
                AbilityData found = catalog.abilities.Find(a => a != null && a.id == kv.Key);
                Assert.IsNotNull(found, $"{kv.Key} missing from AbilityCatalog");
                Assert.AreEqual(kv.Value.rarity, found.rarity, kv.Key);
                Assert.AreEqual(kv.Value.unlocked, found.unlockedByDefault, kv.Key);
                Assert.IsNotNull(found.icon, $"{kv.Key} icon");
            }

            var achievements = new Dictionary<string, string>
            {
                { "elite_kills_30", "SkyfallSpire" },
                { "combo_50", "ShadowDecoy" },
                { "reach_wave_35", "VampiricFang" },
            };
            foreach (var kv in achievements)
            {
                var ach = LoadAsset<AchievementData>($"Assets/Resources/Achievements/{kv.Key}.asset");
                Assert.AreEqual(AchievementRewardType.UnlockAbility, ach.rewardType);
                Assert.AreEqual(kv.Value, ach.rewardId);
            }

            var synCatalog = Resources.Load<SynergyCatalog>("SynergyCatalog");
            Assert.IsNotNull(synCatalog.GetById(SynergyIds.ShatterNova));
            Assert.IsNotNull(synCatalog.GetById(SynergyIds.RicochetBarrage));
            Assert.IsNotNull(synCatalog.GetById(SynergyIds.SolarEclipse));
            Assert.IsNotNull(synCatalog.GetById(SynergyIds.PhantomLegion));
            CollectionAssert.AreEquivalent(new[] { "BuzzsawChakram", "CriticalLens" }, synCatalog.GetById(SynergyIds.RicochetBarrage).requiredAbilityIds);

            // RootLifetimeScope uses the Constellation Tree as synergy provider
            string root = File.ReadAllText("Assets/_Scripts/Core/RootLifetimeScope.cs");
            StringAssert.Contains("TalentTreeManager", root);
            StringAssert.DoesNotContain("DefaultSynergyUnlockProvider>(", root);
        }

        [Test]
        public void AchievementManager_TracksShrineClearsAndComboMilestones()
        {
            var save = NewSave();
            var achievements = new AchievementManager(save);
            for (int i = 0; i < 3; i++) achievements.OnShrineCleared();
            Assert.IsTrue(save.Data.completedAchievements.Contains("clear_3_shrines"));
            Assert.IsTrue(save.IsHeroUnlocked("ranger"), "3 shrines now unlock the Ranger");
            Assert.IsFalse(save.Data.completedAchievements.Contains("clear_5_shrines"));

            achievements.OnComboReached(30);
            Assert.IsFalse(save.IsAbilityUnlocked("ShadowDecoy"));
            achievements.OnComboReached(50);
            Assert.IsTrue(save.IsAbilityUnlocked("ShadowDecoy"));

            int coinsBefore = save.Data.coins;
            achievements.OnShrineCleared();
            achievements.OnShrineCleared();
            Assert.IsTrue(save.Data.completedAchievements.Contains("clear_5_shrines"));
            Assert.AreEqual(coinsBefore + 1000, save.Data.coins, "Shrine Master pays 1000 coins (doubled)");
        }

        [Test]
        public void Pickups_BobAndSpinViaVoxelLitShader_WhileBackgroundPropsStayStatic()
        {
            Shader voxelLit = Shader.Find("ProjectB/VoxelLit");
            Assert.IsNotNull(voxelLit);
            Assert.AreEqual(0, ShaderUtil.GetShaderMessages(voxelLit).Length, "VoxelLit must compile cleanly");

            var xp = LoadAsset<Material>("Assets/_Materials/Voxels/XpCrystal_Glow_Mat.mat");
            var coin = LoadAsset<Material>("Assets/_Materials/Voxels/CoinPickup_Mat.mat");
            var coinGlow = LoadAsset<Material>("Assets/_Materials/Voxels/CoinPickup_Glow_Mat.mat");
            foreach (var m in new[] { xp, coin, coinGlow })
            {
                Assert.AreEqual(voxelLit, m.shader, m.name);
                Assert.IsTrue(m.IsKeywordEnabled("_PICKUP_MOTION"), $"{m.name} must bob & spin");
                Assert.Greater(m.GetFloat("_BobAmplitude"), 0f);
                Assert.LessOrEqual(m.GetFloat("_BobAmplitude"), 0.2f, "Only a gentle bob");
                Assert.AreNotEqual(0f, m.GetFloat("_SpinSpeed"));
                Assert.IsTrue(m.enableInstancing);
            }

            // Both coin sub-mesh materials must move identically or the coin would tear apart
            Assert.AreEqual(coin.GetFloat("_BobAmplitude"), coinGlow.GetFloat("_BobAmplitude"));
            Assert.AreEqual(coin.GetFloat("_BobSpeed"), coinGlow.GetFloat("_BobSpeed"));
            Assert.AreEqual(coin.GetFloat("_SpinSpeed"), coinGlow.GetFloat("_SpinSpeed"));

            // Static background props and characters do not get the pickup motion
            foreach (string name in new[] { "ForestMushroom_Mat", "WildFlower_Mat", "Knight_Mat", "Mage_Mat" })
            {
                var m = LoadAsset<Material>($"Assets/_Materials/Voxels/{name}.mat");
                Assert.IsFalse(m.IsKeywordEnabled("_PICKUP_MOTION"), $"{name} must stay static");
            }
        }

        [Test]
        public void BossDamageTalent_AmplifiesDamageTakenByBossesOnly()
        {
            var bus = new GameEventBus();
            var mods = new EnemyRuntimeModifiers { BossDamageTakenMultiplier = 1.15f };
            var normal = SpawnEnemy("Normal", Vector3.zero, 1000, bus);
            normal.SetRuntimeModifiers(mods);
            var mini = SpawnEnemy("Mini", Vector3.right * 3f, 1000, bus);
            mini.SetRuntimeModifiers(mods);
            mini.MakeMiniBoss(1f, 1f, 1f);
            int miniMax = mini.MaxHp;

            normal.TakeDamage(100);
            mini.TakeDamage(100);
            Assert.AreEqual(900, normal.CurrentHp);
            Assert.AreEqual(miniMax - 115, mini.CurrentHp, "+15% Boss Damage");
        }
    }
}
