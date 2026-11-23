using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Combat;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.UI;
using ProjectB.Visuals;

namespace ProjectB.EditorScripts
{
    public static class Sprint10SetupTool
    {
        private const string SHADER_VOXEL_LIT = "ProjectB/VoxelLit";
        private const string SHADER_URP_LIT = "Universal Render Pipeline/Lit";
        private const string SHADER_URP_UNLIT = "Universal Render Pipeline/Unlit";

        private const string MATERIALS_ROOT = "Assets/_Materials";
        private const string VOXEL_MATERIALS_DIR = "Assets/_Materials/Voxels";
        private const string ENEMY_PREFABS_DIR = "Assets/_Prefabs/Enemies";
        private const string PROJECTILE_PREFABS_DIR = "Assets/_Prefabs/Projectiles";
        private const string DECORATION_PREFABS_DIR = "Assets/_Prefabs/Decorations";
        private const string SO_DIR = "Assets/_ScriptableObjects";
        private const string ACHIEVEMENTS_DIR = "Assets/Resources/Achievements";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";
        private const string WHITE_SPRITE_PATH = "Assets/_UI/Sprint10WhiteSquare.png";

        [MenuItem("ProjectB/Setup/Run Sprint 10 Events, Enemies, Boss & Living Arena Setup")]
        public static void RunAll()
        {
            EnsureFolders();

            // 1. Configure palette textures (Point / Clamp / No Mipmap)
            Texture2D shieldTex = ConfigurePaletteTexture("Assets/_VoxelModels/ShieldOrc/shield_orc.png");
            Texture2D voidTex = ConfigurePaletteTexture("Assets/_VoxelModels/VoidAcolyte/void_acolyte.png");
            Texture2D goldGoblinTex = ConfigurePaletteTexture("Assets/_VoxelModels/GoldGoblin/gold_goblin.png");
            Texture2D demonLordTex = ConfigurePaletteTexture("Assets/_VoxelModels/DemonLord/demon_lord.png");

            Texture2D mushroomTex = ConfigurePaletteTexture("Assets/_VoxelModels/ForestMushroom/forest_mushroom.png");
            Texture2D wildFlowerTex = ConfigurePaletteTexture("Assets/_VoxelModels/WildFlower/wild_flower.png");
            Texture2D mossyRuinTex = ConfigurePaletteTexture("Assets/_VoxelModels/MossyRuin/mossy_ruin.png");

            // 2. Materials
            Material shieldMat = CreateOrUpdateVoxelLitMaterial($"{VOXEL_MATERIALS_DIR}/ShieldOrc_Mat.mat", shieldTex, isGlow: false, Color.black);
            Material shieldGlowMat = CreateOrUpdateVoxelLitMaterial($"{VOXEL_MATERIALS_DIR}/ShieldOrc_Glow_Mat.mat", shieldTex, isGlow: true, new Color(1.3f, 1.3f, 1.3f));

            Material voidMat = CreateOrUpdateVoxelLitMaterial($"{VOXEL_MATERIALS_DIR}/VoidAcolyte_Mat.mat", voidTex, isGlow: false, Color.black);
            Material voidGlowMat = CreateOrUpdateVoxelLitMaterial($"{VOXEL_MATERIALS_DIR}/VoidAcolyte_Glow_Mat.mat", voidTex, isGlow: true, new Color(1.7f, 1.7f, 1.7f));

            Material goldGoblinMat = CreateOrUpdateVoxelLitMaterial($"{VOXEL_MATERIALS_DIR}/GoldGoblin_Mat.mat", goldGoblinTex, isGlow: false, Color.black);
            Material goldGoblinGlowMat = CreateOrUpdateVoxelLitMaterial($"{VOXEL_MATERIALS_DIR}/GoldGoblin_Glow_Mat.mat", goldGoblinTex, isGlow: true, new Color(1.8f, 1.8f, 1.8f));

            Material demonLordMat = CreateOrUpdateVoxelLitMaterial($"{VOXEL_MATERIALS_DIR}/DemonLord_Mat.mat", demonLordTex, isGlow: false, Color.black);
            Material demonLordGlowMat = CreateOrUpdateVoxelLitMaterial($"{VOXEL_MATERIALS_DIR}/DemonLord_Glow_Mat.mat", demonLordTex, isGlow: true, new Color(2.0f, 1.55f, 1.15f));

            Material mushroomMat = CreateOrUpdateInstancedPropMaterial($"{VOXEL_MATERIALS_DIR}/ForestMushroom_Mat.mat", mushroomTex);
            Material wildFlowerMat = CreateOrUpdateInstancedPropMaterial($"{VOXEL_MATERIALS_DIR}/WildFlower_Mat.mat", wildFlowerTex);
            Material mossyRuinMat = CreateOrUpdateInstancedPropMaterial($"{VOXEL_MATERIALS_DIR}/MossyRuin_Mat.mat", mossyRuinTex);

            Material demonChargeTelegraphMat = CreateOrUpdateTransparentUnlitMaterial(
                $"{MATERIALS_ROOT}/DemonChargeTelegraphMat.mat",
                new Color(1.0f, 0.26f, 0.08f, 0.62f));
            Material demonMeteorTelegraphMat = CreateOrUpdateTransparentUnlitMaterial(
                $"{MATERIALS_ROOT}/DemonMeteorTelegraphMat.mat",
                new Color(1.0f, 0.14f, 0.08f, 0.54f));

            // 3. Prefabs
            GameObject shieldPrefab = BuildShieldBearerPrefab(shieldMat, shieldGlowMat);
            GameObject summonerPrefab = BuildVoidSummonerPrefab(voidMat, voidGlowMat);
            GameObject goldGoblinPrefab = BuildGoldGoblinPrefab(goldGoblinMat, goldGoblinGlowMat);
            GameObject demonFireballPrefab = BuildDemonFireballProjectilePrefab();
            GameObject demonLordPrefab = BuildDemonLordBossPrefab(demonLordMat, demonLordGlowMat, demonChargeTelegraphMat, demonMeteorTelegraphMat);

            GameObject mushroomPrefab = BuildDecorationPropPrefab($"{DECORATION_PREFABS_DIR}/ForestMushroom.prefab", "ForestMushroom", "Assets/_VoxelModels/ForestMushroom/ForestMushroom.obj", mushroomMat);
            GameObject wildFlowerPrefab = BuildDecorationPropPrefab($"{DECORATION_PREFABS_DIR}/WildFlower.prefab", "WildFlower", "Assets/_VoxelModels/WildFlower/WildFlower.obj", wildFlowerMat);
            GameObject mossyRuinPrefab = BuildDecorationPropPrefab($"{DECORATION_PREFABS_DIR}/MossyRuin.prefab", "MossyRuin", "Assets/_VoxelModels/MossyRuin/MossyRuin.obj", mossyRuinMat);

            // 4. ScriptableObjects
            EnemyData skeletonData = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/SkeletonData.asset");
            ShieldedEnemyData shieldData = CreateOrUpdateShieldBearerData(shieldPrefab);
            SummonerEnemyData summonerData = CreateOrUpdateVoidSummonerData(summonerPrefab, skeletonData);
            FleeingLootEnemyData goldGoblinData = CreateOrUpdateGoldGoblinData(goldGoblinPrefab);
            WeaponData demonFireballWeapon = CreateOrUpdateDemonFireballWeaponData(demonFireballPrefab);
            BossData demonLordData = CreateOrUpdateDemonLordBossData(demonLordPrefab, skeletonData, demonFireballWeapon);

            UpdateWaveConfig(shieldData, summonerData, goldGoblinData, skeletonData, demonLordData);
            UpdateArenaConfig(mushroomPrefab, wildFlowerPrefab, mossyRuinPrefab);

            // 5. Achievements & Icons
            CreateOrUpdateBossAchievements();
            VoxelIconGenerator.GenerateAllIcons();

            // 6. Scene Setup
            SetupGameplayScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint10SetupTool] Sprint 10 setup completed (materials, prefabs, SOs, achievements, icons, Gameplay scene).");
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/_Materials",
                "Assets/_Materials/Voxels",
                "Assets/_Prefabs",
                "Assets/_Prefabs/Enemies",
                "Assets/_Prefabs/Projectiles",
                "Assets/_Prefabs/Decorations",
                "Assets/_ScriptableObjects",
                "Assets/Resources",
                "Assets/Resources/Achievements",
                "Assets/_UI"
            };

