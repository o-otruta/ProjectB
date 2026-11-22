using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ProjectB.Arena;
using ProjectB.Combat;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Player;
using ProjectB.Visuals;

namespace ProjectB.EditorScripts
{
    public static class Sprint5SetupTool
    {
        private const int ENEMY_LAYER = 8;
        private const int HERO_LAYER = 9;
        private const int HERO_PROJECTILE_LAYER = 11;
        private const int PICKUPS_LAYER = 13;

        private const string VOXEL_MAT_DIR = "Assets/_Materials/Voxels";
        private const string FX_MAT_DIR = "Assets/_Materials";
        private const string HERO_PREFAB_DIR = "Assets/_Prefabs/Heroes";
        private const string PROJ_PREFAB_DIR = "Assets/_Prefabs/Projectiles";
        private const string PICKUP_PREFAB_DIR = "Assets/_Prefabs/Pickups";
        private const string ARENA_PREFAB_DIR = "Assets/_Prefabs/Arena";
        private const string SO_DIR = "Assets/_ScriptableObjects";
        private const string ACH_DIR = "Assets/Resources/Achievements";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";

        [MenuItem("ProjectB/Setup/Run Sprint 5 Asset & Scene Setup")]
        public static void RunAll()
        {
            EnsureFolder(VOXEL_MAT_DIR);
            EnsureFolder(HERO_PREFAB_DIR);
            EnsureFolder(PROJ_PREFAB_DIR);
            EnsureFolder(PICKUP_PREFAB_DIR);
            EnsureFolder(ARENA_PREFAB_DIR);

            ConfigurePaletteTexture("Assets/_VoxelModels/Ranger/ranger.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/NecromancerHero/necromancer_hero.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/RangerArrow/ranger_arrow.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/SoulBolt/soul_bolt.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/ExplosiveBarrel/explosive_barrel.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/VacuumMagnet/vacuum_magnet.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/ChallengeShrine/challenge_shrine.png");

            var rangerMats = EnsureVoxelMaterialPair("Ranger", "Assets/_VoxelModels/Ranger/ranger.png");
            var necroMats = EnsureVoxelMaterialPair("NecromancerHero", "Assets/_VoxelModels/NecromancerHero/necromancer_hero.png");
            var arrowMats = EnsureVoxelMaterialPair("RangerArrow", "Assets/_VoxelModels/RangerArrow/ranger_arrow.png");
            var soulMats = EnsureVoxelMaterialPair("SoulBolt", "Assets/_VoxelModels/SoulBolt/soul_bolt.png");
            var barrelMats = EnsureVoxelMaterialPair("ExplosiveBarrel", "Assets/_VoxelModels/ExplosiveBarrel/explosive_barrel.png");
            var magnetMats = EnsureVoxelMaterialPair("VacuumMagnet", "Assets/_VoxelModels/VacuumMagnet/vacuum_magnet.png");
            var shrineMats = EnsureVoxelMaterialPair("ChallengeShrine", "Assets/_VoxelModels/ChallengeShrine/challenge_shrine.png");

            Material shrineRingMat = EnsureTransparentUnlitMaterial($"{FX_MAT_DIR}/ShrineRingMat.mat", new Color(0.68f, 0.25f, 0.96f, 0.68f));
            Material shrineBeaconMat = EnsureTransparentUnlitMaterial($"{FX_MAT_DIR}/ShrineBeaconMat.mat", new Color(0.95f, 0.28f, 0.62f, 0.28f));
            Material vacuumBeaconMat = EnsureTransparentUnlitMaterial($"{FX_MAT_DIR}/VacuumBeaconMat.mat", new Color(0.24f, 0.92f, 1f, 0.34f));

            GameObject rangerVisual = BuildHeroVisualPrefab("RangerVisual", "RangerModel", "Ranger", rangerMats);
            GameObject necroVisual = BuildHeroVisualPrefab("NecromancerVisual", "NecromancerModel", "NecromancerHero", necroMats);
            GameObject arrowProjPrefab = BuildProjectilePrefab("RangerArrowProjectile", "Assets/_VoxelModels/RangerArrow/RangerArrow.obj", arrowMats);
            GameObject soulProjPrefab = BuildProjectilePrefab("SoulBoltProjectile", "Assets/_VoxelModels/SoulBolt/SoulBolt.obj", soulMats);

            GameObject barrelPrefab = BuildExplosiveBarrelPrefab(barrelMats);
            GameObject vacuumPrefab = BuildVacuumMagnetPrefab(magnetMats, vacuumBeaconMat);
            GameObject shrinePrefab = BuildChallengeShrinePrefab(shrineMats, shrineRingMat, shrineBeaconMat);

            WireHeroPrefabSoulNovaVisuals(
                LoadObjMesh("Assets/_VoxelModels/SoulBolt/SoulBolt.obj"),
                soulMats.baseMat,
                soulMats.glowMat);

            WeaponData bowWeapon = CreateOrUpdateRapidBowWeapon(arrowProjPrefab);
            WeaponData soulWeapon = CreateOrUpdateSoulBoltWeapon(soulProjPrefab);

            HeroData rangerData = CreateOrUpdateRangerData(bowWeapon, rangerVisual);
            HeroData necroData = CreateOrUpdateNecromancerData(soulWeapon, necroVisual);

            UpdateHeroCatalog(rangerData, necroData);
            UpdateHeroUnlockAchievements();
            UpdateArenaAndWaveConfigs(barrelPrefab, shrinePrefab);
            UpdateGameplayScene(vacuumPrefab);

            VoxelIconGenerator.GenerateAllIcons();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint5SetupTool] Sprint 5 heroes, interactive arena objects, materials, prefabs, SOs, icons, and Gameplay scene wired successfully.");
        }

