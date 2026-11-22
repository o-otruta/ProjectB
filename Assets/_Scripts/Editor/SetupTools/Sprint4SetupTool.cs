using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.Visuals;

namespace ProjectB.EditorScripts
{
    public static class Sprint4SetupTool
    {
        private const int ENEMY_LAYER = 8;
        private const string VOXEL_MAT_DIR = "Assets/_Materials/Voxels";
        private const string FX_MAT_DIR = "Assets/_Materials";
        private const string ENEMY_PREFAB_DIR = "Assets/_Prefabs/Enemies";
        private const string SO_DIR = "Assets/_ScriptableObjects";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";

        [MenuItem("ProjectB/Setup/Run Sprint 4 Asset & Scene Setup")]
        public static void RunAll()
        {
            EnsureFolder(VOXEL_MAT_DIR);
            EnsureFolder(ENEMY_PREFAB_DIR);

            ConfigurePaletteTexture("Assets/_VoxelModels/Skeleton/skeleton.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/BombSlime/bomb_slime.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/OrcCharger/orc_charger.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/FrostLich/frost_lich.png");

            var skelMats = EnsureVoxelMaterialPair("Skeleton", "Assets/_VoxelModels/Skeleton/skeleton.png");
            var bombMats = EnsureVoxelMaterialPair("BombSlime", "Assets/_VoxelModels/BombSlime/bomb_slime.png");
            var orcMats = EnsureVoxelMaterialPair("OrcCharger", "Assets/_VoxelModels/OrcCharger/orc_charger.png");
            var lichMats = EnsureVoxelMaterialPair("FrostLich", "Assets/_VoxelModels/FrostLich/frost_lich.png");

            Material chargeTelegraphMat = EnsureTransparentUnlitMaterial($"{FX_MAT_DIR}/ChargeTelegraphMat.mat", new Color(1f, 0.18f, 0.12f, 0.55f));
            Material frostTelegraphMat = EnsureTransparentUnlitMaterial($"{FX_MAT_DIR}/FrostTelegraphMat.mat", new Color(0.35f, 0.88f, 1f, 0.58f));
            Material blizzardRingMat = EnsureTransparentUnlitMaterial($"{FX_MAT_DIR}/BlizzardRingMat.mat", new Color(0.25f, 0.72f, 1f, 0.34f));
            Material frostSpikeMat = EnsureEmissiveLitMaterial(
                $"{FX_MAT_DIR}/FrostSpikeMat.mat",
                new Color(0.6f, 0.95f, 1f, 1f),
                new Color(0.28f, 0.78f, 1.15f, 1f));
            Material bossTelegraphMat = AssetDatabase.LoadAssetAtPath<Material>($"{FX_MAT_DIR}/BossTelegraphMat.mat");

            GameObject skeletonPrefab = BuildSkeletonPrefab(skelMats.baseMat, skelMats.glowMat);
            GameObject bombPrefab = BuildBombSlimePrefab(bombMats.baseMat, bombMats.glowMat, bossTelegraphMat);
            GameObject orcPrefab = BuildOrcChargerPrefab(orcMats.baseMat, orcMats.glowMat, chargeTelegraphMat);
            GameObject lichPrefab = BuildFrostLichPrefab(lichMats.baseMat, lichMats.glowMat, frostTelegraphMat, frostSpikeMat, blizzardRingMat);

            EnemyData skeletonData = CreateOrUpdateSkeletonData(skeletonPrefab);
            ExploderEnemyData bombData = CreateOrUpdateBombBugData(bombPrefab);
            ChargerEnemyData orcData = CreateOrUpdateOrcChargerData(orcPrefab);
            BossData lichData = CreateOrUpdateFrostLichBossData(lichPrefab, skeletonData);

            UpdateExistingEnemyAndBossAssets(skeletonData);
            UpdateWaveConfigAsset(skeletonData, bombData, orcData, lichData);
            ResaveGameplayScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint4SetupTool] Sprint 4 voxel models, materials, prefabs, ScriptableObjects, WaveConfig, and Gameplay scene wired successfully.");
        }

        private static void ConfigurePaletteTexture(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Sprint4SetupTool] Palette texture not found at {assetPath}");
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

        private static Material EnsureEmissiveLitMaterial(string path, Color baseColor, Color emissionColor)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = LoadOrCreateMaterial(path, lit);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
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

