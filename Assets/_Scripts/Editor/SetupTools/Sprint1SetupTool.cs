using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.UI;
using ProjectB.Visuals;

namespace ProjectB.EditorScripts
{
    [System.Obsolete("Legacy one-shot setup tool. Scenes and prefabs are the source of truth (Sprint 15.4).")]
    public static class Sprint1SetupTool
    {
        public static void RunAll()
        {
            SetupGoblinAndBatWavesData();
            SetupMageAndAchievement();
            VoxelIconGenerator.GenerateAllIcons();
            VoxelIconGenerator.GenerateFloorGridTexture();
            FixLaserTurretMaterial();
            FixSlimePrefab();
            CleanDefaultVolumeProfile();
            SetupGameplayScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint1SetupTool] All Sprint 1 asset, prefab, volume, and scene changes applied successfully.");
        }

        private static void SetupGoblinAndBatWavesData()
        {
            const string goblinPath = "Assets/_ScriptableObjects/GoblinData.asset";
            var goblin = AssetDatabase.LoadAssetAtPath<ShootingEnemyData>(goblinPath);
            if (goblin == null)
            {
                goblin = ScriptableObject.CreateInstance<ShootingEnemyData>();
                AssetDatabase.CreateAsset(goblin, goblinPath);
            }

            goblin.hp = 28;
            goblin.speed = 2.8f;
            goblin.contactDamage = 5;
            goblin.damageCooldown = 1f;
            goblin.contactRadius = 1f;
            goblin.xpDrop = 2;
            goblin.coinDropChance = 0.25f;
            goblin.coinDrop = 1;
            goblin.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/GoblinShooter.prefab");
            goblin.weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_ScriptableObjects/EnemyShooterWeapon.asset");
            goblin.stopDistance = 5.5f;
            EditorUtility.SetDirty(goblin);

            var bat = AssetDatabase.LoadAssetAtPath<ShootingEnemyData>("Assets/_ScriptableObjects/BatData.asset");
            if (bat != null)
            {
                bat.hp = 38;
                bat.speed = 3.2f;
                EditorUtility.SetDirty(bat);
            }
        }

        private static void SetupMageAndAchievement()
        {
            const string mageWeaponPath = "Assets/_ScriptableObjects/MageWeaponData.asset";
            var mageWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>(mageWeaponPath);
            if (mageWeapon == null)
            {
                mageWeapon = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(mageWeapon, mageWeaponPath);
            }

            mageWeapon.damage = 14;
            mageWeapon.attackCooldown = 0.65f;
            mageWeapon.range = 10f;
            mageWeapon.projectileSpeed = 14f;
            mageWeapon.splashRadius = 1.5f;
            mageWeapon.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Projectile.prefab");
            EditorUtility.SetDirty(mageWeapon);

            var mage = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/MageData.asset");
            if (mage != null)
            {
                mage.unlockedByDefault = false;
                mage.startingWeapon = mageWeapon;
                mage.passiveType = HeroPassiveType.GlobalDamagePercent;
                mage.passiveValue = 20f;
                mage.passiveName = "Arcane Mastery";
                mage.passiveDescription = "+20% Global Damage\n<b>Weapon:</b> Arcane Blast (14 AoE dmg, 1.5m splash)";
                mage.ultimateType = HeroUltimateType.MeteorStrike;
                mage.ultimateName = "Meteor Shower";
                mage.ultimateDescription = "Calls down 6 explosive meteors around you, dealing 80 AoE damage each in a 3.5m radius.";
                mage.ultimateDuration = 3f;
                mage.ultimateCooldown = 22f;
                EditorUtility.SetDirty(mage);
            }

            var reach25 = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/ReachWave25.asset");
            if (reach25 != null)
            {
                reach25.rewardType = AchievementRewardType.UnlockHero;
                reach25.rewardId = "mage";
                reach25.rewardValue = 0;
                reach25.description = "Reach Wave 25 to unlock the Mage hero";
                EditorUtility.SetDirty(reach25);
            }
        }

