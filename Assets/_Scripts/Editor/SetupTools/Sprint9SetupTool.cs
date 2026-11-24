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
using ProjectB.Data;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Visuals;

namespace ProjectB.EditorScripts
{
    [System.Obsolete("Legacy one-shot setup tool. Scenes and prefabs are the source of truth (Sprint 15.4).")]
    public static class Sprint9SetupTool
    {
        private const string VOXEL_SHADER_NAME = "ProjectB/VoxelLit";
        private const string VOXEL_MAT_DIR = "Assets/_Materials/Voxels";
        private const string ABILITY_PREFAB_DIR = "Assets/_Prefabs/Abilities";
        private const string PROJ_PREFAB_DIR = "Assets/_Prefabs/Projectiles";
        private const string ABILITY_SO_DIR = "Assets/_ScriptableObjects/Abilities";
        private const string HERO_PREFAB_PATH = "Assets/_Prefabs/Hero.prefab";
        private const string AUDIO_CATALOG_PATH = "Assets/Resources/AudioCatalog.asset";
        private const string VIGNETTE_SPRITE_PATH = "Assets/_UI/Icons/Generated/ui_damage_vignette.png";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";

        private static readonly string[] CharacterVoxelMaterials =
        {
            "Knight_Mat",
            "Mage_Mat", "Mage_Glow_Mat",
            "Ranger_Mat", "Ranger_Glow_Mat",
            "NecromancerHero_Mat", "NecromancerHero_Glow_Mat",
            "Goblin_Mat",
            "Golem_Mat", "Golem_Glow_Mat",
            "Necromancer_Mat", "Necromancer_Glow_Mat",
            "Skeleton_Mat", "Skeleton_Glow_Mat",
            "BombSlime_Mat", "BombSlime_Glow_Mat",
            "OrcCharger_Mat", "OrcCharger_Glow_Mat",
            "FrostLich_Mat", "FrostLich_Glow_Mat"
        };
        public static void RunAll()
        {
            EnsureFolder(VOXEL_MAT_DIR);
            EnsureFolder(ABILITY_PREFAB_DIR);
            EnsureFolder(PROJ_PREFAB_DIR);
            EnsureFolder("Assets/_UI/Icons/Generated");

            MigrateCharacterAndEnemyMaterialsToVoxelLit();
            SetupSlimeAndBatVoxelLitMaterials();

            var dragonMats = EnsureLitMaterialPair("DragonWhelp", "Assets/_VoxelModels/DragonWhelp/dragon_whelp.png");
            var shieldMats = EnsureLitMaterialPair("GuardianShield", "Assets/_VoxelModels/GuardianShield/guardian_shield.png");
            var flaskMats = EnsureLitMaterialPair("PoisonFlask", "Assets/_VoxelModels/PoisonFlask/poison_flask.png");
            var puddleMats = EnsureLitMaterialPair("PoisonPuddle", "Assets/_VoxelModels/PoisonPuddle/poison_puddle.png");
            var tornadoMats = EnsureLitMaterialPair("VoxelTornado", "Assets/_VoxelModels/VoxelTornado/voxel_tornado.png");
            var seismicMats = EnsureLitMaterialPair("SeismicRing", "Assets/_VoxelModels/SeismicRing/seismic_ring.png");
            var aegisMats = EnsureTransparentAegisMaterialPair("AegisShield", "Assets/_VoxelModels/AegisShield/aegis_shield.png");
            var iceRingMats = EnsureLitMaterialPair("IceAuraRing", "Assets/_VoxelModels/IceAuraRing/ice_aura_ring.png", emissionIntensity: 1.5f);
            var holeRingMats = EnsureLitMaterialPair("BlackHoleRing", "Assets/_VoxelModels/BlackHoleRing/black_hole_ring.png", emissionIntensity: 1.75f);

            GameObject dragonFollowerPrefab = BuildDragonWhelpFollowerPrefab(dragonMats);
            GameObject dragonFlamePrefab = BuildDragonFlameProjectilePrefab();
            GameObject guardianShieldPrefab = BuildSimpleVoxelVisualPrefab(
                $"{ABILITY_PREFAB_DIR}/GuardianShieldVisual.prefab",
                "GuardianShieldVisual",
                "Assets/_VoxelModels/GuardianShield/GuardianShield.obj",
                shieldMats);
            GameObject poisonFlaskPrefab = BuildPoisonFlaskProjectilePrefab(flaskMats);
            GameObject poisonPuddlePrefab = BuildSimpleVoxelVisualPrefab(
                $"{ABILITY_PREFAB_DIR}/PoisonPuddleVisual.prefab",
                "PoisonPuddleVisual",
                "Assets/_VoxelModels/PoisonPuddle/PoisonPuddle.obj",
                puddleMats);
            GameObject tornadoVisualPrefab = BuildSimpleVoxelVisualPrefab(
                $"{ABILITY_PREFAB_DIR}/VoxelTornadoVisual.prefab",
                "VoxelTornadoVisual",
                "Assets/_VoxelModels/VoxelTornado/VoxelTornado.obj",
                tornadoMats);
            GameObject iceAuraPrefab = BuildSimpleVoxelVisualPrefab(
                $"{ABILITY_PREFAB_DIR}/IceAuraVisual.prefab",
                "IceAuraVisual",
                "Assets/_VoxelModels/IceAuraRing/IceAuraRing.obj",
                iceRingMats);
            GameObject blackHolePrefab = BuildSimpleVoxelVisualPrefab(
                $"{ABILITY_PREFAB_DIR}/BlackHoleVisual.prefab",
                "BlackHoleVisual",
                "Assets/_VoxelModels/BlackHoleRing/BlackHoleRing.obj",
                holeRingMats);
            GameObject seismicRingPrefab = BuildSimpleVoxelVisualPrefab(
                $"{ABILITY_PREFAB_DIR}/SeismicRingVisual.prefab",
                "SeismicRingVisual",
                "Assets/_VoxelModels/SeismicRing/SeismicRing.obj",
                seismicMats);

            WireAbilityAssets(
                dragonFollowerPrefab,
                dragonFlamePrefab,
                guardianShieldPrefab,
                poisonFlaskPrefab,
                poisonPuddlePrefab,
                tornadoVisualPrefab,
                iceAuraPrefab,
                blackHolePrefab,
                seismicRingPrefab);

            WireKnightAegisShieldPrefab(aegisMats);
            WireAudioCatalog();

            Sprite vignetteSprite = GenerateDamageVignetteSprite();
            WireGameplayScene(vignetteSprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint9SetupTool] Sprint 9 VoxelLit shader migration, voxel prefabs, Multi-POI compass, damage vignette, and audio catalog setup completed.");
        }