        private static GameObject BuildSkeletonPrefab(Material baseMat, Material glowMat)
        {
            string prefabPath = $"{ENEMY_PREFAB_DIR}/SkeletonRunner.prefab";
            var root = new GameObject("SkeletonRunner");
            try
            {
                ConfigureEnemyPhysicsRoot(root, radius: 0.45f, height: 1.55f);
                root.AddComponent<EnemyBase>();

                Mesh idle = LoadObjMesh("Assets/_VoxelModels/Skeleton/Skeleton_Idle.obj");
                Mesh[] frames =
                {
                    LoadObjMesh("Assets/_VoxelModels/Skeleton/Skeleton_Walk_00.obj"),
                    LoadObjMesh("Assets/_VoxelModels/Skeleton/Skeleton_Walk_01.obj"),
                    LoadObjMesh("Assets/_VoxelModels/Skeleton/Skeleton_Walk_02.obj"),
                    LoadObjMesh("Assets/_VoxelModels/Skeleton/Skeleton_Walk_03.obj")
                };

                CreateVoxelVisualsChild(root.transform, idle, frames, baseMat, glowMat, fps: 12f, playOnEnable: true, randomizeStart: true, enableBobbing: false, bobSpeed: 0f, bobHeight: 0f);
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildBombSlimePrefab(Material baseMat, Material glowMat, Material telegraphMat)
        {
            string prefabPath = $"{ENEMY_PREFAB_DIR}/BombSlime.prefab";
            var root = new GameObject("BombSlime");
            try
            {
                ConfigureEnemyPhysicsRoot(root, radius: 0.55f, height: 1.2f);
                var exploder = root.AddComponent<ExploderEnemy>();

                Mesh idle = LoadObjMesh("Assets/_VoxelModels/BombSlime/BombSlime_Idle.obj");
                Mesh[] frames =
                {
                    LoadObjMesh("Assets/_VoxelModels/BombSlime/BombSlime_Walk_00.obj"),
                    LoadObjMesh("Assets/_VoxelModels/BombSlime/BombSlime_Walk_01.obj"),
                    LoadObjMesh("Assets/_VoxelModels/BombSlime/BombSlime_Walk_02.obj"),
                    LoadObjMesh("Assets/_VoxelModels/BombSlime/BombSlime_Walk_03.obj")
                };

                var anim = CreateVoxelVisualsChild(root.transform, idle, frames, baseMat, glowMat, fps: 10f, playOnEnable: true, randomizeStart: true, enableBobbing: true, bobSpeed: 7f, bobHeight: 0.08f);
                if (anim != null)
                {
                    var animSo = new SerializedObject(anim);
                    animSo.FindProperty("bobbingTransform").objectReferenceValue = anim.transform;
                    animSo.ApplyModifiedPropertiesWithoutUndo();
                }

                var telegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                telegraph.name = "ExplosionTelegraph";
                telegraph.layer = ENEMY_LAYER;
                telegraph.transform.SetParent(root.transform, false);
                telegraph.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                telegraph.transform.localScale = new Vector3(5f, 0.05f, 5f);
                var col = telegraph.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                var mr = telegraph.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (telegraphMat != null) mr.sharedMaterial = telegraphMat;
                }
                telegraph.SetActive(false);

                var expSo = new SerializedObject(exploder);
                expSo.FindProperty("explosionTelegraphVisual").objectReferenceValue = telegraph.transform;
                expSo.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildOrcChargerPrefab(Material baseMat, Material glowMat, Material chargeTelegraphMat)
        {
            string prefabPath = $"{ENEMY_PREFAB_DIR}/OrcCharger.prefab";
            var root = new GameObject("OrcCharger");
            try
            {
                ConfigureEnemyPhysicsRoot(root, radius: 0.7f, height: 1.9f);
                var charger = root.AddComponent<ChargerEnemy>();

                Mesh idle = LoadObjMesh("Assets/_VoxelModels/OrcCharger/OrcCharger_Idle.obj");
                Mesh chargePose = LoadObjMesh("Assets/_VoxelModels/OrcCharger/OrcCharger_Charge.obj");
                Mesh[] frames =
                {
                    LoadObjMesh("Assets/_VoxelModels/OrcCharger/OrcCharger_Walk_00.obj"),
                    LoadObjMesh("Assets/_VoxelModels/OrcCharger/OrcCharger_Walk_01.obj"),
                    LoadObjMesh("Assets/_VoxelModels/OrcCharger/OrcCharger_Walk_02.obj"),
                    LoadObjMesh("Assets/_VoxelModels/OrcCharger/OrcCharger_Walk_03.obj")
                };

                var anim = CreateVoxelVisualsChild(root.transform, idle, frames, baseMat, glowMat, fps: 9f, playOnEnable: true, randomizeStart: true, enableBobbing: false, bobSpeed: 0f, bobHeight: 0f);
                var bodyMf = anim != null ? anim.TargetMeshFilter : null;

                var lineGo = new GameObject("ChargeTelegraph");
                lineGo.layer = ENEMY_LAYER;
                lineGo.transform.SetParent(root.transform, false);
                lineGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                lineGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var lr = lineGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.alignment = LineAlignment.TransformZ;
                lr.positionCount = 2;
                lr.startWidth = 0.9f;
                lr.endWidth = 0.9f;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.startColor = new Color(1f, 0.2f, 0.12f, 0.55f);
                lr.endColor = new Color(1f, 0.35f, 0.12f, 0.45f);
                if (chargeTelegraphMat != null) lr.sharedMaterial = chargeTelegraphMat;
                lr.enabled = false;

                var so = new SerializedObject(charger);
                so.FindProperty("chargeTelegraphLine").objectReferenceValue = lr;
                so.FindProperty("chargeTelegraphMaterial").objectReferenceValue = chargeTelegraphMat;
                so.FindProperty("meshAnimator").objectReferenceValue = anim;
                so.FindProperty("bodyMeshFilter").objectReferenceValue = bodyMf;
                so.FindProperty("chargePoseMesh").objectReferenceValue = chargePose;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildFrostLichPrefab(
            Material baseMat,
            Material glowMat,
            Material frostTelegraphMat,
            Material frostSpikeMat,
            Material blizzardRingMat)
        {
            string prefabPath = $"{ENEMY_PREFAB_DIR}/FrostLichBoss.prefab";
            var root = new GameObject("FrostLichBoss");
            try
            {
                ConfigureEnemyPhysicsRoot(root, radius: 0.9f, height: 2.9f);
                var lich = root.AddComponent<FrostLichBoss>();

                Mesh idle = LoadObjMesh("Assets/_VoxelModels/FrostLich/FrostLich_Idle.obj");
                Mesh castMesh = LoadObjMesh("Assets/_VoxelModels/FrostLich/FrostLich_Cast.obj");
                Mesh[] frames =
                {
                    LoadObjMesh("Assets/_VoxelModels/FrostLich/FrostLich_Walk_00.obj"),
                    LoadObjMesh("Assets/_VoxelModels/FrostLich/FrostLich_Walk_01.obj"),
                    LoadObjMesh("Assets/_VoxelModels/FrostLich/FrostLich_Walk_02.obj"),
                    LoadObjMesh("Assets/_VoxelModels/FrostLich/FrostLich_Walk_03.obj")
                };

                var anim = CreateVoxelVisualsChild(root.transform, idle, frames, baseMat, glowMat, fps: 8f, playOnEnable: false, randomizeStart: false, enableBobbing: true, bobSpeed: 3.2f, bobHeight: 0.14f);
                var bodyMf = anim != null ? anim.TargetMeshFilter : null;

                var so = new SerializedObject(lich);
                so.FindProperty("meshAnimator").objectReferenceValue = anim;
                so.FindProperty("bodyMeshFilter").objectReferenceValue = bodyMf;
                so.FindProperty("castMesh").objectReferenceValue = castMesh;
                so.FindProperty("frostTelegraphMaterial").objectReferenceValue = frostTelegraphMat;
                so.FindProperty("frostSpikeMaterial").objectReferenceValue = frostSpikeMat;
                so.FindProperty("blizzardRingMaterial").objectReferenceValue = blizzardRingMat;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureEnemyPhysicsRoot(GameObject root, float radius, float height)
        {
            root.layer = ENEMY_LAYER;
            var col = root.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            col.radius = radius;
            col.height = height;
            col.direction = 1;
            col.center = new Vector3(0f, height * 0.5f, 0f);

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }

        private static VoxelMeshAnimator CreateVoxelVisualsChild(
            Transform parent,
            Mesh idleMesh,
            Mesh[] frames,
            Material baseMat,
            Material glowMat,
            float fps,
            bool playOnEnable,
            bool randomizeStart,
            bool enableBobbing,
            float bobSpeed,
            float bobHeight)
        {
            var visuals = new GameObject("Visuals");
            visuals.layer = ENEMY_LAYER;
            visuals.transform.SetParent(parent, false);

            var mf = visuals.AddComponent<MeshFilter>();
            mf.sharedMesh = idleMesh;

            var mr = visuals.AddComponent<MeshRenderer>();
            int subMeshes = idleMesh != null ? Mathf.Max(1, idleMesh.subMeshCount) : 2;
            mr.sharedMaterials = subMeshes >= 2 && glowMat != null
                ? new[] { baseMat, glowMat }
                : new[] { baseMat };

            var anim = visuals.AddComponent<VoxelMeshAnimator>();
            var so = new SerializedObject(anim);
            so.FindProperty("targetMeshFilter").objectReferenceValue = mf;
            so.FindProperty("idleMesh").objectReferenceValue = idleMesh;
            var framesProp = so.FindProperty("frames");
            framesProp.arraySize = frames != null ? frames.Length : 0;
            if (frames != null)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                }
            }
            so.FindProperty("playOnEnable").boolValue = playOnEnable;
            so.FindProperty("framesPerSecond").floatValue = fps;
            so.FindProperty("loop").boolValue = true;
            so.FindProperty("randomizeStartOffset").boolValue = randomizeStart;
            so.FindProperty("enableBobbing").boolValue = enableBobbing;
            so.FindProperty("bobbingSpeed").floatValue = bobSpeed;
            so.FindProperty("bobbingHeight").floatValue = bobHeight;
            so.ApplyModifiedPropertiesWithoutUndo();

            return anim;
        }

        private static EnemyData CreateOrUpdateSkeletonData(GameObject prefab)
        {
            var data = LoadOrCreateSO<EnemyData>($"{SO_DIR}/SkeletonData.asset");
            data.hp = 22;
            data.speed = 4.3f;
            data.contactDamage = 10;
            data.damageCooldown = 0.85f;
            data.contactRadius = 1.0f;
            data.xpDrop = 2;
            data.coinDropChance = 0.22f;
            data.coinDrop = 1;
            data.modelPrefab = prefab;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static ExploderEnemyData CreateOrUpdateBombBugData(GameObject prefab)
        {
            var data = LoadOrCreateSO<ExploderEnemyData>($"{SO_DIR}/BombBugData.asset");
            data.hp = 32;
            data.speed = 3.8f;
            data.contactDamage = 8;
            data.damageCooldown = 1.0f;
            data.contactRadius = 1.0f;
            data.xpDrop = 3;
            data.coinDropChance = 0.25f;
            data.coinDrop = 1;
            data.modelPrefab = prefab;
            data.triggerDistance = 1.8f;
            data.fuseDuration = 0.75f;
            data.explosionRadius = 2.5f;
            data.explosionDamage = 28;
            data.friendlyFireMultiplier = 1.0f;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static ChargerEnemyData CreateOrUpdateOrcChargerData(GameObject prefab)
        {
            var data = LoadOrCreateSO<ChargerEnemyData>($"{SO_DIR}/OrcChargerData.asset");
            data.hp = 85;
            data.speed = 2.4f;
            data.contactDamage = 14;
            data.damageCooldown = 1.0f;
            data.contactRadius = 1.2f;
            data.xpDrop = 4;
            data.coinDropChance = 0.35f;
            data.coinDrop = 2;
            data.modelPrefab = prefab;
            data.chargeTriggerRange = 7.0f;
            data.chargeWindupDuration = 0.8f;
            data.chargeSpeed = 11.0f;
            data.chargeDistance = 10.0f;
            data.chargeDamage = 18;
            data.chargeHitRadius = 1.15f;
            data.chargeCooldown = 4.0f;
            data.recoveryDuration = 0.45f;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static BossData CreateOrUpdateFrostLichBossData(GameObject prefab, EnemyData minionData)
        {
            var data = LoadOrCreateSO<BossData>($"{SO_DIR}/FrostLichBossData.asset");
            data.hp = 750;
            data.speed = 2.4f;
            data.contactDamage = 18;
            data.damageCooldown = 1.0f;
            data.contactRadius = 1.4f;
            data.xpDrop = 8;
            data.coinDropChance = 1.0f;
            data.coinDrop = 5;
            data.modelPrefab = prefab;

            data.bossId = "frost_lich";
            data.bossDisplayName = "Frost Lich";
            data.isMiniBoss = false;
            data.pullResistance = 0.12f;

            data.minionData = minionData;
            data.summonCount = 3;
            data.summonCooldown = 12f;

            data.frostStopDistance = 7.5f;
            data.spikeCooldown = 5.5f;
            data.spikeTelegraphDuration = 1.0f;
            data.spikeLineCount = 6;
            data.spikeLineLength = 10f;
            data.spikeLineHalfWidth = 0.85f;
            data.spikeDamage = 35;
            data.spikeSlowFactor = 0.55f;
            data.spikeSlowDuration = 2.5f;

            data.blizzardCooldown = 9.0f;
            data.blizzardTelegraphDuration = 1.15f;
            data.blizzardActiveDuration = 3.2f;
            data.blizzardSafeInnerRadius = 3.0f;
            data.blizzardOuterRadius = 9.0f;
            data.blizzardTickInterval = 0.5f;
            data.blizzardTickDamage = 14;
            data.blizzardSlowFactor = 0.65f;

            EditorUtility.SetDirty(data);
            return data;
        }

        private static void UpdateExistingEnemyAndBossAssets(EnemyData skeletonData)
        {
            var slime = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/SlimeData.asset");
            if (slime != null)
            {
                slime.contactDamage = 8;
                EditorUtility.SetDirty(slime);
            }

            var necro = AssetDatabase.LoadAssetAtPath<BossData>($"{SO_DIR}/NecromancerBossData.asset");
            if (necro != null && skeletonData != null)
            {
                necro.minionData = skeletonData;
                EditorUtility.SetDirty(necro);
            }
        }

        private static void UpdateWaveConfigAsset(
            EnemyData skeletonData,
            ExploderEnemyData bombData,
            ChargerEnemyData orcData,
            BossData lichData)
        {
            var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>($"{SO_DIR}/WaveConfig.asset");
            if (waveConfig == null)
            {
                Debug.LogError("[Sprint4SetupTool] WaveConfig.asset not found!");
                return;
            }

            var slimeData = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/SlimeData.asset");
            var goblinData = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/GoblinData.asset");
            var batData = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/BatData.asset");
            var golemData = AssetDatabase.LoadAssetAtPath<BossData>($"{SO_DIR}/GolemBossData.asset");
            var necroData = AssetDatabase.LoadAssetAtPath<BossData>($"{SO_DIR}/NecromancerBossData.asset");

            waveConfig.enemySchedule = new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry { enemy = slimeData, minWave = 1, maxWave = 0, spawnWeight = 1.4f, packSize = 1, triggerIntroWave = false, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = goblinData, minWave = 3, maxWave = 0, spawnWeight = 1.0f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = skeletonData, minWave = 6, maxWave = 0, spawnWeight = 0.85f, packSize = 4, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = batData, minWave = 7, maxWave = 0, spawnWeight = 0.9f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = bombData, minWave = 11, maxWave = 0, spawnWeight = 0.7f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = orcData, minWave = 14, maxWave = 0, spawnWeight = 0.65f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
            };

            waveConfig.bosses = new List<BossData>();
            if (golemData != null) waveConfig.bosses.Add(golemData);
            if (necroData != null) waveConfig.bosses.Add(necroData);
            if (lichData != null) waveConfig.bosses.Add(lichData);

            waveConfig.berserkerSpeedMultiplier = 1.25f;
            waveConfig.juggernautHpMultiplier = 4.5f;
            waveConfig.juggernautDamageMultiplier = 1.5f;
            waveConfig.juggernautScaleMultiplier = 1.35f;
            waveConfig.juggernautPullResistance = 0.2f;
            waveConfig.hasteAuraHpMultiplier = 3.0f;
            waveConfig.hasteAuraDamageMultiplier = 1.5f;
            waveConfig.hasteAuraRadius = 3.5f;
            waveConfig.hasteAuraSpeedBonus = 0.2f;

            EditorUtility.SetDirty(waveConfig);
        }

        private static void ResaveGameplayScene()
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath(GAMEPLAY_SCENE_PATH);
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                WaveManager waveMgr = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    waveMgr = root.GetComponentInChildren<WaveManager>(true);
                    if (waveMgr != null) break;
                }

                if (waveMgr != null)
                {
                    EditorUtility.SetDirty(waveMgr);
                }

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

            Debug.LogError($"[Sprint4SetupTool] Failed to load Mesh from {objPath}");
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