        private static void FixLaserTurretMaterial()
        {
            const string matPath = "Assets/_Materials/Voxels/Turret_Mat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (urpLit != null && mat.shader != urpLit)
            {
                mat.shader = urpLit;
            }

            Texture2D turretTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_VoxelModels/Turret/turret-0.png");
            if (turretTex != null)
            {
                mat.SetTexture("_BaseMap", turretTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", turretTex);
            }
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            const string prefabPath = "Assets/_Prefabs/LaserTurret.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform visuals = root.transform.Find("Visuals");
                if (visuals != null)
                {
                    var mr = visuals.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.sharedMaterial = mat;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void FixSlimePrefab()
        {
            const string prefabPath = "Assets/_Prefabs/Enemies/Slime.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                root.transform.localPosition = Vector3.zero;
                Transform slimeChild = root.transform.Find("slime");
                if (slimeChild != null)
                {
                    var mf = slimeChild.GetComponent<MeshFilter>();
                    var anim = slimeChild.GetComponent<VoxelMeshAnimator>();
                    if (anim == null)
                    {
                        anim = slimeChild.gameObject.AddComponent<VoxelMeshAnimator>();
                    }

                    var so = new SerializedObject(anim);
                    so.FindProperty("targetMeshFilter").objectReferenceValue = mf;
                    so.FindProperty("idleMesh").objectReferenceValue = mf != null ? mf.sharedMesh : null;
                    so.FindProperty("playOnEnable").boolValue = true;
                    so.FindProperty("randomizeStartOffset").boolValue = true;
                    so.FindProperty("enableBobbing").boolValue = true;
                    so.FindProperty("bobbingTransform").objectReferenceValue = slimeChild;
                    so.FindProperty("bobbingSpeed").floatValue = 6f;
                    so.FindProperty("bobbingHeight").floatValue = 0.12f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CleanDefaultVolumeProfile()
        {
            const string targetAssetPath = "Assets/Settings/DefaultVolumeProfile.asset";
            const string tempAssetPath = "Assets/Settings/_TempCleanVolumeProfile.asset";

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(tempAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(tempAssetPath);
            }

            var cleanProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            cleanProfile.name = "DefaultVolumeProfile";
            AssetDatabase.CreateAsset(cleanProfile, tempAssetPath);

            var bloom = AddCleanVolumeComponent<Bloom>(cleanProfile);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.35f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.65f;

            var vignette = AddCleanVolumeComponent<Vignette>(cleanProfile);
            vignette.color.overrideState = true;
            vignette.color.value = Color.black;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.22f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.35f;

            var colorAdj = AddCleanVolumeComponent<ColorAdjustments>(cleanProfile);
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 12f;
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = 10f;

            EditorUtility.SetDirty(cleanProfile);
            AssetDatabase.SaveAssets();

            string projectDir = Directory.GetCurrentDirectory();
            string tempFull = Path.Combine(projectDir, tempAssetPath);
            string targetFull = Path.Combine(projectDir, targetAssetPath);
            string cleanYaml = File.ReadAllText(tempFull).Replace("m_Name: _TempCleanVolumeProfile", "m_Name: DefaultVolumeProfile");
            File.WriteAllText(targetFull, cleanYaml);

            AssetDatabase.DeleteAsset(tempAssetPath);
            AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static T AddCleanVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var comp = profile.Add<T>(overrides: true);
            comp.active = true;
            comp.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            if (!AssetDatabase.IsSubAsset(comp))
            {
                AssetDatabase.AddObjectToAsset(comp, profile);
            }
            EditorUtility.SetDirty(comp);
            return comp;
        }

        private static void SetupGameplayScene()
        {
            const string scenePath = "Assets/_Scenes/Gameplay.unity";
            Scene alreadyLoaded = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();

                // 1. Ensure GoblinData is present in WaveConfig.enemySchedule
                var goblin = AssetDatabase.LoadAssetAtPath<ShootingEnemyData>("Assets/_ScriptableObjects/GoblinData.asset");
                var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
                WaveManager waveMgr = null;
                Canvas canvas = null;

                foreach (var root in roots)
                {
                    if (waveMgr == null) waveMgr = root.GetComponentInChildren<WaveManager>(true);
                    if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
                }

                if (waveConfig != null && goblin != null)
                {
                    if (waveConfig.enemySchedule == null)
                    {
                        waveConfig.enemySchedule = new List<EnemyWaveEntry>();
                    }

                    bool found = false;
                    for (int i = 0; i < waveConfig.enemySchedule.Count; i++)
                    {
                        if (waveConfig.enemySchedule[i] != null && waveConfig.enemySchedule[i].enemy == goblin)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        waveConfig.enemySchedule.Add(new EnemyWaveEntry
                        {
                            enemy = goblin,
                            minWave = 3,
                            maxWave = 0,
                            spawnWeight = 1f,
                            packSize = 1
                        });
                        EditorUtility.SetDirty(waveConfig);
                    }
                }

                // 2. Add SecretCompassIndicatorUI under SafeAreaHUD (or Canvas)
                if (canvas != null)
                {
                    SecretCompassIndicatorUI existing = canvas.GetComponentInChildren<SecretCompassIndicatorUI>(true);
                    if (existing == null)
                    {
                        Transform safeArea = canvas.transform.Find("SafeAreaHUD");
                        Transform parentTransform = safeArea != null ? safeArea : canvas.transform;

                        var rootGo = new GameObject("SecretCompassIndicator", typeof(RectTransform), typeof(SecretCompassIndicatorUI));
                        rootGo.transform.SetParent(parentTransform, false);
                        var rootRt = (RectTransform)rootGo.transform;
                        rootRt.anchorMin = Vector2.zero;
                        rootRt.anchorMax = Vector2.one;
                        rootRt.offsetMin = Vector2.zero;
                        rootRt.offsetMax = Vector2.zero;

                        var contentGo = new GameObject("Content", typeof(RectTransform));
                        contentGo.transform.SetParent(rootGo.transform, false);
                        var contentRt = (RectTransform)contentGo.transform;
                        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
                        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
                        contentRt.pivot = new Vector2(0.5f, 0.5f);
                        contentRt.sizeDelta = new Vector2(96f, 96f);

                        var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
                        arrowGo.transform.SetParent(contentGo.transform, false);
                        var arrowRt = (RectTransform)arrowGo.transform;
                        arrowRt.anchorMin = new Vector2(0.5f, 0.5f);
                        arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
                        arrowRt.pivot = new Vector2(0.5f, 0.5f);
                        arrowRt.anchoredPosition = new Vector2(0f, 12f);
                        arrowRt.sizeDelta = new Vector2(64f, 64f);

                        var arrowImg = arrowGo.GetComponent<Image>();
                        arrowImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_UI/Icons/Generated/ui_compass_arrow.png");
                        arrowImg.preserveAspect = true;
                        arrowImg.raycastTarget = false;

                        var textGo = new GameObject("DistanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
                        textGo.transform.SetParent(contentGo.transform, false);
                        var textRt = (RectTransform)textGo.transform;
                        textRt.anchorMin = new Vector2(0.5f, 0.5f);
                        textRt.anchorMax = new Vector2(0.5f, 0.5f);
                        textRt.pivot = new Vector2(0.5f, 0.5f);
                        textRt.anchoredPosition = new Vector2(0f, -34f);
                        textRt.sizeDelta = new Vector2(120f, 36f);

                        var tmp = textGo.GetComponent<TextMeshProUGUI>();
                        var refTmp = parentTransform.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (refTmp != null && refTmp.font != null)
                        {
                            tmp.font = refTmp.font;
                        }
                        tmp.text = "0m";
                        tmp.fontSize = 24f;
                        tmp.fontStyle = FontStyles.Bold;
                        tmp.alignment = TextAlignmentOptions.Center;
                        tmp.color = new Color(1f, 0.9f, 0.35f, 1f);
                        tmp.raycastTarget = false;
                        tmp.outlineWidth = 0.22f;
                        tmp.outlineColor = new Color32(10, 10, 16, 230);

                        var compass = rootGo.GetComponent<SecretCompassIndicatorUI>();
                        var compassSo = new SerializedObject(compass);
                        compassSo.FindProperty("indicatorRect").objectReferenceValue = contentRt;
                        compassSo.FindProperty("arrowRect").objectReferenceValue = arrowRt;
                        compassSo.FindProperty("arrowImage").objectReferenceValue = arrowImg;
                        compassSo.FindProperty("distanceText").objectReferenceValue = tmp;
                        compassSo.ApplyModifiedPropertiesWithoutUndo();
                    }
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
    }
}