            for (int i = 0; i < folders.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(folders[i]))
                {
                    string parent = Path.GetDirectoryName(folders[i]).Replace('\\', '/');
                    string name = Path.GetFileName(folders[i]);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        private static Texture2D ConfigurePaletteTexture(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (!importer.sRGBTexture) { importer.sRGBTexture = true; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }
            else
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Material CreateOrUpdateVoxelLitMaterial(string matPath, Texture2D tex, bool isGlow, Color emissionColor)
        {
            Shader shader = Shader.Find(SHADER_VOXEL_LIT);
            if (shader == null) shader = Shader.Find(SHADER_URP_LIT);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader && shader != null)
            {
                mat.shader = shader;
            }

            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTexture("_MainTex", tex);
            }
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.14f);
            if (mat.HasProperty("_HeightAO")) mat.SetFloat("_HeightAO", 0.25f);
            if (mat.HasProperty("_HeightAORange")) mat.SetFloat("_HeightAORange", 0.75f);
            if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", 2.6f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", Color.clear);
            if (mat.HasProperty("_HitFlash")) mat.SetFloat("_HitFlash", 0f);
            if (mat.HasProperty("_ReceiveShadows")) mat.SetFloat("_ReceiveShadows", 1f);

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
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", null);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateInstancedPropMaterial(string matPath, Texture2D tex)
        {
            Shader shader = Shader.Find(SHADER_URP_LIT);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader && shader != null)
            {
                mat.shader = shader;
            }

            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTexture("_MainTex", tex);
            }
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
            if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
            mat.DisableKeyword("_EMISSION");
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateTransparentUnlitMaterial(string matPath, Color color)
        {
            Shader shader = Shader.Find(SHADER_URP_UNLIT);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader && shader != null)
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", 1f);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
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
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(objPath);
            if (m != null) return m;
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(objPath);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Mesh mesh) return mesh;
            }
            Debug.LogError($"[Sprint10SetupTool] Missing Mesh at {objPath}");
            return null;
        }

        private static Mesh[] LoadWalkFrames(string folder, string prefix)
        {
            var list = new List<Mesh>(4);
            for (int i = 0; i < 4; i++)
            {
                Mesh m = LoadMesh($"{folder}/{prefix}_Walk_0{i}.obj");
                if (m != null) list.Add(m);
            }
            return list.ToArray();
        }

        private static int ResolveEnemyLayer()
        {
            int layer = LayerMask.NameToLayer("Enemy");
            return layer >= 0 ? layer : 8;
        }

        private static int ResolveEnemyProjectileLayer()
        {
            int layer = LayerMask.NameToLayer("EnemyProjectile");
            return layer >= 0 ? layer : 12;
        }

        private static void ConfigureEnemyPhysics(GameObject root, float radius, float height)
        {
            var col = root.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            col.radius = radius;
            col.height = height;
            col.direction = 1;
            col.center = new Vector3(0f, height * 0.5f, 0f);

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
        }

        private static VoxelMeshAnimator AddVisualsChild(
            GameObject root,
            int layer,
            Mesh idleMesh,
            Mesh[] walkFrames,
            Material baseMat,
            Material glowMat,
            float fps,
            bool playOnEnable,
            bool randomizeOffset,
            out MeshFilter meshFilter)
        {
            var visuals = new GameObject("Visuals");
            visuals.layer = layer;
            visuals.transform.SetParent(root.transform, false);

            meshFilter = visuals.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = idleMesh;

            var mr = visuals.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { baseMat, glowMat };
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            var animator = visuals.AddComponent<VoxelMeshAnimator>();
            animator.TargetMeshFilter = meshFilter;
            animator.IdleMesh = idleMesh;
            animator.Frames = walkFrames;
            animator.PlayOnEnable = playOnEnable;
            animator.FramesPerSecond = fps;

            SerializedObject animSo = new SerializedObject(animator);
            animSo.FindProperty("targetMeshFilter").objectReferenceValue = meshFilter;
            animSo.FindProperty("idleMesh").objectReferenceValue = idleMesh;
            SerializedProperty framesProp = animSo.FindProperty("frames");
            framesProp.arraySize = walkFrames.Length;
            for (int i = 0; i < walkFrames.Length; i++)
            {
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = walkFrames[i];
            }
            animSo.FindProperty("playOnEnable").boolValue = playOnEnable;
            animSo.FindProperty("framesPerSecond").floatValue = fps;
            animSo.FindProperty("loop").boolValue = true;
            animSo.FindProperty("randomizeStartOffset").boolValue = randomizeOffset;
            animSo.ApplyModifiedPropertiesWithoutUndo();

            return animator;
        }

        private static GameObject BuildShieldBearerPrefab(Material baseMat, Material glowMat)
        {
            const string folder = "Assets/_VoxelModels/ShieldOrc";
            Mesh idle = LoadMesh($"{folder}/ShieldOrc_Idle.obj");
            Mesh[] walks = LoadWalkFrames(folder, "ShieldOrc");
            int layer = ResolveEnemyLayer();

            var root = new GameObject("ShieldBearer") { layer = layer };
            ConfigureEnemyPhysics(root, radius: 0.65f, height: 1.85f);
            root.AddComponent<ShieldedEnemy>();
            AddVisualsChild(root, layer, idle, walks, baseMat, glowMat, 8f, playOnEnable: true, randomizeOffset: true, out _);

            string path = $"{ENEMY_PREFABS_DIR}/ShieldBearer.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildVoidSummonerPrefab(Material baseMat, Material glowMat)
        {
            const string folder = "Assets/_VoxelModels/VoidAcolyte";
            Mesh idle = LoadMesh($"{folder}/VoidAcolyte_Idle.obj");
            Mesh cast = LoadMesh($"{folder}/VoidAcolyte_Cast.obj");
            Mesh[] walks = LoadWalkFrames(folder, "VoidAcolyte");
            int layer = ResolveEnemyLayer();

            var root = new GameObject("VoidSummoner") { layer = layer };
            ConfigureEnemyPhysics(root, radius: 0.55f, height: 1.9f);
            var summoner = root.AddComponent<SummonerEnemy>();
            VoxelMeshAnimator anim = AddVisualsChild(root, layer, idle, walks, baseMat, glowMat, 8f, playOnEnable: false, randomizeOffset: true, out MeshFilter mf);

            SerializedObject so = new SerializedObject(summoner);
            so.FindProperty("meshAnimator").objectReferenceValue = anim;
            so.FindProperty("bodyMeshFilter").objectReferenceValue = mf;
            so.FindProperty("castMesh").objectReferenceValue = cast;
            so.FindProperty("castPoseDuration").floatValue = 0.45f;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{ENEMY_PREFABS_DIR}/VoidSummoner.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildGoldGoblinPrefab(Material baseMat, Material glowMat)
        {
            const string folder = "Assets/_VoxelModels/GoldGoblin";
            Mesh idle = LoadMesh($"{folder}/GoldGoblin_Idle.obj");
            Mesh[] walks = LoadWalkFrames(folder, "GoldGoblin");
            int layer = ResolveEnemyLayer();

            var root = new GameObject("GoldGoblin") { layer = layer };
            ConfigureEnemyPhysics(root, radius: 0.48f, height: 1.4f);
            root.AddComponent<FleeingLootEnemy>();
            AddVisualsChild(root, layer, idle, walks, baseMat, glowMat, 12f, playOnEnable: true, randomizeOffset: true, out _);

            string path = $"{ENEMY_PREFABS_DIR}/GoldGoblin.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildDemonFireballProjectilePrefab()
        {
            Mesh mesh = LoadMesh("Assets/_VoxelModels/FireballProjectile/FireballProjectile.obj");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>($"{VOXEL_MATERIALS_DIR}/FireballProjectile_Mat.mat");
            Material glowMat = AssetDatabase.LoadAssetAtPath<Material>($"{VOXEL_MATERIALS_DIR}/FireballProjectile_Glow_Mat.mat");
            int layer = ResolveEnemyProjectileLayer();

            var root = new GameObject("DemonFireballProjectile") { layer = layer };
            root.AddComponent<EnemyProjectile>();

            var visuals = new GameObject("Visuals") { layer = layer };
            visuals.transform.SetParent(root.transform, false);
            visuals.transform.localScale = Vector3.one * 1.35f;

            var mf = visuals.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = visuals.AddComponent<MeshRenderer>();
            if (mat != null && glowMat != null)
            {
                mr.sharedMaterials = new[] { mat, glowMat };
            }
            else if (mat != null)
            {
                mr.sharedMaterials = new[] { mat };
            }
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;

            string path = $"{PROJECTILE_PREFABS_DIR}/DemonFireballProjectile.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildDemonLordBossPrefab(
            Material baseMat,
            Material glowMat,
            Material chargeTelegraphMat,
            Material meteorTelegraphMat)
        {
            const string folder = "Assets/_VoxelModels/DemonLord";
            Mesh idle = LoadMesh($"{folder}/DemonLord_Idle.obj");
            Mesh charge = LoadMesh($"{folder}/DemonLord_Charge.obj");
            Mesh cast = LoadMesh($"{folder}/DemonLord_Cast.obj");
            Mesh[] walks = LoadWalkFrames(folder, "DemonLord");
            int layer = ResolveEnemyLayer();

            var root = new GameObject("DemonLordBoss") { layer = layer };
            ConfigureEnemyPhysics(root, radius: 1.0f, height: 2.9f);
            var boss = root.AddComponent<DemonLordBoss>();
            VoxelMeshAnimator anim = AddVisualsChild(root, layer, idle, walks, baseMat, glowMat, 8f, playOnEnable: false, randomizeOffset: false, out MeshFilter mf);

            SerializedObject so = new SerializedObject(boss);
            so.FindProperty("meshAnimator").objectReferenceValue = anim;
            so.FindProperty("bodyMeshFilter").objectReferenceValue = mf;
            so.FindProperty("chargePoseMesh").objectReferenceValue = charge;
            so.FindProperty("castMesh").objectReferenceValue = cast;
            so.FindProperty("chargeTelegraphMaterial").objectReferenceValue = chargeTelegraphMat;
            so.FindProperty("meteorTelegraphMaterial").objectReferenceValue = meteorTelegraphMat;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{ENEMY_PREFABS_DIR}/DemonLordBoss.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildDecorationPropPrefab(string prefabPath, string name, string objPath, Material mat)
        {
            Mesh mesh = LoadMesh(objPath);
            var root = new GameObject(name) { layer = 0 };
            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { mat };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static ShieldedEnemyData CreateOrUpdateShieldBearerData(GameObject prefab)
        {
            var data = LoadOrCreateAsset<ShieldedEnemyData>($"{SO_DIR}/ShieldBearerData.asset");
            data.hp = 110;
            data.speed = 2.3f;
            data.contactDamage = 15;
            data.damageCooldown = 1.0f;
            data.contactRadius = 1.15f;
            data.xpDrop = 5;
            data.coinDropChance = 0.35f;
            data.coinDrop = 2;
            data.modelPrefab = prefab;
            data.frontalBlockHalfAngle = 60f;
            data.frontalDamageReduction = 0.65f;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static SummonerEnemyData CreateOrUpdateVoidSummonerData(GameObject prefab, EnemyData minionData)
        {
            var data = LoadOrCreateAsset<SummonerEnemyData>($"{SO_DIR}/VoidSummonerData.asset");
            data.hp = 78;
            data.speed = 2.6f;
            data.contactDamage = 8;
            data.damageCooldown = 1.0f;
            data.contactRadius = 1.0f;
            data.xpDrop = 6;
            data.coinDropChance = 0.40f;
            data.coinDrop = 2;
            data.modelPrefab = prefab;
            data.preferredDistance = 8.0f;
            data.retreatDistance = 4.8f;
            data.summonInterval = 6.0f;
            data.summonCount = 2;
            data.minionData = minionData;
            data.healRadius = 6.0f;
            data.healPercent = 0.15f;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static FleeingLootEnemyData CreateOrUpdateGoldGoblinData(GameObject prefab)
        {
            var data = LoadOrCreateAsset<FleeingLootEnemyData>($"{SO_DIR}/GoldGoblinData.asset");
            data.hp = 65;
            data.speed = 5.2f;
            data.contactDamage = 0;
            data.damageCooldown = 1.0f;
            data.contactRadius = 0.8f;
            data.xpDrop = 8;
            data.coinDropChance = 0f;
            data.coinDrop = 0;
            data.modelPrefab = prefab;
            data.fleeOrbitDistance = 20f;
            data.coinsPerHit = 2;
            data.hitDropCooldown = 0.25f;
            data.deathCoinMin = 40;
            data.deathCoinMax = 80;
            data.deathCoinBurstCount = 12;
            data.compassRevealDistance = 80f;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static WeaponData CreateOrUpdateDemonFireballWeaponData(GameObject fireballPrefab)
        {
            var data = LoadOrCreateAsset<WeaponData>($"{SO_DIR}/DemonFireballWeaponData.asset");
            data.damage = 22;
            data.attackCooldown = 2.0f;
            data.range = 18f;
            data.projectileSpeed = 9.5f;
            data.splashRadius = 0f;
            data.lifestealChance = 0f;
            data.lifestealAmount = 0;
            data.projectilePrefab = fireballPrefab;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static BossData CreateOrUpdateDemonLordBossData(GameObject prefab, EnemyData minionData, WeaponData fireballWeapon)
        {
            var data = LoadOrCreateAsset<BossData>($"{SO_DIR}/DemonLordBossData.asset");
            data.bossId = "demon_lord";
            data.bossDisplayName = "Demon Lord";
            data.isMiniBoss = false;
            data.pullResistance = 0.10f;
            data.hp = 950;
            data.speed = 2.85f;
            data.contactDamage = 26;
            data.damageCooldown = 0.85f;
            data.contactRadius = 1.65f;
            data.xpDrop = 10;
            data.coinDropChance = 1f;
            data.coinDrop = 7;
            data.modelPrefab = prefab;

            data.minionData = minionData;
            data.summonCount = 3;
            data.summonCooldown = 12f;

            data.demonEnrageHpThreshold = 0.50f;
            data.demonEnrageSpeedMultiplier = 1.35f;
            data.demonChargeCooldown = 5.5f;
            data.demonChargeTriggerRange = 10.5f;
            data.demonChargeWindupDuration = 0.425f;
            data.demonChargeSpeed = 13f;
            data.demonChargeDistance = 11f;
            data.demonChargeHitRadius = 1.45f;
            data.demonChargeDamage = 32;
            data.demonFireballCooldown = 4.5f;
            data.demonPhase1FireballCount = 8;
            data.demonPhase2FireballCount = 12;
            data.demonFireballWeaponData = fireballWeapon;
            data.demonMeteorCooldown = 6.5f;
            data.demonMeteorCount = 5;
            data.demonMeteorRadius = 2.6f;
            data.demonMeteorTelegraphDuration = 1.05f;
            data.demonMeteorDamage = 38;

            EditorUtility.SetDirty(data);
            return data;
        }

        private static void UpdateWaveConfig(
            ShieldedEnemyData shieldData,
            SummonerEnemyData summonerData,
            FleeingLootEnemyData goldGoblinData,
            EnemyData skeletonData,
            BossData demonLordData)
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>($"{SO_DIR}/WaveConfig.asset");
            if (config == null)
            {
                Debug.LogError("[Sprint10SetupTool] Missing WaveConfig.asset!");
                return;
            }

            var slime = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/SlimeData.asset");
            var goblin = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/GoblinData.asset");
            var bat = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/BatData.asset");
            var bomb = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/BombBugData.asset");
            var orc = AssetDatabase.LoadAssetAtPath<EnemyData>($"{SO_DIR}/OrcChargerData.asset");

            config.enemySchedule = new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry { enemy = slime, minWave = 1, maxWave = 0, spawnWeight = 1.4f, packSize = 1, triggerIntroWave = false, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = goblin, minWave = 3, maxWave = 0, spawnWeight = 1.0f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = skeletonData, minWave = 6, maxWave = 0, spawnWeight = 0.85f, packSize = 4, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = bat, minWave = 7, maxWave = 0, spawnWeight = 0.9f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = bomb, minWave = 11, maxWave = 0, spawnWeight = 0.7f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = orc, minWave = 14, maxWave = 0, spawnWeight = 0.65f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = shieldData, minWave = 18, maxWave = 0, spawnWeight = 0.75f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = summonerData, minWave = 22, maxWave = 0, spawnWeight = 0.55f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f }
            };

            var golem = AssetDatabase.LoadAssetAtPath<BossData>($"{SO_DIR}/GolemBossData.asset");
            var necro = AssetDatabase.LoadAssetAtPath<BossData>($"{SO_DIR}/NecromancerBossData.asset");
            var frostLich = AssetDatabase.LoadAssetAtPath<BossData>($"{SO_DIR}/FrostLichBossData.asset");
            config.bosses = new List<BossData> { golem, necro, frostLich, demonLordData };

            config.waveEventsEnabled = true;
            config.firstWaveEventWave = 4;
            config.minWavesBetweenEvents = 3;
            config.maxWavesBetweenEvents = 4;
            config.swarmRushEnemyData = skeletonData;
            config.swarmRushMinCount = 24;
            config.swarmRushMaxCount = 32;
            config.swarmRushRadius = 15f;
            config.swarmRushDuration = 18f;
            config.goldGoblinData = goldGoblinData;
            config.goldGoblinDuration = 22f;
            config.bloodMoonDuration = 25f;
            config.bloodMoonEnemySpeedMultiplier = 1.2f;
            config.bloodMoonXpMultiplier = 2.0f;
            config.bloodMoonCoinMultiplier = 2.0f;
            config.eliteAmbushRadius = 13f;
            config.eliteAmbushCoinReward = 50;
            config.eliteAmbushCoinBurstCount = 10;

            EditorUtility.SetDirty(config);
        }

        private static void UpdateArenaConfig(GameObject mushroomPrefab, GameObject wildFlowerPrefab, GameObject mossyRuinPrefab)
        {
            var arena = AssetDatabase.LoadAssetAtPath<ArenaConfig>($"{SO_DIR}/ArenaConfig.asset");
            if (arena == null)
            {
                Debug.LogError("[Sprint10SetupTool] Missing ArenaConfig.asset!");
                return;
            }

            arena.ObstacleClearancePadding = 1.0f;
            arena.PropDecorationPrefabs = new[] { mushroomPrefab, wildFlowerPrefab, mossyRuinPrefab };
            arena.PropDecorationWeights = new[] { 0.35f, 0.50f, 0.15f };
            arena.MinPropDecorations = 1400;
            arena.MaxPropDecorations = 1800;
            arena.DynamicRespawnWaveInterval = 12;
            arena.DynamicRespawnMinBarrels = 2;
            arena.DynamicRespawnMaxBarrels = 3;
            arena.DynamicRespawnMinRadius = 22f;
            arena.DynamicRespawnMaxRadius = 45f;

            EditorUtility.SetDirty(arena);
        }

        private static void CreateOrUpdateBossAchievements()
        {
            var frostLichAch = LoadOrCreateAsset<AchievementData>($"{ACHIEVEMENTS_DIR}/DefeatFrostLich.asset");
            frostLichAch.id = "defeat_frost_lich";
            frostLichAch.title = "Frostbreaker";
            frostLichAch.description = "Defeat the Frost Lich on Wave 75.";
            frostLichAch.conditionType = AchievementConditionType.DefeatBoss;
            frostLichAch.targetId = "frost_lich";
            frostLichAch.targetValue = 1;
            frostLichAch.rewardType = AchievementRewardType.GiveCoins;
            frostLichAch.rewardValue = 300;
            frostLichAch.rewardId = string.Empty;
            EditorUtility.SetDirty(frostLichAch);

            var demonLordAch = LoadOrCreateAsset<AchievementData>($"{ACHIEVEMENTS_DIR}/DefeatDemonLord.asset");
            demonLordAch.id = "defeat_demon_lord";
            demonLordAch.title = "Demon Slayer";
            demonLordAch.description = "Slay the Demon Lord on Wave 100.";
            demonLordAch.conditionType = AchievementConditionType.DefeatBoss;
            demonLordAch.targetId = "demon_lord";
            demonLordAch.targetValue = 1;
            demonLordAch.rewardType = AchievementRewardType.GiveCoins;
            demonLordAch.rewardValue = 500;
            demonLordAch.rewardId = string.Empty;
            EditorUtility.SetDirty(demonLordAch);
        }

        private static Sprite EnsureWhiteUiSprite()
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), WHITE_SPRITE_PATH);
            if (!File.Exists(fullPath))
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(WHITE_SPRITE_PATH, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(WHITE_SPRITE_PATH) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(WHITE_SPRITE_PATH);
        }

        private static void SetupGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Single);

            // 1. Wire WaveEventManager
            WaveManager waveManager = Object.FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include);
            WaveEventManager eventManager = Object.FindAnyObjectByType<WaveEventManager>(FindObjectsInactive.Include);
            if (eventManager == null)
            {
                var emGo = new GameObject("WaveEventManager");
                if (waveManager != null && waveManager.transform.parent != null)
                {
                    emGo.transform.SetParent(waveManager.transform.parent, false);
                }
                eventManager = emGo.AddComponent<WaveEventManager>();
            }

            Light directionalLight = null;
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    directionalLight = lights[i];
                    break;
                }
            }

            SerializedObject emSo = new SerializedObject(eventManager);
            emSo.FindProperty("arenaDirectionalLight").objectReferenceValue = directionalLight;
            emSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(eventManager);

            // 2. Wire WaveEventBannerUI under SafeAreaHUD
            Transform safeAreaHud = FindTransformInScene(scene, "SafeAreaHUD");
            if (safeAreaHud != null)
            {
                Sprite whiteSprite = EnsureWhiteUiSprite();
                TMP_FontAsset font = ResolveSceneFont(safeAreaHud);

                Transform existing = safeAreaHud.Find("WaveEventBanner");
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                var bannerGo = new GameObject("WaveEventBanner", typeof(RectTransform));
                bannerGo.transform.SetParent(safeAreaHud, false);
                RectTransform bannerRect = bannerGo.GetComponent<RectTransform>();
                bannerRect.anchorMin = new Vector2(0.5f, 1f);
                bannerRect.anchorMax = new Vector2(0.5f, 1f);
                bannerRect.pivot = new Vector2(0.5f, 1f);
                bannerRect.anchoredPosition = new Vector2(0f, -236f);
                bannerRect.sizeDelta = new Vector2(740f, 88f);

                WaveEventBannerUI bannerUi = bannerGo.AddComponent<WaveEventBannerUI>();

                var contentGo = new GameObject("Content", typeof(RectTransform), typeof(Image), typeof(Outline));
                contentGo.transform.SetParent(bannerRect, false);
                RectTransform contentRect = contentGo.GetComponent<RectTransform>();
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;

                Image bgImage = contentGo.GetComponent<Image>();
                bgImage.sprite = whiteSprite;
                bgImage.color = new Color(0.09f, 0.11f, 0.17f, 0.92f);
                bgImage.raycastTarget = false;

                Outline outline = contentGo.GetComponent<Outline>();
                outline.effectColor = new Color(1.0f, 0.56f, 0.14f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);

                TextMeshProUGUI titleText = CreateBannerTmp(
                    "TitleText",
                    contentRect,
                    font,
                    fontSize: 30f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Left,
                    new Vector2(0f, 0.45f),
                    new Vector2(0.68f, 1f),
                    new Vector2(20f, 0f),
                    new Vector2(-8f, -6f));

                TextMeshProUGUI statusText = CreateBannerTmp(
                    "StatusText",
                    contentRect,
                    font,
                    fontSize: 28f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Right,
                    new Vector2(0.68f, 0.45f),
                    new Vector2(1f, 1f),
                    new Vector2(8f, 0f),
                    new Vector2(-20f, -6f));
                statusText.color = Color.white;

                TextMeshProUGUI subtitleText = CreateBannerTmp(
                    "SubtitleText",
                    contentRect,
                    font,
                    fontSize: 20f,
                    FontStyles.Normal,
                    TextAlignmentOptions.Left,
                    new Vector2(0f, 0.16f),
                    new Vector2(1f, 0.52f),
                    new Vector2(20f, 0f),
                    new Vector2(-20f, 0f));
                subtitleText.color = new Color(0.86f, 0.90f, 0.98f, 1f);

                var barBgGo = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
                barBgGo.transform.SetParent(contentRect, false);
                RectTransform barBgRect = barBgGo.GetComponent<RectTransform>();
                barBgRect.anchorMin = new Vector2(0f, 0f);
                barBgRect.anchorMax = new Vector2(1f, 0f);
                barBgRect.pivot = new Vector2(0.5f, 0f);
                barBgRect.anchoredPosition = new Vector2(0f, 8f);
                barBgRect.sizeDelta = new Vector2(-36f, 8f);
                Image barBgImg = barBgGo.GetComponent<Image>();
                barBgImg.sprite = whiteSprite;
                barBgImg.color = new Color(0.05f, 0.06f, 0.09f, 0.9f);
                barBgImg.raycastTarget = false;

                var barFillGo = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
                barFillGo.transform.SetParent(barBgRect, false);
                RectTransform barFillRect = barFillGo.GetComponent<RectTransform>();
                barFillRect.anchorMin = Vector2.zero;
                barFillRect.anchorMax = Vector2.one;
                barFillRect.offsetMin = Vector2.zero;
                barFillRect.offsetMax = Vector2.zero;
                Image barFillImg = barFillGo.GetComponent<Image>();
                barFillImg.sprite = whiteSprite;
                barFillImg.type = Image.Type.Filled;
                barFillImg.fillMethod = Image.FillMethod.Horizontal;
                barFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                barFillImg.fillAmount = 1f;
                barFillImg.color = new Color(1.0f, 0.56f, 0.14f, 1f);
                barFillImg.raycastTarget = false;

                contentGo.SetActive(false);

                SerializedObject bannerSo = new SerializedObject(bannerUi);
                bannerSo.FindProperty("bannerRoot").objectReferenceValue = contentGo;
                bannerSo.FindProperty("backgroundImage").objectReferenceValue = bgImage;
                bannerSo.FindProperty("borderOutline").objectReferenceValue = outline;
                bannerSo.FindProperty("titleText").objectReferenceValue = titleText;
                bannerSo.FindProperty("subtitleText").objectReferenceValue = subtitleText;
                bannerSo.FindProperty("statusText").objectReferenceValue = statusText;
                bannerSo.FindProperty("progressFillImage").objectReferenceValue = barFillImg;
                bannerSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bannerUi);
            }
            else
            {
                Debug.LogWarning("[Sprint10SetupTool] Could not find SafeAreaHUD in Gameplay.unity.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static TextMeshProUGUI CreateBannerTmp(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static TMP_FontAsset ResolveSceneFont(Transform root)
        {
            TextMeshProUGUI existing = root.GetComponentInChildren<TextMeshProUGUI>(true);
            return existing != null && existing.font != null ? existing.font : TMP_Settings.defaultFontAsset;
        }

        private static Transform FindTransformInScene(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindRecursive(roots[i].transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindRecursive(Transform current, string name)
        {
            if (current.name == name) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = FindRecursive(current.GetChild(i), name);
                if (child != null) return child;
            }
            return null;
        }
    }
}
