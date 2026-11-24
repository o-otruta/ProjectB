using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using ProjectB.UI;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// Editor helpers that assemble themed UI from the Sprint 13 kit: images bound to <see cref="UiColor"/> tokens,
    /// texts bound to <see cref="UiTextStyle"/>, <see cref="ThemedButton"/>s, layout groups, serialized-field wiring
    /// and persistent click listeners. Used by <c>Sprint13SetupTool</c>.
    /// </summary>
    public static class UiBuilder
    {
        public static UiTheme Theme => UiTheme.Current;

        public static GameObject Go(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            foreach (Type t in components)
            {
                if (go.GetComponent(t) == null) go.AddComponent(t);
            }
            return go;
        }

        /// <summary>Get-or-add without <c>??</c> (Unity returns a "fake null" for missing components in the Editor).</summary>
        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        public static T GetOrAdd<T>(Component c) where T : Component => GetOrAdd<T>(c.gameObject);

        public static RectTransform Rt(Component c) => (RectTransform)c.transform;
        public static RectTransform Rt(GameObject go) => (RectTransform)go.transform;

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(t.GetChild(i).gameObject);
            }
        }

        public static Transform Find(Transform root, string path) => root != null ? root.Find(path) : null;

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        /// <summary>Horizontal band: stretched across the width, fixed height, anchored to the top (y from top) or bottom.</summary>
        public static void Band(RectTransform rt, bool top, float y, float height, float left = 0f, float right = 0f)
        {
            float a = top ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, a);
            rt.anchorMax = new Vector2(1f, a);
            rt.pivot = new Vector2(0.5f, a);
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(-right, 0f);
            rt.anchoredPosition = new Vector2((left - right) * 0.5f, top ? -y : y);
            rt.sizeDelta = new Vector2(-(left + right), height);
        }

        public static Image Img(Transform parent, string name, UiSprite sprite, UiColor color, float alpha = 1f, bool raycast = false, bool themed = true)
        {
            var go = Go(name, parent, typeof(Image));
            var img = go.GetComponent<Image>();
            img.raycastTarget = raycast;
            Sprite s = Theme.GetSprite(sprite);
            img.sprite = s;
            img.type = s != null && s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            Color c = Theme.GetColor(color);
            c.a *= alpha;
            img.color = c;
            if (themed) go.AddComponent<ThemedGraphic>().Configure(color, alpha, sprite);
            return img;
        }

        public static Image Icon(Transform parent, string name, Sprite sprite, float size, Color? tint = null)
        {
            var go = Go(name, parent, typeof(Image));
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = tint ?? Color.white;
            Rt(go).sizeDelta = new Vector2(size, size);
            return img;
        }

        public static TextMeshProUGUI Txt(Transform parent, string name, string text, UiTextStyle style, UiColor color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool themedColor = true, float sizeScale = 1f, bool autoSize = false)
        {
            var go = Go(name, parent, typeof(TextMeshProUGUI));
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = Theme.GetColor(color);
            tmp.richText = true;
            if (Theme.iconSpriteAsset != null) tmp.spriteAsset = Theme.iconSpriteAsset;
            if (autoSize)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = Theme.GetFontSize(style) * sizeScale * 0.55f;
            }
            go.AddComponent<ThemedText>().Configure(style, sizeScale);
            if (themedColor) go.AddComponent<ThemedGraphic>().Configure(color);
            return tmp;
        }

        public static ThemedButton Btn(Transform parent, string name, string label, ButtonStyle style,
            Sprite icon = null, float iconSize = 56f, UiTextStyle labelStyle = UiTextStyle.Button, float labelScale = 1f,
            ButtonSound sound = ButtonSound.Click)
        {
            var go = Go(name, parent, typeof(Image), typeof(Button));
            var face = go.GetComponent<Image>();
            face.raycastTarget = true;
            var button = go.GetComponent<Button>();
            button.targetGraphic = face;

            TextMeshProUGUI text = null;
            Image iconImage = null;
            bool hasLabel = !string.IsNullOrEmpty(label);

            if (icon != null && hasLabel)
            {
                var row = Go("Content", go.transform, typeof(HorizontalLayoutGroup));
                Stretch(Rt(row), 16f, style == ButtonStyle.Icon ? 0f : 12f, 16f, 0f);
                var hl = row.GetComponent<HorizontalLayoutGroup>();
                hl.childAlignment = TextAnchor.MiddleCenter;
                hl.spacing = 12f;
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = false;
                iconImage = Icon(row.transform, "Icon", icon, iconSize);
                LE(iconImage.gameObject, prefW: iconSize, prefH: iconSize);
                text = Txt(row.transform, "Text", label, labelStyle, UiColor.White, TextAlignmentOptions.Center, false, labelScale);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
            }
            else if (icon != null)
            {
                iconImage = Icon(go.transform, "Icon", icon, iconSize);
                Place(Rt(iconImage), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, style == ButtonStyle.Icon ? 2f : 6f), new Vector2(iconSize, iconSize));
            }
            else if (hasLabel)
            {
                text = Txt(go.transform, "Text", label, labelStyle, UiColor.White, TextAlignmentOptions.Center, false, labelScale);
                Stretch(Rt(text), 16f, 14f, 16f, 2f);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.enableAutoSizing = true;
                text.fontSizeMin = 18f;
                text.fontSizeMax = Theme.GetFontSize(labelStyle) * labelScale;
            }

            var themed = go.AddComponent<ThemedButton>();
            themed.EditorSetup(style, face, text, iconImage, sound);
            return themed;
        }

        public static HorizontalLayoutGroup HL(GameObject go, float spacing = 0f, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft, bool controlW = true, bool controlH = true, bool expandW = false, bool expandH = false)
        {
            var hl = GetOrAdd<HorizontalLayoutGroup>(go);
            hl.spacing = spacing;
            hl.padding = padding ?? new RectOffset();
            hl.childAlignment = align;
            hl.childControlWidth = controlW;
            hl.childControlHeight = controlH;
            hl.childForceExpandWidth = expandW;
            hl.childForceExpandHeight = expandH;
            return hl;
        }

        public static VerticalLayoutGroup VL(GameObject go, float spacing = 0f, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft, bool controlW = true, bool controlH = true, bool expandW = true, bool expandH = false)
        {
            var vl = GetOrAdd<VerticalLayoutGroup>(go);
            vl.spacing = spacing;
            vl.padding = padding ?? new RectOffset();
            vl.childAlignment = align;
            vl.childControlWidth = controlW;
            vl.childControlHeight = controlH;
            vl.childForceExpandWidth = expandW;
            vl.childForceExpandHeight = expandH;
            return vl;
        }

        public static LayoutElement LE(GameObject go, float minH = -1f, float prefH = -1f, float flexH = -1f,
            float minW = -1f, float prefW = -1f, float flexW = -1f, bool ignore = false)
        {
            var le = GetOrAdd<LayoutElement>(go);
            le.minHeight = minH;
            le.preferredHeight = prefH;
            le.flexibleHeight = flexH;
            le.minWidth = minW;
            le.preferredWidth = prefW;
            le.flexibleWidth = flexW;
            le.ignoreLayout = ignore;
            return le;
        }

        public static RectOffset Pad(int all) => new RectOffset(all, all, all, all);
        public static RectOffset Pad(int left, int right, int top, int bottom) => new RectOffset(left, right, top, bottom);

        /// <summary>Currency pill: rounded plate + icon + amount (with <see cref="CurrencyLabel"/>).</summary>
        public static CurrencyLabel Pill(Transform parent, string name, CurrencyType currency, float width, float height, out TextMeshProUGUI amount)
        {
            var bg = Img(parent, name, UiSprite.Pill, UiColor.SurfaceSunken, 0.92f);
            Rt(bg).sizeDelta = new Vector2(width, height);
            HL(bg.gameObject, 10f, Pad(14, 26, 6, 6), TextAnchor.MiddleLeft, true, true, false, false);
            var icon = Icon(bg.transform, "Icon", currency == CurrencyType.Coins ? Theme.iconCoin : Theme.iconShard, height - 18f);
            LE(icon.gameObject, prefW: height - 18f, prefH: height - 18f);
            amount = Txt(bg.transform, "Amount", "0", UiTextStyle.Number, currency == CurrencyType.Coins ? UiColor.Gold : UiColor.Shard,
                TextAlignmentOptions.MidlineLeft, false, 0.85f);
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            amount.overflowMode = TextOverflowModes.Overflow;
            LE(amount.gameObject, flexW: 1f);
            var label = bg.gameObject.AddComponent<CurrencyLabel>();
            label.EditorSetup(currency, amount, icon);
            return label;
        }

        /// <summary>Stat chip: plate + icon + value + caption (Pause / Game Over summaries, hero stats).</summary>
        public static TextMeshProUGUI StatChip(Transform parent, string name, Sprite icon, string caption, float width, float height, out GameObject chip)
        {
            var bg = Img(parent, name, UiSprite.Card, UiColor.SurfaceRaised);
            chip = bg.gameObject;
            LE(chip, minH: height, prefH: height, prefW: width, flexW: 1f);
            VL(chip, 2f, Pad(8, 8, 12, 14), TextAnchor.MiddleCenter, true, true, false, false);
            var ic = Icon(chip.transform, "Icon", icon, 48f);
            LE(ic.gameObject, prefH: 48f, prefW: 48f);
            var value = Txt(chip.transform, "Value", "0", UiTextStyle.Number, UiColor.TextPrimary, TextAlignmentOptions.Center, true, 0.9f);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Overflow;
            if (!string.IsNullOrEmpty(caption))
            {
                Txt(chip.transform, "Caption", caption, UiTextStyle.Micro, UiColor.TextSecondary);
            }
            return value;
        }

        public static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[UiBuilder] {target.GetType().Name}.{field} not found");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[UiBuilder] {target.GetType().Name}.{field} not found");
                return;
            }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(UnityEngine.Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p != null) { p.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"[UiBuilder] {target.GetType().Name}.{field} not found");
        }

        public static void SetBool(UnityEngine.Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p != null) { p.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"[UiBuilder] {target.GetType().Name}.{field} not found");
        }

        public static void SetInt(UnityEngine.Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p != null) { p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"[UiBuilder] {target.GetType().Name}.{field} not found");
        }

        /// <summary>Replaces all persistent listeners of a button with one target method.</summary>
        public static void OnClick(Button button, UnityAction action)
        {
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        /// <summary>Scroll view with a vertical content list; returns the content transform.</summary>
        public static Transform ScrollList(Transform parent, string name, float spacing, RectOffset padding, out ScrollRect scroll)
        {
            var root = Go(name, parent, typeof(ScrollRect));
            scroll = root.GetComponent<ScrollRect>();
            var viewport = Go("Viewport", root.transform, typeof(RectMask2D));
            Stretch(Rt(viewport));
            var content = Go("Content", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var crt = Rt(content);
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;
            VL(content, spacing, padding, TextAnchor.UpperCenter, true, true, true, false);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = Rt(viewport);
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            // Raycast surface so drags anywhere in the list scroll it.
            viewport.AddComponent<InvisibleRaycastTarget>();
            return content.transform;
        }

        /// <summary>Themed slider with a round knob (Settings popup).</summary>
        public static Slider Slider(Transform parent, string name)
        {
            var root = Go(name, parent, typeof(Slider));
            var slider = root.GetComponent<Slider>();
            var bg = Img(root.transform, "Background", UiSprite.BarFrame, UiColor.SurfaceSunken);
            Stretch(Rt(bg), 0f, 18f, 0f, 18f);
            var fillArea = Go("Fill Area", root.transform);
            Stretch(Rt(fillArea), 14f, 22f, 14f, 22f);
            var fill = Img(fillArea.transform, "Fill", UiSprite.BarFill, UiColor.Accent);
            Stretch(Rt(fill));
            var handleArea = Go("Handle Slide Area", root.transform);
            Stretch(Rt(handleArea), 24f, 0f, 24f, 0f);
            var handle = Img(handleArea.transform, "Handle", UiSprite.Knob, UiColor.White, 1f, true);
            Rt(handle).sizeDelta = new Vector2(64f, 0f);
            slider.fillRect = Rt(fill);
            slider.handleRect = Rt(handle);
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        public static CanvasGroup Group(GameObject go)
        {
            return GetOrAdd<CanvasGroup>(go);
        }

        public static ScreenTransition Transition(GameObject go, Vector2 slide, bool pop = false)
        {
            Group(go);
            var st = GetOrAdd<ScreenTransition>(go);
            st.EditorSetup(Rt(go), slide, pop);
            return st;
        }
    }
}