        private static void ConfigurePaletteTexture(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Sprint5SetupTool] Palette texture not found at {assetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static (Material baseMat, Material glowMat) EnsureVoxelMaterialPair(string prefix, string palettePath)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);

            Material baseMat = LoadOrCreateMaterial($"{VOXEL_MAT_DIR}/{prefix}_Mat.mat", urpLit);
            ConfigureVoxelLitMaterial(baseMat, tex, emissive: false);

            Material glowMat = LoadOrCreateMaterial($"{VOXEL_MAT_DIR}/{prefix}_Glow_Mat.mat", urpLit);
            ConfigureVoxelLitMaterial(glowMat, tex, emissive: true);

            return (baseMat, glowMat);
        }

        private static void ConfigureVoxelLitMaterial(Material mat, Texture2D tex, bool emissive)
        {
            if (mat == null) return;
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                if (emissive && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);

            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1.8f, 1.8f, 1.8f, 1f));
            }
            else
            {
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.DisableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        private static Material EnsureTransparentUnlitMaterial(string path, Color color)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            Material mat = LoadOrCreateMaterial(path, unlit);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        private static GameObject BuildHeroVisualPrefab(
            string prefabName,
            string modelChildName,
            string voxelFolder,
            (Material baseMat, Material glowMat) mats)
        {
            string prefabPath = $"{HERO_PREFAB_DIR}/{prefabName}.prefab";
            var root = new GameObject(prefabName);
            root.layer = HERO_LAYER;
            try
            {
                var model = new GameObject(modelChildName);
                model.layer = HERO_LAYER;
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = new Vector3(0f, -1f, 0f);
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                Mesh idle = LoadObjMesh($"Assets/_VoxelModels/{voxelFolder}/{voxelFolder}_Idle.obj");
                Mesh[] frames =
                {
                    LoadObjMesh($"Assets/_VoxelModels/{voxelFolder}/{voxelFolder}_Walk_00.obj"),
                    LoadObjMesh($"Assets/_VoxelModels/{voxelFolder}/{voxelFolder}_Walk_01.obj"),
                    LoadObjMesh($"Assets/_VoxelModels/{voxelFolder}/{voxelFolder}_Walk_02.obj"),
                    LoadObjMesh($"Assets/_VoxelModels/{voxelFolder}/{voxelFolder}_Walk_03.obj")
                };

                var mf = model.AddComponent<MeshFilter>();
                mf.sharedMesh = idle;

                var mr = model.AddComponent<MeshRenderer>();
                int subMeshes = idle != null ? Mathf.Max(1, idle.subMeshCount) : 2;
                mr.sharedMaterials = subMeshes >= 2 && mats.glowMat != null
                    ? new[] { mats.baseMat, mats.glowMat }
                    : new[] { mats.baseMat };

                var anim = model.AddComponent<VoxelMeshAnimator>();
                var so = new SerializedObject(anim);
                so.FindProperty("targetMeshFilter").objectReferenceValue = mf;
                so.FindProperty("idleMesh").objectReferenceValue = idle;
                var framesProp = so.FindProperty("frames");
                framesProp.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                {
                    framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                }
                so.FindProperty("playOnEnable").boolValue = false;
                so.FindProperty("framesPerSecond").floatValue = 10f;
                so.FindProperty("loop").boolValue = true;
                so.FindProperty("randomizeStartOffset").boolValue = false;
                so.FindProperty("enableBobbing").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildProjectilePrefab(
            string prefabName,
            string objPath,
            (Material baseMat, Material glowMat) mats)
        {
            string prefabPath = $"{PROJ_PREFAB_DIR}/{prefabName}.prefab";
            var root = new GameObject(prefabName);
            root.layer = HERO_PROJECTILE_LAYER;
            try
            {
                root.AddComponent<Projectile>();

                var visuals = new GameObject("Visuals");
                visuals.layer = HERO_PROJECTILE_LAYER;
                visuals.transform.SetParent(root.transform, false);

                Mesh mesh = LoadObjMesh(objPath);
                var mf = visuals.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = visuals.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;
                int subMeshes = mesh != null ? Mathf.Max(1, mesh.subMeshCount) : 2;
                mr.sharedMaterials = subMeshes >= 2 && mats.glowMat != null
                    ? new[] { mats.baseMat, mats.glowMat }
                    : new[] { mats.baseMat };

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildExplosiveBarrelPrefab((Material baseMat, Material glowMat) mats)
        {
            string prefabPath = $"{ARENA_PREFAB_DIR}/ExplosiveBarrel.prefab";
            var root = new GameObject("ExplosiveBarrel");
            root.layer = ENEMY_LAYER;
            try
            {
                var col = root.AddComponent<CapsuleCollider>();
                col.isTrigger = true;
                col.radius = 0.65f;
                col.height = 1.2f;
                col.direction = 1;
                col.center = new Vector3(0f, 0.6f, 0f);

                var barrel = root.AddComponent<ExplosiveBarrel>();

                var visuals = new GameObject("Visuals");
                visuals.layer = ENEMY_LAYER;
                visuals.transform.SetParent(root.transform, false);

                Mesh mesh = LoadObjMesh("Assets/_VoxelModels/ExplosiveBarrel/ExplosiveBarrel.obj");
                var mf = visuals.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = visuals.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { mats.baseMat, mats.glowMat };

                var so = new SerializedObject(barrel);
                so.FindProperty("maxHp").intValue = 1;
                so.FindProperty("explosionRadius").floatValue = 4f;
                so.FindProperty("explosionDamage").intValue = 150;
                so.FindProperty("heroTouchRadius").floatValue = 1.25f;
                so.FindProperty("enemyLayer").intValue = 1 << ENEMY_LAYER;
                so.FindProperty("visualRoot").objectReferenceValue = visuals.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildVacuumMagnetPrefab((Material baseMat, Material glowMat) mats, Material beaconMat)
        {
            string prefabPath = $"{PICKUP_PREFAB_DIR}/VacuumMagnetPickup.prefab";
            var root = new GameObject("VacuumMagnetPickup");
            root.layer = PICKUPS_LAYER;
            try
            {
                var col = root.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 1.4f;
                col.center = new Vector3(0f, 0.7f, 0f);

                var pickup = root.AddComponent<VacuumMagnetPickup>();

                var beam = CreateBeaconCylinder("BeaconBeam", PICKUPS_LAYER, radius: 0.32f, height: 3.2f, beaconMat);
                beam.transform.SetParent(root.transform, false);

                var visuals = new GameObject("Visuals");
                visuals.layer = PICKUPS_LAYER;
                visuals.transform.SetParent(root.transform, false);
                visuals.transform.localPosition = new Vector3(0f, 0.4f, 0f);

                var meshGo = new GameObject("Mesh");
                meshGo.layer = PICKUPS_LAYER;
                meshGo.transform.SetParent(visuals.transform, false);

                Mesh mesh = LoadObjMesh("Assets/_VoxelModels/VacuumMagnet/VacuumMagnet.obj");
                var mf = meshGo.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { mats.baseMat, mats.glowMat };

                var so = new SerializedObject(pickup);
                so.FindProperty("pickupRadius").floatValue = 2.0f;
                so.FindProperty("visualRoot").objectReferenceValue = visuals.transform;
                so.FindProperty("bobHeight").floatValue = 0.2f;
                so.FindProperty("bobSpeed").floatValue = 3.0f;
                so.FindProperty("rotateSpeed").floatValue = 90f;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildChallengeShrinePrefab(
            (Material baseMat, Material glowMat) mats,
            Material ringMat,
            Material beaconMat)
        {
            string prefabPath = $"{ARENA_PREFAB_DIR}/ChallengeShrine.prefab";
            var root = new GameObject("ChallengeShrine");
            root.layer = PICKUPS_LAYER;
            try
            {
                var shrine = root.AddComponent<ChallengeShrine>();

                var beam = CreateBeaconCylinder("BeaconBeam", PICKUPS_LAYER, radius: 0.52f, height: 4.0f, beaconMat);
                beam.transform.SetParent(root.transform, false);

                var ringGo = new GameObject("RuneRing");
                ringGo.layer = PICKUPS_LAYER;
                ringGo.transform.SetParent(root.transform, false);
                ringGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);

                const int segments = 12;
                const float ringRadius = 2.6f;
                for (int i = 0; i < segments; i++)
                {
                    float angle = (Mathf.PI * 2f / segments) * i;
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    block.name = $"RuneBlock_{i}";
                    block.layer = PICKUPS_LAYER;
                    block.transform.SetParent(ringGo.transform, false);
                    block.transform.localPosition = new Vector3(Mathf.Cos(angle) * ringRadius, 0f, Mathf.Sin(angle) * ringRadius);
                    block.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                    block.transform.localScale = new Vector3(0.22f, 0.08f, 0.52f);
                    var bCol = block.GetComponent<Collider>();
                    if (bCol != null) Object.DestroyImmediate(bCol);
                    var bMr = block.GetComponent<MeshRenderer>();
                    if (bMr != null)
                    {
                        bMr.shadowCastingMode = ShadowCastingMode.Off;
                        bMr.receiveShadows = false;
                        if (ringMat != null) bMr.sharedMaterial = ringMat;
                    }
                }

                var visuals = new GameObject("Visuals");
                visuals.layer = PICKUPS_LAYER;
                visuals.transform.SetParent(root.transform, false);

                var meshGo = new GameObject("Mesh");
                meshGo.layer = PICKUPS_LAYER;
                meshGo.transform.SetParent(visuals.transform, false);

                Mesh mesh = LoadObjMesh("Assets/_VoxelModels/ChallengeShrine/ChallengeShrine.obj");
                var mf = meshGo.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { mats.baseMat, mats.glowMat };

                var so = new SerializedObject(shrine);
                so.FindProperty("activationRadius").floatValue = 2.6f;
                so.FindProperty("eliteCount").intValue = 6;
                so.FindProperty("ringSpawnRadius").floatValue = 6.5f;
                so.FindProperty("coinReward").intValue = 100;
                so.FindProperty("grantLevelUp").boolValue = true;
                so.FindProperty("visualRoot").objectReferenceValue = visuals.transform;
                so.FindProperty("runeRingVisual").objectReferenceValue = ringGo.transform;
                so.FindProperty("beaconBeam").objectReferenceValue = beam;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateBeaconCylinder(string name, int layer, float radius, float height, Material mat)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = name;
            beam.layer = layer;
            beam.transform.localPosition = new Vector3(0f, height, 0f);
            beam.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
            var col = beam.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var mr = beam.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mat != null) mr.sharedMaterial = mat;
            }
            return beam;
        }

        private static void WireHeroPrefabSoulNovaVisuals(Mesh soulMesh, Material baseMat, Material glowMat)
        {
            string heroPrefabPath = "Assets/_Prefabs/Hero.prefab";
            var contents = PrefabUtility.LoadPrefabContents(heroPrefabPath);
            try
            {
                var ult = contents.GetComponent<HeroUltimate>();
                if (ult != null)
                {
                    var so = new SerializedObject(ult);
                    so.FindProperty("soulNovaMesh").objectReferenceValue = soulMesh;
                    so.FindProperty("soulNovaBaseMaterial").objectReferenceValue = baseMat;
                    so.FindProperty("soulNovaGlowMaterial").objectReferenceValue = glowMat;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(contents, heroPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static WeaponData CreateOrUpdateRapidBowWeapon(GameObject arrowPrefab)
        {
            var data = LoadOrCreateSO<WeaponData>($"{SO_DIR}/RapidBowWeaponData.asset");
            data.damage = 9;
            data.attackCooldown = 0.32f;
            data.range = 11f;
            data.projectileSpeed = 22f;
            data.splashRadius = 0f;
            data.lifestealChance = 0f;
            data.lifestealAmount = 0;
            data.projectilePrefab = arrowPrefab;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static WeaponData CreateOrUpdateSoulBoltWeapon(GameObject soulPrefab)
        {
            var data = LoadOrCreateSO<WeaponData>($"{SO_DIR}/SoulBoltWeaponData.asset");
            data.damage = 11;
            data.attackCooldown = 0.5f;
            data.range = 9f;
            data.projectileSpeed = 16f;
            data.splashRadius = 0f;
            data.lifestealChance = 0.2f;
            data.lifestealAmount = 2;
            data.projectilePrefab = soulPrefab;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static HeroData CreateOrUpdateRangerData(WeaponData bowWeapon, GameObject visualPrefab)
        {
            var data = LoadOrCreateSO<HeroData>($"{SO_DIR}/RangerData.asset");
            data.id = "ranger";
            data.heroName = "Ranger";
            data.description = "Swift forest marksman firing rapid long-range arrows (9 dmg, 3.1/s, 11m range) and deadly volleys.";
            data.unlockedByDefault = false;
            data.moveSpeed = 5.6f;
            data.rotationSpeed = 15f;
            data.maxHp = 90;
            data.startingWeapon = bowWeapon;
            data.visualModelPrefab = visualPrefab;
            data.passiveType = HeroPassiveType.MoveSpeedPercent;
            data.passiveValue = 25f;
            data.passiveName = "Windrunner";
            data.passiveDescription = "+25% Move Speed\n<b>Weapon:</b> Rapid Bow (9 dmg, 3.1/s, 11m range)";
            data.ultimateType = HeroUltimateType.ArrowRain;
            data.ultimateName = "Arrow Rain";
            data.ultimateDescription = "Unleashes 3 waves of arrows in an 8m radius, dealing 150 total damage (3x50) and slowing enemies by 50% for 4s.";
            data.ultimateDuration = 2.4f;
            data.ultimateCooldown = 20f;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static HeroData CreateOrUpdateNecromancerData(WeaponData soulWeapon, GameObject visualPrefab)
        {
            var data = LoadOrCreateSO<HeroData>($"{SO_DIR}/NecromancerHeroData.asset");
            data.id = "necromancer";
            data.heroName = "Necromancer";
            data.description = "Master of souls firing life-draining bolts (20% chance on hit to heal 2 HP) and harvesting essence for faster growth.";
            data.unlockedByDefault = false;
            data.moveSpeed = 5.0f;
            data.rotationSpeed = 15f;
            data.maxHp = 85;
            data.startingWeapon = soulWeapon;
            data.visualModelPrefab = visualPrefab;
            data.passiveType = HeroPassiveType.XpAndMagnetPercent;
            data.passiveValue = 35f;
            data.passiveName = "Soul Harvest";
            data.passiveDescription = "+35% XP & Magnet Radius\n<b>Weapon:</b> Soul Bolt (11 dmg, 20% chance to heal 2 HP on hit)";
            data.ultimateType = HeroUltimateType.SoulNova;
            data.ultimateName = "Army of the Dead";
            data.ultimateDescription = "Fires 12 piercing spectral skulls in all directions (60 dmg each) and instantly heals 35% Max HP.";
            data.ultimateDuration = 1.0f;
            data.ultimateCooldown = 24f;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void UpdateHeroCatalog(HeroData rangerData, HeroData necroData)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HeroCatalog>("Assets/Resources/HeroCatalog.asset");
            if (catalog == null)
            {
                Debug.LogError("[Sprint5SetupTool] HeroCatalog.asset not found!");
                return;
            }

            var knight = AssetDatabase.LoadAssetAtPath<HeroData>($"{SO_DIR}/HeroData.asset");
            var mage = AssetDatabase.LoadAssetAtPath<HeroData>($"{SO_DIR}/MageData.asset");

            catalog.heroes = new List<HeroData>();
            if (knight != null) catalog.heroes.Add(knight);
            if (mage != null) catalog.heroes.Add(mage);
            if (rangerData != null) catalog.heroes.Add(rangerData);
            if (necroData != null) catalog.heroes.Add(necroData);
            EditorUtility.SetDirty(catalog);
        }

        private static void UpdateHeroUnlockAchievements()
        {
            var kill5000 = AssetDatabase.LoadAssetAtPath<AchievementData>($"{ACH_DIR}/Kill5000Enemies.asset");
            if (kill5000 != null)
            {
                kill5000.description = "Kill 5,000 enemies cumulatively to unlock the Ranger hero";
                kill5000.rewardType = AchievementRewardType.UnlockHero;
                kill5000.rewardId = "ranger";
                kill5000.rewardValue = 0;
                EditorUtility.SetDirty(kill5000);
            }

            var defeatNecro = AssetDatabase.LoadAssetAtPath<AchievementData>($"{ACH_DIR}/DefeatNecromancer.asset");
            if (defeatNecro != null)
            {
                defeatNecro.description = "Defeat the Necromancer boss to unlock the Necromancer hero";
                defeatNecro.rewardType = AchievementRewardType.UnlockHero;
                defeatNecro.rewardId = "necromancer";
                defeatNecro.rewardValue = 0;
                EditorUtility.SetDirty(defeatNecro);
            }
        }

        private static void UpdateArenaAndWaveConfigs(GameObject barrelPrefab, GameObject shrinePrefab)
        {
            var arenaConfig = AssetDatabase.LoadAssetAtPath<ArenaConfig>($"{SO_DIR}/ArenaConfig.asset");
            if (arenaConfig != null)
            {
                arenaConfig.ExplosiveBarrelPrefab = barrelPrefab;
                arenaConfig.MinExplosiveBarrels = 8;
                arenaConfig.MaxExplosiveBarrels = 12;
                arenaConfig.BarrelMinDistanceFromCenter = 12f;
                arenaConfig.BarrelMaxDistanceFromCenter = 65f;
                arenaConfig.ChallengeShrinePrefab = shrinePrefab;
                arenaConfig.ShrineMinDistanceFromCenter = 18f;
                arenaConfig.ShrineMaxDistanceFromCenter = 48f;
                EditorUtility.SetDirty(arenaConfig);
            }

            var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>($"{SO_DIR}/WaveConfig.asset");
            if (waveConfig != null)
            {
                waveConfig.miniBossVacuumDropChance = 0.25f;
                waveConfig.bossVacuumDropChance = 1.0f;
                EditorUtility.SetDirty(waveConfig);
            }
        }

        private static void UpdateGameplayScene(GameObject vacuumPrefab)
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath(GAMEPLAY_SCENE_PATH);
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                VacuumMagnetSpawner spawner = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    spawner = root.GetComponentInChildren<VacuumMagnetSpawner>(true);
                    if (spawner != null) break;
                }

                if (spawner == null)
                {
                    var spawnerGo = new GameObject("VacuumMagnetSpawner");
                    SceneManager.MoveGameObjectToScene(spawnerGo, scene);
                    spawner = spawnerGo.AddComponent<VacuumMagnetSpawner>();
                }

                var pickupComp = vacuumPrefab != null ? vacuumPrefab.GetComponent<VacuumMagnetPickup>() : null;
                var so = new SerializedObject(spawner);
                so.FindProperty("vacuumMagnetPrefab").objectReferenceValue = pickupComp;
                so.FindProperty("pullSpeed").floatValue = 28f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(spawner);

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

        private static Mesh LoadObjMesh(string objPath)
        {
            Mesh direct = AssetDatabase.LoadAssetAtPath<Mesh>(objPath);
            if (direct != null) return direct;

            Object[] all = AssetDatabase.LoadAllAssetsAtPath(objPath);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Mesh mesh) return mesh;
            }

            Debug.LogError($"[Sprint5SetupTool] Failed to load Mesh from {objPath}");
            return null;
        }

        private static T LoadOrCreateSO<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
