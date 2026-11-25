using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class Sprint8SynergiesAndDpsTests
    {
        private readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
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
            Time.timeScale = 1f;

            preExistingRoots.Clear();
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                foreach (GameObject root in activeScene.GetRootGameObjects())
                {
                    if (root != null) preExistingRoots.Add(root);
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) UnityEngine.Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();

            for (int i = disposables.Count - 1; i >= 0; i--)
            {
                disposables[i]?.Dispose();
            }
            disposables.Clear();

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                foreach (GameObject root in activeScene.GetRootGameObjects())
                {
                    if (root != null && !preExistingRoots.Contains(root))
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }
            preExistingRoots.Clear();
            Physics.SyncTransforms();

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
            if (obj != null) spawned.Add(obj);
            return obj;
        }

        private sealed class ToggleSynergyProvider : ISynergyUnlockProvider
        {
            public bool AllowAll;
            public bool IsSynergyUnlocked(string synergyId) => AllowAll && !string.IsNullOrEmpty(synergyId);
        }

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

        private HeroAbilities CreateHeroAbilities(
            HeroRuntimeStats stats,
            GameEventBus bus,
            RunStatistics runStats,
            ISynergyUnlockProvider unlockProvider = null,
            List<SynergyData> synergies = null,
            GameObject existingHeroGo = null)
        {
            GameObject go = existingHeroGo != null ? existingHeroGo : new GameObject("Sprint8Hero");
            var save = new SaveManager();
            save.AddBonusActiveSlots(3, false);
            save.AddBonusPassiveSlots(3, false);
            var slotService = new AbilitySlotService(save, null);
            disposables.Add(slotService);
            disposables.Add(save);

            var ha = go.AddComponent<HeroAbilities>();
            ha.Construct(stats, bus, null, null, slotService, runStats, unlockProvider);
            if (synergies != null)
            {
                ha.SetAvailableSynergies(synergies);
            }
            return ha;
        }

        [Test]
        public void ReactiveSynergies_RespectUnlockProvider_AutoActivateWithoutInflationOrCards()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var runStats = new RunStatistics();
            var provider = new ToggleSynergyProvider { AllowAll = false };

            int activatedEvents = 0;
            string activatedId = null;
            bus.Subscribe<SynergyActivatedEvent>(evt =>
            {
                activatedEvents++;
                activatedId = evt.SynergyId;
            });

            var syn = AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_ToxicInferno.asset");
            Assert.IsNotNull(syn);

            var heroGo = Track(new GameObject("UnlockHero"));
            var ha = CreateHeroAbilities(stats, bus, runStats, provider, new List<SynergyData> { syn }, heroGo);

            var poison = AssetDatabase.LoadAssetAtPath<PoisonFlaskAbilityData>("Assets/_ScriptableObjects/Abilities/PoisonFlaskData.asset");
            var fireball = AssetDatabase.LoadAssetAtPath<FireballAbilityData>("Assets/_ScriptableObjects/Abilities/FireballData.asset");
            Assert.IsTrue(ha.AddAbility(poison));
            Assert.IsTrue(ha.AddAbility(fireball));

            // Provider locked -> synergy does not activate
            Assert.IsFalse(ha.HasSynergy(SynergyIds.ToxicInferno));
            Assert.AreEqual(0, activatedEvents);

            // Unlock provider -> re-evaluates & auto-activates cleanly with zero level inflation
            provider.AllowAll = true;
            ha.SetSynergyUnlockProvider(provider);

            Assert.IsTrue(ha.HasSynergy(SynergyIds.ToxicInferno));
            Assert.AreEqual(1, activatedEvents);
            Assert.AreEqual(SynergyIds.ToxicInferno, activatedId);
            Assert.AreEqual(1, ha.GetAbilityLevel("PoisonAlchemist"));
            Assert.AreEqual(1, ha.GetAbilityLevel("Fireball"));
            Assert.AreEqual(1f, stats.GlobalDamageMultiplier, 0.001f);
        }

        [Test]
        public void ToxicInferno_FireballIgnitesPoisonPuddle_DealsDetonationAndDoubleTickDamage()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var runStats = new RunStatistics();

            var syn = AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_ToxicInferno.asset");
            var heroGo = Track(new GameObject("ToxicHero"));
            var ha = CreateHeroAbilities(stats, bus, runStats, new DefaultSynergyUnlockProvider(), new List<SynergyData> { syn }, heroGo);

            var poisonData = Track(ScriptableObject.CreateInstance<PoisonFlaskAbilityData>());
            poisonData.id = "PoisonAlchemist";
            poisonData.baseDamage = 10f;
            poisonData.puddleRadius = 3f;
            poisonData.puddleDuration = 6f;
            poisonData.tickInterval = 0.5f;
            poisonData.flaskCount = 1;
            poisonData.slowFactor = 0.8f;
            poisonData.targetLayer = 1 << 8;

            var fireData = Track(ScriptableObject.CreateInstance<FireballAbilityData>());
            fireData.id = "Fireball";
            fireData.baseDamage = 50f;
            fireData.explosionRadius = 2f;
            fireData.projectileSpeed = 12f;
            fireData.searchRadius = 10f;
            fireData.targetLayer = 1 << 8;

            Assert.IsTrue(ha.AddAbility(poisonData));
            Assert.IsTrue(ha.AddAbility(fireData));
            Assert.IsTrue(ha.HasSynergy(SynergyIds.ToxicInferno));

            var enemy = SpawnEnemy("NapalmTarget", new Vector3(3f, 0f, 0f), 1000, bus);
            Physics.SyncTransforms();

            var poisonAbility = ha.GetActiveAbility<PoisonFlaskAbility>();
            PoisonPuddle puddle = poisonAbility.SpawnPuddleAt(new Vector3(3f, 0f, 0f));
            Assert.IsNotNull(puddle);
            Assert.IsFalse(puddle.IsIgnited);
            Assert.AreEqual(990, enemy.CurrentHp); // initial 10 dmg tick

            // Explode fireball on puddle edge -> ignites puddle for 1.6x detonation (80) + 50 fireball = 130
            var projGo = Track(new GameObject("TestFireballProj"));
            projGo.transform.position = new Vector3(3.5f, 0f, 0f);
            var fbProj = projGo.AddComponent<FireballProjectile>();
            fbProj.InitializeFireball(50f, 12f, 2f, enemy.transform, 1 << 8, null, bus, stats, ha, runStats, "Fireball", 1f);
            fbProj.DetonateAtCurrentPosition();

            Assert.IsTrue(puddle.IsIgnited);
            Assert.AreEqual(860, enemy.CurrentHp, "Expected 50 fireball + 80 (1.6x) Toxic Inferno detonation");
            Assert.AreEqual(80L, runStats.GetDamageBySource(SynergyIds.ToxicInferno));
            Assert.AreEqual(50L, runStats.GetDamageBySource("Fireball"));

            // Subsequent puddle tick deals 2x (20 dmg)
            puddle.ApplyTick();
            Assert.AreEqual(840, enemy.CurrentHp, "Ignited napalm puddle must deal 2x tick damage");
        }

        [Test]
        public void SteamBlast_FireballOnSlowedEnemy_TriggersNovaAndKnockback_AndSkipsUnslowed()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var runStats = new RunStatistics();
            int steamVfx = 0;
            bus.Subscribe<SynergyReactionVfxEvent>(e => { if (e.ReactionType == SynergyReactionVfxType.SteamBlastNova) steamVfx++; });

            var syn = AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_SteamBlast.asset");
            var heroGo = Track(new GameObject("SteamHero"));
            var ha = CreateHeroAbilities(stats, bus, runStats, new DefaultSynergyUnlockProvider(), new List<SynergyData> { syn }, heroGo);

            var fireData = Track(ScriptableObject.CreateInstance<FireballAbilityData>());
            fireData.id = "Fireball";
            fireData.baseDamage = 40f;
            fireData.explosionRadius = 1.5f;
            fireData.targetLayer = 1 << 8;

            var iceData = Track(ScriptableObject.CreateInstance<IceAuraAbilityData>());
            iceData.id = "IceAura";
            iceData.baseDamage = 5f;
            iceData.auraRadius = 4f;
            iceData.slowFactor = 0.6f;
            iceData.slowDuration = 2f;
            iceData.targetLayer = 1 << 8;

            Assert.IsTrue(ha.AddAbility(fireData));
            Assert.IsTrue(ha.AddAbility(iceData));
            Assert.IsTrue(ha.HasSynergy(SynergyIds.SteamBlast));

            // 1. Unslowed target -> no Steam Blast
            var unslowed = SpawnEnemy("Unslowed", new Vector3(20f, 0f, 0f), 1000, bus);
            Physics.SyncTransforms();

            var fb1 = Track(new GameObject("FB1")).AddComponent<FireballProjectile>();
            fb1.transform.position = new Vector3(20f, 0f, 0f);
            fb1.InitializeFireball(40f, 12f, 1.5f, unslowed.transform, 1 << 8, null, bus, stats, ha, runStats, "Fireball", 1f);
            fb1.DetonateAtCurrentPosition();
            Assert.AreEqual(0, steamVfx);
            Assert.AreEqual(0L, runStats.GetDamageBySource(SynergyIds.SteamBlast));

            // 2. Slowed primary target + neighbor at 3.2m (outside 1.5m fireball, inside 4.2m steam nova)
            var slowed = SpawnEnemy("Slowed", new Vector3(0f, 0f, 0f), 1000, bus);
            slowed.ApplySlow(0.5f, 3f);
            var neighbor = SpawnEnemy("SteamNeighbor", new Vector3(3.2f, 0f, 0f), 1000, bus);
            Physics.SyncTransforms();

            var fb2 = Track(new GameObject("FB2")).AddComponent<FireballProjectile>();
            fb2.transform.position = Vector3.zero;
            fb2.InitializeFireball(40f, 12f, 1.5f, slowed.transform, 1 << 8, null, bus, stats, ha, runStats, "Fireball", 1f);
            fb2.DetonateAtCurrentPosition();

            Assert.AreEqual(1, steamVfx);
            Assert.AreEqual(888, slowed.CurrentHp, "40 fireball + 72 (1.8x) steam nova = 112 dmg");
            Assert.AreEqual(928, neighbor.CurrentHp, "Neighbor within 4.2m takes 72 (1.8x) steam nova dmg");
            Assert.Greater(neighbor.transform.position.x, 4.5f, "Steam nova must knock enemies back");
            Assert.AreEqual(144L, runStats.GetDamageBySource(SynergyIds.SteamBlast));
        }

        [Test]
        public void Thunderstorm_ChainLightningOnSlowedTarget_DeepFreezesAndLaunchesTwoIceArcs()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var runStats = new RunStatistics();

            var syn = AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_Thunderstorm.asset");
            var heroGo = Track(new GameObject("StormHero"));
            heroGo.transform.position = Vector3.zero;
            var ha = CreateHeroAbilities(stats, bus, runStats, new DefaultSynergyUnlockProvider(), new List<SynergyData> { syn }, heroGo);

            var chainData = Track(ScriptableObject.CreateInstance<ChainLightningAbilityData>());
            chainData.id = "ChainLightning";
            chainData.baseDamage = 40f;
            chainData.bounceCount = 0; // Only hit primary target so neighbors prove Thunderstorm arcs!
            chainData.searchRadius = 10f;
            chainData.bounceRadius = 5f;
            chainData.targetLayer = 1 << 8;

            var iceData = Track(ScriptableObject.CreateInstance<IceAuraAbilityData>());
            iceData.id = "IceAura";
            iceData.targetLayer = 1 << 8;

            Assert.IsTrue(ha.AddAbility(chainData));
            Assert.IsTrue(ha.AddAbility(iceData));
            Assert.IsTrue(ha.HasSynergy(SynergyIds.Thunderstorm));

            var primary = SpawnEnemy("SlowedPrimary", new Vector3(2f, 0f, 0f), 1000, bus);
            primary.ApplySlow(0.6f, 2f);
            var n1 = SpawnEnemy("IceArc1", new Vector3(4f, 0f, 0f), 1000, bus);
            var n2 = SpawnEnemy("IceArc2", new Vector3(4.5f, 0f, 1.5f), 1000, bus);
            Physics.SyncTransforms();

            var chain = ha.GetActiveAbility<ChainLightningAbility>();
            int chainCount = chain.TriggerChainCast();

            Assert.AreEqual(1, chainCount);
            Assert.AreEqual(960, primary.CurrentHp);
            Assert.AreEqual(0.10f, primary.CurrentSlowFactor, 0.001f, "Thunderstorm must apply 90% deep freeze (factor 0.10)");
            Assert.AreEqual(970, n1.CurrentHp, "Neighbor 1 takes 0.75x (30) icy discharge");
            Assert.AreEqual(970, n2.CurrentHp, "Neighbor 2 takes 0.75x (30) icy discharge");
            Assert.AreEqual(60L, runStats.GetDamageBySource(SynergyIds.Thunderstorm));
        }

        [Test]
        public void OverchargedGrid_FormsLaserFenceBetweenTurrets_AndOverchargesShooterProjectiles()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var runStats = new RunStatistics();

            var syn = AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_OverchargedGrid.asset");
            var heroGo = Track(new GameObject("GridHero"));
            heroGo.transform.position = Vector3.zero;
            var ha = CreateHeroAbilities(stats, bus, runStats, new DefaultSynergyUnlockProvider(), new List<SynergyData> { syn }, heroGo);

            var turretData = Track(ScriptableObject.CreateInstance<LaserTurretAbilityData>());
            turretData.id = "LaserTurret";
            turretData.dps = 40f;
            turretData.baseCooldown = 10f;
            turretData.searchRadius = 10f;
            turretData.turretLifetime = 10f;
            turretData.targetLayer = 1 << 8;

            var shooterData = Track(ScriptableObject.CreateInstance<ShooterAbilityData>());
            shooterData.id = "Shooter";
            shooterData.baseDamage = 25f;
            shooterData.targetLayer = 1 << 8;

            Assert.IsTrue(ha.AddAbility(turretData));
            Assert.IsTrue(ha.AddAbility(shooterData));
            Assert.IsTrue(ha.HasSynergy(SynergyIds.OverchargedGrid));

            var turretAbility = ha.GetActiveAbility<LaserTurretAbility>();
            LaserTurret t1 = turretAbility.SpawnTurretAt(new Vector3(0f, 0f, 0f));
            LaserTurret t2 = turretAbility.SpawnTurretAt(new Vector3(8f, 0f, 0f));
            Assert.IsNotNull(t1);
            Assert.IsNotNull(t2);

            var fenceVictim = SpawnEnemy("FenceVictim", new Vector3(4f, 0f, 0f), 1000, bus);
            Physics.SyncTransforms();

            int hitCount = turretAbility.TickOverchargedGrid(forceDamageTick: true);
            Assert.AreEqual(1, turretAbility.ActiveFenceLinkCount);
            Assert.AreEqual(1, hitCount);
            Assert.Less(fenceVictim.CurrentHp, 1000);
            Assert.Greater(runStats.GetDamageBySource(SynergyIds.OverchargedGrid), 0L);

            // Shooter projectile passing near turret at (0,0,0) gets overcharged (+2 pierce)
            var e1 = SpawnEnemy("Pierce1", new Vector3(3f, 0f, 0f), 1000, bus);
            var e2 = SpawnEnemy("Pierce2", new Vector3(5f, 0f, 0f), 1000, bus);
            Physics.SyncTransforms();

            var projGo = Track(new GameObject("OverchargedShooterProj"));
            projGo.transform.position = new Vector3(0.5f, 0f, 0f);
            var proj = projGo.AddComponent<AbilityProjectile>();
            proj.Initialize(25f, 20f, e1.transform, 1 << 8, null, stats);
            proj.ConfigureCombatContext(runStats, "Shooter", canOverchargeFromTurrets: true, visualScale: 1f);

            for (int step = 0; step < 12; step++)
            {
                proj.StepSimulation(0.03f);
            }

            Assert.IsTrue(proj.IsOvercharged, "Projectile near LaserTurret must overcharge");
            Assert.AreEqual(975, e1.CurrentHp, "First target struck by Shooter");
            Assert.AreEqual(975, e2.CurrentHp, "Overcharged projectile must pierce to second enemy");
        }

        [Test]
        public void RemainingFourSynergies_BladeStorm_AegisPhalanx_DragonLord_CataclysmVortex_TriggerReactions()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var runStats = new RunStatistics();

            var synergies = new List<SynergyData>
            {
                AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_BladeStorm.asset"),
                AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_AegisPhalanx.asset"),
                AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_DragonLord.asset"),
                AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_CataclysmVortex.asset")
            };

            // 1. Blade Storm + Aegis Phalanx
            var hero1Go = Track(new GameObject("BladeAegisHero"));
            hero1Go.transform.position = Vector3.zero;
            var ha1 = CreateHeroAbilities(stats, bus, runStats, new DefaultSynergyUnlockProvider(), synergies, hero1Go);

            var orbitalData = Track(ScriptableObject.CreateInstance<OrbitalCompanionAbilityData>());
            orbitalData.id = "orbital_small_sword";
            orbitalData.baseDamage = 20f;
            orbitalData.count = 2;
            orbitalData.orbitDistance = 2f;
            orbitalData.baseArea = 1f;
            orbitalData.targetLayer = 1 << 8;

            var boomData = Track(ScriptableObject.CreateInstance<BoomerangAbilityData>());
            boomData.id = "Boomerang";
            boomData.baseDamage = 20f;
            boomData.pierceCount = 4;
            boomData.targetLayer = 1 << 8;

            var shieldData = Track(ScriptableObject.CreateInstance<GuardianShieldAbilityData>());
            shieldData.id = "GuardianShield";
            shieldData.baseDamage = 20f;
            shieldData.shieldCount = 1;
            shieldData.orbitRadius = 2f;
            shieldData.knockbackDistance = 1.5f;
            shieldData.shieldHitRadius = 1.4f;
            shieldData.targetLayer = 1 << 8;

            Assert.IsTrue(ha1.AddAbility(orbitalData));
            Assert.IsTrue(ha1.AddAbility(boomData));
            Assert.IsTrue(ha1.AddAbility(shieldData));
            Assert.IsTrue(ha1.HasSynergy(SynergyIds.BladeStorm));
            Assert.IsTrue(ha1.HasSynergy(SynergyIds.AegisPhalanx));

            var phalanxPrimary = SpawnEnemy("PhalanxPrimary", new Vector3(0f, 0f, 2f), 1000, bus);
            var phalanxNeighbor = SpawnEnemy("PhalanxNeighbor", new Vector3(0.8f, 0f, 2f), 1000, bus);
            var bladeTarget = SpawnEnemy("BladeTarget", new Vector3(0f, 0f, 3.5f), 1000, bus);
            Physics.SyncTransforms();

            var orbital = ha1.GetActiveAbility<OrbitalCompanionAbility>();
            Assert.AreEqual(2, orbital.FireBladeStormVolley(), "BladeStorm volley spawns 1 phantom blade per orbital sword");

            var blades = UnityEngine.Object.FindObjectsByType<PhantomBladeProjectile>(FindObjectsSortMode.None);
            for (int step = 0; step < 10; step++)
            {
                for (int b = 0; b < blades.Length; b++)
                {
                    if (blades[b] != null && blades[b].gameObject.activeInHierarchy)
                    {
                        blades[b].StepSimulation(0.03f);
                    }
                }
            }
            Assert.Greater(runStats.GetDamageBySource(SynergyIds.BladeStorm), 0L, "Phantom blades must damage enemies under blade_storm source");

            var shield = ha1.GetActiveAbility<GuardianShieldAbility>();
            shield.ApplyShieldScan();
            Assert.Less(phalanxNeighbor.CurrentHp, 1000, "Aegis Phalanx shockwave damages nearby enemies");
            Assert.Greater(runStats.GetDamageBySource(SynergyIds.AegisPhalanx), 0L);

            // 2. Dragon Lord (pierce 3 + burn zone) & Cataclysm Vortex (6 gravity shards)
            var hero2Go = Track(new GameObject("DragonCataclysmHero"));
            hero2Go.transform.position = new Vector3(30f, 0f, 0f);
            var ha2 = CreateHeroAbilities(stats, bus, runStats, new DefaultSynergyUnlockProvider(), synergies, hero2Go);

            var dragonData = Track(ScriptableObject.CreateInstance<MiniDragonAbilityData>());
            dragonData.id = "MiniDragon";
            dragonData.baseDamage = 30f;
            dragonData.dragonCount = 1;
            dragonData.coneProjectileCount = 1;
            dragonData.projectileSpeed = 20f;
            dragonData.searchRadius = 12f;
            dragonData.targetLayer = 1 << 8;

            var furyData = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            furyData.id = "BattleFury";
            furyData.effectType = PassiveEffectType.DamagePercent;
            furyData.baseValue = 15f;

            var blackHoleData = Track(ScriptableObject.CreateInstance<BlackHoleAbilityData>());
            blackHoleData.id = "BlackHole";
            blackHoleData.pullRadius = 5f;
            blackHoleData.targetLayer = 1 << 8;

            var tornadoData = Track(ScriptableObject.CreateInstance<VoxelTornadoAbilityData>());
            tornadoData.id = "VoxelTornado";
            tornadoData.baseDamage = 20f;
            tornadoData.vortexRadius = 3f;
            tornadoData.duration = 4f;
            tornadoData.tornadoCount = 1;
            tornadoData.targetLayer = 1 << 8;

            Assert.IsTrue(ha2.AddAbility(dragonData));
            Assert.IsTrue(ha2.AddAbility(furyData));
            Assert.IsTrue(ha2.AddAbility(blackHoleData));
            Assert.IsTrue(ha2.AddAbility(tornadoData));
            Assert.IsTrue(ha2.HasSynergy(SynergyIds.DragonLord));
            Assert.IsTrue(ha2.HasSynergy(SynergyIds.CataclysmVortex));

            var d1 = SpawnEnemy("DragonLine1", new Vector3(33f, 0f, 0f), 1000, bus);
            var d2 = SpawnEnemy("DragonLine2", new Vector3(35f, 0f, 0f), 1000, bus);
            var d3 = SpawnEnemy("DragonLine3", new Vector3(37f, 0f, 0f), 1000, bus);
            Physics.SyncTransforms();

            var dragonAbility = ha2.GetActiveAbility<MiniDragonAbility>();
            Assert.AreEqual(1, dragonAbility.FireAllDragons());
            var flames = UnityEngine.Object.FindObjectsByType<DragonFlameProjectile>(FindObjectsSortMode.None);
            Assert.Greater(flames.Length, 0);
            for (int step = 0; step < 20; step++)
            {
                for (int f = 0; f < flames.Length; f++)
                {
                    if (flames[f] != null && flames[f].gameObject.activeInHierarchy)
                    {
                        flames[f].StepSimulation(0.03f);
                    }
                }
            }
            Assert.Less(d1.CurrentHp, 1000);
            Assert.Less(d2.CurrentHp, 1000, "Dragon Lord flame must pierce 2nd target");
            Assert.Less(d3.CurrentHp, 1000, "Dragon Lord flame must pierce 3rd target");
            Assert.Greater(runStats.GetDamageBySource(SynergyIds.DragonLord), 0L);

            // Cataclysm Vortex destabilize spawns 6 gravity shards
            var tornadoAbility = ha2.GetActiveAbility<VoxelTornadoAbility>();
            Assert.AreEqual(1, tornadoAbility.SpawnTornadoes());
            Assert.IsTrue(tornadoAbility.LastSpawnedTornado.TriggerDestabilize());
            var shards = UnityEngine.Object.FindObjectsByType<GravityShardProjectile>(FindObjectsSortMode.None);
            int activeShards = 0;
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] != null && shards[i].gameObject.activeInHierarchy) activeShards++;
            }
            Assert.AreEqual(6, activeShards, "Cataclysm Vortex must spawn 6 gravity shards");
        }

        [Test]
        public void Overdrive_TriggersEveryTenLevelsOnly_Lv10_Lv20_Lv30_AndNotAtLv5()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var runStats = new RunStatistics();
            var overdriveEvents = new List<AbilityOverdriveEvent>();
            bus.Subscribe<AbilityOverdriveEvent>(e => overdriveEvents.Add(e));

            var heroGo = Track(new GameObject("OverdriveHero"));
            var ha = CreateHeroAbilities(stats, bus, runStats, null, null, heroGo);
            // Overdrive tiers past Lv 5 need the Constellation Path of Mastery; emulate Mythic Ascension (Lv 50 cap).
            ha.SetAbilityLevelCapOverride(50);

            var fireData = Track(ScriptableObject.CreateInstance<FireballAbilityData>());
            fireData.id = "Fireball";
            fireData.abilityName = "Fireball";
            fireData.type = AbilityType.Active;
            fireData.baseDamage = 20f;
            fireData.projectileCount = 1;
            fireData.explosionRadius = 2.0f;
            fireData.projectileSpeed = 10f;
            fireData.targetLayer = 1 << 8;

            Assert.IsTrue(ha.AddAbility(fireData));
            var fireAbility = ha.GetActiveAbility<FireballAbility>();
            Assert.IsNotNull(fireAbility);

            var modCard = Track(ScriptableObject.CreateInstance<AbilityModifierCardData>());
            modCard.targetAbilityId = "Fireball";
            modCard.modifierType = ModifierType.Damage;
            modCard.value = 1f;

            // Level 1 -> Level 4 -> Level 5: MUST NOT trigger Overdrive!
            for (int i = 0; i < 3; i++) ha.UpgradeAbility("Fireball", ModifierType.Damage, 1f);
            Assert.AreEqual(4, fireAbility.Level);
            Assert.AreEqual("Lv 4 -> Lv 5", CardSelectionUI.FormatLevelBadge(modCard, ha, out _));

            ha.UpgradeAbility("Fireball", ModifierType.Damage, 1f);
            Assert.AreEqual(5, fireAbility.Level);
            Assert.AreEqual(0, fireAbility.OverdriveTier, "Level 5 must NOT trigger Overdrive");
            Assert.AreEqual(1f, fireAbility.VisualScaleMultiplier, 0.001f);
            Assert.AreEqual(0, overdriveEvents.Count);

            // Level 5 -> Level 9: still Tier 0, badge at Lv 9 predicts Lv 10 OVERDRIVE!
            for (int i = 0; i < 4; i++) ha.UpgradeAbility("Fireball", ModifierType.Damage, 1f);
            Assert.AreEqual(9, fireAbility.Level);
            Assert.AreEqual(0, fireAbility.OverdriveTier);
            Assert.AreEqual("Lv 9 -> Lv 10 OVERDRIVE!", CardSelectionUI.FormatLevelBadge(modCard, ha, out _));

            // Level 10 -> Tier 1 OVERDRIVE!
            ha.UpgradeAbility("Fireball", ModifierType.Damage, 1f);
            Assert.AreEqual(10, fireAbility.Level);
            Assert.AreEqual(1, fireAbility.OverdriveTier);
            Assert.AreEqual(1.15f, fireAbility.VisualScaleMultiplier, 0.001f);
            Assert.AreEqual(1, overdriveEvents.Count);
            Assert.AreEqual("OVERDRIVE!", overdriveEvents[0].TierLabel);

            // Level 15 -> stays Tier 1
            for (int i = 0; i < 5; i++) ha.UpgradeAbility("Fireball", ModifierType.Damage, 1f);
            Assert.AreEqual(15, fireAbility.Level);
            Assert.AreEqual(1, fireAbility.OverdriveTier);
            Assert.AreEqual(1, overdriveEvents.Count);

            // Level 20 -> Tier 2 AWAKENED!
            for (int i = 0; i < 5; i++) ha.UpgradeAbility("Fireball", ModifierType.Damage, 1f);
            Assert.AreEqual(20, fireAbility.Level);
            Assert.AreEqual(2, fireAbility.OverdriveTier);
            Assert.AreEqual(1.25f, fireAbility.VisualScaleMultiplier, 0.001f);
            Assert.AreEqual(2, overdriveEvents.Count);
            Assert.AreEqual("AWAKENED!", overdriveEvents[1].TierLabel);

            // Level 30 -> Tier 3 MYTHIC! (+25% OverdriveDamageMultiplier)
            for (int i = 0; i < 10; i++) ha.UpgradeAbility("Fireball", ModifierType.Damage, 1f);
            Assert.AreEqual(30, fireAbility.Level);
            Assert.AreEqual(3, fireAbility.OverdriveTier);
            Assert.AreEqual(1.25f, fireAbility.OverdriveDamageMultiplier, 0.001f);
            Assert.AreEqual(3, overdriveEvents.Count);
            Assert.AreEqual("MYTHIC!", overdriveEvents[2].TierLabel);
        }

        [Test]
        public void RunComboTracker_TracksMilestonesScalesXpAndCoins_ExpiresAfter3s_AndAllocatesZeroGc()
        {
            var bus = new GameEventBus();
            var runStats = new RunStatistics();
            var combo = new RunComboTracker(bus, runStats);

            int milestoneEvents = 0;
            bus.Subscribe<ComboMilestoneEvent>(_ => milestoneEvents++);

            var heroGo = Track(new GameObject("ComboHero"));
            var stats = new HeroRuntimeStats();
            var exp = heroGo.AddComponent<HeroExperience>();
            exp.Construct(stats, bus, combo);
            exp.EnsureStatsInitialized();

            var econ = heroGo.AddComponent<HeroEconomy>();
            econ.Construct(runStats, bus, stats, combo);

            // Warmup once
            combo.RegisterKill();
            combo.Tick(0.016f);

            long gc0 = GC.GetAllocatedBytesForCurrentThread();
            combo.RegisterKill();
            combo.Tick(0.016f);
            long gc1 = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, gc1 - gc0, "RunComboTracker steady-state RegisterKill/Tick must allocate 0 bytes");

            // Reach 15 (+10%), 30 (+15%), 50 (+20%), 100 (+30%)
            for (int i = combo.CurrentCombo; i < 15; i++) combo.RegisterKill();
            Assert.AreEqual(1, combo.CurrentMilestoneTier);
            Assert.AreEqual(10f, combo.RewardBonusPercent, 0.001f);

            for (int i = combo.CurrentCombo; i < 30; i++) combo.RegisterKill();
            Assert.AreEqual(2, combo.CurrentMilestoneTier);
            Assert.AreEqual(15f, combo.RewardBonusPercent, 0.001f);

            for (int i = combo.CurrentCombo; i < 50; i++) combo.RegisterKill();
            Assert.AreEqual(3, combo.CurrentMilestoneTier);
            Assert.AreEqual(20f, combo.RewardBonusPercent, 0.001f);

            for (int i = combo.CurrentCombo; i < 100; i++) combo.RegisterKill();
            Assert.AreEqual(4, combo.CurrentMilestoneTier);
            Assert.AreEqual(30f, combo.RewardBonusPercent, 0.001f);
            Assert.AreEqual(4, milestoneEvents);
            Assert.AreEqual(100, runStats.BestCombo);

            // XP and Coin bonus scaling (+30%)
            econ.AddCoin(100);
            Assert.AreEqual(130, runStats.CoinsEarned);
            exp.AddExperience(100);
            Assert.GreaterOrEqual(exp.CurrentLevel, 4, "130 XP should level hero further than 100 base XP");

            // Expiry after 3.0s
            combo.Tick(3.05f);
            Assert.AreEqual(0, combo.CurrentCombo);
            Assert.AreEqual(0, combo.CurrentMilestoneTier);
            Assert.AreEqual(100, combo.BestCombo);
        }

        [Test]
        public void RunDamageTracker_ZeroGc_AndGameOverBreakdownAndSceneWiring_PassValidation()
        {
            var runStats = new RunStatistics();
            runStats.UpdatePlayTime(60f);

            // Warmup sources
            runStats.RecordDamage(DamageSourceIds.Weapon, 100);
            runStats.RecordDamage(DamageSourceIds.Ultimate, 100);
            runStats.RecordDamage("Fireball", 100);
            runStats.RecordDamage(SynergyIds.ToxicInferno, 100);
            runStats.RecordDamage(DamageSourceIds.Barrel, 100);

            long gc0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 25; i++)
            {
                runStats.RecordDamage(DamageSourceIds.Weapon, 20);
                runStats.RecordDamage("Fireball", 40);
                runStats.RecordDamage(SynergyIds.ToxicInferno, 60);
            }
            long gc1 = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, gc1 - gc0, "RunDamageTracker.RecordDamage must allocate 0 bytes in steady state");

            var results = new List<DamageSourceEntry>(8);
            runStats.GetTopDamageSources(results, 6);
            Assert.AreEqual(5, results.Count);
            Assert.AreEqual(SynergyIds.ToxicInferno, results[0].SourceId);
            Assert.AreEqual(1600L, results[0].TotalDamage);
            Assert.AreEqual(1600f / 60f, results[0].Dps, 0.05f);

            float totalShare = 0f;
            for (int i = 0; i < results.Count; i++) totalShare += results[i].Share;
            Assert.AreEqual(1f, totalShare, 0.001f);

            // Human-readable RewardDisplayFormatter / GameOverUI.FormatReward
            var laserAch = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/reach_wave_10.asset");
            var rangerAch = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/clear_3_shrines.asset");
            var dragonAch = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/collect_5_abilities.asset");

            Assert.AreEqual("Unlocked: Laser Turret", GameOverUI.FormatReward(laserAch));
            Assert.AreEqual("Hero: Ranger", GameOverUI.FormatReward(rangerAch));
            Assert.AreEqual("Unlocked: Dragon Whelp", GameOverUI.FormatReward(dragonAch));

            // SynergyCatalog
            var synCatalog = Resources.Load<SynergyCatalog>("SynergyCatalog");
            Assert.IsNotNull(synCatalog, "Resources/SynergyCatalog.asset must exist");
            Assert.GreaterOrEqual(synCatalog.synergies.Count, 8, "8 Sprint 8 reactions (+4 added in Sprint 11)");
            for (int i = 0; i < synCatalog.synergies.Count; i++)
            {
                SynergyData syn = synCatalog.synergies[i];
                Assert.IsNotNull(syn);
                Assert.IsFalse(string.IsNullOrEmpty(syn.description));
            }

            // Gameplay.unity & MainMenu.unity wiring
            Scene gp = EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive);
            try
            {
                ComboHudUI comboHud = null;
                GameOverUI gameOverUi = null;
                foreach (var root in gp.GetRootGameObjects())
                {
                    if (comboHud == null) comboHud = root.GetComponentInChildren<ComboHudUI>(true);
                    if (gameOverUi == null) gameOverUi = root.GetComponentInChildren<GameOverUI>(true);
                }

                Assert.IsNotNull(comboHud, "Gameplay.unity must have ComboHudUI");
                Assert.IsNotNull(gameOverUi, "Gameplay.unity must have GameOverUI");

                var goSo = new SerializedObject(gameOverUi);
                Assert.IsNotNull(goSo.FindProperty("damageBreakdownSection").objectReferenceValue);
                Assert.IsNotNull(goSo.FindProperty("damageBreakdownContainer").objectReferenceValue);
                Assert.IsNotNull(goSo.FindProperty("abilityCatalog").objectReferenceValue);
                Assert.IsNotNull(goSo.FindProperty("synergyCatalog").objectReferenceValue);
            }
            finally
            {
                EditorSceneManager.CloseScene(gp, true);
            }

            Scene mm = EditorSceneManager.OpenScene("Assets/_Scenes/MainMenu.unity", OpenSceneMode.Additive);
            try
            {
                AbilitiesScreenUI abScreen = null;
                foreach (var root in mm.GetRootGameObjects())
                {
                    abScreen = root.GetComponentInChildren<AbilitiesScreenUI>(true);
                    if (abScreen != null) break;
                }
                Assert.IsNotNull(abScreen, "MainMenu.unity must have AbilitiesScreenUI");
                var abSo = new SerializedObject(abScreen);
                Assert.IsNotNull(abSo.FindProperty("synergyCatalog").objectReferenceValue, "AbilitiesScreenUI.synergyCatalog must be wired");
            }
            finally
            {
                EditorSceneManager.CloseScene(mm, true);
            }
        }
    }
}
