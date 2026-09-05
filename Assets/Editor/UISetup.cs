using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ProjectB.UI;
using ProjectB.Core;
using ProjectB.Player;
using ProjectB.Enemies;
using ProjectB.LevelUp;

public class UISetup
{
    [MenuItem("Tools/Setup UI")]
    public static void Setup()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("Canvas not found");
            return;
        }

        var resources = new DefaultControls.Resources();

        // Create GameManager
        var gmObj = new GameObject("GameManager");
        var gameManager = gmObj.AddComponent<GameManager>();

        // 1. Create HpBar (Slider)
        var hpBarObj = DefaultControls.CreateSlider(resources);
        hpBarObj.name = "HpBar";
        hpBarObj.transform.SetParent(canvas.transform, false);
        var hpRect = hpBarObj.GetComponent<RectTransform>();
        hpRect.anchorMin = new Vector2(0, 1);
        hpRect.anchorMax = new Vector2(0, 1);
        hpRect.pivot = new Vector2(0, 1);
        hpRect.anchoredPosition = new Vector2(20, -100);
        hpRect.sizeDelta = new Vector2(300, 30);

        var fillRect = hpBarObj.transform.Find("Fill Area/Fill").GetComponent<Image>();
        fillRect.color = Color.red;

        // Add HpText
        var hpTextObj = new GameObject("HpText");
        hpTextObj.transform.SetParent(hpBarObj.transform, false);
        var hpTextRect = hpTextObj.AddComponent<RectTransform>();
        hpTextRect.anchorMin = Vector2.zero;
        hpTextRect.anchorMax = Vector2.one;
        hpTextRect.offsetMin = Vector2.zero;
        hpTextRect.offsetMax = Vector2.zero;
        var hpText = hpTextObj.AddComponent<TextMeshProUGUI>();
        hpText.text = "HP / MaxHP";
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.color = Color.white;
        hpText.fontSize = 18;

        var hpBarUI = hpBarObj.AddComponent<HpBarUI>();
        var heroHealth = Object.FindAnyObjectByType<HeroHealth>();
        var hpBarUISer = new SerializedObject(hpBarUI);
        hpBarUISer.FindProperty("heroHealth").objectReferenceValue = heroHealth;
        hpBarUISer.FindProperty("hpSlider").objectReferenceValue = hpBarObj.GetComponent<Slider>();
        hpBarUISer.FindProperty("hpText").objectReferenceValue = hpText;
        hpBarUISer.ApplyModifiedProperties();

        // 2. Create GameOverPanel
        var gameOverObj = DefaultControls.CreatePanel(resources);
        gameOverObj.name = "GameOverPanel";
        gameOverObj.transform.SetParent(canvas.transform, false);
        var goRect = gameOverObj.GetComponent<RectTransform>();
        goRect.anchorMin = Vector2.zero;
        goRect.anchorMax = Vector2.one;
        goRect.offsetMin = Vector2.zero;
        goRect.offsetMax = Vector2.zero;
        gameOverObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);

        // "GAME OVER" Text
        var goTitleObj = new GameObject("Title");
        goTitleObj.transform.SetParent(gameOverObj.transform, false);
        var goTitleRect = goTitleObj.AddComponent<RectTransform>();
        goTitleRect.anchoredPosition = new Vector2(0, 200);
        goTitleRect.sizeDelta = new Vector2(500, 100);
        var goTitle = goTitleObj.AddComponent<TextMeshProUGUI>();
        goTitle.text = "GAME OVER";
        goTitle.alignment = TextAlignmentOptions.Center;
        goTitle.color = Color.red;
        goTitle.fontSize = 72;
        goTitle.fontStyle = FontStyles.Bold;

        // "Wave Reached" Text
        var goWaveObj = new GameObject("WaveText");
        goWaveObj.transform.SetParent(gameOverObj.transform, false);
        var goWaveRect = goWaveObj.AddComponent<RectTransform>();
        goWaveRect.anchoredPosition = new Vector2(0, 50);
        goWaveRect.sizeDelta = new Vector2(500, 50);
        var goWaveText = goWaveObj.AddComponent<TextMeshProUGUI>();
        goWaveText.text = "Wave Reached: 1";
        goWaveText.alignment = TextAlignmentOptions.Center;
        goWaveText.color = Color.white;
        goWaveText.fontSize = 36;

        // Restart Button
        var restartBtnObj = DefaultControls.CreateButton(resources);
        restartBtnObj.name = "RestartButton";
        restartBtnObj.transform.SetParent(gameOverObj.transform, false);
        var restartRect = restartBtnObj.GetComponent<RectTransform>();
        restartRect.anchoredPosition = new Vector2(0, -50);
        restartRect.sizeDelta = new Vector2(200, 60);
        restartBtnObj.GetComponentInChildren<Text>().text = "Restart";

        // Menu Button
        var menuBtnObj = DefaultControls.CreateButton(resources);
        menuBtnObj.name = "MenuButton";
        menuBtnObj.transform.SetParent(gameOverObj.transform, false);
        var menuRect = menuBtnObj.GetComponent<RectTransform>();
        menuRect.anchoredPosition = new Vector2(0, -130);
        menuRect.sizeDelta = new Vector2(200, 60);
        menuBtnObj.GetComponentInChildren<Text>().text = "Main Menu";

        // Revive Button
        var reviveBtnObj = DefaultControls.CreateButton(resources);
        reviveBtnObj.name = "ReviveButton";
        reviveBtnObj.transform.SetParent(gameOverObj.transform, false);
        var reviveRect = reviveBtnObj.GetComponent<RectTransform>();
        reviveRect.anchoredPosition = new Vector2(0, -210);
        reviveRect.sizeDelta = new Vector2(200, 60);
        reviveBtnObj.GetComponentInChildren<Text>().text = "Revive (Ad)";

        // Attach GameOverUI
        var gameOverUI = gameOverObj.AddComponent<GameOverUI>();
        var goUISer = new SerializedObject(gameOverUI);
        goUISer.FindProperty("panel").objectReferenceValue = gameOverObj;
        goUISer.FindProperty("waveText").objectReferenceValue = goWaveText;
        goUISer.ApplyModifiedProperties();

        // Add listeners to buttons
        UnityEditor.Events.UnityEventTools.AddPersistentListener(restartBtnObj.GetComponent<Button>().onClick, gameOverUI.OnRestartClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(menuBtnObj.GetComponent<Button>().onClick, gameOverUI.OnMenuClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(reviveBtnObj.GetComponent<Button>().onClick, gameOverUI.OnReviveClicked);

        Debug.Log("UI Setup Complete!");
    }
}
