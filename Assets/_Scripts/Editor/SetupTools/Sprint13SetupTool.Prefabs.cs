using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts
{
    public static partial class Sprint13SetupTool
    {
        public const string UPGRADE_ITEM_PREFAB = "Assets/_Prefabs/UI/UpgradeItem.prefab";
        public const string ABILITY_ITEM_PREFAB = "Assets/_Prefabs/UI/AbilityCollectionItem.prefab";
        public const string ACHIEVEMENT_ITEM_PREFAB = "Assets/_Prefabs/UI/AchievementItem.prefab";
        public const string HUD_SLOT_PREFAB = "Assets/_Prefabs/UI/AbilityHudSlot.prefab";

        private delegate void PrefabBuilder(GameObject root);

        private static void BuildPrefabs()
        {
            EditPrefab(UPGRADE_ITEM_PREFAB, BuildUpgradeItem);
            EditPrefab(ABILITY_ITEM_PREFAB, BuildAbilityItem);
            EditPrefab(ACHIEVEMENT_ITEM_PREFAB, BuildAchievementItem);
            EditPrefab(HUD_SLOT_PREFAB, BuildHudSlot);
        }

        private static void EditPrefab(string path, PrefabBuilder builder)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                builder(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Removes visual components from a root that is about to be rebuilt (keeps scripts / transforms).</summary>
        private static void StripVisuals(GameObject go)
        {
            ClearChildren(go.transform);
            foreach (var c in go.GetComponents<LayoutGroup>()) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponents<Shadow>()) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponents<ThemedGraphic>()) Object.DestroyImmediate(c);
        }

        private static Image EnsureImage(GameObject go, UiSprite sprite, UiColor color, float alpha = 1f, bool raycast = false)
        {
            var img = GetOrAdd<Image>(go);
            img.sprite = Theme.GetSprite(sprite);
            img.type = img.sprite != null && img.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            Color c = Theme.GetColor(color);
            c.a *= alpha;
            img.color = c;
            img.raycastTarget = raycast;
            return img;
        }

        // ------------------------------------------------------------------ UpgradeItem

        private static void BuildUpgradeItem(GameObject root)
        {
            StripVisuals(root);
            var ui = root.GetComponent<UpgradeItemUI>();
            var rt = Rt(root);
            rt.sizeDelta = new Vector2(1000f, 200f);
            var bg = EnsureImage(root, UiSprite.Card, UiColor.Surface);
            root.AddComponent<ThemedGraphic>().Configure(UiColor.Surface, 1f, UiSprite.Card);
            LE(root, minH: 200f, prefH: 200f);
            HL(root, 22f, Pad(22, 22, 20, 28), TextAnchor.MiddleLeft, true, true, false, false);

            var iconFrame = Img(root.transform, "IconFrame", UiSprite.Card, UiColor.SurfaceRaised);
            LE(iconFrame.gameObject, prefW: 136f, prefH: 136f, minW: 136f);
            var icon = Icon(iconFrame.transform, "Icon", null, 104f);
            Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(104f, 104f));

            var col = Go("Info", root.transform);
            VL(col, 6f, Pad(0), TextAnchor.MiddleLeft, true, true, true, false);
            LE(col, prefW: 0f, minW: 0f, flexW: 1f);
            var name = Txt(col.transform, "Name", "Upgrade", UiTextStyle.H2, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft, true, 0.8f);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            var delta = Txt(col.transform, "Delta", "+0% → +8%", UiTextStyle.Body, UiColor.Success, TextAlignmentOptions.MidlineLeft, true, 0.9f);
            var levelRow = Go("LevelRow", col.transform);
            HL(levelRow, 14f, Pad(0), TextAnchor.MiddleLeft, true, true, false, false);
            var level = Txt(levelRow.transform, "Level", "Lv 0/20", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft, true);
            level.textWrappingMode = TextWrappingModes.NoWrap;
            level.overflowMode = TextOverflowModes.Overflow;
            LE(level.gameObject, prefW: 190f, minW: 190f);
            var segs = Go("Segments", levelRow.transform);
            HL(segs, 4f, Pad(0), TextAnchor.MiddleLeft, true, true, true, false);
            LE(segs, minH: 14f, prefH: 14f, prefW: 0f, flexW: 1f);
            var segTemplate = Img(segs.transform, "SegmentTemplate", UiSprite.Solid, UiColor.SurfaceSunken, 1f, false, false);
            LE(segTemplate.gameObject, minH: 14f, prefH: 14f, flexW: 1f);

            var buy = Btn(root.transform, "BuyButton", null, ButtonStyle.Primary, null, 0f, UiTextStyle.Number, 0.85f, ButtonSound.None);
            LE(buy.gameObject, prefW: 236f, prefH: 112f, minW: 236f);
            var buyRow = Go("Content", buy.transform);
            Stretch(Rt(buyRow), 14f, 14f, 14f, 2f);
            HL(buyRow, 8f, Pad(0), TextAnchor.MiddleCenter, true, true, false, false);
            var coin = Icon(buyRow.transform, "CoinIcon", Theme.iconCoin, 52f);
            LE(coin.gameObject, prefW: 52f, prefH: 52f);
            var cost = Txt(buyRow.transform, "Cost", "100", UiTextStyle.Number, UiColor.White, TextAlignmentOptions.Center, false, 0.85f);
            cost.textWrappingMode = TextWrappingModes.NoWrap;
            cost.overflowMode = TextOverflowModes.Overflow;
            buy.EditorSetup(ButtonStyle.Primary, buy.Face, null, null, ButtonSound.None);

            Set(ui, "nameText", name);
            Set(ui, "descriptionText", null);
            Set(ui, "levelText", level);
            Set(ui, "costText", cost);
            Set(ui, "buyButton", buy.Button);
            Set(ui, "iconImage", icon);
            Set(ui, "deltaText", delta);
            Set(ui, "segmentsContainer", segs.transform);
            Set(ui, "segmentTemplate", segTemplate);
            Set(ui, "costIcon", coin);
            Set(ui, "themedBuyButton", buy);
        }

        // ------------------------------------------------------------------ AbilityCollectionItem

        private static void BuildAbilityItem(GameObject root)
        {
            StripVisuals(root);
            var ui = root.GetComponent<AbilityCollectionItemUI>();
            Rt(root).sizeDelta = new Vector2(1000f, 220f);
            var bg = EnsureImage(root, UiSprite.Card, UiColor.Surface);
            HL(root, 22f, Pad(28, 24, 22, 30), TextAnchor.UpperLeft, true, true, false, false);
            LE(root, minH: 200f);

            var frame = Img(root.transform, "Frame", UiSprite.Outline, UiColor.RarityCommon, 1f, false, false);
            LE(frame.gameObject, ignore: true);
            Stretch(Rt(frame), 0f, 6f, 0f, 0f);
            var stripe = Img(root.transform, "RarityStripe", UiSprite.Solid, UiColor.RarityCommon, 1f, false, false);
            LE(stripe.gameObject, ignore: true);
            var srt = Rt(stripe);
            srt.anchorMin = new Vector2(0f, 0.16f);
            srt.anchorMax = new Vector2(0f, 0.84f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = new Vector2(12f, 3f);
            srt.sizeDelta = new Vector2(6f, 0f);

            var iconBg = Img(root.transform, "IconBg", UiSprite.Card, UiColor.SurfaceRaised, 1f, false, false);
            LE(iconBg.gameObject, prefW: 132f, prefH: 132f, minW: 132f, minH: 132f);
            var icon = Icon(iconBg.transform, "Icon", null, 100f);
            Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(100f, 100f));
            var initials = Txt(iconBg.transform, "Initials", "AB", UiTextStyle.H2, UiColor.TextPrimary, TextAlignmentOptions.Center, false);
            Stretch(Rt(initials));
            var lockBadge = Img(iconBg.transform, "LockBadge", UiSprite.Circle, UiColor.SurfaceSunken, 1f, false, false);
            Place(Rt(lockBadge), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-10f, 14f), new Vector2(56f, 56f));
            var lockIcon = Icon(lockBadge.transform, "Lock", Theme.iconLock, 34f, Theme.warning);
            Place(Rt(lockIcon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(34f, 34f));

            var col = Go("Info", root.transform);
            VL(col, 6f, Pad(0), TextAnchor.UpperLeft, true, true, true, false);
            LE(col, prefW: 0f, minW: 0f, flexW: 1f);
            var name = Txt(col.transform, "Name", "Ability", UiTextStyle.H2, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft, false, 0.8f);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            LE(name.gameObject, minH: 46f, prefH: 46f);
            var meta = Txt(col.transform, "MetaTag", "Active • Rare", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            var desc = Txt(col.transform, "Description", "Description", UiTextStyle.Body, UiColor.TextSecondary, TextAlignmentOptions.TopLeft, false, 0.85f);
            desc.overflowMode = TextOverflowModes.Overflow;
            var status = Txt(col.transform, "Status", "Unlocked", UiTextStyle.Caption, UiColor.Success, TextAlignmentOptions.MidlineLeft, false);

            var chips = Go("SynergyChips", col.transform);
            VL(chips, 8f, Pad(0, 0, 6, 0), TextAnchor.UpperLeft, true, true, false, false);
            var chip = Img(chips.transform, "ChipTemplate", UiSprite.Pill, UiColor.Shard, 0.18f, false, false);
            HL(chip.gameObject, 8f, Pad(10, 20, 4, 4), TextAnchor.MiddleLeft, true, true, false, false);
            LE(chip.gameObject, prefH: 60f);
            var left = Icon(chip.transform, "Left", null, 48f);
            LE(left.gameObject, prefW: 48f, prefH: 48f);
            var plus = Txt(chip.transform, "Plus", "+", UiTextStyle.Body, UiColor.TextSecondary, TextAlignmentOptions.Center, false, 0.9f);
            LE(plus.gameObject, prefW: 22f);
            var right = Icon(chip.transform, "Right", null, 48f);
            LE(right.gameObject, prefW: 48f, prefH: 48f);
            var arrow = Txt(chip.transform, "Arrow", "→", UiTextStyle.Body, UiColor.TextSecondary, TextAlignmentOptions.Center, false, 0.9f);
            LE(arrow.gameObject, prefW: 30f);
            var chipName = Txt(chip.transform, "Name", "Synergy", UiTextStyle.Caption, UiColor.Shard, TextAlignmentOptions.MidlineLeft, false);
            chipName.textWrappingMode = TextWrappingModes.NoWrap;
            chipName.overflowMode = TextOverflowModes.Overflow;

            Set(ui, "cardBackground", bg);
            Set(ui, "rarityStripe", stripe);
            Set(ui, "iconBackground", iconBg);
            Set(ui, "iconImage", icon);
            Set(ui, "initialsText", initials);
            Set(ui, "nameText", name);
            Set(ui, "metaTagText", meta);
            Set(ui, "descriptionText", desc);
            Set(ui, "statusText", status);
            Set(ui, "frameImage", frame);
            Set(ui, "lockBadge", lockBadge.gameObject);
            Set(ui, "synergyChipsContainer", chips.transform);
            Set(ui, "synergyChipTemplate", chip.gameObject);
        }

        // ------------------------------------------------------------------ AchievementItem

        private static void BuildAchievementItem(GameObject root)
        {
            StripVisuals(root);
            foreach (var oldLe in root.GetComponents<LayoutElement>()) Object.DestroyImmediate(oldLe);
            var ui = root.GetComponent<AchievementItemUI>();
            Rt(root).sizeDelta = new Vector2(1000f, 220f);
            EnsureImage(root, UiSprite.Card, UiColor.Surface, 1f, true);
            root.AddComponent<ThemedGraphic>().Configure(UiColor.Surface, 1f, UiSprite.Card);
            var button = GetOrAdd<Button>(root);
            button.transition = Selectable.Transition.None;
            var group = Group(root);
            VL(root, 14f, Pad(24, 24, 22, 30), TextAnchor.UpperLeft, true, true, true, false);

            var top = Go("TopRow", root.transform);
            HL(top, 20f, Pad(0), TextAnchor.MiddleLeft, true, true, false, false);

            var iconFrame = Img(top.transform, "IconFrame", UiSprite.Card, UiColor.Outline, 1f, false, false);
            LE(iconFrame.gameObject, prefW: 116f, prefH: 116f, minW: 116f);
            var icon = Icon(iconFrame.transform, "Icon", null, 88f);
            Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(88f, 88f));
            var check = Img(iconFrame.transform, "CompletedMark", UiSprite.Circle, UiColor.Success, 1f, false, true);
            Place(Rt(check), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(52f, 52f));
            var checkIcon = Icon(check.transform, "Check", Theme.iconCheck, 34f);
            Place(Rt(checkIcon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(34f, 34f));

            var col = Go("Texts", top.transform);
            VL(col, 4f, Pad(0), TextAnchor.MiddleLeft, true, true, true, false);
            LE(col, prefW: 0f, minW: 0f, flexW: 1f);
            var title = Txt(col.transform, "Title", "Achievement", UiTextStyle.Body, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft, true, 1.05f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.GetComponent<ThemedText>().Configure(UiTextStyle.H2, 0.75f);
            var desc = Txt(col.transform, "Description", "Do something", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.TopLeft, true, 1.1f);

            var chip = Img(top.transform, "RewardChip", UiSprite.Pill, UiColor.Gold, 0.18f, false, false);
            HL(chip.gameObject, 8f, Pad(12, 20, 6, 6), TextAnchor.MiddleCenter, true, true, false, false);
            LE(chip.gameObject, prefH: 72f, minW: 140f);
            var rIcon = Icon(chip.transform, "Icon", Theme.iconCoin, 52f);
            LE(rIcon.gameObject, prefW: 52f, prefH: 52f);
            var rText = Txt(chip.transform, "Text", "+500", UiTextStyle.Caption, UiColor.Gold, TextAlignmentOptions.MidlineLeft, false, 1.05f);
            rText.textWrappingMode = TextWrappingModes.NoWrap;
            rText.overflowMode = TextOverflowModes.Overflow;

            var progressRow = Go("ProgressRow", root.transform);
            HL(progressRow, 16f, Pad(0), TextAnchor.MiddleLeft, true, true, false, false);
            var barBg = Img(progressRow.transform, "ProgressBg", UiSprite.BarFrame, UiColor.SurfaceSunken);
            LE(barBg.gameObject, minH: 26f, prefH: 26f, prefW: 0f, flexW: 1f);
            var fill = Img(barBg.transform, "Fill", UiSprite.BarFill, UiColor.Accent, 1f, false, false);
            Stretch(Rt(fill), 6f, 6f, 6f, 6f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            var progress = Txt(progressRow.transform, "ProgressText", "0 / 5", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.MidlineRight, true);
            progress.textWrappingMode = TextWrappingModes.NoWrap;
            LE(progress.gameObject, prefW: 170f);

            Set(ui, "iconImage", icon);
            Set(ui, "titleText", title);
            Set(ui, "descriptionText", desc);
            Set(ui, "progressText", progress);
            Set(ui, "progressBarFill", fill);
            Set(ui, "progressBarBackground", barBg.gameObject);
            Set(ui, "completedMark", check.gameObject);
            Set(ui, "itemButton", button);
            Set(ui, "rewardIcon", rIcon);
            Set(ui, "rewardText", rText);
            Set(ui, "rewardChipBg", chip);
            Set(ui, "canvasGroup", group);
            Set(ui, "iconFrame", iconFrame);
        }

        // ------------------------------------------------------------------ HUD slot

        private static void BuildHudSlot(GameObject root)
        {
            StripVisuals(root);
            var ui = root.GetComponent<AbilityHudSlotUI>();
            foreach (var img in root.GetComponents<Image>()) Object.DestroyImmediate(img);
            Rt(root).sizeDelta = new Vector2(64f, 64f);

            var glow = Img(root.transform, "Glow", UiSprite.Glow, UiColor.White, 0.6f, false, false);
            Place(Rt(glow), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 116f));
            glow.gameObject.SetActive(false);
            var bg = Img(root.transform, "Background", UiSprite.Card, UiColor.Surface, 0.94f, false, false);
            Stretch(Rt(bg));
            var icon = Icon(root.transform, "Icon", null, 48f);
            Stretch(Rt(icon), 7f, 9f, 7f, 5f);
            var initials = Txt(root.transform, "Initials", "AB", UiTextStyle.Caption, UiColor.TextPrimary, TextAlignmentOptions.Center, false);
            Stretch(Rt(initials));
            var border = Img(root.transform, "Border", UiSprite.SlotFrame, UiColor.RarityCommon, 1f, false, false);
            Stretch(Rt(border), -2f, -2f, -2f, -2f);
            var badge = Img(root.transform, "LevelBadge", UiSprite.Pill, UiColor.SurfaceSunken, 0.95f, false, false);
            Place(Rt(badge), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(8f, -8f), new Vector2(48f, 26f));
            var level = Txt(badge.transform, "Text", "Lv1", UiTextStyle.Micro, UiColor.TextPrimary, TextAlignmentOptions.Center, false, 0.85f);
            Stretch(Rt(level), 2f, 1f, 2f, 0f);
            level.textWrappingMode = TextWrappingModes.NoWrap;
            level.overflowMode = TextOverflowModes.Overflow;

            Set(ui, "borderImage", border);
            Set(ui, "backgroundImage", bg);
            Set(ui, "iconImage", icon);
            Set(ui, "initialsText", initials);
            Set(ui, "levelText", level);
            Set(ui, "lockedIconSprite", Theme.iconLock);
            Set(ui, "glowImage", glow);
        }
    }
}
