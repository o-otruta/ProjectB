using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.Abilities;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    public static class Sprint3SetupTool
    {
        private const string ABILITIES_DIR = "Assets/_ScriptableObjects/Abilities";
        private const string CARDS_DIR = "Assets/_ScriptableObjects/Cards";
        private const string SYNERGIES_DIR = "Assets/_ScriptableObjects/Synergies";
        private const string ACHIEVEMENTS_DIR = "Assets/Resources/Achievements";
        private const string MATERIALS_DIR = "Assets/_Materials";
        private const string ABILITY_CATALOG_PATH = "Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset";
        private const string SLOT_PREFAB_PATH = "Assets/_Prefabs/UI/AbilityHudSlot.prefab";
        private const string LOCK_ICON_PATH = "Assets/_UI/Icons/Generated/ui_slot_lock.png";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";
        private const int ENEMY_LAYER_MASK = 1 << 8;

        [MenuItem("ProjectB/Setup/Run Sprint 3 Asset & Scene Setup")]
        public static void RunAll()
        {
            var materials = CreateOrUpdateMaterials();
            var newAbilities = new List<AbilityData>();
            var newAbilityCards = new List<AbilityCardData>();
            var newSynergies = new List<SynergyData>();

            CreateActiveAbilities(materials, newAbilities, newAbilityCards);
            CreatePassiveAbilities(newAbilities, newAbilityCards);
            CreateSynergies(newSynergies);
            CreateOrUpdateCritMultiplierMetaUpgrade();
            UpdateAbilityUnlockAchievements();
            CreateSlotExpansionAchievements();
            UpdateAbilityCatalog(newAbilities);

            VoxelIconGenerator.GenerateAllIcons();
            WireLockIconIntoHudSlotPrefab();
            WireGameplayScene(newAbilityCards, newSynergies);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint3SetupTool] Sprint 3 abilities, passives, synergies, slot achievements, icons, and scene wiring completed successfully.");
        }

        private static Dictionary<string, Material> CreateOrUpdateMaterials()
        {
            var dict = new Dictionary<string, Material>();
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? litShader;

            dict["ChainLightning"] = EnsureMaterial(
                $"{MATERIALS_DIR}/ChainLightningMat.mat",
                unlitShader,
                new Color(0.45f, 0.92f, 1.0f, 1f),
                emissive: false,
                default);

            dict["PoisonPuddle"] = EnsureMaterial(
                $"{MATERIALS_DIR}/PoisonPuddleMat.mat",
                litShader,
                new Color(0.22f, 0.85f, 0.25f, 1f),
                emissive: true,
                new Color(0.15f, 0.65f, 0.18f, 1f));

            dict["GuardianShield"] = EnsureMaterial(
                $"{MATERIALS_DIR}/GuardianShieldMat.mat",
                litShader,
                new Color(0.35f, 0.72f, 1.0f, 1f),
                emissive: false,
                default);

            dict["DragonWhelp"] = EnsureMaterial(
                $"{MATERIALS_DIR}/DragonWhelpMat.mat",
                litShader,
                new Color(0.95f, 0.28f, 0.14f, 1f),
                emissive: true,
                new Color(0.55f, 0.18f, 0.04f, 1f));

            dict["ArcaneTornado"] = EnsureMaterial(
                $"{MATERIALS_DIR}/ArcaneTornadoMat.mat",
                litShader,
                new Color(0.35f, 0.9f, 0.95f, 1f),
                emissive: true,
                new Color(0.12f, 0.45f, 0.55f, 1f));

            dict["SeismicWave"] = EnsureMaterial(
                $"{MATERIALS_DIR}/SeismicWaveMat.mat",
                litShader,
                new Color(0.95f, 0.56f, 0.16f, 1f),
                emissive: true,
                new Color(0.55f, 0.26f, 0.05f, 1f));

            return dict;
        }

        private static Material EnsureMaterial(
            string assetPath,
            Shader shader,
            Color baseColor,
            bool emissive,
            Color emissionColor)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CreateActiveAbilities(
            Dictionary<string, Material> materials,
            List<AbilityData> abilitiesOut,
            List<AbilityCardData> cardsOut)
        {
            // 1. Chain Lightning
            var lightning = LoadOrCreateAsset<ChainLightningAbilityData>($"{ABILITIES_DIR}/ChainLightningData.asset");
            lightning.id = "ChainLightning";
            lightning.abilityName = "Chain Lightning";
            lightning.description = "Strikes the closest enemy with lightning that jumps between nearby targets.";
            lightning.type = AbilityType.Active;
            lightning.rarity = CardRarity.Rare;
            lightning.unlockedByDefault = true;
            lightning.baseDamage = 14f;
            lightning.baseCooldown = 1.4f;
            lightning.baseArea = 1f;
            lightning.targetLayer = ENEMY_LAYER_MASK;
            lightning.bounceCount = 3;
            lightning.searchRadius = 9f;
            lightning.bounceRadius = 5f;
            lightning.boltVisualDuration = 0.16f;
            lightning.lineMaterial = materials["ChainLightning"];
            EditorUtility.SetDirty(lightning);
            abilitiesOut.Add(lightning);

            var lightningMods = new List<CardData>
            {
                CreateModifierCard("ChainLightningMod_Damage", "ChainLightning", "Chain Lightning: +6 Damage", "Increases lightning bolt damage by 6.", CardRarity.Common, ModifierType.Damage, 6f),
                CreateModifierCard("ChainLightningMod_Count", "ChainLightning", "Chain Lightning: +2 Bounces", "Lightning jumps to 2 additional enemies.", CardRarity.Rare, ModifierType.Count, 2f),
                CreateModifierCard("ChainLightningMod_Cooldown", "ChainLightning", "Chain Lightning: Rapid Discharge", "Reduces cooldown by 0.25s.", CardRarity.Common, ModifierType.Cooldown, 0.25f)
            };
            cardsOut.Add(CreateAbilityCard("ChainLightningCard", lightning, lightning.abilityName, lightning.description, lightning.rarity, lightningMods));

            // 2. Poison Flask
            var poison = LoadOrCreateAsset<PoisonFlaskAbilityData>($"{ABILITIES_DIR}/PoisonFlaskData.asset");
            poison.id = "PoisonAlchemist";
            poison.abilityName = "Poison Flask";
            poison.description = "Hurls toxic flasks that leave boiling acid puddles dealing periodic damage.";
            poison.type = AbilityType.Active;
            poison.rarity = CardRarity.Rare;
            poison.unlockedByDefault = true;
            poison.baseDamage = 6f;
            poison.baseCooldown = 2.5f;
            poison.baseArea = 2.5f;
            poison.targetLayer = ENEMY_LAYER_MASK;
            poison.puddleRadius = 2.5f;
            poison.puddleDuration = 4f;
            poison.tickInterval = 0.4f;
            poison.flaskCount = 1;
            poison.throwRange = 10f;
            poison.flightDuration = 0.45f;
            poison.slowFactor = 0.8f;
            poison.puddleMaterial = materials["PoisonPuddle"];
            EditorUtility.SetDirty(poison);
            abilitiesOut.Add(poison);

            var poisonMods = new List<CardData>
            {
                CreateModifierCard("PoisonFlaskMod_Damage", "PoisonAlchemist", "Poison Flask: +4 Tick Damage", "Increases poison puddle tick damage by 4.", CardRarity.Common, ModifierType.Damage, 4f),
                CreateModifierCard("PoisonFlaskMod_Count", "PoisonAlchemist", "Poison Flask: +1 Flask", "Throws 1 additional toxic flask.", CardRarity.Rare, ModifierType.Count, 1f),
                CreateModifierCard("PoisonFlaskMod_Area", "PoisonAlchemist", "Poison Flask: Wider Puddle", "Increases puddle radius by +1.0m.", CardRarity.Common, ModifierType.Area, 1.0f)
            };
            cardsOut.Add(CreateAbilityCard("PoisonFlaskCard", poison, poison.abilityName, poison.description, poison.rarity, poisonMods));

            // 3. Guardian Shields
            var shield = LoadOrCreateAsset<GuardianShieldAbilityData>($"{ABILITIES_DIR}/GuardianShieldData.asset");
            shield.id = "GuardianShield";
            shield.abilityName = "Guardian Shields";
            shield.description = "Voxel shields orbit close around you, damaging and knocking back enemies.";
            shield.type = AbilityType.Active;
            shield.rarity = CardRarity.Common;
            shield.unlockedByDefault = true;
            shield.baseDamage = 12f;
            shield.baseCooldown = 0.45f;
            shield.baseArea = 1.4f;
            shield.targetLayer = ENEMY_LAYER_MASK;
            shield.shieldCount = 2;
            shield.orbitSpeed = 150f;
            shield.orbitRadius = 1.6f;
            shield.knockbackDistance = 1.4f;
            shield.shieldHitRadius = 0.75f;
            shield.hitCooldownPerEnemy = 0.45f;
            shield.shieldMaterial = materials["GuardianShield"];
            EditorUtility.SetDirty(shield);
            abilitiesOut.Add(shield);

            var shieldMods = new List<CardData>
            {
                CreateModifierCard("GuardianShieldMod_Count", "GuardianShield", "Guardian Shields: +1 Shield", "Adds 1 orbiting guardian shield.", CardRarity.Rare, ModifierType.Count, 1f),
                CreateModifierCard("GuardianShieldMod_Damage", "GuardianShield", "Guardian Shields: +8 Damage", "Increases shield impact damage by 8.", CardRarity.Common, ModifierType.Damage, 8f),
                CreateModifierCard("GuardianShieldMod_Area", "GuardianShield", "Guardian Shields: Heavy Knockback", "Increases enemy knockback distance by +0.5m.", CardRarity.Common, ModifierType.Area, 0.5f)
            };
            cardsOut.Add(CreateAbilityCard("GuardianShieldCard", shield, shield.abilityName, shield.description, shield.rarity, shieldMods));

            // 4. Dragon Whelp
            var dragon = LoadOrCreateAsset<MiniDragonAbilityData>($"{ABILITIES_DIR}/MiniDragonData.asset");
            dragon.id = "MiniDragon";
            dragon.abilityName = "Dragon Whelp";
            dragon.description = "Summons a loyal dragon minion that trails behind you and breathes a 3-bolt flame cone.";
            dragon.type = AbilityType.Active;
            dragon.rarity = CardRarity.Epic;
            dragon.unlockedByDefault = false;
            dragon.baseDamage = 12f;
            dragon.baseCooldown = 1.2f;
            dragon.baseArea = 1f;
            dragon.targetLayer = ENEMY_LAYER_MASK;
            dragon.dragonCount = 1;
            dragon.coneProjectileCount = 3;
            dragon.coneSpreadAngle = 26f;
            dragon.searchRadius = 8f;
            dragon.projectileSpeed = 14f;
            dragon.followDistance = 2.0f;
            dragon.followSmoothTime = 0.22f;
            dragon.dragonMaterial = materials["DragonWhelp"];
            dragon.flameMaterial = materials["SeismicWave"];
            EditorUtility.SetDirty(dragon);
            abilitiesOut.Add(dragon);

            var dragonMods = new List<CardData>
            {
                CreateModifierCard("MiniDragonMod_Count", "MiniDragon", "Dragon Whelp: +1 Dragon", "Summons 1 additional Dragon Whelp.", CardRarity.Epic, ModifierType.Count, 1f),
                CreateModifierCard("MiniDragonMod_Damage", "MiniDragon", "Dragon Whelp: +6 Flame Damage", "Increases each flame bolt's damage by 6.", CardRarity.Rare, ModifierType.Damage, 6f),
                CreateModifierCard("MiniDragonMod_Cooldown", "MiniDragon", "Dragon Whelp: Rapid Breath", "Reduces fire breath cooldown by 0.2s.", CardRarity.Common, ModifierType.Cooldown, 0.2f)
            };
            cardsOut.Add(CreateAbilityCard("MiniDragonCard", dragon, dragon.abilityName, dragon.description, dragon.rarity, dragonMods));

            // 5. Arcane Tornado
            var tornado = LoadOrCreateAsset<VoxelTornadoAbilityData>($"{ABILITIES_DIR}/VoxelTornadoData.asset");
            tornado.id = "VoxelTornado";
            tornado.abilityName = "Arcane Tornado";
            tornado.description = "Releases wandering voxel whirlwinds that pull enemies inward and shred them.";
            tornado.type = AbilityType.Active;
            tornado.rarity = CardRarity.Epic;
            tornado.unlockedByDefault = false;
            tornado.baseDamage = 9f;
            tornado.baseCooldown = 3.2f;
            tornado.baseArea = 2.2f;
            tornado.targetLayer = ENEMY_LAYER_MASK;
            tornado.vortexRadius = 2.2f;
            tornado.duration = 4f;
            tornado.tornadoCount = 1;
            tornado.moveSpeed = 4.2f;
            tornado.pullForce = 4.5f;
            tornado.tickInterval = 0.25f;
            tornado.waveAmplitude = 1.6f;
            tornado.waveFrequency = 2.5f;
            tornado.tornadoMaterial = materials["ArcaneTornado"];
            EditorUtility.SetDirty(tornado);
            abilitiesOut.Add(tornado);

            var tornadoMods = new List<CardData>
            {
                CreateModifierCard("VoxelTornadoMod_Count", "VoxelTornado", "Arcane Tornado: +1 Tornado", "Spawns 1 additional whirlwind per cast.", CardRarity.Epic, ModifierType.Count, 1f),
                CreateModifierCard("VoxelTornadoMod_Damage", "VoxelTornado", "Arcane Tornado: +5 Tick Damage", "Increases whirlwind tick damage by 5.", CardRarity.Rare, ModifierType.Damage, 5f),
                CreateModifierCard("VoxelTornadoMod_Duration", "VoxelTornado", "Arcane Tornado: Extended Gale", "Increases tornado duration by +1.2s.", CardRarity.Common, ModifierType.Duration, 1.2f)
            };
            cardsOut.Add(CreateAbilityCard("VoxelTornadoCard", tornado, tornado.abilityName, tornado.description, tornado.rarity, tornadoMods));

            // 6. Seismic Hammer
            var hammer = LoadOrCreateAsset<SeismicHammerAbilityData>($"{ABILITIES_DIR}/SeismicHammerData.asset");
            hammer.id = "EarthquakeSlam";
            hammer.abilityName = "Seismic Hammer";
            hammer.description = "Unleashes a crushing shockwave around you, dealing massive AoE damage and slowing enemies by 80%.";
            hammer.type = AbilityType.Active;
            hammer.rarity = CardRarity.Legendary;
            hammer.unlockedByDefault = false;
            hammer.baseDamage = 45f;
            hammer.baseCooldown = 4.0f;
            hammer.baseArea = 4.5f;
            hammer.targetLayer = ENEMY_LAYER_MASK;
            hammer.slamRadius = 4.5f;
            hammer.slowFactor = 0.2f;
            hammer.slowDuration = 1.5f;
            hammer.waveMaterial = materials["SeismicWave"];
            EditorUtility.SetDirty(hammer);
            abilitiesOut.Add(hammer);

            var hammerMods = new List<CardData>
            {
                CreateModifierCard("SeismicHammerMod_Damage", "EarthquakeSlam", "Seismic Hammer: +20 Damage", "Increases shockwave damage by 20.", CardRarity.Epic, ModifierType.Damage, 20f),
                CreateModifierCard("SeismicHammerMod_Radius", "EarthquakeSlam", "Seismic Hammer: +1.2m Radius", "Increases shockwave radius by +1.2m.", CardRarity.Rare, ModifierType.Radius, 1.2f),
                CreateModifierCard("SeismicHammerMod_Cooldown", "EarthquakeSlam", "Seismic Hammer: -0.6s Cooldown", "Reduces shockwave cooldown by 0.6s.", CardRarity.Rare, ModifierType.Cooldown, 0.6f)
            };
            cardsOut.Add(CreateAbilityCard("SeismicHammerCard", hammer, hammer.abilityName, hammer.description, hammer.rarity, hammerMods));
        }

        private static void CreatePassiveAbilities(
            List<AbilityData> abilitiesOut,
            List<AbilityCardData> cardsOut)
        {
            // 1. Chrono Tome
            var chrono = CreatePassiveData(
                "ChronoTomeData",
                "ChronoTome",
                "Chrono Tome",
                "Reduces cooldowns of all active abilities by +10% (up to 50% total).",
                CardRarity.Rare,
                unlockedByDefault: true,
                PassiveEffectType.CooldownReductionPercent,
                10f);
            abilitiesOut.Add(chrono);
            var chronoMod = CreateModifierCard("ChronoTomeMod", "ChronoTome", "Chrono Tome: +8% CDR", "Reduces active ability cooldowns by an extra 8%.", CardRarity.Rare, ModifierType.Cooldown, 8f);
            cardsOut.Add(CreateAbilityCard("ChronoTomeCard", chrono, chrono.abilityName, chrono.description, chrono.rarity, new List<CardData> { chronoMod }));

            // 2. Sniper Lens
            var crit = CreatePassiveData(
                "CriticalLensData",
                "CriticalLens",
                "Sniper Lens",
                "Grants +12% Critical Hit Chance for weapons and abilities.",
                CardRarity.Epic,
                unlockedByDefault: false,
                PassiveEffectType.CritChancePercent,
                12f);
            abilitiesOut.Add(crit);
            var critMod = CreateModifierCard("CriticalLensMod", "CriticalLens", "Sniper Lens: +8% Crit Chance", "Increases Critical Hit Chance by +8%.", CardRarity.Rare, ModifierType.Damage, 8f);
            var critMultMod = CreateModifierCard("CriticalLensMod_Multiplier", "CriticalLens", "Sniper Lens: +0.25x Crit Damage", "Increases Critical Damage multiplier by +0.25x.", CardRarity.Rare, ModifierType.CritMultiplier, 0.25f);
            cardsOut.Add(CreateAbilityCard("CriticalLensCard", crit, crit.abilityName, crit.description, crit.rarity, new List<CardData> { critMod, critMultMod }));

            // 3. Titan Heart
            var titan = CreatePassiveData(
                "TitanHeartData",
                "TitanHeart",
                "Titan Heart",
                "Continuously restores 2 HP every second.",
                CardRarity.Common,
                unlockedByDefault: true,
                PassiveEffectType.HealthRegenPerSecond,
                2f);
            abilitiesOut.Add(titan);
            var titanMod = CreateModifierCard("TitanHeartMod", "TitanHeart", "Titan Heart: +1.5 HP/s", "Increases health regeneration by +1.5 HP/s.", CardRarity.Common, ModifierType.Duration, 1.5f);
            cardsOut.Add(CreateAbilityCard("TitanHeartCard", titan, titan.abilityName, titan.description, titan.rarity, new List<CardData> { titanMod }));

            // 4. Golden Idol
            var idol = CreatePassiveData(
                "GoldenIdolData",
                "GoldenIdol",
                "Golden Idol",
                "Increases all coins earned during the run by +25%.",
                CardRarity.Rare,
                unlockedByDefault: false,
                PassiveEffectType.CoinBonusPercent,
                25f);
            abilitiesOut.Add(idol);
            var idolMod = CreateModifierCard("GoldenIdolMod", "GoldenIdol", "Golden Idol: +20% Coin Gain", "Increases coin gain by an extra +20%.", CardRarity.Rare, ModifierType.Area, 20f);
            cardsOut.Add(CreateAbilityCard("GoldenIdolCard", idol, idol.abilityName, idol.description, idol.rarity, new List<CardData> { idolMod }));
        }

        private static StatPassiveAbilityData CreatePassiveData(
            string assetName,
            string id,
            string displayName,
            string description,
            CardRarity rarity,
            bool unlockedByDefault,
            PassiveEffectType effectType,
            float baseValue)
        {
            var data = LoadOrCreateAsset<StatPassiveAbilityData>($"{ABILITIES_DIR}/{assetName}.asset");
            data.id = id;
            data.abilityName = displayName;
            data.description = description;
            data.type = AbilityType.Passive;
            data.rarity = rarity;
            data.unlockedByDefault = unlockedByDefault;
            data.effectType = effectType;
            data.baseValue = baseValue;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void CreateSynergies(List<SynergyData> synergiesOut)
        {
            // 1. Thunderstorm: ChainLightning + IceAura
            synergiesOut.Add(CreateSynergy(
                "Synergy_Thunderstorm",
                "SynergyCard_Thunderstorm",
                "thunder_storm",
                "Thunderstorm",
                "Synergy: Thunderstorm",
                "Chain Lightning + Ice Aura: +3 Bounces & +12 Damage to Chain Lightning; +1.5m Radius & +20% Slow to Ice Aura.",
                CardRarity.Legendary,
                new List<string> { "ChainLightning", "IceAura" },
                new List<SynergyAbilityBonus>
                {
                    new SynergyAbilityBonus { targetAbilityId = "ChainLightning", modifierType = ModifierType.Count, value = 3f },
                    new SynergyAbilityBonus { targetAbilityId = "ChainLightning", modifierType = ModifierType.Damage, value = 12f },
                    new SynergyAbilityBonus { targetAbilityId = "IceAura", modifierType = ModifierType.Radius, value = 1.5f },
                    new SynergyAbilityBonus { targetAbilityId = "IceAura", modifierType = ModifierType.SlowAmount, value = 0.2f }
                }));

            // 2. Toxic Inferno: PoisonAlchemist + Fireball
            synergiesOut.Add(CreateSynergy(
                "Synergy_ToxicInferno",
                "SynergyCard_ToxicInferno",
                "toxic_inferno",
                "Toxic Inferno",
                "Synergy: Toxic Inferno",
                "Poison Flask + Fireball: +1 Flask, +1.2m Puddle & +8 Damage; +1 Fireball & +10 Fireball Damage.",
                CardRarity.Epic,
                new List<string> { "PoisonAlchemist", "Fireball" },
                new List<SynergyAbilityBonus>
                {
                    new SynergyAbilityBonus { targetAbilityId = "PoisonAlchemist", modifierType = ModifierType.Count, value = 1f },
                    new SynergyAbilityBonus { targetAbilityId = "PoisonAlchemist", modifierType = ModifierType.Area, value = 1.2f },
                    new SynergyAbilityBonus { targetAbilityId = "PoisonAlchemist", modifierType = ModifierType.Damage, value = 8f },
                    new SynergyAbilityBonus { targetAbilityId = "Fireball", modifierType = ModifierType.Count, value = 1f },
                    new SynergyAbilityBonus { targetAbilityId = "Fireball", modifierType = ModifierType.Damage, value = 10f }
                }));

            // 3. Aegis Phalanx: orbital_small_sword + GuardianShield
            synergiesOut.Add(CreateSynergy(
                "Synergy_AegisPhalanx",
                "SynergyCard_AegisPhalanx",
                "aegis_phalanx",
                "Aegis Phalanx",
                "Synergy: Aegis Phalanx",
                "Orbital Sword + Guardian Shields: +2 Swords, +2 Shields, +60°/s Spin Speed, and +15 Armor.",
                CardRarity.Epic,
                new List<string> { "orbital_small_sword", "GuardianShield" },
                new List<SynergyAbilityBonus>
                {
                    new SynergyAbilityBonus { targetAbilityId = "orbital_small_sword", modifierType = ModifierType.Count, value = 2f },
                    new SynergyAbilityBonus { targetAbilityId = "orbital_small_sword", modifierType = ModifierType.Speed, value = 60f },
                    new SynergyAbilityBonus { targetAbilityId = "GuardianShield", modifierType = ModifierType.Count, value = 2f },
                    new SynergyAbilityBonus { targetAbilityId = "GuardianShield", modifierType = ModifierType.Speed, value = 60f }
                },
                armorBonus: 15f));

            // 4. Dragon Lord: MiniDragon + BattleFury
            synergiesOut.Add(CreateSynergy(
                "Synergy_DragonLord",
                "SynergyCard_DragonLord",
                "dragon_lord",
                "Dragon Lord",
                "Synergy: Dragon Lord",
                "Dragon Whelp + Battle Fury: Summons +1 Dragon Whelp, +10 Dragon Damage, and +25% Global Damage.",
                CardRarity.Legendary,
                new List<string> { "MiniDragon", "BattleFury" },
                new List<SynergyAbilityBonus>
                {
                    new SynergyAbilityBonus { targetAbilityId = "MiniDragon", modifierType = ModifierType.Count, value = 1f },
                    new SynergyAbilityBonus { targetAbilityId = "MiniDragon", modifierType = ModifierType.Damage, value = 10f }
                },
                globalDamageBonusPercent: 25f));

            // 5. Cataclysm Vortex: BlackHole + VoxelTornado
            synergiesOut.Add(CreateSynergy(
                "Synergy_CataclysmVortex",
                "SynergyCard_CataclysmVortex",
                "cataclysm_vortex",
                "Cataclysm Vortex",
                "Synergy: Cataclysm Vortex",
                "Black Hole + Arcane Tornado: +2m Black Hole Radius & +8 Damage; +2 Tornadoes & +8 Damage.",
                CardRarity.Legendary,
                new List<string> { "BlackHole", "VoxelTornado" },
                new List<SynergyAbilityBonus>
                {
                    new SynergyAbilityBonus { targetAbilityId = "BlackHole", modifierType = ModifierType.Radius, value = 2f },
                    new SynergyAbilityBonus { targetAbilityId = "BlackHole", modifierType = ModifierType.Damage, value = 8f },
                    new SynergyAbilityBonus { targetAbilityId = "VoxelTornado", modifierType = ModifierType.Count, value = 2f },
                    new SynergyAbilityBonus { targetAbilityId = "VoxelTornado", modifierType = ModifierType.Damage, value = 8f }
                }));
        }

        private static SynergyData CreateSynergy(
            string synergyAssetName,
            string cardAssetName,
            string synergyId,
            string synergyName,
            string cardTitle,
            string cardDescription,
            CardRarity rarity,
            List<string> requiredAbilityIds,
            List<SynergyAbilityBonus> abilityBonuses,
            float globalDamageBonusPercent = 0f,
            float armorBonus = 0f)
        {
            var card = LoadOrCreateAsset<SynergyCardData>($"{CARDS_DIR}/{cardAssetName}.asset");
            card.cardName = cardTitle;
            card.description = cardDescription;
            card.rarity = rarity;
            card.isConsumable = true;
            card.synergyId = synergyId;
            card.abilityBonuses = abilityBonuses;
            card.globalDamageBonusPercent = globalDamageBonusPercent;
            card.armorBonus = armorBonus;
            EditorUtility.SetDirty(card);

            var syn = LoadOrCreateAsset<SynergyData>($"{SYNERGIES_DIR}/{synergyAssetName}.asset");
            syn.id = synergyId;
            syn.synergyName = synergyName;
            syn.requiredAbilityIds = requiredAbilityIds;
            syn.rewardCard = card;
            EditorUtility.SetDirty(syn);

            return syn;
        }

        private static AbilityCardData CreateAbilityCard(
            string assetName,
            AbilityData abilityData,
            string cardName,
            string description,
            CardRarity rarity,
            List<CardData> unlockedModifiers)
        {
            var card = LoadOrCreateAsset<AbilityCardData>($"{CARDS_DIR}/{assetName}.asset");
            card.cardName = cardName;
            card.description = description;
            card.rarity = rarity;
            card.isConsumable = true;
            card.abilityData = abilityData;
            card.unlockedModifiers = unlockedModifiers;
            EditorUtility.SetDirty(card);
            return card;
        }

        private static AbilityModifierCardData CreateModifierCard(
            string assetName,
            string targetAbilityId,
            string cardName,
            string description,
            CardRarity rarity,
            ModifierType modifierType,
            float value)
        {
            var mod = LoadOrCreateAsset<AbilityModifierCardData>($"{CARDS_DIR}/{assetName}.asset");
            mod.cardName = cardName;
            mod.description = description;
            mod.rarity = rarity;
            mod.isConsumable = false;
            mod.targetAbilityId = targetAbilityId;
            mod.modifierType = modifierType;
            mod.value = value;
            mod.hasSecondaryModifier = false;
            EditorUtility.SetDirty(mod);
            return mod;
        }

        private static void UpdateAbilityUnlockAchievements()
        {
            SetAchievementAbilityReward("Collect5Abilities.asset", "MiniDragon", "Equip 5 different abilities in a single run to unlock Dragon Whelp");
            SetAchievementAbilityReward("Kill1000Enemies.asset", "VoxelTornado", "Kill 1,000 enemies cumulatively to unlock Arcane Tornado");
            SetAchievementAbilityReward("ReachWave50.asset", "EarthquakeSlam", "Reach Wave 50 to unlock Seismic Hammer");
            SetAchievementAbilityReward("NoDamage5Waves.asset", "CriticalLens", "Complete 5 consecutive waves without taking damage to unlock Sniper Lens");
            SetAchievementAbilityReward("Find5Chests.asset", "GoldenIdol", "Find 5 secret Treasure Chests to unlock Golden Idol");
        }

        private static void SetAchievementAbilityReward(string fileName, string abilityId, string description)
        {
            var ach = AssetDatabase.LoadAssetAtPath<AchievementData>($"{ACHIEVEMENTS_DIR}/{fileName}");
            if (ach == null)
            {
                Debug.LogWarning($"[Sprint3SetupTool] Achievement not found: {fileName}");
                return;
            }

            ach.rewardType = AchievementRewardType.UnlockAbility;
            ach.rewardId = abilityId;
            ach.rewardValue = 0;
            ach.description = description;
            EditorUtility.SetDirty(ach);
        }

        private static void CreateSlotExpansionAchievements()
        {
            CreateOrUpdateAchievement(
                "SlotActive1_Wave5.asset",
                "unlock_active_slot_1",
                "Battle Ready",
                "Reach Wave 5 to unlock +1 Active Ability Slot",
                AchievementConditionType.ReachWave,
                5,
                AchievementRewardType.UnlockActiveSlot,
                1);

            CreateOrUpdateAchievement(
                "SlotPassive1_Kills250.asset",
                "unlock_passive_slot_1",
                "Tactician",
                "Kill 250 enemies cumulatively to unlock +1 Passive Ability Slot",
                AchievementConditionType.TotalKills,
                250,
                AchievementRewardType.UnlockPassiveSlot,
                1);

            CreateOrUpdateAchievement(
                "SlotActive2_Wave15.asset",
                "unlock_active_slot_2",
                "Weapon Master",
                "Reach Wave 15 to unlock +1 Active Ability Slot",
                AchievementConditionType.ReachWave,
                15,
                AchievementRewardType.UnlockActiveSlot,
                1);

            CreateOrUpdateAchievement(
                "SlotPassive2_AbilityLvl8.asset",
                "unlock_passive_slot_2",
                "Arcane Scholar",
                "Upgrade any ability to Level 8 to unlock +1 Passive Ability Slot",
                AchievementConditionType.MaxAbilityLevel,
                8,
                AchievementRewardType.UnlockPassiveSlot,
                1);

            CreateOrUpdateAchievement(
                "SlotActive3_Kills2500.asset",
                "unlock_active_slot_3",
                "Warlord",
                "Kill 2,500 enemies cumulatively to unlock +1 Active Ability Slot",
                AchievementConditionType.TotalKills,
                2500,
                AchievementRewardType.UnlockActiveSlot,
                1);

            CreateOrUpdateAchievement(
                "SlotPassive3_Wave35.asset",
                "unlock_passive_slot_3",
                "Grand Sage",
                "Reach Wave 35 to unlock +1 Passive Ability Slot",
                AchievementConditionType.ReachWave,
                35,
                AchievementRewardType.UnlockPassiveSlot,
                1);
        }

        private static void CreateOrUpdateAchievement(
            string fileName,
            string id,
            string title,
            string description,
            AchievementConditionType conditionType,
            int targetValue,
            AchievementRewardType rewardType,
            int rewardValue)
        {
            var ach = LoadOrCreateAsset<AchievementData>($"{ACHIEVEMENTS_DIR}/{fileName}");
            ach.id = id;
            ach.title = title;
            ach.description = description;
            ach.conditionType = conditionType;
            ach.targetValue = targetValue;
            ach.targetId = string.Empty;
            ach.rewardType = rewardType;
            ach.rewardValue = rewardValue;
            ach.rewardId = string.Empty;
            EditorUtility.SetDirty(ach);
        }

        private static void CreateOrUpdateCritMultiplierMetaUpgrade()
        {
            var meta = LoadOrCreateAsset<MetaUpgradeData>("Assets/Resources/MetaUpgrades/crit_multiplier.asset");
            meta.id = "crit_multiplier";
            meta.displayName = "Crit Damage";
            meta.description = "+0.1x Critical Damage multiplier per level.";
            meta.baseCost = 150;
            meta.costMultiplier = 1.35f;
            meta.maxLevel = 10;
            meta.effectPerLevel = 0.1f;
            meta.effectType = MetaUpgradeEffectType.CritMultiplier;
            EditorUtility.SetDirty(meta);
        }

        private static void UpdateAbilityCatalog(List<AbilityData> newAbilities)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(ABILITY_CATALOG_PATH);
            if (catalog == null)
            {
                Debug.LogWarning($"[Sprint3SetupTool] AbilityCatalog missing at {ABILITY_CATALOG_PATH}");
                return;
            }

            if (catalog.abilities == null)
            {
                catalog.abilities = new List<AbilityData>();
            }

            for (int i = 0; i < newAbilities.Count; i++)
            {
                if (newAbilities[i] != null && !catalog.abilities.Contains(newAbilities[i]))
                {
                    catalog.abilities.Add(newAbilities[i]);
                }
            }

            EditorUtility.SetDirty(catalog);
        }

        private static void WireLockIconIntoHudSlotPrefab()
        {
            var lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LOCK_ICON_PATH);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SLOT_PREFAB_PATH);
            if (prefab == null || lockSprite == null)
            {
                Debug.LogWarning($"[Sprint3SetupTool] Missing slot prefab or lock sprite ({SLOT_PREFAB_PATH}, {LOCK_ICON_PATH}).");
                return;
            }

            var slotUi = prefab.GetComponent<AbilityHudSlotUI>();
            if (slotUi != null)
            {
                var so = new SerializedObject(slotUi);
                so.FindProperty("lockedIconSprite").objectReferenceValue = lockSprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(slotUi);
                EditorUtility.SetDirty(prefab);
            }
        }

        private static void WireGameplayScene(List<AbilityCardData> newCards, List<SynergyData> newSynergies)
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath(GAMEPLAY_SCENE_PATH);
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                UpgradeManager upgradeMgr = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    upgradeMgr = root.GetComponentInChildren<UpgradeManager>(true);
                    if (upgradeMgr != null) break;
                }

                if (upgradeMgr == null)
                {
                    Debug.LogError("[Sprint3SetupTool] UpgradeManager not found in Gameplay.unity");
                    return;
                }

                if (upgradeMgr.cardPool == null) upgradeMgr.cardPool = new List<CardData>();
                for (int i = 0; i < newCards.Count; i++)
                {
                    if (newCards[i] != null && !upgradeMgr.cardPool.Contains(newCards[i]))
                    {
                        upgradeMgr.cardPool.Add(newCards[i]);
                    }
                }

                if (upgradeMgr.synergies == null) upgradeMgr.synergies = new List<SynergyData>();
                for (int i = 0; i < newSynergies.Count; i++)
                {
                    if (newSynergies[i] != null && !upgradeMgr.synergies.Contains(newSynergies[i]))
                    {
                        upgradeMgr.synergies.Add(newSynergies[i]);
                    }
                }

                EditorUtility.SetDirty(upgradeMgr);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
