using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.Combat;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.UI;
using ProjectB.Visuals;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// Sprint 11: Constellation Tree, weapon upgrade cards,
    /// 4 new active + 2 passive abilities and 4 reactive synergies.
    /// </summary>
    [System.Obsolete("Legacy one-shot setup tool. Scenes and prefabs are the source of truth (Sprint 15.4).")]
    public static class Sprint11SetupTool
    {
        private const string SHADER_VOXEL_LIT = "ProjectB/VoxelLit";
        private const string SHADER_URP_LIT = "Universal Render Pipeline/Lit";
        private const string SHADER_URP_UNLIT = "Universal Render Pipeline/Unlit";

        private const int HERO_LAYER = 9;
        private const int HERO_PROJECTILE_LAYER = 11;

        private const string VOX = "Assets/_VoxelModels";
        private const string VOXEL_MAT_DIR = "Assets/_Materials/Voxels";
        private const string MAT_DIR = "Assets/_Materials";
        private const string HERO_PREFAB_DIR = "Assets/_Prefabs/Heroes";
        private const string PROJ_PREFAB_DIR = "Assets/_Prefabs/Projectiles";
        private const string ABILITY_PREFAB_DIR = "Assets/_Prefabs/Abilities";
        private const string SO_DIR = "Assets/_ScriptableObjects";
        private const string ABILITY_DIR = "Assets/_ScriptableObjects/Abilities";
        private const string CARDS_DIR = "Assets/_ScriptableObjects/Cards";
        private const string SYNERGIES_DIR = "Assets/_ScriptableObjects/Synergies";
        private const string TALENTS_DIR = "Assets/_ScriptableObjects/Talents";
        private const string ACH_DIR = "Assets/Resources/Achievements";
        private const string TALENT_TREE_PATH = "Assets/Resources/TalentTree.asset";
        private const string HERO_CATALOG_PATH = "Assets/Resources/HeroCatalog.asset";
        private const string SYNERGY_CATALOG_PATH = "Assets/Resources/SynergyCatalog.asset";
        private const string ABILITY_CATALOG_PATH = "Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset";
        private const string HERO_PREFAB_PATH = "Assets/_Prefabs/Hero.prefab";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";
        private const string MAINMENU_SCENE_PATH = "Assets/_Scenes/MainMenu.unity";
        private const string NODE_SPRITE_PATH = "Assets/_UI/ConstellationNode.png";
        private const string HALO_SPRITE_PATH = "Assets/_UI/ConstellationHalo.png";
        private const string WHITE_SPRITE_PATH = "Assets/_UI/Sprint10WhiteSquare.png";
        public static void RunAll()
        {
            EnsureFolders();

            // 1. Palettes & materials
            var chakramMats = VoxelPair("BuzzsawChakram", $"{VOX}/BuzzsawChakram/buzzsaw_chakram.png", new Color(1.6f, 1.8f, 1.9f));
            var spireMats = VoxelPair("SkyfallSpire", $"{VOX}/SkyfallSpire/skyfall_spire.png", new Color(2.2f, 1.7f, 1.2f));
            var decoyMats = VoxelPair("ShadowDecoy", $"{VOX}/ShadowDecoy/shadow_decoy.png", new Color(1.8f, 1.5f, 2.2f));
            Material skyfallTelegraphMat = TransparentUnlit($"{MAT_DIR}/SkyfallTelegraphMat.mat", new Color(1f, 0.38f, 0.1f, 0.5f));

            // 2. Prefabs
            GameObject chakramDisc = BuildSimpleVisualPrefab($"{ABILITY_PREFAB_DIR}/ChakramDiscVisual.prefab", "ChakramDisc", $"{VOX}/BuzzsawChakram/BuzzsawChakram.obj", chakramMats, typeof(ChakramDisc), Vector3.one);
            GameObject spireVisual = BuildSimpleVisualPrefab($"{ABILITY_PREFAB_DIR}/SkyfallSpireVisual.prefab", "SkyfallSpire", $"{VOX}/SkyfallSpire/SkyfallSpire.obj", spireMats, null, Vector3.one);
            GameObject spireTelegraph = BuildRingPrefab($"{ABILITY_PREFAB_DIR}/SkyfallTelegraph.prefab", "SkyfallTelegraph", $"{VOX}/IceAuraRing/IceAuraRing.obj", skyfallTelegraphMat, null);
            GameObject decoyPrefab = BuildSimpleVisualPrefab($"{ABILITY_PREFAB_DIR}/ShadowDecoy.prefab", "ShadowDecoy", $"{VOX}/ShadowDecoy/ShadowDecoy.obj", decoyMats, typeof(ShadowDecoyEntity), Vector3.one);
            GameObject phantomFireball = BuildSimpleVisualPrefab($"{ABILITY_PREFAB_DIR}/PhantomFireball.prefab", "PhantomFireball", $"{VOX}/FireballProjectile/FireballProjectile.obj",
                (AssetDatabase.LoadAssetAtPath<Material>($"{VOXEL_MAT_DIR}/FireballProjectile_Mat.mat"), AssetDatabase.LoadAssetAtPath<Material>($"{VOXEL_MAT_DIR}/FireballProjectile_Glow_Mat.mat")),
                typeof(GravityShardProjectile), Vector3.one * 1.1f);
            GameObject glacialWave = BuildRingPrefab($"{ABILITY_PREFAB_DIR}/GlacialWaveVisual.prefab", "GlacialWave", $"{VOX}/IceAuraRing/IceAuraRing.obj",
                AssetDatabase.LoadAssetAtPath<Material>($"{VOXEL_MAT_DIR}/IceAuraRing_Mat.mat"), AssetDatabase.LoadAssetAtPath<Material>($"{VOXEL_MAT_DIR}/IceAuraRing_Glow_Mat.mat"));

            // 4. Abilities, cards, synergies
            var newAbilityCards = new List<CardData>();
            var newAbilities = new List<AbilityData>();
            CreateAbilities(chakramDisc, spireVisual, spireTelegraph, decoyPrefab, phantomFireball, glacialWave, newAbilities, newAbilityCards);
            List<CardData> weaponCards = CreateWeaponCards();
            UpdateAbilityCatalog(newAbilities);
            List<SynergyData> allSynergies = CreateSynergiesAndCatalog();

            // 5. Achievements, wave config, talent tree
            CreateOrUpdateAchievements();
            UpdateWaveConfig();
            CreateTalentTree();

            ConfigurePickupMotion();

            // 6. Icons (abilities, cards, heroes, achievements, talent nodes)
            VoxelIconGenerator.GenerateAllIcons();

            // 7. Scenes
            WireHeroPrefab(allSynergies);
            WireGameplayScene(newAbilityCards, weaponCards, allSynergies);
            BuildMainMenuConstellationUI();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint11SetupTool] Sprint 11 setup completed (models, heroes, abilities, synergies, cards, talent tree, achievements, scenes).");
        }

        /// <summary>
        /// XP crystals and coins: VoxelLit with vertex bob + spin (_PICKUP_MOTION) so collectibles read as
        /// interactive against static background props. Coin base/glow materials share identical motion values
        /// so both sub-meshes stay in sync.
        /// </summary>
        public static void ConfigurePickupMotion()
        {
            ConfigurePickupMaterial($"{VOXEL_MAT_DIR}/XpCrystal_Glow_Mat.mat", bobAmplitude: 0.12f, bobSpeed: 2.8f, spinSpeed: 70f);
            ConfigurePickupMaterial($"{VOXEL_MAT_DIR}/CoinPickup_Mat.mat", bobAmplitude: 0.09f, bobSpeed: 3.2f, spinSpeed: 150f);
            ConfigurePickupMaterial($"{VOXEL_MAT_DIR}/CoinPickup_Glow_Mat.mat", bobAmplitude: 0.09f, bobSpeed: 3.2f, spinSpeed: 150f);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigurePickupMaterial(string path, float bobAmplitude, float bobSpeed, float spinSpeed)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader voxelLit = Shader.Find(SHADER_VOXEL_LIT);
            if (mat == null || voxelLit == null)
            {
                Debug.LogError($"[Sprint11SetupTool] Cannot configure pickup motion for {path}");
                return;
            }

            bool emissive = mat.IsKeywordEnabled("_EMISSION");
            Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            Color emission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            if (mat.shader != voxelLit) mat.shader = voxelLit;
            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
                if (emissive) mat.SetTexture("_EmissionMap", baseMap);
            }
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_HeightAO", 0f);
            mat.SetFloat("_Smoothness", 0.3f);
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            mat.SetFloat("_PickupMotion", 1f);
            mat.EnableKeyword("_PICKUP_MOTION");
            mat.SetFloat("_BobAmplitude", bobAmplitude);
            mat.SetFloat("_BobSpeed", bobSpeed);
            mat.SetFloat("_SpinSpeed", spinSpeed);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        [MenuItem("ProjectB/Debug/Grant 25 Boss Shards")]
        public static void GrantDebugShards()
        {
            string json = PlayerPrefs.GetString(SaveManager.SAVE_KEY, string.Empty);
            SaveData data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            data.bossShards += 25;
            data.totalBossShardsEarned += 25;
            PlayerPrefs.SetString(SaveManager.SAVE_KEY, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Debug.Log($"[Sprint11SetupTool] Boss Shards: {data.bossShards}");
        }

        // ------------------------------------------------------------------ Folders / materials

        private static void EnsureFolders()
        {
            string[] folders = { VOXEL_MAT_DIR, HERO_PREFAB_DIR, PROJ_PREFAB_DIR, ABILITY_PREFAB_DIR, ABILITY_DIR, CARDS_DIR, SYNERGIES_DIR, TALENTS_DIR, ACH_DIR, "Assets/_UI" };
            foreach (string f in folders) EnsureFolder(f);
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static Texture2D ConfigurePaletteTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static (Material baseMat, Material glowMat) VoxelPair(string prefix, string palettePath, Color emission)
        {
            Texture2D tex = ConfigurePaletteTexture(palettePath);
            Material baseMat = VoxelLitMaterial($"{VOXEL_MAT_DIR}/{prefix}_Mat.mat", tex, false, Color.black);
            Material glowMat = VoxelLitMaterial($"{VOXEL_MAT_DIR}/{prefix}_Glow_Mat.mat", tex, true, emission);
            return (baseMat, glowMat);
        }

        private static Material VoxelLitMaterial(string matPath, Texture2D tex, bool isGlow, Color emissionColor)
        {
            Shader shader = Shader.Find(SHADER_VOXEL_LIT) ?? Shader.Find(SHADER_URP_LIT);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.14f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_HeightAO")) mat.SetFloat("_HeightAO", 0.25f);
            if (mat.HasProperty("_HeightAORange")) mat.SetFloat("_HeightAORange", 0.75f);
            if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", 2.6f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", Color.clear);
            if (mat.HasProperty("_HitFlash")) mat.SetFloat("_HitFlash", 0f);

            if (isGlow)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (tex != null && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material TransparentUnlit(string matPath, Color color)
        {
            Shader shader = Shader.Find(SHADER_URP_UNLIT);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh LoadMesh(string objPath)
        {
            AssetDatabase.ImportAsset(objPath, ImportAssetOptions.ForceSynchronousImport);
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(objPath);
            if (m != null) return m;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(objPath))
            {
                if (o is Mesh mesh) return mesh;
            }
            Debug.LogError($"[Sprint11SetupTool] Missing Mesh at {objPath}");
            return null;
        }

        private static void AssignMaterials(MeshRenderer mr, Mesh mesh, Material baseMat, Material glowMat)
        {
            int sub = mesh != null ? Mathf.Max(1, mesh.subMeshCount) : 1;
            if (sub >= 2 && glowMat != null) mr.sharedMaterials = new[] { baseMat, glowMat };
            else mr.sharedMaterials = new[] { baseMat != null ? baseMat : glowMat };
        }

        // ------------------------------------------------------------------ Prefabs

        private static GameObject BuildSimpleVisualPrefab(string path, string name, string objPath, (Material baseMat, Material glowMat) mats, System.Type component, Vector3 visualScale)
        {
            var root = new GameObject(name) { layer = HERO_PROJECTILE_LAYER };
            try
            {
                if (component != null) root.AddComponent(component);
                var visuals = new GameObject("Visuals") { layer = HERO_PROJECTILE_LAYER };
                visuals.transform.SetParent(root.transform, false);
                visuals.transform.localScale = visualScale;
                Mesh mesh = LoadMesh(objPath);
                visuals.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = visuals.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                AssignMaterials(mr, mesh, mats.baseMat, mats.glowMat);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildRingPrefab(string path, string name, string objPath, Material primary, Material secondary)
        {
            var root = new GameObject(name);
            try
            {
                var visuals = new GameObject("Visuals");
                visuals.transform.SetParent(root.transform, false);
                Mesh mesh = LoadMesh(objPath);
                visuals.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = visuals.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                int sub = mesh != null ? Mathf.Max(1, mesh.subMeshCount) : 1;
                if (sub >= 2) mr.sharedMaterials = new[] { primary, secondary != null ? secondary : primary };
                else mr.sharedMaterials = new[] { secondary != null ? secondary : primary };
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ ScriptableObjects

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static LayerMask EnemyMask()
        {
            int layer = LayerMask.NameToLayer("Enemy");
            return 1 << (layer >= 0 ? layer : 8);
        }

        private static void CreateAbilities(
            GameObject chakramDisc, GameObject spireVisual, GameObject spireTelegraph, GameObject decoyPrefab,
            GameObject phantomFireball, GameObject glacialWave, List<AbilityData> abilitiesOut, List<CardData> cardsOut)
        {
            LayerMask enemies = EnemyMask();

            // Buzzsaw Chakram (Rare, unlocked)
            var chakram = LoadOrCreate<BuzzsawChakramAbilityData>($"{ABILITY_DIR}/BuzzsawChakramData.asset");
            chakram.id = "BuzzsawChakram";
            chakram.abilityName = "Buzzsaw Chakram";
            chakram.description = "Hurls a ricocheting voxel disc that bounces between 3 enemies.";
            chakram.rarity = CardRarity.Rare;
            chakram.unlockedByDefault = true;
            chakram.baseDamage = 16f;
            chakram.baseCooldown = 1.8f;
            chakram.targetLayer = enemies;
            chakram.discPrefab = chakramDisc;
            chakram.discCount = 1;
            chakram.ricochets = 3;
            chakram.ricochetRange = 5.5f;
            chakram.discSpeed = 15f;
            chakram.searchRadius = 9f;
            EditorUtility.SetDirty(chakram);

            // Skyfall Spire (Epic, achievement Clear3Shrines)
            var spire = LoadOrCreate<SkyfallSpireAbilityData>($"{ABILITY_DIR}/SkyfallSpireData.asset");
            spire.id = "SkyfallSpire";
            spire.abilityName = "Skyfall Spire";
            spire.description = "Drops a pillar of fire onto the densest group of enemies (3.2m blast).";
            spire.rarity = CardRarity.Epic;
            spire.unlockedByDefault = false;
            spire.baseDamage = 70f;
            spire.baseCooldown = 4.2f;
            spire.targetLayer = enemies;
            spire.spirePrefab = spireVisual;
            spire.telegraphPrefab = spireTelegraph;
            spire.strikeCount = 1;
            spire.strikeRadius = 3.2f;
            spire.telegraphDelay = 0.7f;
            spire.searchRadius = 12f;
            spire.clusterRadius = 3f;
            EditorUtility.SetDirty(spire);

            // Glacial Shatter (Rare, unlocked)
            var glacial = LoadOrCreate<GlacialShatterAbilityData>($"{ABILITY_DIR}/GlacialShatterData.asset");
            glacial.id = "GlacialShatter";
            glacial.abilityName = "Glacial Shatter";
            glacial.description = "A frost wave freezes nearby enemies; still-frozen enemies shatter into shrapnel.";
            glacial.rarity = CardRarity.Rare;
            glacial.unlockedByDefault = true;
            glacial.baseDamage = 12f;
            glacial.baseCooldown = 5f;
            glacial.targetLayer = enemies;
            glacial.waveVisualPrefab = glacialWave;
            glacial.waveRadius = 5f;
            glacial.freezeDuration = 1.4f;
            glacial.shatterDamageMultiplier = 1.5f;
            glacial.shrapnelRadius = 1.8f;
            glacial.shrapnelDamageFactor = 0.5f;
            EditorUtility.SetDirty(glacial);

            // Shadow Decoy (Epic, achievement Combo50)
            var decoy = LoadOrCreate<ShadowDecoyAbilityData>($"{ABILITY_DIR}/ShadowDecoyData.asset");
            decoy.id = "ShadowDecoy";
            decoy.abilityName = "Shadow Decoy";
            decoy.description = "Deploys a hologram that taunts nearby enemies and explodes after 3.5s.";
            decoy.rarity = CardRarity.Epic;
            decoy.unlockedByDefault = false;
            decoy.baseDamage = 55f;
            decoy.baseCooldown = 7f;
            decoy.targetLayer = enemies;
            decoy.decoyPrefab = decoyPrefab;
            decoy.decoyLifetime = 3.5f;
            decoy.tauntRadius = 7f;
            decoy.explosionRadius = 3.5f;
            decoy.phantomFireballPrefab = phantomFireball;
            EditorUtility.SetDirty(decoy);

            // Vampiric Fang (Epic passive, achievement ReachWave35)
            var fang = LoadOrCreate<StatPassiveAbilityData>($"{ABILITY_DIR}/VampiricFangData.asset");
            fang.id = "VampiricFang";
            fang.abilityName = "Vampiric Fang";
            fang.description = "Heal 2 HP for every 8 enemies slain.";
            fang.rarity = CardRarity.Epic;
            fang.unlockedByDefault = false;
            fang.effectType = PassiveEffectType.HealOnKills;
            fang.baseValue = 2f;
            fang.killsPerTrigger = 8;
            EditorUtility.SetDirty(fang);

            // Area Tome (Rare passive, unlocked)
            var tome = LoadOrCreate<StatPassiveAbilityData>($"{ABILITY_DIR}/AreaTomeData.asset");
            tome.id = "AreaTome";
            tome.abilityName = "Area Tome";
            tome.description = "+12% radius / area for all abilities, weapon splash and ultimates.";
            tome.rarity = CardRarity.Rare;
            tome.unlockedByDefault = true;
            tome.effectType = PassiveEffectType.AreaPercent;
            tome.baseValue = 12f;
            EditorUtility.SetDirty(tome);

            abilitiesOut.AddRange(new AbilityData[] { chakram, spire, glacial, decoy, fang, tome });

            cardsOut.Add(AbilityCard("BuzzsawChakramCard", chakram, "+Buzzsaw Chakram", "Ricocheting disc bouncing between 3 enemies.", CardRarity.Rare, new List<CardData>
            {
                Mod("BuzzsawChakramMod_Damage", "BuzzsawChakram", "Chakram: Sharpened Edge", "+8 damage", CardRarity.Common, ModifierType.Damage, 8f),
                Mod("BuzzsawChakramMod_Ricochet", "BuzzsawChakram", "Chakram: Extra Bounce", "+1 ricochet", CardRarity.Rare, ModifierType.PierceCount, 1f),
                Mod("BuzzsawChakramMod_Count", "BuzzsawChakram", "Chakram: Twin Discs", "+1 disc per volley", CardRarity.Epic, ModifierType.Count, 1f),
            }));
            cardsOut.Add(AbilityCard("SkyfallSpireCard", spire, "+Skyfall Spire", "Pillar of fire on the densest enemy group.", CardRarity.Epic, new List<CardData>
            {
                Mod("SkyfallSpireMod_Damage", "SkyfallSpire", "Spire: Solar Core", "+30 damage", CardRarity.Rare, ModifierType.Damage, 30f),
                Mod("SkyfallSpireMod_Radius", "SkyfallSpire", "Spire: Wider Blast", "+0.6m blast radius", CardRarity.Common, ModifierType.Radius, 0.6f),
                Mod("SkyfallSpireMod_Count", "SkyfallSpire", "Spire: Meteor Shower", "+1 spire per cast", CardRarity.Epic, ModifierType.Count, 1f),
            }));
            cardsOut.Add(AbilityCard("GlacialShatterCard", glacial, "+Glacial Shatter", "Freeze wave; frozen enemies shatter into shrapnel.", CardRarity.Rare, new List<CardData>
            {
                Mod("GlacialShatterMod_Damage", "GlacialShatter", "Glacial: Razor Ice", "+7 damage", CardRarity.Common, ModifierType.Damage, 7f),
                Mod("GlacialShatterMod_Radius", "GlacialShatter", "Glacial: Polar Reach", "+0.8m wave radius", CardRarity.Rare, ModifierType.Radius, 0.8f),
                Mod("GlacialShatterMod_Cooldown", "GlacialShatter", "Glacial: Cold Snap", "-0.6s cooldown", CardRarity.Rare, ModifierType.Cooldown, 0.6f),
            }));
            cardsOut.Add(AbilityCard("ShadowDecoyCard", decoy, "+Shadow Decoy", "Taunting hologram that explodes after 3.5s.", CardRarity.Epic, new List<CardData>
            {
                Mod("ShadowDecoyMod_Damage", "ShadowDecoy", "Decoy: Volatile Shadow", "+25 explosion damage", CardRarity.Rare, ModifierType.Damage, 25f),
                Mod("ShadowDecoyMod_Radius", "ShadowDecoy", "Decoy: Dark Burst", "+0.8m explosion radius", CardRarity.Common, ModifierType.Radius, 0.8f),
                Mod("ShadowDecoyMod_Count", "ShadowDecoy", "Decoy: Mirror Image", "+1 decoy", CardRarity.Epic, ModifierType.Count, 1f),
            }));
            cardsOut.Add(AbilityCard("VampiricFangCard", fang, "+Vampiric Fang", "Heal 2 HP every 8 kills.", CardRarity.Epic, new List<CardData>
            {
                Mod("VampiricFangMod", "VampiricFang", "Fang: Bloodthirst", "+1 HP per 8 kills", CardRarity.Rare, ModifierType.Damage, 1f),
            }));
            cardsOut.Add(AbilityCard("AreaTomeCard", tome, "+Area Tome", "+12% global radius / area.", CardRarity.Rare, new List<CardData>
            {
                Mod("AreaTomeMod", "AreaTome", "Tome: Expanded Margins", "+6% global area", CardRarity.Rare, ModifierType.Area, 6f),
            }));
        }

        private static AbilityCardData AbilityCard(string asset, AbilityData data, string name, string desc, CardRarity rarity, List<CardData> mods)
        {
            var card = LoadOrCreate<AbilityCardData>($"{CARDS_DIR}/{asset}.asset");
            card.cardName = name;
            card.description = desc;
            card.rarity = rarity;
            card.isConsumable = true;
            card.abilityData = data;
            card.unlockedModifiers = mods;
            EditorUtility.SetDirty(card);
            return card;
        }

        private static AbilityModifierCardData Mod(string asset, string abilityId, string name, string desc, CardRarity rarity, ModifierType type, float value)
        {
            var mod = LoadOrCreate<AbilityModifierCardData>($"{CARDS_DIR}/{asset}.asset");
            mod.cardName = name;
            mod.description = desc;
            mod.rarity = rarity;
            mod.isConsumable = false;
            mod.targetAbilityId = abilityId;
            mod.modifierType = type;
            mod.value = value;
            mod.hasSecondaryModifier = false;
            EditorUtility.SetDirty(mod);
            return mod;
        }

        private static List<CardData> CreateWeaponCards()
        {
            var list = new List<CardData>
            {
                WeaponCard("WeaponMod_Multishot", "Weapon: Multishot", "+1 projectile fired in a fan at another target", CardRarity.Epic, WeaponUpgradeType.Multishot, 1f, 4),
                WeaponCard("WeaponMod_FireRate", "Weapon: Rapid Fire", "+20% weapon attack speed", CardRarity.Rare, WeaponUpgradeType.FireRate, 20f, 0),
                WeaponCard("WeaponMod_Power", "Weapon: Overcharge", "+30% weapon damage", CardRarity.Common, WeaponUpgradeType.Power, 30f, 0),
            };
            return list;
        }

        private static WeaponUpgradeCardData WeaponCard(string asset, string name, string desc, CardRarity rarity, WeaponUpgradeType type, float value, int maxStacks)
        {
            var card = LoadOrCreate<WeaponUpgradeCardData>($"{CARDS_DIR}/{asset}.asset");
            card.cardName = name;
            card.description = desc;
            card.rarity = rarity;
            card.isConsumable = false;
            card.upgradeType = type;
            card.value = value;
            card.maxStacks = maxStacks;
            EditorUtility.SetDirty(card);
            return card;
        }

        private static void UpdateAbilityCatalog(List<AbilityData> abilities)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(ABILITY_CATALOG_PATH);
            if (catalog == null) return;
            foreach (var a in abilities)
            {
                if (a != null && !catalog.abilities.Contains(a)) catalog.abilities.Add(a);
            }
            EditorUtility.SetDirty(catalog);
        }

        private static List<SynergyData> CreateSynergiesAndCatalog()
        {
            CreateSynergy("Synergy_ShatterNova", SynergyIds.ShatterNova, "Shatter Nova", CardRarity.Legendary,
                "Seismic Hammer slams on frozen enemies deal 2.5x damage and burst them into 4 ice shards that strike neighbours.",
                "GlacialShatter", "EarthquakeSlam");
            CreateSynergy("Synergy_RicochetBarrage", SynergyIds.RicochetBarrage, "Ricochet Barrage", CardRarity.Epic,
                "Every critical hit of a Buzzsaw Chakram spawns a shrapnel mini-disc (2 ricochets) and speeds up the main disc.",
                "BuzzsawChakram", "CriticalLens");
            CreateSynergy("Synergy_SolarEclipse", SynergyIds.SolarEclipse, "Solar Eclipse", CardRarity.Legendary,
                "0.3s before a Skyfall Spire lands, an eclipse pulse drags enemies into the strike point; the strike deals +35% damage and blinds survivors.",
                "SkyfallSpire", "BlackHole");
            CreateSynergy("Synergy_PhantomLegion", SynergyIds.PhantomLegion, "Phantom Legion", CardRarity.Legendary,
                "When a Shadow Decoy detonates it explodes into a ring of 8 dragon fireballs flying in every direction.",
                "ShadowDecoy", "MiniDragon");

            var catalog = AssetDatabase.LoadAssetAtPath<SynergyCatalog>(SYNERGY_CATALOG_PATH);
            var all = new List<SynergyData>();
            foreach (string guid in AssetDatabase.FindAssets("t:SynergyData", new[] { SYNERGIES_DIR }))
            {
                var syn = AssetDatabase.LoadAssetAtPath<SynergyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (syn != null && !string.IsNullOrEmpty(syn.id)) all.Add(syn);
            }
            all.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (catalog != null)
            {
                foreach (var syn in all)
                {
                    if (!catalog.synergies.Contains(syn)) catalog.synergies.Add(syn);
                }
                EditorUtility.SetDirty(catalog);
                return new List<SynergyData>(catalog.synergies);
            }
            return all;
        }

        private static SynergyData CreateSynergy(string asset, string id, string displayName, CardRarity rarity, string desc, string abilityA, string abilityB)
        {
            var syn = LoadOrCreate<SynergyData>($"{SYNERGIES_DIR}/{asset}.asset");
            syn.id = id;
            syn.synergyName = displayName;
            syn.description = desc;
            syn.rarity = rarity;
            syn.requiredAbilityIds = new List<string> { abilityA, abilityB };
            EditorUtility.SetDirty(syn);
            return syn;
        }

        private static void CreateOrUpdateAchievements()
        {
            Achievement("Clear3Shrines", "clear_3_shrines", "Shrine Seeker", "Clear 3 Challenge Shrines (cumulative) to unlock Skyfall Spire",
                AchievementConditionType.ClearShrines, 3, AchievementRewardType.UnlockAbility, "SkyfallSpire");
            Achievement("Combo50", "combo_50", "Combo Artist", "Reach a x50 kill combo to unlock Shadow Decoy",
                AchievementConditionType.ReachCombo, 50, AchievementRewardType.UnlockAbility, "ShadowDecoy");
            Achievement("ReachWave35", "reach_wave_35", "Night Stalker", "Reach Wave 35 to unlock Vampiric Fang",
                AchievementConditionType.ReachWave, 35, AchievementRewardType.UnlockAbility, "VampiricFang");

        }

        private static void Achievement(string asset, string id, string title, string desc, AchievementConditionType cond, int target, AchievementRewardType reward, string rewardId)
        {
            var a = LoadOrCreate<AchievementData>($"{ACH_DIR}/{asset}.asset");
            a.id = id;
            a.title = title;
            a.description = desc;
            a.conditionType = cond;
            a.targetValue = target;
            a.targetId = string.Empty;
            a.rewardType = reward;
            a.rewardId = rewardId;
            a.rewardValue = 0;
            EditorUtility.SetDirty(a);
        }

        private static void UpdateWaveConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>($"{SO_DIR}/WaveConfig.asset");
            if (config == null) return;
            config.miniBossShardReward = 1;
            config.bossShardReward = 5;
            config.shrineShardReward = 2;
            EditorUtility.SetDirty(config);
        }

        // ------------------------------------------------------------------ Talent tree

        private static Vector2 Polar(float degrees, float radius)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
        }

        private static TalentNodeData Node(
            List<TalentNodeData> nodes, string id, string name, string desc, TalentNodeType type, TalentConstellation c,
            Vector2 pos, int shards, int coins, string[] prereqs, string synergyId, params TalentEffect[] effects)
        {
            var n = LoadOrCreate<TalentNodeData>($"{TALENTS_DIR}/{id}.asset");
            n.id = id;
            n.displayName = name;
            n.description = desc;
            n.nodeType = type;
            n.constellation = c;
            n.position = pos;
            n.shardCost = shards;
            n.coinCost = coins;
            n.prerequisiteIds = new List<string>(prereqs ?? new string[0]);
            n.synergyId = synergyId ?? string.Empty;
            n.effects = new List<TalentEffect>(effects ?? new TalentEffect[0]);
            EditorUtility.SetDirty(n);
            nodes.Add(n);
            return n;
        }

        private static TalentEffect E(TalentEffectType t, float v) => new TalentEffect(t, v);

        private static string SynergyDesc(string synergyId, string fallback)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SynergyData", new[] { SYNERGIES_DIR }))
            {
                var syn = AssetDatabase.LoadAssetAtPath<SynergyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (syn != null && syn.id == synergyId)
                {
                    string pair = syn.requiredAbilityIds != null ? string.Join(" + ", syn.requiredAbilityIds) : string.Empty;
                    return $"Unlocks the reactive synergy <b>{syn.synergyName}</b> ({pair}). Auto-activates in a run once both abilities are equipped.\n{syn.description}";
                }
            }
            return fallback;
        }

        private static void CreateTalentTree()
        {
            var nodes = new List<TalentNodeData>();

            Node(nodes, "origin", "Constellation Core", "The heart of your star map. Every constellation grows outward from here. Defeat mini-bosses (+1), bosses (+5) and clear Shrines (+2) to earn Boss Shards.",
                TalentNodeType.Origin, TalentConstellation.Core, Vector2.zero, 0, 0, null, null);

            BuildConstellation(nodes, TalentConstellation.Fury, "fury", 90f,
                ("+6% Crit Chance", "Permanently +6% critical hit chance.", E(TalentEffectType.CritChancePercent, 6f)),
                ("+5% Damage", "Permanently +5% damage for weapon and abilities.", E(TalentEffectType.GlobalDamagePercent, 5f)),
                ("+10% Global Area", "+10% radius / area for abilities, weapon splash and ultimates.", E(TalentEffectType.GlobalAreaPercent, 10f)),
                new[] { SynergyIds.ToxicInferno, SynergyIds.DragonLord, SynergyIds.BladeStorm, SynergyIds.RicochetBarrage },
                ("+15% Boss Damage", "+15% damage against bosses and mini-bosses.", E(TalentEffectType.BossDamagePercent, 15f)),
                ("+10% Cooldown Reduction", "Abilities, weapon and ultimate recharge 10% faster.", E(TalentEffectType.CooldownReductionPercent, 10f)),
                ("Overclocked Arsenal", "KEYSTONE: +1 Active ability slot and an extra -8% cooldown on everything.",
                    new[] { E(TalentEffectType.BonusActiveSlots, 1f), E(TalentEffectType.CooldownReductionPercent, 8f) }));

            BuildConstellation(nodes, TalentConstellation.Bastion, "bastion", 210f,
                ("+15 Armor", "Permanently +15 armor.", E(TalentEffectType.ArmorFlat, 15f)),
                ("+15% Healing", "All healing you receive is 15% stronger.", E(TalentEffectType.HealingPercent, 15f)),
                ("+8% Max HP", "Permanently +8% maximum health.", E(TalentEffectType.MaxHpPercent, 8f)),
                new[] { SynergyIds.SteamBlast, SynergyIds.Thunderstorm, SynergyIds.AegisPhalanx, SynergyIds.ShatterNova },
                ("+0.15s I-Frames", "Invulnerability after taking a hit lasts 0.15s longer.", E(TalentEffectType.IFrameSeconds, 0.15f)),
                ("+25% Ultimate Recharge", "Your hero ultimate recharges 25% faster.", E(TalentEffectType.UltimateCooldownPercent, 25f)),
                ("Second Wind", "KEYSTONE: once per run survive a lethal hit with 30% HP, 2.5s of invulnerability and a knockback wave.",
                    new[] { E(TalentEffectType.SecondWind, 1f) }));

            BuildConstellation(nodes, TalentConstellation.Fortune, "fortune", 330f,
                ("+20% Coin Gain", "Permanently +20% coins collected in runs.", E(TalentEffectType.CoinGainPercent, 20f)),
                ("+8% XP Gain", "Permanently +8% experience gained.", E(TalentEffectType.XpGainPercent, 8f)),
                ("+1 Free Reroll", "One extra free card reroll every run.", E(TalentEffectType.FreeRerolls, 1f)),
                new[] { SynergyIds.OverchargedGrid, SynergyIds.CataclysmVortex, SynergyIds.SolarEclipse, SynergyIds.PhantomLegion },
                ("Barrel Respawn +2", "+2 extra explosive barrels each time the arena restocks.", E(TalentEffectType.BarrelRespawnBonus, 2f)),
                ("Secret & Shrine +30%", "+30% coins from secret chests, crystals and Challenge Shrines.", E(TalentEffectType.SecretAndShrineRewardPercent, 30f)),
                ("Collector's Greed", "KEYSTONE: +1 Passive ability slot and +5 coins for every 25 XP crystals collected.",
                    new[] { E(TalentEffectType.BonusPassiveSlots, 1f), E(TalentEffectType.CrystalCoinBonus, 5f) }));

            // Cross bridges between constellations
            Node(nodes, "bridge_fury_bastion", "+6% Move Speed", "Bridge star: permanently +6% movement speed.", TalentNodeType.Stat, TalentConstellation.Core,
                Polar(150f, 560f), 2, 0, new[] { "fury_r2", "bastion_r2" }, null, E(TalentEffectType.MoveSpeedPercent, 6f));
            Node(nodes, "bridge_bastion_fortune", "+6% Max HP", "Bridge star: permanently +6% maximum health.", TalentNodeType.Stat, TalentConstellation.Core,
                Polar(270f, 560f), 2, 0, new[] { "bastion_r2", "fortune_r2" }, null, E(TalentEffectType.MaxHpPercent, 6f));
            Node(nodes, "bridge_fortune_fury", "+4% Crit Chance", "Bridge star: permanently +4% critical hit chance.", TalentNodeType.Stat, TalentConstellation.Core,
                Polar(30f, 560f), 2, 0, new[] { "fortune_r2", "fury_r2" }, null, E(TalentEffectType.CritChancePercent, 4f));

            var tree = LoadOrCreate<TalentTreeData>(TALENT_TREE_PATH);
            tree.canvasSize = 2200f;
            tree.nodes = nodes;
            EditorUtility.SetDirty(tree);

            // Remove stale node assets that are no longer part of the tree
            var keep = new HashSet<string>();
            foreach (var n in nodes) keep.Add(AssetDatabase.GetAssetPath(n));
            foreach (string guid in AssetDatabase.FindAssets("t:TalentNodeData", new[] { TALENTS_DIR }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!keep.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        private static void BuildConstellation(
            List<TalentNodeData> nodes, TalentConstellation c, string prefix, float angle,
            (string name, string desc, TalentEffect effect) r1a,
            (string name, string desc, TalentEffect effect) r1b,
            (string name, string desc, TalentEffect effect) r2,
            string[] synergyIds,
            (string name, string desc, TalentEffect effect) r4a,
            (string name, string desc, TalentEffect effect) r4b,
            (string name, string desc, TalentEffect[] effects) keystone)
        {
            Node(nodes, $"{prefix}_r1a", r1a.name, r1a.desc + " (Inner ring: also purchasable with coins.)", TalentNodeType.Stat, c, Polar(angle - 15f, 240f), 1, 150, null, null, r1a.effect);
            Node(nodes, $"{prefix}_r1b", r1b.name, r1b.desc + " (Inner ring: also purchasable with coins.)", TalentNodeType.Stat, c, Polar(angle + 15f, 240f), 1, 150, null, null, r1b.effect);
            Node(nodes, $"{prefix}_r2", r2.name, r2.desc, TalentNodeType.Stat, c, Polar(angle, 440f), 2, 0, new[] { $"{prefix}_r1a", $"{prefix}_r1b" }, null, r2.effect);

            float[] synAngles = { -33f, -11f, 11f, 33f };
            var synIds = new string[synergyIds.Length];
            for (int i = 0; i < synergyIds.Length; i++)
            {
                string id = $"{prefix}_syn_{synergyIds[i]}";
                synIds[i] = id;
                string title = SynergyTitle(synergyIds[i]);
                Node(nodes, id, title, SynergyDesc(synergyIds[i], $"Unlocks the reactive synergy {title}."), TalentNodeType.Synergy, c,
                    Polar(angle + synAngles[i], 720f), 3, 0, new[] { $"{prefix}_r2" }, synergyIds[i]);
            }

            Node(nodes, $"{prefix}_r4a", r4a.name, r4a.desc, TalentNodeType.Stat, c, Polar(angle - 13f, 900f), 4, 0, new[] { synIds[0], synIds[1] }, null, r4a.effect);
            Node(nodes, $"{prefix}_r4b", r4b.name, r4b.desc, TalentNodeType.Stat, c, Polar(angle + 13f, 900f), 4, 0, new[] { synIds[2], synIds[3] }, null, r4b.effect);
            Node(nodes, $"{prefix}_keystone", keystone.name, keystone.desc, TalentNodeType.Keystone, c, Polar(angle, 1010f), 8, 0, new[] { $"{prefix}_r4a", $"{prefix}_r4b" }, null, keystone.effects);
        }

        private static string SynergyTitle(string id)
        {
            return id switch
            {
                SynergyIds.ToxicInferno => "Toxic Inferno",
                SynergyIds.SteamBlast => "Steam Blast",
                SynergyIds.Thunderstorm => "Thunderstorm",
                SynergyIds.OverchargedGrid => "Overcharged Grid",
                SynergyIds.BladeStorm => "Blade Storm",
                SynergyIds.AegisPhalanx => "Aegis Phalanx",
                SynergyIds.DragonLord => "Dragon Lord",
                SynergyIds.CataclysmVortex => "Cataclysm Vortex",
                SynergyIds.ShatterNova => "Shatter Nova",
                SynergyIds.RicochetBarrage => "Ricochet Barrage",
                SynergyIds.SolarEclipse => "Solar Eclipse",
                SynergyIds.PhantomLegion => "Phantom Legion",
                _ => id
            };
        }

        // ------------------------------------------------------------------ Scene wiring

        private static void WriteSynergyList(SerializedProperty prop, List<SynergyData> list)
        {
            if (prop == null) return;
            prop.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        }

        private static void WireHeroPrefab(List<SynergyData> synergies)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HERO_PREFAB_PATH);
            if (root == null) return;
            try
            {
                var heroAbilities = root.GetComponent<HeroAbilities>();
                if (heroAbilities != null)
                {
                    var so = new SerializedObject(heroAbilities);
                    WriteSynergyList(so.FindProperty("availableSynergies"), synergies);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, HERO_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireGameplayScene(List<CardData> abilityCards, List<CardData> weaponCards, List<SynergyData> synergies)
        {
            Scene scene = EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Single);
            var upgrade = Object.FindAnyObjectByType<UpgradeManager>(FindObjectsInactive.Include);
            if (upgrade != null)
            {
                if (upgrade.cardPool == null) upgrade.cardPool = new List<CardData>();
                foreach (var c in abilityCards) if (c != null && !upgrade.cardPool.Contains(c)) upgrade.cardPool.Add(c);
                foreach (var c in weaponCards) if (c != null && !upgrade.cardPool.Contains(c)) upgrade.cardPool.Add(c);
                if (upgrade.synergies == null) upgrade.synergies = new List<SynergyData>();
                foreach (var s in synergies) if (s != null && !upgrade.synergies.Contains(s)) upgrade.synergies.Add(s);
                EditorUtility.SetDirty(upgrade);
            }
            else
            {
                Debug.LogError("[Sprint11SetupTool] UpgradeManager not found in Gameplay scene.");
            }

            var heroAbilities = Object.FindAnyObjectByType<HeroAbilities>(FindObjectsInactive.Include);
            if (heroAbilities != null)
            {
                var so = new SerializedObject(heroAbilities);
                WriteSynergyList(so.FindProperty("availableSynergies"), synergies);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(heroAbilities);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------ Main menu UI

        private static Sprite EnsureGeneratedSprite(string path, int size, bool halo)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), path);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a;
                    if (halo) a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                    else a = Mathf.Clamp01((1f - d) * c * 0.5f); // crisp anti-aliased disc
                    byte v = 255;
                    px[y * size + x] = new Color32(v, v, v, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static RectTransform UiObject(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Image AddImage(RectTransform rt, Sprite sprite, Color color, bool raycast)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        private static TextMeshProUGUI AddText(RectTransform rt, TMP_FontAsset font, string text, float size, TextAlignmentOptions align, Color color)
        {
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static Button MakeButton(Transform parent, string name, TMP_FontAsset font, Sprite sprite, string label, float fontSize, Color bg,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, out TextMeshProUGUI labelText)
        {
            RectTransform rt = UiObject(name, parent);
            Anchor(rt, anchor, pivot, pos, size);
            Image img = AddImage(rt, sprite, bg, true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            RectTransform t = UiObject("Text", rt);
            Stretch(t, Vector2.zero, Vector2.zero);
            labelText = AddText(t, font, label, fontSize, TextAlignmentOptions.Center, Color.white);
            labelText.fontStyle = FontStyles.Bold;
            return btn;
        }
        public static void BuildMainMenuConstellationUI()
        {
            Scene scene = EditorSceneManager.OpenScene(MAINMENU_SCENE_PATH, OpenSceneMode.Single);
            var menu = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
            var metaUi = Object.FindAnyObjectByType<MetaUpgradeUI>(FindObjectsInactive.Include);
            if (menu == null || metaUi == null)
            {
                Debug.LogError("[Sprint11SetupTool] MainMenuUI / MetaUpgradeUI not found in MainMenu scene.");
                return;
            }

            Transform upgradesPanel = null;
            var metaSo = new SerializedObject(metaUi);
            var panelObj = metaSo.FindProperty("panel").objectReferenceValue as GameObject;
            upgradesPanel = panelObj != null ? panelObj.transform : metaUi.transform;
            Transform safeRoot = upgradesPanel.parent;

            TMP_FontAsset font = null;
            var anyTmp = Object.FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            if (anyTmp != null) font = anyTmp.font;

            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(WHITE_SPRITE_PATH);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_UI/tab_bg_rounded.png") ?? white;
            Sprite nodeSprite = EnsureGeneratedSprite(NODE_SPRITE_PATH, 128, halo: false);
            Sprite haloSprite = EnsureGeneratedSprite(HALO_SPRITE_PATH, 128, halo: true);

            // --- Entry button on the Upgrades panel
            Transform title = upgradesPanel.Find("Title");
            Transform buttonParent = title != null ? title : upgradesPanel;
            Button openButton = MakeButton(buttonParent, "ConstellationButton", font, rounded, "STARS", 30f, new Color(0.45f, 0.28f, 0.85f, 1f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(230f, 90f), out _);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(openButton.onClick, menu.OnConstellationClicked);

            // --- Constellation root (always active, holds the component; panel child toggles)
            var upRt = (RectTransform)upgradesPanel;
            RectTransform root = UiObject("ConstellationScreen", safeRoot);
            root.anchorMin = upRt.anchorMin;
            root.anchorMax = upRt.anchorMax;
            root.pivot = upRt.pivot;
            root.anchoredPosition = upRt.anchoredPosition;
            root.sizeDelta = upRt.sizeDelta;
            root.SetSiblingIndex(upgradesPanel.GetSiblingIndex() + 1);

            RectTransform panel = UiObject("ConstellationPanel", root);
            Stretch(panel, Vector2.zero, Vector2.zero);
            AddImage(panel, white, new Color(0.035f, 0.045f, 0.10f, 0.98f), true);

            // Viewport + content
            RectTransform viewport = UiObject("Viewport", panel);
            Stretch(viewport, new Vector2(0f, 0f), new Vector2(0f, -170f));
            AddImage(viewport, white, new Color(0f, 0f, 0f, 0f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var panZoom = viewport.gameObject.AddComponent<ConstellationPanZoom>();

            RectTransform content = UiObject("StarMap", viewport);
            Anchor(content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200f, 2200f));
            RectTransform stars = UiObject("Stars", content);
            Stretch(stars, Vector2.zero, Vector2.zero);
            stars.anchorMin = stars.anchorMax = new Vector2(0.5f, 0.5f);
            stars.sizeDelta = Vector2.zero;
            RectTransform lines = UiObject("Lines", content);
            lines.anchorMin = lines.anchorMax = new Vector2(0.5f, 0.5f);
            lines.sizeDelta = Vector2.zero;
            RectTransform nodesRoot = UiObject("Nodes", content);
            nodesRoot.anchorMin = nodesRoot.anchorMax = new Vector2(0.5f, 0.5f);
            nodesRoot.sizeDelta = Vector2.zero;

            var pzSo = new SerializedObject(panZoom);
            pzSo.FindProperty("viewport").objectReferenceValue = viewport;
            pzSo.FindProperty("content").objectReferenceValue = content;
            pzSo.ApplyModifiedPropertiesWithoutUndo();

            // Header
            RectTransform header = UiObject("Header", panel);
            Anchor(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 170f));
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.sizeDelta = new Vector2(0f, 170f);
            AddImage(header, white, new Color(0.06f, 0.07f, 0.15f, 1f), true);

            RectTransform titleRt = UiObject("Title", header);
            Anchor(titleRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -14f), new Vector2(620f, 70f));
            AddText(titleRt, font, "CONSTELLATIONS", 52f, TextAlignmentOptions.Left, new Color(1f, 0.86f, 0.4f, 1f)).fontStyle = FontStyles.Bold;

            RectTransform shardsRt = UiObject("ShardsText", header);
            Anchor(shardsRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -86f), new Vector2(680f, 44f));
            TextMeshProUGUI shardsText = AddText(shardsRt, font, "Boss Shards: 0", 32f, TextAlignmentOptions.Left, Color.white);

            RectTransform progressRt = UiObject("ProgressText", header);
            Anchor(progressRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -128f), new Vector2(680f, 36f));
            TextMeshProUGUI progressText = AddText(progressRt, font, "Stars lit: 0/33", 26f, TextAlignmentOptions.Left, new Color(0.7f, 0.75f, 0.9f, 1f));

            Button backButton = MakeButton(header, "BackButton", font, rounded, "UPGRADES", 30f, new Color(0.22f, 0.26f, 0.42f, 1f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(260f, 96f), out _);

            // Zoom controls (right side, one-thumb reachable)
            Button zoomIn = MakeButton(panel, "ZoomInButton", font, rounded, "+", 56f, new Color(0.2f, 0.24f, 0.4f, 0.92f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 600f), new Vector2(110f, 110f), out _);
            Button zoomOut = MakeButton(panel, "ZoomOutButton", font, rounded, "-", 64f, new Color(0.2f, 0.24f, 0.4f, 0.92f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 476f), new Vector2(110f, 110f), out _);
            Button center = MakeButton(panel, "CenterButton", font, rounded, "CORE", 28f, new Color(0.45f, 0.28f, 0.85f, 0.95f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 352f), new Vector2(110f, 110f), out _);

            // Detail sheet (bottom)
            RectTransform sheet = UiObject("DetailSheet", panel);
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0f);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.anchoredPosition = new Vector2(0f, 12f);
            sheet.sizeDelta = new Vector2(-24f, 330f);
            AddImage(sheet, rounded, new Color(0.08f, 0.09f, 0.18f, 0.97f), true);

            RectTransform iconRt = UiObject("Icon", sheet);
            Anchor(iconRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(120f, 120f));
            Image detailIcon = AddImage(iconRt, null, Color.white, false);
            detailIcon.preserveAspect = true;

            RectTransform dTitle = UiObject("Title", sheet);
            Anchor(dTitle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(164f, -20f), new Vector2(680f, 56f));
            TextMeshProUGUI detailTitle = AddText(dTitle, font, "", 40f, TextAlignmentOptions.Left, new Color(1f, 0.88f, 0.45f, 1f));
            detailTitle.fontStyle = FontStyles.Bold;

            RectTransform dType = UiObject("Type", sheet);
            Anchor(dType, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(164f, -76f), new Vector2(680f, 36f));
            TextMeshProUGUI detailType = AddText(dType, font, "", 24f, TextAlignmentOptions.Left, new Color(0.65f, 0.75f, 1f, 1f));

            RectTransform dDesc = UiObject("Description", sheet);
            dDesc.anchorMin = new Vector2(0f, 1f);
            dDesc.anchorMax = new Vector2(1f, 1f);
            dDesc.pivot = new Vector2(0f, 1f);
            dDesc.anchoredPosition = new Vector2(24f, -150f);
            dDesc.sizeDelta = new Vector2(-48f, 80f);
            TextMeshProUGUI detailDesc = AddText(dDesc, font, "", 26f, TextAlignmentOptions.TopLeft, Color.white);
            detailDesc.enableAutoSizing = true;
            detailDesc.fontSizeMin = 18f;
            detailDesc.fontSizeMax = 26f;

            RectTransform dStatus = UiObject("Status", sheet);
            Anchor(dStatus, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(164f, -112f), new Vector2(680f, 34f));
            TextMeshProUGUI detailStatus = AddText(dStatus, font, "", 24f, TextAlignmentOptions.Left, Color.white);

            Button unlock = MakeButton(sheet, "UnlockButton", font, rounded, "UNLOCK", 32f, new Color(0.45f, 0.28f, 0.85f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 20f), new Vector2(520f, 92f), out TextMeshProUGUI unlockText);
            Button unlockCoins = MakeButton(sheet, "UnlockCoinsButton", font, rounded, "150 COINS", 30f, new Color(0.75f, 0.55f, 0.12f, 1f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(300f, 92f), out TextMeshProUGUI unlockCoinsText);
            Button closeDetail = MakeButton(sheet, "CloseButton", font, rounded, "X", 34f, new Color(0.3f, 0.32f, 0.45f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(80f, 80f), out _);
            sheet.gameObject.SetActive(false);

            // Component wiring
            var tree = root.gameObject.AddComponent<ConstellationTreeUI>();
            var so = new SerializedObject(tree);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("panZoom").objectReferenceValue = panZoom;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("starsRoot").objectReferenceValue = stars;
            so.FindProperty("linesRoot").objectReferenceValue = lines;
            so.FindProperty("nodesRoot").objectReferenceValue = nodesRoot;
            so.FindProperty("nodeSprite").objectReferenceValue = nodeSprite;
            so.FindProperty("haloSprite").objectReferenceValue = haloSprite;
            so.FindProperty("lineSprite").objectReferenceValue = white;
            so.FindProperty("font").objectReferenceValue = font;
            so.FindProperty("shardsText").objectReferenceValue = shardsText;
            so.FindProperty("progressText").objectReferenceValue = progressText;
            so.FindProperty("detailSheet").objectReferenceValue = sheet.gameObject;
            so.FindProperty("detailIcon").objectReferenceValue = detailIcon;
            so.FindProperty("detailTitle").objectReferenceValue = detailTitle;
            so.FindProperty("detailType").objectReferenceValue = detailType;
            so.FindProperty("detailDescription").objectReferenceValue = detailDesc;
            so.FindProperty("detailStatus").objectReferenceValue = detailStatus;
            so.FindProperty("unlockButton").objectReferenceValue = unlock;
            so.FindProperty("unlockButtonText").objectReferenceValue = unlockText;
            so.FindProperty("unlockCoinsButton").objectReferenceValue = unlockCoins;
            so.FindProperty("unlockCoinsButtonText").objectReferenceValue = unlockCoinsText;
            so.FindProperty("closeDetailButton").objectReferenceValue = closeDetail;
            so.FindProperty("zoomInButton").objectReferenceValue = zoomIn;
            so.FindProperty("zoomOutButton").objectReferenceValue = zoomOut;
            so.FindProperty("centerButton").objectReferenceValue = center;
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);

            var menuSo = new SerializedObject(menu);
            menuSo.FindProperty("constellationTreeUI").objectReferenceValue = tree;
            menuSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
