#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ProjectB.Meta;

namespace ProjectB.Editor
{
    public static class SaveDebugger
    {
        [MenuItem("ProjectB/Save Data/Print Save Data")]
        public static void PrintSaveData()
        {
            if (PlayerPrefs.HasKey(SaveManager.SAVE_KEY))
            {
                string json = PlayerPrefs.GetString(SaveManager.SAVE_KEY);
                Debug.Log($"<color=green><b>Current Save Data:</b></color>\n{json}");
            }
            else
            {
                Debug.LogWarning("No save data found in PlayerPrefs.");
            }
        }

        [MenuItem("ProjectB/Save Data/Clear Save Data")]
        public static void ClearSaveData()
        {
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            PlayerPrefs.Save();
            Debug.Log("<color=red><b>Save Data Cleared!</b></color>");
        }
        
        [MenuItem("ProjectB/Save Data/Add 1000 Coins")]
        public static void AddCoins()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SaveDebugger] Use runtime SaveManager in Play Mode to avoid desync!");
                return;
            }

            SaveData data = new SaveData();
            if (PlayerPrefs.HasKey(SaveManager.SAVE_KEY))
            {
                data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveManager.SAVE_KEY));
            }
            data.coins += 1000;
            PlayerPrefs.SetString(SaveManager.SAVE_KEY, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Debug.Log($"<color=yellow><b>Added 1000 coins! Total: {data.coins}</b></color>");
        }
    }
}
#endif
