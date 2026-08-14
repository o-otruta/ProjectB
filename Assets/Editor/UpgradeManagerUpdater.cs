using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ProjectB.LevelUp;
using System.Collections.Generic;

public class UpgradeManagerUpdater
{
    [MenuItem("Tools/Update Upgrade Manager")]
    public static void UpdateManager()
    {
        string scenePath = "Assets/_Scenes/Gameplay.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        var manager = Object.FindAnyObjectByType<UpgradeManager>();
        if (manager != null)
        {
            if (manager.cardPool == null)
            {
                manager.cardPool = new List<CardData>();
            }

            string[] guids = AssetDatabase.FindAssets("t:AbilityCardData", new[] { "Assets/_Scripts/LevelUp/Cards" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<AbilityCardData>(path);
                if (card != null && !manager.cardPool.Contains(card))
                {
                    manager.cardPool.Add(card);
                }
            }
            
            EditorUtility.SetDirty(manager);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("UpgradeManager updated successfully in Gameplay scene.");
        }
        else
        {
            Debug.LogWarning("UpgradeManager not found in Gameplay scene.");
        }
    }
}