        private static void MigrateCharacterAndEnemyMaterialsToVoxelLit()
        {
            Shader voxelLit = Shader.Find(VOXEL_SHADER_NAME);
            if (voxelLit == null)
            {
                Debug.LogError($"[Sprint9SetupTool] Shader '{VOXEL_SHADER_NAME}' not found!");
                return;
            }

            for (int i = 0; i < CharacterVoxelMaterials.Length; i++)
            {
                string matName = CharacterVoxelMaterials[i];
                string path = $"{VOXEL_MAT_DIR}/{matName}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    Debug.LogWarning($"[Sprint9SetupTool] Material not found: {path}");
                    continue;
                }

                Texture baseTex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                if (baseTex == null && mat.HasProperty("_MainTex")) baseTex = mat.GetTexture("_MainTex");
                Texture emissionTex = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                bool isGlow = matName.EndsWith("_Glow_Mat") || mat.IsKeywordEnabled("_EMISSION");
                Color emissionColor = mat.HasProperty("_EmissionColor")
                    ? mat.GetColor("_EmissionColor")
                    : (isGlow ? new Color(1.8f, 1.8f, 1.8f, 1f) : Color.black);
                if (isGlow && emissionColor.maxColorComponent < 0.1f)
                {
                    emissionColor = new Color(1.8f, 1.8f, 1.8f, 1f);
                }

                mat.shader = voxelLit;
                ApplyVoxelLitMaterialDefaults(mat, baseTex, isGlow, emissionTex ?? baseTex, emissionColor);
            }
        }

