using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;

namespace ProjectB.Tests.EditMode
{
    /// <summary>Knight melee sweep: offset circle in the facing direction, Radius weapon cards.</summary>
    public class KnightMeleeSweepTests
    {
        private const string KnightWeaponPath = "Assets/_ScriptableObjects/Weapons/KnightWeaponData.asset";
        private const string RadiusCardPath = "Assets/_ScriptableObjects/Cards/WeaponMod_Radius.asset";

        private readonly List<Object> created = new List<Object>();
        private readonly HashSet<GameObject> preExistingRoots = new HashSet<GameObject>();

        private static LayerMask EnemyMask => 1 << 8;

        [SetUp]
        public void SetUp()
        {
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
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            }
            created.Clear();

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded)
            {
                foreach (GameObject root in active.GetRootGameObjects())
                {
                    if (root != null && !preExistingRoots.Contains(root)) Object.DestroyImmediate(root);
                }
            }
            Physics.SyncTransforms();
        }

        private T Track<T>(T obj) where T : Object
        {
            if (obj != null) created.Add(obj);
            return obj;
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
            go.AddComponent<BoxCollider>().size = Vector3.one * 0.5f;
            var enemy = go.AddComponent<EnemyBase>();
            enemy.Initialize(data, null, null, bus, 1f);
            return enemy;
        }

        private WeaponData MeleeWeapon(float radius = 2f, float offsetRatio = 0.5f)
        {
            var weapon = Track(ScriptableObject.CreateInstance<WeaponData>());
            weapon.attackMode = WeaponAttackMode.MeleeSweep;
            weapon.damage = 10;
            weapon.attackCooldown = 0.5f;
            weapon.meleeRadius = radius;
            weapon.meleeForwardOffsetRatio = offsetRatio;
            return weapon;
        }

        private (HeroCombat combat, WeaponController controller, Transform facing) CreateHero(WeaponData weapon)
        {
            var go = Track(new GameObject("KnightHero"));
            var facing = new GameObject("Visuals").transform;
            facing.SetParent(go.transform, false);

            var combat = go.AddComponent<HeroCombat>();
            var so = new SerializedObject(combat);
            so.FindProperty("enemyLayer").intValue = EnemyMask;
            so.ApplyModifiedPropertiesWithoutUndo();
            combat.Construct(null, new HeroRuntimeStats(), null, null);
            combat.EnsureInitialized();
            combat.AddWeapon(weapon);

            var controller = combat.ActiveWeapons[combat.ActiveWeapons.Count - 1];
            typeof(WeaponController).GetField("facingTransform", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, facing);
            return (combat, controller, facing);
        }

        [Test]
        public void KnightWeaponAsset_IsMeleeSweep_WithWindSlashVfx()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(KnightWeaponPath);
            Assert.IsNotNull(weapon);
            Assert.AreEqual(WeaponAttackMode.MeleeSweep, weapon.attackMode);
            Assert.Greater(weapon.meleeRadius, 0.5f);
            Assert.Greater(weapon.meleeForwardOffsetRatio, 0f, "Sweep centre must be shifted toward the facing");
            Assert.IsNotNull(weapon.meleeVfxPrefab);
            Assert.IsNotNull(weapon.meleeVfxPrefab.GetComponent<MeleeSweepVfx>());
        }

        [Test]
        public void Sweep_CentreIsShiftedAlongFacing()
        {
            var (_, controller, facing) = CreateHero(MeleeWeapon(2f, 0.5f));
            facing.rotation = Quaternion.LookRotation(Vector3.right);

            Vector3 center = controller.MeleeSweepCenter;
            Assert.AreEqual(1f, center.x, 0.001f, "Offset = radius * ratio along facing");
            Assert.AreEqual(0f, center.z, 0.001f);
        }

        [Test]
        public void Sweep_HitsAllEnemiesInFront_AndMissesEnemiesBehind()
        {
            var bus = new GameEventBus();
            var front1 = SpawnEnemy("Front1", new Vector3(0f, 0f, 2f), 100, bus);
            var front2 = SpawnEnemy("Front2", new Vector3(1f, 0f, 1.5f), 100, bus);
            var behind = SpawnEnemy("Behind", new Vector3(0f, 0f, -2f), 100, bus);
            Physics.SyncTransforms();

            var (_, controller, facing) = CreateHero(MeleeWeapon(2f, 0.5f));
            facing.rotation = Quaternion.LookRotation(Vector3.forward);

            Assert.AreEqual(2, controller.Sweep(), "Both enemies in front are hit by one swing");
            Assert.Less(front1.CurrentHp, 100);
            Assert.Less(front2.CurrentHp, 100);
            Assert.AreEqual(100, behind.CurrentHp, "Enemy behind the hero is outside the offset circle");

            facing.rotation = Quaternion.LookRotation(Vector3.back);
            Assert.AreEqual(1, controller.Sweep(), "Turning around moves the sweep circle");
            Assert.Less(behind.CurrentHp, 100);
        }

        [Test]
        public void Sweep_IsAFlatCircle_HitsHighFlyersNearTheRim()
        {
            var bus = new GameEventBus();
            // Planar distance to the sweep centre is 1.8 (< radius 2), but it hovers 3m above the hero.
            var flyer = SpawnEnemy("Flyer", new Vector3(0f, 3f, 2.8f), 100, bus);
            Physics.SyncTransforms();

            var (_, controller, facing) = CreateHero(MeleeWeapon(2f, 0.5f));
            facing.rotation = Quaternion.LookRotation(Vector3.forward);

            Assert.AreEqual(1, controller.Sweep());
            Assert.Less(flyer.CurrentHp, 100);
        }

        [Test]
        public void Sweep_WithNoEnemyInRange_DoesNothing()
        {
            var bus = new GameEventBus();
            var far = SpawnEnemy("Far", new Vector3(0f, 0f, 10f), 100, bus);
            Physics.SyncTransforms();

            var (_, controller, _) = CreateHero(MeleeWeapon(2f, 0.5f));
            Assert.AreEqual(0, controller.Sweep());
            Assert.AreEqual(100, far.CurrentHp);
        }

        [Test]
        public void RadiusCard_GrowsSweepRadius_AndReachesFartherEnemies()
        {
            var card = AssetDatabase.LoadAssetAtPath<WeaponUpgradeCardData>(RadiusCardPath);
            Assert.IsNotNull(card, "WeaponMod_Radius card asset is missing");
            Assert.AreEqual(WeaponUpgradeType.Radius, card.upgradeType);

            var bus = new GameEventBus();
            var target = SpawnEnemy("Edge", new Vector3(0f, 0f, 3.6f), 100, bus);
            Physics.SyncTransforms();

            var (combat, controller, facing) = CreateHero(MeleeWeapon(2f, 0.5f));
            facing.rotation = Quaternion.LookRotation(Vector3.forward);
            Assert.AreEqual(0, controller.Sweep(), "Out of reach before the upgrade");

            float baseRadius = controller.EffectiveMeleeRadius;
            var applier = new UpgradeApplier(null, null, null, bus, combat);
            for (int i = 0; i < 2; i++) applier.ApplyCard(card, null);

            float expectedMult = 1f + 2f * card.value / 100f;
            Assert.AreEqual(baseRadius * expectedMult, controller.EffectiveMeleeRadius, 0.001f);
            Assert.AreEqual(2, combat.GetWeaponUpgradeStacks(WeaponUpgradeType.Radius));
            Assert.AreEqual(1, controller.Sweep(), "Bigger radius (and offset) reaches the enemy");
            Assert.Less(target.CurrentHp, 100);
        }

        [Test]
        public void WeaponCards_RadiusOnlyForMelee_MultishotOnlyForRanged()
        {
            var (melee, _, _) = CreateHero(MeleeWeapon());
            Assert.IsTrue(melee.SupportsWeaponUpgrade(WeaponUpgradeType.Radius));
            Assert.IsFalse(melee.SupportsWeaponUpgrade(WeaponUpgradeType.Multishot));
            Assert.IsTrue(melee.SupportsWeaponUpgrade(WeaponUpgradeType.Power));
            Assert.IsTrue(melee.SupportsWeaponUpgrade(WeaponUpgradeType.FireRate));

            var ranged = Track(ScriptableObject.CreateInstance<WeaponData>());
            ranged.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Projectiles/HolyHammerProjectile.prefab");
            var (rangedHero, _, _) = CreateHero(ranged);
            Assert.IsFalse(rangedHero.SupportsWeaponUpgrade(WeaponUpgradeType.Radius));
            Assert.IsTrue(rangedHero.SupportsWeaponUpgrade(WeaponUpgradeType.Multishot));
        }
    }
}
