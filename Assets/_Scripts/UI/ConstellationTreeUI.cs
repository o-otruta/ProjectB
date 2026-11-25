using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.UI
{
    /// <summary>
    /// Radial Constellation Talent Tree screen (main menu). Builds the star map at runtime from
    /// <see cref="TalentTreeData"/>: node buttons, constellation lines (dim = locked, pulsing = available,
    /// gold = unlocked), synergy halos, larger keystones, and a compact bottom detail sheet with UNLOCK.
    /// Navigation is handled by <see cref="ConstellationPanZoom"/>.
    /// </summary>
    public class ConstellationTreeUI : MonoBehaviour
    {
        public const float NODE_SIZE = 96f;
        public const float KEYSTONE_SCALE = 1.45f;
        public const float ORIGIN_SCALE = 1.3f;
        public const float MASTERY_SCALE = 1.2f;
        private const float LINE_THICKNESS = 7f;
        private const int STAR_COUNT = 140;

        // Sprint 13: constellation colours come from UiTheme (rarity blue = available, gold = lit, shard = synergy).
        private static Color LockedNodeColor => UiTheme.Current.surfaceRaised;
        private static Color AvailableNodeColor => UiTheme.Current.rarityRare;
        private static Color UnlockedNodeColor => UiTheme.Current.gold;
        private static Color LockedLineColor => UiTheme.WithAlpha(UiTheme.Current.outline, 0.25f);
        private static Color AvailableLineColor => UiTheme.WithAlpha(UiTheme.Current.rarityRare, 0.9f);
        private static Color UnlockedLineColor => UiTheme.WithAlpha(UiTheme.Current.gold, 0.95f);
        private static Color SynergyHaloColor => UiTheme.WithAlpha(UiTheme.Current.shard, 0.75f);
        private static Color SynergyHaloAltColor => UiTheme.WithAlpha(UiTheme.Current.rarityEpic, 0.75f);
        private static Color KeystoneHaloColor => UiTheme.WithAlpha(UiTheme.Current.warning, 0.8f);
        // Path of Mastery stars glow in the Overdrive tier colour they unlock (Lv 10 / 20 / 50).
        private static Color GetMasteryHaloColor(TalentNodeData node)
        {
            float cap = node.GetEffectValue(TalentEffectType.AbilityMaxLevel);
            int tier = cap >= 30f ? 3 : (cap >= 20f ? 2 : 1);
            return UiTheme.WithAlpha(UiTheme.Current.GetOverdriveColor(tier, UiTheme.Current.warning), 0.8f);
        }
        private static readonly Color SelectedOutlineColor = new Color(1f, 1f, 1f, 1f);

        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private ConstellationPanZoom panZoom;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform starsRoot;
        [SerializeField] private RectTransform linesRoot;
        [SerializeField] private RectTransform nodesRoot;

        [Header("Sprites & Font")]
        [SerializeField] private Sprite nodeSprite;
        [SerializeField] private Sprite haloSprite;
        [SerializeField] private Sprite lineSprite;
        [SerializeField] private TMP_FontAsset font;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI shardsText;
        [SerializeField] private TextMeshProUGUI progressText;

        [Header("Detail Sheet")]
        [SerializeField] private GameObject detailSheet;
        [SerializeField] private Image detailIcon;
        [SerializeField] private TextMeshProUGUI detailTitle;
        [SerializeField] private TextMeshProUGUI detailType;
        [SerializeField] private TextMeshProUGUI detailDescription;
        [SerializeField] private TextMeshProUGUI detailStatus;
        [SerializeField] private Button unlockButton;
        [SerializeField] private TextMeshProUGUI unlockButtonText;
        [SerializeField] private Button unlockCoinsButton;
        [SerializeField] private TextMeshProUGUI unlockCoinsButtonText;
        [SerializeField] private Button closeDetailButton;

        [Header("Controls")]
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private Button centerButton;
        [SerializeField] private Button backButton;

        private class NodeView
        {
            public TalentNodeData data;
            public RectTransform rect;
            public Image frame;
            public Image icon;
            public Image halo;
            public Image selection;
            public TextMeshProUGUI costLabel;
        }

        private class LineView
        {
            public Image image;
            public TalentNodeData from;
            public TalentNodeData to;
            public bool pulsing;
        }

        private readonly Dictionary<string, NodeView> nodeViews = new Dictionary<string, NodeView>();
        private readonly List<LineView> lineViews = new List<LineView>();
        private readonly List<LineView> pulsingLines = new List<LineView>();

        private TalentTreeManager talentTree;
        private SaveManager saveManager;
        private AudioManager audioManager;
        private MainMenuUI mainMenu;
        private bool built;
        private TalentNodeData selectedNode;
        private Action cachedLanguageChanged;

        public bool IsBuilt => built;
        public int NodeViewCount => nodeViews.Count;
        public int LineViewCount => lineViews.Count;
        public TalentNodeData SelectedNode => selectedNode;
        public ConstellationPanZoom PanZoom => panZoom;
        public bool IsOpen => panel != null && panel.activeSelf;
        public bool IsDetailOpen => detailSheet != null && detailSheet.activeSelf;

        [Inject]
        public void Construct(TalentTreeManager talentTree, SaveManager saveManager, AudioManager audioManager = null)
        {
            Unsubscribe();
            this.talentTree = talentTree;
            this.saveManager = saveManager;
            this.audioManager = audioManager;

            if (this.talentTree != null) this.talentTree.OnTreeChanged += RefreshAll;
            if (this.saveManager != null) this.saveManager.OnDataChanged += RefreshHeader;
        }

        public void SetMainMenu(MainMenuUI menu) => mainMenu = menu;

        private void Awake()
        {
            if (zoomInButton != null) zoomInButton.onClick.AddListener(() => panZoom?.ZoomIn());
            if (zoomOutButton != null) zoomOutButton.onClick.AddListener(() => panZoom?.ZoomOut());
            if (centerButton != null) centerButton.onClick.AddListener(() => panZoom?.CenterOnOrigin());
            if (closeDetailButton != null) closeDetailButton.onClick.AddListener(CloseDetail);
            if (unlockButton != null) unlockButton.onClick.AddListener(() => TryUnlockSelected(false));
            if (unlockCoinsButton != null) unlockCoinsButton.onClick.AddListener(() => TryUnlockSelected(true));
            if (backButton != null) backButton.onClick.AddListener(HandleBack);
        }

        private void OnEnable()
        {
            cachedLanguageChanged ??= RefreshAll;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;
        }

        private void OnDisable()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (talentTree != null) talentTree.OnTreeChanged -= RefreshAll;
            if (saveManager != null) saveManager.OnDataChanged -= RefreshHeader;
        }

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
            SetMenuHeroPreviewVisible(false);
            EnsureBuilt();
            CloseDetail();
            RefreshAll();
            panZoom?.ResetView();
            UiFocusHelper.SetFirstSelected(backButton != null ? (Selectable)backButton : centerButton);
        }

        public void Hide()
        {
            bool wasOpen = panel != null && panel.activeSelf;
            if (panel != null) panel.SetActive(false);
            if (wasOpen) SetMenuHeroPreviewVisible(true);
        }

        private GameObject menuHeroPreview;

        /// <summary>The 3D menu hero would otherwise shine through the star map; hide it while the tree is open.</summary>
        private void SetMenuHeroPreviewVisible(bool visible)
        {
            if (menuHeroPreview == null)
            {
                var rotator = FindAnyObjectByType<MenuHeroRotator>(FindObjectsInactive.Include);
                if (rotator != null) menuHeroPreview = rotator.gameObject;
            }
            if (menuHeroPreview != null && menuHeroPreview.activeSelf != visible)
            {
                menuHeroPreview.SetActive(visible);
            }
        }

        public void HandleBack()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            if (mainMenu != null) mainMenu.OnUpgradesClicked();
            else Hide();
        }

        public void EnsureBuilt()
        {
            if (built || talentTree == null || talentTree.TreeData == null || nodesRoot == null) return;
            built = true;

            float size = talentTree.TreeData.canvasSize;
            if (content != null) content.sizeDelta = new Vector2(size, size);

            BuildStars(size);

            // Lines first (rendered beneath nodes)
            foreach (TalentNodeData node in talentTree.Nodes)
            {
                if (node.prerequisiteIds == null) continue;
                for (int i = 0; i < node.prerequisiteIds.Count; i++)
                {
                    TalentNodeData parent = talentTree.GetNode(node.prerequisiteIds[i]);
                    if (parent != null) CreateLine(parent, node);
                }
                if (node.prerequisiteIds.Count == 0 && node.nodeType != TalentNodeType.Origin)
                {
                    TalentNodeData origin = talentTree.TreeData.GetOrigin();
                    if (origin != null) CreateLine(origin, node);
                }
            }

            foreach (TalentNodeData node in talentTree.Nodes)
            {
                CreateNode(node);
            }
        }

        private void BuildStars(float size)
        {
            if (starsRoot == null) return;
            var rng = new System.Random(1337);
            float half = size * 0.5f;
            for (int i = 0; i < STAR_COUNT; i++)
            {
                var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(starsRoot, false);
                float s = 3f + (float)rng.NextDouble() * 6f;
                rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = new Vector2((float)(rng.NextDouble() * 2 - 1) * half, (float)(rng.NextDouble() * 2 - 1) * half);
                var img = go.GetComponent<Image>();
                img.sprite = haloSprite != null ? haloSprite : nodeSprite;
                img.raycastTarget = false;
                float a = 0.25f + (float)rng.NextDouble() * 0.6f;
                img.color = new Color(0.8f, 0.86f, 1f, a);
            }
        }

        private void CreateLine(TalentNodeData from, TalentNodeData to)
        {
            if (linesRoot == null) return;
            var go = new GameObject($"Line_{from.id}_{to.id}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(linesRoot, false);
            Vector2 a = from.position;
            Vector2 b = to.position;
            Vector2 d = b - a;
            rt.sizeDelta = new Vector2(d.magnitude, LINE_THICKNESS);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = go.GetComponent<Image>();
            img.sprite = lineSprite;
            img.raycastTarget = false;
            lineViews.Add(new LineView { image = img, from = from, to = to });
        }

        private void CreateNode(TalentNodeData node)
        {
            float scale = node.nodeType switch
            {
                TalentNodeType.Keystone => KEYSTONE_SCALE,
                TalentNodeType.Origin => ORIGIN_SCALE,
                TalentNodeType.Mastery => MASTERY_SCALE,
                _ => 1f
            };
            float size = NODE_SIZE * scale;

            var root = new GameObject($"Node_{node.id}", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.SetParent(nodesRoot, false);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = node.position;

            var view = new NodeView { data = node, rect = rt };

            if (node.nodeType == TalentNodeType.Synergy || node.nodeType == TalentNodeType.Keystone ||
                node.nodeType == TalentNodeType.Origin || node.nodeType == TalentNodeType.Mastery)
            {
                view.halo = CreateImage("Halo", rt, size * 1.7f, haloSprite, false);
                view.halo.color = node.nodeType == TalentNodeType.Keystone ? KeystoneHaloColor
                    : node.nodeType == TalentNodeType.Mastery ? GetMasteryHaloColor(node)
                    : (node.nodeType == TalentNodeType.Synergy
                        ? (Mathf.Abs(node.position.x) > Mathf.Abs(node.position.y) ? SynergyHaloAltColor : SynergyHaloColor)
                        : UnlockedNodeColor * new Color(1f, 1f, 1f, 0.5f));
            }

            view.selection = CreateImage("Selection", rt, size * 1.22f, nodeSprite, false);
            view.selection.color = SelectedOutlineColor;
            view.selection.enabled = false;

            view.frame = CreateImage("Frame", rt, size, nodeSprite, true);
            if (node.nodeType == TalentNodeType.Keystone)
            {
                // Distinct diamond frame for keystones
                view.frame.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                view.frame.rectTransform.sizeDelta = new Vector2(size * 0.82f, size * 0.82f);
                view.selection.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            view.icon = CreateImage("Icon", rt, size * 0.66f, node.icon, false);
            view.icon.preserveAspect = true;
            view.icon.enabled = node.icon != null;

            if (node.nodeType != TalentNodeType.Origin)
            {
                var labelGo = new GameObject("Cost", typeof(RectTransform), typeof(TextMeshProUGUI));
                var lrt = (RectTransform)labelGo.transform;
                lrt.SetParent(rt, false);
                lrt.sizeDelta = new Vector2(size * 1.6f, 34f);
                lrt.anchoredPosition = new Vector2(0f, -size * 0.5f - 18f);
                var tmp = labelGo.GetComponent<TextMeshProUGUI>();
                if (font != null) tmp.font = font;
                tmp.fontSize = 24f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                view.costLabel = tmp;
            }

            var button = root.AddComponent<Button>();
            button.targetGraphic = view.frame;
            button.transition = Selectable.Transition.None;
            TalentNodeData captured = node;
            button.onClick.AddListener(() => SelectNode(captured));

            nodeViews[node.id] = view;
        }

        private Image CreateImage(string name, RectTransform parent, float size, Sprite sprite, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = raycast;
            return img;
        }

        public void RefreshAll()
        {
            RefreshHeader();
            if (!built || talentTree == null) return;

            foreach (var kv in nodeViews)
            {
                NodeView view = kv.Value;
                TalentNodeState state = talentTree.GetState(view.data);
                view.frame.color = state == TalentNodeState.Unlocked ? UnlockedNodeColor
                    : (state == TalentNodeState.Available ? AvailableNodeColor : LockedNodeColor);
                if (view.icon != null && view.icon.enabled)
                {
                    view.icon.color = state == TalentNodeState.Locked ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                }
                if (view.halo != null)
                {
                    Color c = view.halo.color;
                    c.a = state == TalentNodeState.Unlocked ? 0.85f : (state == TalentNodeState.Available ? 0.6f : 0.22f);
                    view.halo.color = c;
                }
                if (view.costLabel != null)
                {
                    // Sprint 13: price only on nodes you can buy right now — no overlapping labels on the inner ring.
                    bool showCost = state == TalentNodeState.Available;
                    view.costLabel.text = showCost ? FormatCost(view.data) : string.Empty;
                    view.costLabel.color = UiTheme.Current.textPrimary;
                }
                view.selection.enabled = selectedNode == view.data;
            }

            pulsingLines.Clear();
            for (int i = 0; i < lineViews.Count; i++)
            {
                LineView line = lineViews[i];
                bool fromUnlocked = talentTree.IsUnlocked(line.from.id);
                bool toUnlocked = talentTree.IsUnlocked(line.to.id);
                line.pulsing = false;
                if (fromUnlocked && toUnlocked)
                {
                    line.image.color = UnlockedLineColor;
                }
                else if (fromUnlocked || toUnlocked)
                {
                    line.image.color = AvailableLineColor;
                    line.pulsing = true;
                    pulsingLines.Add(line);
                }
                else
                {
                    line.image.color = LockedLineColor;
                }
            }

            if (selectedNode != null) RefreshDetail();
        }

        private void RefreshHeader()
        {
            if (shardsText != null)
            {
                int shards = talentTree != null ? talentTree.BossShards : 0;
                int coins = saveManager?.Data != null ? saveManager.Data.coins : 0;
                string shardsPart = Loc.Format("ui.constellation.boss_shards", shards);
                string coinsPart = Loc.Format("ui.constellation.coins", coins);
                shardsText.text = $"<color=#C59BFF>{shardsPart}</color>   <color=#FFD54A>{coinsPart}</color>";
            }
            if (progressText != null && talentTree != null)
            {
                int total = Mathf.Max(0, talentTree.Nodes.Count - 1);
                progressText.text = Loc.Format("ui.constellation.stars_lit", talentTree.UnlockedCount, total);
            }
        }

        private void Update()
        {
            if (pulsingLines.Count == 0) return;
            float pulse = 0.45f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            for (int i = 0; i < pulsingLines.Count; i++)
            {
                Color c = pulsingLines[i].image.color;
                c.a = pulse;
                pulsingLines[i].image.color = c;
            }
        }

        public void SelectNode(TalentNodeData node)
        {
            if (node == null) return;
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            selectedNode = node;
            if (detailSheet != null) detailSheet.SetActive(true);

            if (panZoom != null && panZoom.Viewport != null)
            {
                // Bring the tapped node into the upper half of the screen (above the bottom sheet)
                float upper = panZoom.Viewport.rect.height * 0.2f;
                panZoom.FocusOn(node.position, new Vector2(0f, upper));
            }

            RefreshAll();
        }

        public void CloseDetail()
        {
            selectedNode = null;
            if (detailSheet != null) detailSheet.SetActive(false);
            foreach (var kv in nodeViews) kv.Value.selection.enabled = false;
        }

        private void RefreshDetail()
        {
            TalentNodeData node = selectedNode;
            if (node == null || talentTree == null) return;

            TalentNodeState state = talentTree.GetState(node);
            if (detailIcon != null)
            {
                detailIcon.sprite = node.icon;
                detailIcon.enabled = node.icon != null;
            }
            if (detailTitle != null) detailTitle.text = Loc.Name(node);
            if (detailType != null) detailType.text = DescribeType(node);
            if (detailDescription != null) detailDescription.text = Loc.Desc(node);

            bool canShards = talentTree.CanUnlock(node.id, false) == TalentUnlockResult.Success;
            bool canCoins = talentTree.CanUnlock(node.id, true) == TalentUnlockResult.Success;

            if (detailStatus != null)
            {
                detailStatus.text = state switch
                {
                    TalentNodeState.Unlocked => $"<color=#FFD54A>{Loc.Get("ui.constellation.status_unlocked", "UNLOCKED")}</color>",
                    TalentNodeState.Available => canShards || canCoins
                        ? $"<color=#8FD3FF>{Loc.Get("ui.constellation.status_ready", "Ready to unlock")}</color>"
                        : $"<color=#FF8A80>{Loc.Get("ui.constellation.status_no_shards", "Not enough Boss Shards")}</color>",
                    _ => talentTree.IsConnected(node) && !talentTree.MeetsStarRequirement(node)
                        ? $"<color=#9AA0B8>{Loc.Format("ui.constellation.status_need_stars", talentTree.UnlockedCount, node.minUnlockedStars)}</color>"
                        : $"<color=#9AA0B8>{Loc.Get("ui.constellation.status_locked", "Unlock a connected star first")}</color>"
                };
            }

            if (unlockButton != null)
            {
                unlockButton.gameObject.SetActive(state != TalentNodeState.Unlocked && node.nodeType != TalentNodeType.Origin);
                unlockButton.interactable = canShards;
                if (unlockButtonText != null) unlockButtonText.text = Loc.Format("ui.constellation.unlock_shards", node.shardCost);
            }

            if (unlockCoinsButton != null)
            {
                bool showCoins = state != TalentNodeState.Unlocked && node.coinCost > 0;
                unlockCoinsButton.gameObject.SetActive(showCoins);
                unlockCoinsButton.interactable = canCoins;
                if (unlockCoinsButtonText != null) unlockCoinsButtonText.text = Loc.Format("ui.constellation.unlock_coins", node.coinCost);
            }
        }

        public TalentUnlockResult TryUnlockSelected(bool payWithCoins)
        {
            if (selectedNode == null || talentTree == null) return TalentUnlockResult.UnknownNode;
            TalentUnlockResult result = talentTree.TryUnlock(selectedNode.id, payWithCoins);
            audioManager?.PlaySfx(result == TalentUnlockResult.Success ? SfxType.SynergyFanfare : SfxType.UiClick, randomizePitch: false);
            RefreshAll();
            return result;
        }

        private static string FormatCost(TalentNodeData node)
        {
            if (node.nodeType == TalentNodeType.Origin) return string.Empty;
            // Sprint 13: compact "1◆" label (the coin alternative is shown in the detail sheet) — no overlaps.
            return $"{node.shardCost}<sprite name=\"shard\">";
        }

        private static string DescribeType(TalentNodeData node)
        {
            string constellation = node.constellation switch
            {
                TalentConstellation.Fury => Loc.Get("ui.constellation.fury", "Constellation of Fury"),
                TalentConstellation.Bastion => Loc.Get("ui.constellation.bastion", "Constellation of the Bastion"),
                TalentConstellation.Fortune => Loc.Get("ui.constellation.fortune", "Constellation of Fortune & Chaos"),
                TalentConstellation.Mastery => Loc.Get("ui.constellation.mastery", "Path of Mastery"),
                _ => Loc.Get("ui.constellation.core", "Core")
            };
            string type = node.nodeType switch
            {
                TalentNodeType.Synergy => Loc.Get("ui.constellation.type_synergy", "Synergy Star"),
                TalentNodeType.Keystone => Loc.Get("ui.constellation.type_keystone", "KEYSTONE"),
                TalentNodeType.Mastery => Loc.Get("ui.constellation.type_mastery", "MASTERY"),
                TalentNodeType.Origin => Loc.Get("ui.constellation.type_origin", "Origin"),
                _ => Loc.Get("ui.constellation.type_star", "Star")
            };
            return $"{type}  •  {constellation}";
        }
    }
}