        private static void SetupSlimeAndBatVoxelLitMaterials()
        {
            Shader voxelLit = Shader.Find(VOXEL_SHADER_NAME);
            if (voxelLit == null) return;

            ConfigurePaletteTexture("Assets/_VoxelModels/Slime/slime.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/Bat/Bat_Fly_00.png");
            ConfigurePaletteTexture("Assets/_VoxelModels/Bat/Bat_Head.png");

            Texture2D slimeTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_VoxelModels/Slime/slime.png");
            Texture2D batBodyTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_VoxelModels/Bat/Bat_Fly_00.png");
            Texture2D batHeadTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_VoxelModels/Bat/Bat_Head.png");

            Material slimeMat = LoadOrCreateMaterial($"{VOXEL_MAT_DIR}/Slime_Mat.mat", voxelLit);
            ApplyVoxelLitMaterialDefaults(slimeMat, slimeTex, isGlow: false, null, Color.black);

            Material batBodyMat = LoadOrCreateMaterial($"{VOXEL_MAT_DIR}/Bat_Mat.mat", voxelLit);
            ApplyVoxelLitMaterialDefaults(batBodyMat, batBodyTex, isGlow: false, null, Color.black);

            Material batHeadMat = LoadOrCreateMaterial($"{VOXEL_MAT_DIR}/Bat_Head_Mat.mat", voxelLit);
            ApplyVoxelLitMaterialDefaults(batHeadMat, batHeadTex, isGlow: false, null, Color.black);

            AssignPrefabChildMaterial("Assets/_Prefabs/Enemies/Slime.prefab", "slime", slimeMat);
            AssignPrefabChildMaterial("Assets/_Prefabs/Enemies/BatShooter.prefab", "Visuals/Bat_Body", batBodyMat);
            AssignPrefabChildMaterial("Assets/_Prefabs/Enemies/BatShooter.prefab", "Visuals/Bat_Head", batHeadMat);
        }

