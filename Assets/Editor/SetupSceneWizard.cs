using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Player;
using ProjectB.Enemies;
using ProjectB.UI;

public class SetupSceneWizard : EditorWindow
{
    [MenuItem("ProjectB/Setup Test Scene")]
    public static void Setup()
    {
        // 1. Setup Layers and Tags
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        bool hasPlayerTag = false;
        for (int i = 0; i < tagsProp.arraySize; i++) {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == "Player") hasPlayerTag = true;
        }
        if (!hasPlayerTag) {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = "Player";
        }

        SerializedProperty layersProp = tagManager.FindProperty("layers");
        SerializedProperty enemyLayerProp = layersProp.GetArrayElementAtIndex(8);
        if (enemyLayerProp.stringValue != "Enemy") {
            enemyLayerProp.stringValue = "Enemy";
        }
        tagManager.ApplyModifiedProperties();

        // 2. Create folders
        if (!AssetDatabase.IsValidFolder("Assets/_Prefabs")) AssetDatabase.CreateFolder("Assets", "_Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_ScriptableObjects")) AssetDatabase.CreateFolder("Assets", "_ScriptableObjects");
        if (!AssetDatabase.IsValidFolder("Assets/_Scenes")) AssetDatabase.CreateFolder("Assets", "_Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/_Materials")) AssetDatabase.CreateFolder("Assets", "_Materials");

        // 3. Create Materials
        Material floorMat = GetOrCreateMaterial("Assets/_Materials/FloorMat.mat", new Color(0.2f, 0.3f, 0.2f));
        Material heroMat = GetOrCreateMaterial("Assets/_Materials/HeroMat.mat", Color.blue);
        Material enemyMat = GetOrCreateMaterial("Assets/_Materials/EnemyMat.mat", Color.red);
        Material projectileMat = GetOrCreateMaterial("Assets/_Materials/ProjectileMat.mat", Color.yellow);

        // 4. Create ScriptableObjects
        HeroData heroData = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/HeroData.asset");
        if (heroData == null) {
            heroData = ScriptableObject.CreateInstance<HeroData>();
            heroData.moveSpeed = 5f;
            heroData.rotationSpeed = 15f;
            heroData.maxHp = 100;
            AssetDatabase.CreateAsset(heroData, "Assets/_ScriptableObjects/HeroData.asset");
        }

        WeaponData weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_ScriptableObjects/WeaponData.asset");
        if (weaponData == null) {
            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            weaponData.damage = 10;
            weaponData.attackCooldown = 0.5f;
            weaponData.range = 8f;
            weaponData.projectileSpeed = 15f;
            AssetDatabase.CreateAsset(weaponData, "Assets/_ScriptableObjects/WeaponData.asset");
        }

        EnemyData enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/EnemyData.asset");
        if (enemyData == null) {
            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            enemyData.hp = 30;
            enemyData.speed = 3f;
            enemyData.contactDamage = 10;
            enemyData.damageCooldown = 1f;
            enemyData.xpDrop = 1;
            AssetDatabase.CreateAsset(enemyData, "Assets/_ScriptableObjects/EnemyData.asset");
        }

        WaveConfig waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
        if (waveConfig == null) {
            waveConfig = ScriptableObject.CreateInstance<WaveConfig>();
            waveConfig.baseEnemyCount = 5;
            waveConfig.enemiesPerWaveMultiplier = 1.2f;
            waveConfig.spawnDelay = 0.5f;
            waveConfig.waveDelay = 2f;
            waveConfig.spawnRadiusMin = 10f;
            waveConfig.spawnRadiusMax = 15f;
            AssetDatabase.CreateAsset(waveConfig, "Assets/_ScriptableObjects/WaveConfig.asset");
        }

        // 5. Create Prefabs
        GameObject projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Projectile.prefab");
        if (projPrefab == null) {
            GameObject projGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projGo.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            projGo.GetComponent<Renderer>().sharedMaterial = projectileMat;
            DestroyImmediate(projGo.GetComponent<Collider>());
            projGo.AddComponent<Projectile>();
            projPrefab = PrefabUtility.SaveAsPrefabAsset(projGo, "Assets/_Prefabs/Projectile.prefab");
            DestroyImmediate(projGo);
        }

        weaponData.projectilePrefab = projPrefab;
        EditorUtility.SetDirty(weaponData);

        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemy.prefab");
        if (enemyPrefab == null) {
            GameObject enemyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyGo.name = "Enemy";
            enemyGo.GetComponent<Renderer>().sharedMaterial = enemyMat;
            // Коллайдер больше не нужен для контактного урона (distance-based),
            // но оставляем для OverlapSphere (поиск цели HeroCombat)
            enemyGo.layer = 8;
            enemyGo.AddComponent<EnemyBase>();
            enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemyGo, "Assets/_Prefabs/Enemy.prefab");
            DestroyImmediate(enemyGo);
        }

        enemyData.modelPrefab = enemyPrefab;
        EditorUtility.SetDirty(enemyData);
        AssetDatabase.SaveAssets();

        // 6. Create Scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(5f, 1f, 5f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        GameObject hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        hero.name = "Hero";
        hero.transform.position = new Vector3(0, 1, 0);
        hero.GetComponent<Renderer>().sharedMaterial = heroMat;
        hero.tag = "Player";

        // Rigidbody не нужен — движение через transform
        var heroMovement = hero.AddComponent<HeroMovement>();
        var heroHealth = hero.AddComponent<HeroHealth>();
        var heroCombat = hero.AddComponent<HeroCombat>();

        SerializedObject soMovement = new SerializedObject(heroMovement);
        soMovement.FindProperty("heroData").objectReferenceValue = heroData;
        soMovement.FindProperty("visualModel").objectReferenceValue = hero.transform;
        soMovement.ApplyModifiedProperties();

        SerializedObject soHealth = new SerializedObject(heroHealth);
        soHealth.FindProperty("heroData").objectReferenceValue = heroData;
        soHealth.ApplyModifiedProperties();

        GameObject firePointGo = new GameObject("FirePoint");
        firePointGo.transform.SetParent(hero.transform);
        firePointGo.transform.localPosition = new Vector3(0, 1, 1);

        SerializedObject soCombat = new SerializedObject(heroCombat);
        soCombat.FindProperty("weaponData").objectReferenceValue = weaponData;
        soCombat.FindProperty("enemyLayer").intValue = 1 << 8;
        soCombat.FindProperty("firePoint").objectReferenceValue = firePointGo.transform;
        soCombat.ApplyModifiedProperties();

        Camera mainCam = Camera.main;
        if (mainCam != null) {
            var camCtrl = mainCam.gameObject.AddComponent<CameraController>();
            camCtrl.target = hero.transform;
            camCtrl.offset = new Vector3(0, 15f, -10f);
            camCtrl.smoothTime = 0.2f;
        }

        GameObject waveMgrGo = new GameObject("WaveManager");
        var waveMgr = waveMgrGo.AddComponent<WaveManager>();
        SerializedObject soWave = new SerializedObject(waveMgr);
        soWave.FindProperty("waveConfig").objectReferenceValue = waveConfig;
        soWave.FindProperty("baseEnemyData").objectReferenceValue = enemyData;
        soWave.FindProperty("heroTarget").objectReferenceValue = hero.transform;
        soWave.ApplyModifiedProperties();

        var oldCanvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        if (oldCanvas.Length == 0) {
            GameObject canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            GameObject bg = new GameObject("JoystickBg");
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.1f);
            bgRect.anchorMax = new Vector2(0.5f, 0.1f);
            bgRect.sizeDelta = new Vector2(200, 200);
            var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(1, 1, 1, 0.5f);

            GameObject handle = new GameObject("JoystickHandle");
            handle.transform.SetParent(bg.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(100, 100);
            var handleImg = handle.AddComponent<UnityEngine.UI.Image>();
            handleImg.color = Color.white;

            var vj = bg.AddComponent<VirtualJoystick>();
            SerializedObject soVJ = new SerializedObject(vj);
            soVJ.FindProperty("background").objectReferenceValue = bgRect;
            soVJ.FindProperty("handle").objectReferenceValue = handleRect;
            soVJ.FindProperty("handleLimit").floatValue = 100f;
            soVJ.ApplyModifiedProperties();
        }

        EditorSceneManager.SaveScene(scene, "Assets/_Scenes/Gameplay.unity");
        Debug.Log("Scene successfully set up and saved to Assets/_Scenes/Gameplay.unity");
    }

    /// <summary>Loads or creates a simple URP Lit material with the given color.</summary>
    private static Material GetOrCreateMaterial(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        // Берём шейдер URP Lit (или Standard как fallback)
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        
        mat = new Material(shader);
        mat.SetColor("_BaseColor", color); // URP Lit property
        mat.color = color; // fallback для Standard
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
