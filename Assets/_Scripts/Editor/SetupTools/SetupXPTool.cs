#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.LevelUp;
using ProjectB.UI;
using ProjectB.Player;

namespace ProjectB.Editor
{
    public class SetupXPTool
    {
        [MenuItem("ProjectB/Setup XP System")]
        public static void SetupXP()
        {
            // 1. Add HeroExperience to Player
            var player = GameObject.FindGameObjectWithTag("Player");
            HeroExperience heroExp = null;
            if (player != null)
            {
                heroExp = player.GetComponent<HeroExperience>();
                if (heroExp == null)
                {
                    heroExp = player.AddComponent<HeroExperience>();
                }
            }
            else
            {
                Debug.LogWarning("HeroExperience could not be added (Player tag not found).");
            }

            // 2. Create XpManager
            var xpManagerGo = GameObject.Find("XpManager");
            if (xpManagerGo == null)
            {
                xpManagerGo = new GameObject("XpManager");
                xpManagerGo.AddComponent<XpManager>();
            }

            // 3. Setup UI Canvas
            var canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("Canvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // 4. Setup XpBar
            var xpBarGo = GameObject.Find("XpBar");
            if (xpBarGo == null)
            {
                xpBarGo = new GameObject("XpBar");
                xpBarGo.transform.SetParent(canvasGo.transform, false);
                
                var rt = xpBarGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(20, -40); // left, bottom
                rt.offsetMax = new Vector2(-20, -10); // right, top

                var slider = xpBarGo.AddComponent<Slider>();
                
                // Background
                var bgGo = new GameObject("Background");
                bgGo.transform.SetParent(xpBarGo.transform, false);
                var bgImage = bgGo.AddComponent<Image>();
                bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = Vector2.zero;

                // Fill Area
                var fillAreaGo = new GameObject("Fill Area");
                fillAreaGo.transform.SetParent(xpBarGo.transform, false);
                var fillAreaRt = fillAreaGo.AddComponent<RectTransform>();
                fillAreaRt.anchorMin = Vector2.zero;
                fillAreaRt.anchorMax = Vector2.one;
                fillAreaRt.sizeDelta = new Vector2(-10, -10); // padding

                // Fill
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(fillAreaGo.transform, false);
                var fillImage = fillGo.AddComponent<Image>();
                fillImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);
                var fillRt = fillGo.GetComponent<RectTransform>();
                fillRt.sizeDelta = Vector2.zero;

                slider.fillRect = fillRt;
                
                // Level Text
                var textGo = new GameObject("LevelText");
                textGo.transform.SetParent(xpBarGo.transform, false);
                var text = textGo.AddComponent<TextMeshProUGUI>();
                text.text = "Lv 1";
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.fontSize = 24;
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.sizeDelta = Vector2.zero;

                // Setup XpBarUI script
                var xpBarUI = xpBarGo.AddComponent<XpBarUI>();
                
                var serializedObj = new SerializedObject(xpBarUI);
                serializedObj.FindProperty("heroExperience").objectReferenceValue = heroExp;
                serializedObj.FindProperty("xpSlider").objectReferenceValue = slider;
                serializedObj.FindProperty("levelText").objectReferenceValue = text;
                serializedObj.ApplyModifiedProperties();
            }

            Debug.Log("XP System Setup Complete!");
        }
    }
}
#endif
