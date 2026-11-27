using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data.Platform;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// UI redesign (variant A), step 3 — Play tab of <c>MainMenu.unity</c>, restyled in place (idempotent):
    /// the hero stands on a voxel island podium seen from a slightly raised camera; the name plate becomes an
    /// outlined title with a role / HP / speed chip row; a goal card shows best wave → next reward; Passive / Weapon /
    /// Ultimate become tiles in Portrait (a list in Landscape) with a shared detail panel instead of accordions.
    /// </summary>
    public static class UiRedesignPlayScreenTool
    {
        public const string MAIN_MENU_SCENE = "Assets/_Scenes/MainMenu.unity";
        public const string PODIUM_MATERIAL_DIR = "Assets/_Materials/MenuPodium";
        public const float CAMERA_PITCH = 14f;
        public const float PODIUM_SCALE = 0.75f;
        public const float TILE_NAME_HEIGHT = 74f;

        [MenuItem("ProjectB/UI/Redesign/Apply Play Screen (Podium, Goal Card, Tiles)")]
        public static void ApplyMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(MAIN_MENU_SCENE, OpenSceneMode.Single);
            Apply(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[UiRedesignPlayScreenTool] Play screen restyled.");
        }

        public static void Apply(Scene scene)
        {
            HeroSelectionUI hero = FindInScene<HeroSelectionUI>(scene);
            if (hero == null)
            {
                Debug.LogError("[UiRedesignPlayScreenTool] HeroSelectionUI not found.");
                return;
            }

            BuildPodium(scene);
            ConfigureCamera(scene);
            BuildBackdrop(scene);
            RestyleTopBar(scene);
            RestyleHeader(hero.transform);
            BuildGoalCard(hero.transform);
            RestyleTiles(hero);
            RelayoutPreviewControls(hero.transform);
            RestyleStartAndBottomBar(scene, hero.transform);

            EditorUtility.SetDirty(hero);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------ 3D: podium + camera

        private static void BuildPodium(Scene scene)
        {
            GameObject heroRoot = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<MenuHeroRotator>() != null) heroRoot = root;
                if (root.name == "HeroPodium") Object.DestroyImmediate(root);
            }
            if (heroRoot == null) return;

            var rotator = heroRoot.GetComponent<MenuHeroRotator>();
            rotator.motion = PreviewMotion.Sway;
            EditorUtility.SetDirty(rotator);

            Bounds bounds = new Bounds(heroRoot.transform.position, Vector3.zero);
            foreach (Renderer r in heroRoot.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            Vector3 feet = new Vector3(heroRoot.transform.position.x, bounds.min.y, heroRoot.transform.position.z);

            var podium = new GameObject("HeroPodium");
            SceneManager.MoveGameObjectToScene(podium, scene);
            podium.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, 45f, 0f));

            Material grass = PodiumMaterial("Podium_Grass", UiTheme.Hex(0x5FBF3E));
            Material grassLight = PodiumMaterial("Podium_GrassLight", UiTheme.Hex(0x7DD957));
            Material dirt = PodiumMaterial("Podium_Dirt", UiTheme.Hex(0x9A6A3F));
            Material stone = PodiumMaterial("Podium_Stone", UiTheme.Hex(0x5E5A7E));

            Block(podium.transform, "Grass", grass, new Vector3(0f, -0.15f, 0f), new Vector3(3.4f, 0.3f, 3.4f));
            Block(podium.transform, "Dirt", dirt, new Vector3(0f, -0.65f, 0f), new Vector3(3.2f, 0.7f, 3.2f));
            Block(podium.transform, "Stone", stone, new Vector3(0f, -1.22f, 0f), new Vector3(2.5f, 0.44f, 2.5f));

            // Checker tiles and a few raised voxels so the top reads as blocks, not a flat slab.
            float tile = 3.4f / 4f;
            for (int x = 0; x < 4; x++)
            for (int z = 0; z < 4; z++)
            {
                if ((x + z) % 2 != 0) continue;
                var pos = new Vector3((x - 1.5f) * tile, 0.006f, (z - 1.5f) * tile);
                Block(podium.transform, $"Tile_{x}_{z}", grassLight, pos, new Vector3(tile, 0.012f, tile));
            }
            // Island yaw is 45°: local (-x,-z) = left corner, (+x,+z) = right corner, (-x,+z) = back, (+x,-z) = front.
            Block(podium.transform, "Voxel_A", grass, new Vector3(-1.28f, 0.21f, 0.35f), new Vector3(0.42f, 0.42f, 0.42f));
            Block(podium.transform, "Voxel_B", grassLight, new Vector3(-1.28f, 0.1f, -0.08f), new Vector3(0.42f, 0.2f, 0.42f));

            Decor(podium.transform, "Assets/_Prefabs/Decorations/ForestMushroom.prefab", new Vector3(0.35f, 0f, 1.3f), 0.55f, 20f);
            Decor(podium.transform, "Assets/_Prefabs/Decorations/WildFlower.prefab", new Vector3(1.2f, 0f, 1.1f), 0.8f, 0f);
            Decor(podium.transform, "Assets/_Prefabs/Obstacles/BoulderSmall.prefab", new Vector3(-1.25f, 0f, -1.1f), 0.38f, 35f);

            podium.transform.localScale = Vector3.one * PODIUM_SCALE;

            // Soft dark oval shadow under the floating island podium.
            var glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(UiKitGenerator.FRAMES_DIR + "/kit_glow.png");
            if (glowTex != null)
            {
                var shadowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                shadowGo.name = "PodiumShadow";
                Object.DestroyImmediate(shadowGo.GetComponent<Collider>());
                shadowGo.transform.SetParent(podium.transform, false);
                shadowGo.transform.localPosition = new Vector3(0f, -1.46f, 0f);
                shadowGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                shadowGo.transform.localScale = new Vector3(5.4f, 5.4f, 1f);
                var sr = shadowGo.GetComponent<MeshRenderer>();
                sr.sharedMaterial = SpriteUnlitMaterial("PodiumShadow", glowTex, new Color(0.035f, 0.039f, 0.133f, 0.78f));
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
            }
        }

        private static void BuildBackdrop(Scene scene)
        {
            Camera cam = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out Camera c) && c.CompareTag("MainCamera")) { cam = c; break; }
            }
            if (cam == null) return;

            Transform old = cam.transform.Find("MenuBackdrop");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>(UiKitGenerator.FRAMES_DIR + "/kit_menu_bg.png");
            if (bgTex == null) return;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "MenuBackdrop";
            Object.DestroyImmediate(bg.GetComponent<Collider>());
            bg.transform.SetParent(cam.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 24f);
            bg.transform.localRotation = Quaternion.identity;
            bg.transform.localScale = new Vector3(60f, 60f, 1f);
            var mr = bg.GetComponent<MeshRenderer>();
            mr.sharedMaterial = SpriteUnlitMaterial("MenuBackdrop", bgTex, Color.white);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static Material SpriteUnlitMaterial(string name, Texture2D tex, Color tint)
        {
            if (!AssetDatabase.IsValidFolder(PODIUM_MATERIAL_DIR)) AssetDatabase.CreateFolder("Assets/_Materials", "MenuPodium");
            string path = $"{PODIUM_MATERIAL_DIR}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = tex;
            mat.color = tint;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material PodiumMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(PODIUM_MATERIAL_DIR)) AssetDatabase.CreateFolder("Assets/_Materials", "MenuPodium");
            string path = $"{PODIUM_MATERIAL_DIR}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("ProjectB/VoxelLit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.enableInstancing = true;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void Block(Transform parent, string name, Material mat, Vector3 localPos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            go.isStatic = true;
        }

        private static void Decor(Transform parent, string prefabPath, Vector3 localPos, float scale, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
        }

        private static void ConfigureCamera(Scene scene)
        {
            var framing = FindInScene<MenuPreviewFraming>(scene);
            if (framing == null) return;
            framing.transform.rotation = Quaternion.Euler(CAMERA_PITCH, 0f, 0f);
            SetVector2(framing, "portraitViewportPoint", new Vector2(0.5f, 0.6f));
            SetFloat(framing, "portraitDistance", 8.2f);
            // Landscape: centre of the left half next to the side navigation, a bit higher so the island stays in view.
            SetVector2(framing, "landscapeViewportPoint", new Vector2(0.33f, 0.56f));
            SetFloat(framing, "landscapeDistance", 7.4f);
            framing.Reframe();
            EditorUtility.SetDirty(framing);
            EditorUtility.SetDirty(framing.transform);
        }

        private static void SetVector2(Object target, string field, Vector2 value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;
            p.vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ floating top bar

        private static void RestyleTopBar(Scene scene)
        {
            Transform topBar = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = root.transform.Find("SafeAreaRoot/TopBar");
                if (found != null) { topBar = found; break; }
            }
            if (topBar == null) return;

            GetOrAdd<ThemedGraphic>(topBar.gameObject).Configure(UiColor.SurfaceSunken, 0f, UiSprite.Solid);
            Transform bottomLine = topBar.Find("BottomLine");
            if (bottomLine != null) bottomLine.gameObject.SetActive(false);

            Transform settings = topBar.Find("SettingsButton");
            if (settings != null)
            {
                Rt(settings).sizeDelta = new Vector2(100f, 100f);
                var img = settings.GetComponent<Image>();
                if (img != null && Theme.squircleSprite != null)
                {
                    img.sprite = Theme.squircleSprite;
                    img.type = Image.Type.Sliced;
                }
                if (settings.TryGetComponent(out ThemedButton tb))
                {
                    Image sIcon = settings.Find("Icon")?.GetComponent<Image>();
                    if (sIcon != null) Rt(sIcon).anchoredPosition = new Vector2(0f, 7f);
                    tb.EditorSetup(ButtonStyle.Icon, img, null, sIcon, ButtonSound.Click, customSprite: true);
                }
            }

            Transform currencies = topBar.Find("Currencies");
            if (currencies != null)
            {
                var crt = Rt(currencies);
                crt.anchorMin = new Vector2(1f, 0.5f);
                crt.anchorMax = new Vector2(1f, 0.5f);
                crt.pivot = new Vector2(1f, 0.5f);
                crt.anchoredPosition = new Vector2(-28f, 0f);
                crt.sizeDelta = new Vector2(880f, 88f);

                if (currencies.TryGetComponent(out HorizontalLayoutGroup chl))
                {
                    chl.spacing = 14f;
                    chl.childAlignment = TextAnchor.MiddleRight;
                    chl.childControlWidth = true;
                    chl.childControlHeight = true;
                    chl.childForceExpandWidth = false;
                    chl.childForceExpandHeight = false;
                }
                Transform coins = currencies.Find("CoinsPill");
                Transform shards = currencies.Find("ShardsPill");
                if (coins != null) coins.SetSiblingIndex(0);
                if (shards != null) shards.SetSiblingIndex(1);

                StyleCurrencyPill(coins, 156f, 76f);
                StyleCurrencyPill(shards, 124f, 76f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(crt);
            }
        }

        private static void StyleCurrencyPill(Transform pill, float minWidth, float height)
        {
            if (pill == null) return;
            Rt(pill).sizeDelta = new Vector2(minWidth, height);
            var csf = pill.GetComponent<ContentSizeFitter>();
            if (csf != null) Object.DestroyImmediate(csf);
            LE(pill.gameObject, minH: height, prefH: height, flexH: 0f, minW: minWidth, prefW: -1f, flexW: 0f);
            GetOrAdd<ThemedGraphic>(pill.gameObject).Configure(UiColor.SurfaceSunken, 0.98f, UiSprite.Pill);
            if (pill.TryGetComponent(out HorizontalLayoutGroup hl))
            {
                hl.padding = new RectOffset(14, 24, 6, 14);
                hl.spacing = 12f;
                hl.childAlignment = TextAnchor.MiddleCenter;
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = false;
            }
            Transform icon = pill.Find("Icon");
            if (icon != null) LE(icon.gameObject, minH: 44f, prefH: 44f, minW: 44f, prefW: 44f);
            Transform amount = pill.Find("Amount");
            if (amount != null)
            {
                LE(amount.gameObject, minW: -1f, prefW: -1f, flexW: -1f);
                var tmp = amount.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.alignment = TextAlignmentOptions.MidlineLeft;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                }
                GetOrAdd<ThemedText>(amount.gameObject).Configure(UiTextStyle.Number, 0.78f);
            }
            if (pill.TryGetComponent(out CurrencyLabel cl))
            {
                SetBool(cl, "useWhiteText", true);
                cl.ApplyTheme(Theme);
                EditorUtility.SetDirty(cl);
            }
        }

        // ------------------------------------------------------------------ header

        private static void RestyleHeader(Transform panel)
        {
            Transform plate = panel.Find("NamePlate");
            if (plate == null) return;
            GetOrAdd<ThemedGraphic>(plate.gameObject).Configure(UiColor.Surface, 0f, UiSprite.Panel);
            Presets(Rt(plate),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(940f, 172f)),
                P(new Vector2(0.25f, 1f), new Vector2(0.25f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(760f, 172f)));

            Transform name = plate.Find("HeroNameText");
            if (name != null)
            {
                GetOrAdd<ThemedText>(name.gameObject).Configure(UiTextStyle.Display, 1.12f);
                var rt = Rt(name);
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(0f, 118f);
            }

            Transform oldRow = plate.Find("ChipRow");
            Transform role = plate.Find("RoleText") ?? (oldRow != null ? oldRow.Find("RoleChip/RoleText") : null);
            Transform card = panel.Find("HeroInfoCard");
            Transform stats = card != null ? card.Find("StatChips") : null;
            Transform hp = (stats != null ? stats.Find("HpChip") : null) ?? (oldRow != null ? oldRow.Find("HpChip") : null);
            Transform speed = (stats != null ? stats.Find("SpeedChip") : null) ?? (oldRow != null ? oldRow.Find("SpeedChip") : null);

            var row = Go("ChipRow", plate);
            var rrt = Rt(row);
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -118f);
            rrt.sizeDelta = new Vector2(0f, 56f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (role != null)
            {
                var chip = Img(row.transform, "RoleChip", UiSprite.Pill, UiColor.Gold, 1f, false, true);
                var chipLayout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
                chipLayout.padding = new RectOffset(26, 26, 6, 12);
                chipLayout.childControlWidth = true;
                chipLayout.childControlHeight = true;
                chipLayout.childForceExpandWidth = false;
                chipLayout.childForceExpandHeight = false;
                chipLayout.childAlignment = TextAnchor.MiddleCenter;
                LE(chip.gameObject, prefH: 54f);
                role.SetParent(chip.transform, false);
                var roleText = role.GetComponent<TextMeshProUGUI>();
                Object.DestroyImmediate(role.GetComponent<ThemedText>());
                if (Theme.bodyBoldFont != null) roleText.font = Theme.bodyBoldFont;
                if (Theme.heavyFlatMaterial != null && Theme.bodyBoldFont != null)
                {
                    roleText.fontSharedMaterial = Theme.heavyFlatMaterial;
                }
                roleText.fontSize = 25f;
                GetOrAdd<ThemedGraphic>(role.gameObject).Configure(UiColor.TextDark);
                roleText.alignment = TextAlignmentOptions.Center;
                roleText.textWrappingMode = TextWrappingModes.NoWrap;
                Object.DestroyImmediate(role.GetComponent<ResponsiveRect>());
            }
            if (hp != null) MoveStatChip(hp, row.transform);
            if (speed != null) MoveStatChip(speed, row.transform);

            if (oldRow != null) Object.DestroyImmediate(oldRow.gameObject);
            if (stats != null) Object.DestroyImmediate(stats.gameObject);
        }

        /// <summary>HP / Speed chip → compact "icon value" pair in the header row (caption hidden, no pill).</summary>
        private static void MoveStatChip(Transform chip, Transform row)
        {
            chip.SetParent(row, false);
            GetOrAdd<ThemedGraphic>(chip.gameObject).Configure(UiColor.SurfaceSunken, 0f, UiSprite.Pill);
            var layout = chip.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleCenter;
            }
            LE(chip.gameObject, prefH: 52f);
            Transform caption = chip.Find("Caption");
            if (caption != null) caption.gameObject.SetActive(false);
            Transform icon = chip.Find("Icon");
            if (icon != null) LE(icon.gameObject, prefH: 40f, prefW: 40f);
            Transform value = chip.Find("Value");
            if (value != null) GetOrAdd<ThemedText>(value.gameObject).Configure(UiTextStyle.Number, 0.9f);
        }

        // ------------------------------------------------------------------ goal card

        private static void BuildGoalCard(Transform panel)
        {
            Transform old = panel.Find("GoalCard");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var card = Img(panel, "GoalCard", UiSprite.Card, UiColor.Surface, 1f, true, true);
            card.transform.SetSiblingIndex(panel.Find("NamePlate") != null ? panel.Find("NamePlate").GetSiblingIndex() + 1 : 0);
            Presets(Rt(card),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -204f), new Vector2(940f, 164f)),
                P(new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-12f, -24f), new Vector2(-72f, 164f)));
            var button = card.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = card;

            var best = Txt(card.transform, "BestCaption", "BEST", UiTextStyle.Micro, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft, true, 1.1f);
            Place(Rt(best), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(260f, 34f));
            best.gameObject.AddComponent<LocalizedText>().EditorSetup("ui.play.best", true);

            var value = Txt(card.transform, "BestValue", "Wave 12 / 20", UiTextStyle.Caption, UiColor.TextPrimary, TextAlignmentOptions.MidlineRight, true, 1.25f);
            var vrt = Rt(value);
            vrt.anchorMin = new Vector2(0f, 1f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(1f, 1f);
            vrt.anchoredPosition = new Vector2(-150f, -14f);
            vrt.sizeDelta = new Vector2(-300f, 40f);
            value.textWrappingMode = TextWrappingModes.NoWrap;

            var bar = Img(card.transform, "Bar", UiSprite.BarFrame, UiColor.SurfaceSunken, 1f, false, true);
            var brt = Rt(bar);
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(28f, -60f);
            brt.sizeDelta = new Vector2(-178f, 30f);
            var fill = Img(bar.transform, "Fill", UiSprite.BarFill, UiColor.XpFill, 1f, false, true);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.6f;
            Stretch(Rt(fill), 5f, 5f, 5f, 5f);

            var reward = Txt(card.transform, "Reward", "Reward at wave 20: Mage", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft, true, 1f, true);
            var rwt = Rt(reward);
            rwt.anchorMin = new Vector2(0f, 0f);
            rwt.anchorMax = new Vector2(1f, 0f);
            rwt.pivot = new Vector2(0f, 0f);
            rwt.anchoredPosition = new Vector2(28f, 24f);
            rwt.sizeDelta = new Vector2(-218f, 40f);
            reward.textWrappingMode = TextWrappingModes.NoWrap;

            // Tap affordance: clean golden squircle reward frame without cramped chevron.
            var chevron = Icon(card.transform, "Chevron", Theme.iconChevronRight, 30f, Theme.textSecondary);
            Place(Rt(chevron), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-144f, 22f), new Vector2(30f, 30f));
            chevron.gameObject.SetActive(false);

            var frame = Img(card.transform, "RewardFrame", UiSprite.Squircle, UiColor.SurfaceSunken, 1f, false, true);
            Place(Rt(frame), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(108f, 108f));
            var ring = Img(frame.transform, "Ring", UiSprite.SlotFrame, UiColor.Gold, 1f, false, true);
            Stretch(Rt(ring), 2f, 8f, 2f, 1f);
            var icon = Icon(frame.transform, "Icon", null, 70f);
            Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(70f, 70f));

            var catalog = AssetDatabase.LoadAssetAtPath<ProjectB.Abilities.AbilityCatalog>("Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset");
            var ui = card.gameObject.AddComponent<PlayGoalCardUI>();
            ui.EditorSetup(value, fill, reward, icon, button, catalog);
        }

        // ------------------------------------------------------------------ tiles + detail panel

        /// <summary>
        /// "AbilityBlock": a bottom-anchored vertical stack [detail panel, tiles] that grows upwards, so the
        /// description always sits right above the tiles whatever their height. Same tiles in both orientations
        /// (Portrait: above START; Landscape: right column above START).
        /// </summary>
        private static void RestyleTiles(HeroSelectionUI hero)
        {
            Transform panel = hero.transform;
            Transform block = panel.Find("AbilityBlock");
            Transform card = panel.Find("HeroInfoCard") ?? (block != null ? block.Find("HeroInfoCard") : null);
            if (card == null) return;

            if (block == null)
            {
                block = Go("AbilityBlock", panel).transform;
                var vl = block.gameObject.AddComponent<VerticalLayoutGroup>();
                vl.spacing = 18f;
                vl.childAlignment = TextAnchor.LowerCenter;
                vl.childControlWidth = true;
                vl.childControlHeight = true;
                vl.childForceExpandWidth = true;
                vl.childForceExpandHeight = false;
                block.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            block.SetSiblingIndex(card.GetSiblingIndex());
            Presets(Rt(block),
                P(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 206f), new Vector2(-64f, 0f)),
                P(new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-12f, 196f), new Vector2(-72f, 0f)));

            // Detail panel: speech bubble above the tiles; its tail is moved under the expanded tile at runtime.
            Transform detailT = panel.Find("DetailPanel") ?? block.Find("DetailPanel");
            GameObject detail = detailT != null ? detailT.gameObject : null;
            if (detail == null)
            {
                var img = Img(block, "DetailPanel", UiSprite.Row, UiColor.SurfaceRaised, 1f, true, true);
                detail = img.gameObject;
                var dl = detail.AddComponent<VerticalLayoutGroup>();
                dl.padding = new RectOffset(30, 30, 20, 30);
                dl.childControlWidth = true;
                dl.childControlHeight = true;
                dl.childForceExpandWidth = true;
                dl.childForceExpandHeight = false;
            }
            detail.transform.SetParent(block, false);
            detail.transform.SetSiblingIndex(0);
            Object.DestroyImmediate(detail.GetComponent<ContentSizeFitter>());
            Object.DestroyImmediate(detail.GetComponent<ResponsiveRect>());

            Transform oldPointer = detail.transform.Find("Pointer");
            if (oldPointer != null) Object.DestroyImmediate(oldPointer.gameObject);
            var pointer = Img(detail.transform, "Pointer", UiSprite.Solid, UiColor.SurfaceRaised, 1f, false, true);
            LE(pointer.gameObject, ignore: true);
            Place(Rt(pointer), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(26f, 26f));
            Rt(pointer).localRotation = Quaternion.Euler(0f, 0f, 45f);
            pointer.transform.SetAsFirstSibling();

            // Tiles row: same in both orientations.
            card.SetParent(block, false);
            card.SetSiblingIndex(1);
            Object.DestroyImmediate(card.GetComponent<ContentSizeFitter>());
            Object.DestroyImmediate(card.GetComponent<ResponsiveRect>());
            Object.DestroyImmediate(card.GetComponent<AdaptiveLayoutGroup>());
            Object.DestroyImmediate(card.GetComponent<VerticalLayoutGroup>());
            GetOrAdd<ThemedGraphic>(card.gameObject).Configure(UiColor.Surface, 0f, UiSprite.Panel);
            var row = GetOrAdd<HorizontalLayoutGroup>(card.gameObject);
            row.padding = new RectOffset(0, 0, 0, 0);
            row.spacing = 16f;
            row.childAlignment = TextAnchor.LowerCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            string[] rows = { "PassiveRow", "WeaponRow", "UltimateRow" };
            string[] texts = { "PassiveText", "WeaponText", "UltimateText" };
            var highlights = new GameObject[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                Transform tile = card.Find(rows[i]);
                if (tile != null) highlights[i] = RestyleTile(tile);
                if (i == 2 && tile != null)
                {
                    Transform ultCap = tile.Find("Caption");
                    if (ultCap != null)
                    {
                        Object.DestroyImmediate(ultCap.GetComponent<LocalizedText>());
                        Set(hero, "ultimateCaptionText", ultCap.GetComponent<TMP_Text>());
                    }
                }
                Transform text = card.Find(texts[i]) ?? detail.transform.Find(texts[i]);
                if (text != null)
                {
                    text.SetParent(detail.transform, false);
                    var tmp = text.GetComponent<TextMeshProUGUI>();
                    tmp.alignment = TextAlignmentOptions.TopLeft;
                    tmp.textWrappingMode = TextWrappingModes.Normal;
                    text.gameObject.SetActive(false);
                }
            }
            detail.SetActive(false);
            Set(hero, "detailPanel", detail);
            Set(hero, "detailPointer", Rt(pointer));
            SetArray(hero, "tileHighlights", highlights);
        }

        /// <summary>Row → tile (icon, caption, name centred; name up to 2 lines). Returns its gold selection frame.</summary>
        private static GameObject RestyleTile(Transform tile)
        {
            GetOrAdd<ThemedGraphic>(tile.gameObject).Configure(UiColor.Surface, 1f, UiSprite.Card);
            Object.DestroyImmediate(tile.GetComponent<AdaptiveLayoutGroup>());
            Object.DestroyImmediate(tile.GetComponent<HorizontalLayoutGroup>());
            var stack = GetOrAdd<VerticalLayoutGroup>(tile.gameObject);
            stack.padding = new RectOffset(14, 14, 20, 28);
            stack.spacing = 6f;
            stack.childAlignment = TextAnchor.UpperCenter;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = false;
            stack.childForceExpandHeight = false;

            var le = GetOrAdd<LayoutElement>(tile.gameObject);
            le.preferredHeight = -1f;
            le.minHeight = 216f;
            le.preferredWidth = 0f;
            le.flexibleWidth = 1f;

            Transform icon = tile.Find("Icon");
            if (icon != null) LE(icon.gameObject, prefH: 72f, prefW: 72f);

            Transform caption = tile.Find("Caption");
            Transform name = tile.Find("Name");
            // Drop the per-orientation alignment component of the earlier tiles/list version (script removed).
            if (caption != null) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(caption.gameObject);
            if (name != null) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(name.gameObject);
            if (caption != null)
            {
                var cle = GetOrAdd<LayoutElement>(caption.gameObject);
                cle.preferredWidth = -1f;
                cle.flexibleWidth = -1f;
                var tmp = caption.GetComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
            }

            if (name != null)
            {
                // Fixed two-line box: every hero's tiles get the same height, longer names end with "…".
                var nle = GetOrAdd<LayoutElement>(name.gameObject);
                nle.preferredWidth = -1f;
                nle.flexibleWidth = 1f;
                nle.minHeight = TILE_NAME_HEIGHT;
                nle.preferredHeight = TILE_NAME_HEIGHT;
                var tmp = name.GetComponent<TextMeshProUGUI>();
                tmp.enableAutoSizing = false;
                GetOrAdd<ThemedText>(name.gameObject).Configure(UiTextStyle.Caption, 1.32f);
                GetOrAdd<ThemedGraphic>(name.gameObject).Configure(UiColor.White);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
            }

            Transform chevron = tile.Find("Chevron");
            if (chevron != null) chevron.gameObject.SetActive(false);

            Transform old = tile.Find("Selected");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var frame = Img(tile, "Selected", UiSprite.Outline, UiColor.Gold, 1f, false, true);
            LE(frame.gameObject, ignore: true);
            Stretch(Rt(frame), 3f, 15f, 3f, 2f);
            frame.gameObject.SetActive(false);
            return frame.gameObject;
        }

        // ------------------------------------------------------------------ preview controls

        private static void RelayoutPreviewControls(Transform panel)
        {
            StyleSquircleNavButton(panel.Find("PrevHeroButton"));
            StyleSquircleNavButton(panel.Find("NextHeroButton"));
            SetPortrait(panel.Find("PrevHeroButton"), P(new Vector2(0f, 0.56f), new Vector2(0f, 0.56f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(100f, 100f)));
            SetPortrait(panel.Find("NextHeroButton"), P(new Vector2(1f, 0.56f), new Vector2(1f, 0.56f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(100f, 100f)));
            Transform lockOv = panel.Find("LockOverlay");
            if (lockOv != null)
            {
                GetOrAdd<ThemedGraphic>(lockOv.gameObject).Configure(UiColor.Surface, 0.94f, UiSprite.Card);
                Transform lockIcon = lockOv.Find("LockIcon");
                if (lockIcon != null)
                {
                    Place(Rt(lockIcon), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(64f, 64f));
                }
                Transform lockText = lockOv.Find("Requirement");
                if (lockText != null)
                {
                    Band(Rt(lockText), false, 18f, 54f, 20f, 20f);
                }
            }
            SetPortrait(lockOv, P(new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(460f, 162f)));
            Transform pagination = panel.Find("Pagination");
            if (pagination != null)
            {
                var hl = GetOrAdd<HorizontalLayoutGroup>(pagination.gameObject);
                hl.spacing = 12f;
                hl.childAlignment = TextAnchor.MiddleCenter;
                hl.childControlWidth = false;
                hl.childControlHeight = false;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = false;
                SetPortrait(pagination, P(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 468f), new Vector2(360f, 28f)));
                Transform abilityBlock = panel.Find("AbilityBlock");
                if (abilityBlock != null && abilityBlock.GetSiblingIndex() < pagination.GetSiblingIndex())
                {
                    abilityBlock.SetSiblingIndex(pagination.GetSiblingIndex() + 1);
                }
            }
        }

        private static void StyleSquircleNavButton(Transform t)
        {
            if (t == null) return;
            var img = t.GetComponent<Image>();
            if (img != null && Theme.squircleSprite != null)
            {
                img.sprite = Theme.squircleSprite;
                img.type = Image.Type.Sliced;
            }
            if (t.TryGetComponent(out ThemedButton tb))
            {
                Image navIcon = t.Find("Icon")?.GetComponent<Image>();
                if (navIcon != null) Rt(navIcon).anchoredPosition = new Vector2(0f, 7f);
                tb.EditorSetup(ButtonStyle.Icon, img, null, navIcon, ButtonSound.Click, customSprite: true);
            }
        }

        private static void RestyleStartAndBottomBar(Scene scene, Transform panel)
        {
            Transform start = panel.Find("StartButton");
            if (start != null)
            {
                Presets(Rt(start),
                    P(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(-64f, 158f)),
                    P(new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-12f, 28f), new Vector2(-72f, 148f)));
                Transform startText = start.Find("Text");
                if (startText != null)
                {
                    var tg = startText.GetComponent<ThemedGraphic>();
                    if (tg != null) Object.DestroyImmediate(tg);
                    GetOrAdd<ThemedText>(startText.gameObject).Configure(UiTextStyle.Display, 0.86f);
                    Stretch(Rt(startText), 16f, 32f, 16f, 4f);
                }
                if (start.TryGetComponent(out ThemedButton tb)) tb.ApplyTheme(Theme);
            }

            Transform bottomBar = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = root.transform.Find("SafeAreaRoot/BottomBar");
                if (found != null) { bottomBar = found; break; }
            }
            if (bottomBar == null) return;

            Transform topLine = bottomBar.Find("TopLine");
            if (topLine != null) GetOrAdd<ThemedGraphic>(topLine.gameObject).Configure(UiColor.Outline, 0.45f, UiSprite.Solid);

            foreach (BottomTabUI tab in bottomBar.GetComponentsInChildren<BottomTabUI>(true))
            {
                tab.activeIconY = 34f;
                if (tab.bgImage != null && Theme.buttonSprite != null)
                {
                    tab.bgImage.sprite = Theme.buttonSprite;
                    tab.bgImage.type = Image.Type.Sliced;
                }
                if (tab.labelText != null)
                {
                    Rt(tab.labelText).anchoredPosition = new Vector2(0f, 28f);
                }
                tab.ApplyTheme(Theme);
                EditorUtility.SetDirty(tab);
            }
        }

        private static void SetPortrait(Transform t, RectPreset portrait)
        {
            if (t == null) return;
            var r = GetOrAdd<ResponsiveRect>(t.gameObject);
            r.SetPreset(LayoutMode.Portrait, portrait);
            portrait.ApplyTo(Rt(t));
            EditorUtility.SetDirty(r);
        }

        // ------------------------------------------------------------------ presets

        private static void Presets(RectTransform rt, RectPreset portrait, RectPreset landscape)
        {
            portrait.ApplyTo(rt);
            var r = GetOrAdd<ResponsiveRect>(rt.gameObject);
            r.SetPreset(LayoutMode.Portrait, portrait);
            r.SetPreset(LayoutMode.Landscape, landscape);
            EditorUtility.SetDirty(r);
        }

        private static RectPreset P(Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            return new RectPreset { captured = true, anchorMin = aMin, anchorMax = aMax, pivot = pivot, anchoredPosition = pos, sizeDelta = size };
        }
    }
}