        private static void AssignPrefabChildMaterial(string prefabPath, string childPath, Material mat)
        {
            if (mat == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;

            try
            {
                Transform child = root.transform.Find(childPath);
                if (child != null)
                {
                    var mr = child.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.sharedMaterial = mat;
                        EditorUtility.SetDirty(mr);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyVoxelLitMaterialDefaults(
            Material mat,
            Texture baseTex,
            bool isGlow,
            Texture emissionTex,
            Color emissionColor)
        {
            if (mat == null) return;
            if (baseTex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseTex);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            if (mat.HasProperty("_HitFlash")) mat.SetFloat("_HitFlash", 0f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", Color.clear);
            if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", 2.6f);
            if (mat.HasProperty("_HeightAO")) mat.SetFloat("_HeightAO", 0.25f);
            if (mat.HasProperty("_HeightAORange")) mat.SetFloat("_HeightAORange", 0.75f);

            if (isGlow)
            {
                mat.EnableKeyword("_EMISSION");
                if (emissionTex != null && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emissionTex);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        private static (Material baseMat, Material glowMat) EnsureLitMaterialPair(
            string prefix,
            string palettePath,
            float emissionIntensity = 1.8f)
        {
            ConfigurePaletteTexture(palettePath);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);

            Material baseMat = LoadOrCreateMaterial($"{VOXEL_MAT_DIR}/{prefix}_Mat.mat", urpLit);
            ConfigureUrpLitMaterial(baseMat, tex, emissive: false, emissionIntensity);

            Material glowMat = LoadOrCreateMaterial($"{VOXEL_MAT_DIR}/{prefix}_Glow_Mat.mat", urpLit);
            ConfigureUrpLitMaterial(glowMat, tex, emissive: true, emissionIntensity);

            return (baseMat, glowMat);
        }

        private static (Material baseMat, Material glowMat) EnsureTransparentAegisMaterialPair(string prefix, string palettePath)
        {
            ConfigurePaletteTexture(palettePath);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);

            Material baseMat = LoadOrCreateMaterial($"{VOXEL_MAT_DIR}/{prefix}_Mat.mat", urpLit);
            ConfigureTransparentUrpLit(baseMat, tex, new Color(1f, 1f, 1f, 0.45f), emissive: false, queue: (int)RenderQueue.Transparent);

            Material glowMat = LoadOrCreateMaterial($"{VOXEL_MAT_DIR}/{prefix}_Glow_Mat.mat", urpLit);
            ConfigureTransparentUrpLit(glowMat, tex, new Color(1f, 1f, 1f, 0.88f), emissive: true, queue: (int)RenderQueue.Transparent + 1);

            return (baseMat, glowMat);
        }

        private static void ConfigureUrpLitMaterial(Material mat, Texture2D tex, bool emissive, float emissionIntensity)
        {
            if (mat == null) return;
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                if (emissive && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.14f);

            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(emissionIntensity, emissionIntensity, emissionIntensity, 1f));
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

        private static void ConfigureTransparentUrpLit(Material mat, Texture2D tex, Color baseColor, bool emissive, int queue)
        {
            if (mat == null) return;
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                if (emissive && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = queue;

            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1.6f, 1.6f, 1.6f, 1f));
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        private static GameObject BuildDragonWhelpFollowerPrefab((Material baseMat, Material glowMat) mats)
        {
            string prefabPath = $"{ABILITY_PREFAB_DIR}/DragonWhelpFollower.prefab";
            var root = new GameObject("DragonWhelpFollower");
            try
            {
                root.AddComponent<DragonFollower>();

                var visuals = new GameObject("Visuals");
                visuals.transform.SetParent(root.transform, false);

                Mesh idle = LoadObjMesh("Assets/_VoxelModels/DragonWhelp/DragonWhelp_Idle.obj");
                Mesh[] flyFrames =
                {
                    LoadObjMesh("Assets/_VoxelModels/DragonWhelp/DragonWhelp_Fly_00.obj"),
                    LoadObjMesh("Assets/_VoxelModels/DragonWhelp/DragonWhelp_Fly_01.obj"),
                    LoadObjMesh("Assets/_VoxelModels/DragonWhelp/DragonWhelp_Fly_02.obj"),
                    LoadObjMesh("Assets/_VoxelModels/DragonWhelp/DragonWhelp_Fly_03.obj")
                };

                var mf = visuals.AddComponent<MeshFilter>();
                mf.sharedMesh = idle;

                var mr = visuals.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                AssignMaterialsForMesh(mr, idle, mats);

                var anim = visuals.AddComponent<VoxelMeshAnimator>();
                var so = new SerializedObject(anim);
                so.FindProperty("targetMeshFilter").objectReferenceValue = mf;
                so.FindProperty("idleMesh").objectReferenceValue = idle;
                var framesProp = so.FindProperty("frames");
                framesProp.arraySize = flyFrames.Length;
                for (int i = 0; i < flyFrames.Length; i++)
                {
                    framesProp.GetArrayElementAtIndex(i).objectReferenceValue = flyFrames[i];
                }
                so.FindProperty("playOnEnable").boolValue = true;
                so.FindProperty("framesPerSecond").floatValue = 12f;
                so.FindProperty("loop").boolValue = true;
                so.FindProperty("randomizeStartOffset").boolValue = true;
                so.FindProperty("enableBobbing").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                var muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(root.transform, false);
                muzzle.transform.localPosition = new Vector3(0f, 0.12f, 0.52f);

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildDragonFlameProjectilePrefab()
        {
            string prefabPath = $"{PROJ_PREFAB_DIR}/DragonFlameProjectile.prefab";
            var root = new GameObject("DragonFlameProjectile");
            try
            {
                root.AddComponent<DragonFlameProjectile>();

                var visuals = new GameObject("Visuals");
                visuals.transform.SetParent(root.transform, false);
                visuals.transform.localScale = Vector3.one * 0.72f;

                Mesh mesh = LoadObjMesh("Assets/_VoxelModels/FireballProjectile/FireballProjectile.obj");
                Material baseMat = AssetDatabase.LoadAssetAtPath<Material>($"{VOXEL_MAT_DIR}/FireballProjectile_Mat.mat");
                Material glowMat = AssetDatabase.LoadAssetAtPath<Material>($"{VOXEL_MAT_DIR}/FireballProjectile_Glow_Mat.mat");

                var mf = visuals.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = visuals.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                AssignMaterialsForMesh(mr, mesh, (baseMat, glowMat));

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildPoisonFlaskProjectilePrefab((Material baseMat, Material glowMat) mats)
        {
            string prefabPath = $"{PROJ_PREFAB_DIR}/PoisonFlaskProjectile.prefab";
            var root = new GameObject("PoisonFlaskProjectile");
            try
            {
                root.AddComponent<PoisonFlaskProjectile>();

                var visuals = new GameObject("Visuals");
                visuals.transform.SetParent(root.transform, false);

                Mesh mesh = LoadObjMesh("Assets/_VoxelModels/PoisonFlask/PoisonFlask.obj");
                var mf = visuals.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = visuals.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                AssignMaterialsForMesh(mr, mesh, mats);

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildSimpleVoxelVisualPrefab(
            string prefabPath,
            string rootName,
            string objPath,
            (Material baseMat, Material glowMat) mats)
        {
            var root = new GameObject(rootName);
            try
            {
                var visuals = new GameObject("Visuals");
                visuals.transform.SetParent(root.transform, false);

                Mesh mesh = LoadObjMesh(objPath);
                var mf = visuals.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = visuals.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                AssignMaterialsForMesh(mr, mesh, mats);

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WireAbilityAssets(
            GameObject dragonFollowerPrefab,
            GameObject dragonFlamePrefab,
            GameObject guardianShieldPrefab,
            GameObject poisonFlaskPrefab,
            GameObject poisonPuddlePrefab,
            GameObject tornadoVisualPrefab,
            GameObject iceAuraPrefab,
            GameObject blackHolePrefab,
            GameObject seismicRingPrefab)
        {
            var miniDragon = AssetDatabase.LoadAssetAtPath<MiniDragonAbilityData>($"{ABILITY_SO_DIR}/MiniDragonData.asset");
            if (miniDragon != null)
            {
                miniDragon.followerPrefab = dragonFollowerPrefab;
                miniDragon.flamePrefab = dragonFlamePrefab;
                EditorUtility.SetDirty(miniDragon);
            }

            var guardian = AssetDatabase.LoadAssetAtPath<GuardianShieldAbilityData>($"{ABILITY_SO_DIR}/GuardianShieldData.asset");
            if (guardian != null)
            {
                guardian.shieldPrefab = guardianShieldPrefab;
                EditorUtility.SetDirty(guardian);
            }

            var poison = AssetDatabase.LoadAssetAtPath<PoisonFlaskAbilityData>($"{ABILITY_SO_DIR}/PoisonFlaskData.asset");
            if (poison != null)
            {
                poison.flaskPrefab = poisonFlaskPrefab;
                poison.puddleVisualPrefab = poisonPuddlePrefab;
                EditorUtility.SetDirty(poison);
            }

            var tornado = AssetDatabase.LoadAssetAtPath<VoxelTornadoAbilityData>($"{ABILITY_SO_DIR}/VoxelTornadoData.asset");
            if (tornado != null)
            {
                tornado.tornadoVisualPrefab = tornadoVisualPrefab;
                EditorUtility.SetDirty(tornado);
            }

            var iceAura = AssetDatabase.LoadAssetAtPath<IceAuraAbilityData>($"{ABILITY_SO_DIR}/IceAuraData.asset");
            if (iceAura != null)
            {
                iceAura.visualPrefab = iceAuraPrefab;
                EditorUtility.SetDirty(iceAura);
            }

            var blackHole = AssetDatabase.LoadAssetAtPath<BlackHoleAbilityData>($"{ABILITY_SO_DIR}/BlackHoleData.asset");
            if (blackHole != null)
            {
                blackHole.visualPrefab = blackHolePrefab;
                EditorUtility.SetDirty(blackHole);
            }

            var seismic = AssetDatabase.LoadAssetAtPath<SeismicHammerAbilityData>($"{ABILITY_SO_DIR}/SeismicHammerData.asset");
            if (seismic != null)
            {
                seismic.waveVisualPrefab = seismicRingPrefab;
                EditorUtility.SetDirty(seismic);
            }
        }

        private static void WireKnightAegisShieldPrefab((Material baseMat, Material glowMat) aegisMats)
        {
            Mesh aegisMesh = LoadObjMesh("Assets/_VoxelModels/AegisShield/AegisShield.obj");
            if (aegisMesh == null) return;

            GameObject heroRoot = PrefabUtility.LoadPrefabContents(HERO_PREFAB_PATH);
            if (heroRoot == null) return;

            try
            {
                Transform shieldTf = heroRoot.transform.Find("ShieldVisual");
                if (shieldTf != null)
                {
                    shieldTf.localPosition = Vector3.zero;
                    shieldTf.localRotation = Quaternion.identity;
                    shieldTf.localScale = Vector3.one;

                    var mf = shieldTf.GetComponent<MeshFilter>() ?? shieldTf.gameObject.AddComponent<MeshFilter>();
                    mf.sharedMesh = aegisMesh;

                    var mr = shieldTf.GetComponent<MeshRenderer>() ?? shieldTf.gameObject.AddComponent<MeshRenderer>();
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    AssignMaterialsForMesh(mr, aegisMesh, aegisMats);

                    var ult = heroRoot.GetComponent<HeroUltimate>();
                    if (ult != null)
                    {
                        var so = new SerializedObject(ult);
                        so.FindProperty("shieldVisual").objectReferenceValue = shieldTf.gameObject;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(heroRoot, HERO_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(heroRoot);
            }
        }

        private static void WireAudioCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AUDIO_CATALOG_PATH);
            if (catalog == null)
            {
                Debug.LogError($"[Sprint9SetupTool] AudioCatalog not found at {AUDIO_CATALOG_PATH}");
                return;
            }

            catalog.explosionClip = LoadClip("Assets/_Audio/SFX/sfx_explosion.wav");
            catalog.lightningZapClip = LoadClip("Assets/_Audio/SFX/sfx_lightning_zap.wav");
            catalog.critHitClip = LoadClip("Assets/_Audio/SFX/sfx_crit_hit.wav");
            catalog.synergyFanfareClip = LoadClip("Assets/_Audio/SFX/sfx_synergy_fanfare.wav");
            catalog.poisonSplashClip = LoadClip("Assets/_Audio/SFX/sfx_poison_splash.wav");
            catalog.arrowWhooshClip = LoadClip("Assets/_Audio/SFX/sfx_arrow_whoosh.wav");
            catalog.iceShatterClip = LoadClip("Assets/_Audio/SFX/sfx_ice_shatter.wav");
            catalog.bossDeathClip = LoadClip("Assets/_Audio/SFX/sfx_boss_death.wav");
            catalog.shrineStartClip = LoadClip("Assets/_Audio/SFX/sfx_shrine_start.wav");
            catalog.comboMilestoneClip = LoadClip("Assets/_Audio/SFX/sfx_combo_milestone.wav");
            catalog.bossMusicClip = LoadClip("Assets/_Audio/Music/music_boss.wav");

            EditorUtility.SetDirty(catalog);
        }

        private static AudioClip LoadClip(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"[Sprint9SetupTool] Missing AudioClip at {path}");
            }
            return clip;
        }

        private static Sprite GenerateDamageVignetteSprite()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            var pixels = new Color32[size * size];

            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float ny = (y - half) / half;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - half) / half;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    float edge = Mathf.Clamp01((dist - 0.52f) / 0.65f);
                    float alpha = edge * edge * (3f - 2f * edge);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(VIGNETTE_SPRITE_PATH, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(VIGNETTE_SPRITE_PATH, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(VIGNETTE_SPRITE_PATH) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(VIGNETTE_SPRITE_PATH);
        }

        private static void WireGameplayScene(Sprite vignetteSprite)
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath(GAMEPLAY_SCENE_PATH);
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                Canvas canvas = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    canvas = root.GetComponentInChildren<Canvas>(true);
                    if (canvas != null) break;
                }

                if (canvas != null)
                {
                    SetupDamageVignette(canvas, vignetteSprite);
                    SetupMultiPoiCompass(canvas);
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

        private static void SetupDamageVignette(Canvas canvas, Sprite vignetteSprite)
        {
            Transform existing = canvas.transform.Find("DamageVignette");
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject("DamageVignette", typeof(RectTransform), typeof(Image), typeof(DamageVignetteUI));
            if (existing == null)
            {
                go.transform.SetParent(canvas.transform, false);
            }

            go.transform.SetAsFirstSibling();

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite = vignetteSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = new Color(0.95f, 0.08f, 0.08f, 0f);
            img.raycastTarget = false;
            img.enabled = false;

            var vignette = go.GetComponent<DamageVignetteUI>() ?? go.AddComponent<DamageVignetteUI>();
            var so = new SerializedObject(vignette);
            so.FindProperty("vignetteImage").objectReferenceValue = img;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(vignette);
        }

        private static void SetupMultiPoiCompass(Canvas canvas)
        {
            var compass = canvas.GetComponentInChildren<SecretCompassIndicatorUI>(true);
            if (compass == null)
            {
                Debug.LogWarning("[Sprint9SetupTool] SecretCompassIndicatorUI not found in Gameplay.unity.");
                return;
            }

            var compassSo = new SerializedObject(compass);
            var slot0Rect = compassSo.FindProperty("indicatorRect").objectReferenceValue as RectTransform;
            if (slot0Rect == null) return;

            var extraProp = compassSo.FindProperty("extraSlots");
            extraProp.arraySize = 2;

            for (int slotIdx = 1; slotIdx <= 2; slotIdx++)
            {
                string slotName = $"Content_Slot{slotIdx}";
                Transform existingSlot = compass.transform.Find(slotName);
                GameObject slotGo;
                if (existingSlot != null)
                {
                    slotGo = existingSlot.gameObject;
                }
                else
                {
                    slotGo = Object.Instantiate(slot0Rect.gameObject, compass.transform, false);
                    slotGo.name = slotName;
                }

                slotGo.SetActive(false);
                var graphics = slotGo.GetComponentsInChildren<Graphic>(true);
                for (int g = 0; g < graphics.Length; g++)
                {
                    if (graphics[g] != null) graphics[g].raycastTarget = false;
                }

                Transform arrowTf = slotGo.transform.Find("Arrow");
                Transform textTf = slotGo.transform.Find("DistanceText");

                SerializedProperty elem = extraProp.GetArrayElementAtIndex(slotIdx - 1);
                elem.FindPropertyRelative("indicatorRect").objectReferenceValue = (RectTransform)slotGo.transform;
                elem.FindPropertyRelative("arrowRect").objectReferenceValue = arrowTf as RectTransform;
                elem.FindPropertyRelative("arrowImage").objectReferenceValue = arrowTf != null ? arrowTf.GetComponent<Image>() : slotGo.GetComponentInChildren<Image>(true);
                elem.FindPropertyRelative("distanceText").objectReferenceValue = textTf != null ? textTf.GetComponent<TextMeshProUGUI>() : slotGo.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            compassSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(compass);
        }

        private static void ConfigurePaletteTexture(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Sprint9SetupTool] Palette texture not found at {assetPath}");
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

        private static void AssignMaterialsForMesh(MeshRenderer mr, Mesh mesh, (Material baseMat, Material glowMat) mats)
        {
            int subMeshes = mesh != null ? Mathf.Max(1, mesh.subMeshCount) : 2;
            mr.sharedMaterials = subMeshes >= 2 && mats.glowMat != null
                ? new[] { mats.baseMat, mats.glowMat }
                : new[] { mats.baseMat };
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

        private static Mesh LoadObjMesh(string objPath)
        {
            AssetDatabase.ImportAsset(objPath, ImportAssetOptions.ForceUpdate);
            Mesh direct = AssetDatabase.LoadAssetAtPath<Mesh>(objPath);
            if (direct != null) return direct;

            Object[] all = AssetDatabase.LoadAllAssetsAtPath(objPath);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Mesh mesh) return mesh;
            }

            Debug.LogError($"[Sprint9SetupTool] Failed to load Mesh from {objPath}");
            return null;
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
