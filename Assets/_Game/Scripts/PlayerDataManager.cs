using System;
using System.IO;
using UnityEngine;

namespace ColonyFlow
{
    [Serializable]
    public sealed class PlayerData
    {
        public int version = 1;
        public int currentLevelIndex;
        public int highestUnlockedLevel;
    }

    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class PlayerDataManager : Singleton<PlayerDataManager>
    {
        private const string LegacyCurrentLevelKey = "ColonyFlow.CurrentLevel";
        private const string LegacyHighestLevelKey = "ColonyFlow.HighestLevel";
        private const string SaveFileName = "player-data.json";

        public PlayerData Data { get; private set; } = new PlayerData();
        public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public void Load(int levelCount)
        {
            int maxIndex = Mathf.Max(0, levelCount - 1);
            PlayerData loaded = null;
            if (File.Exists(SavePath))
            {
                try
                {
                    loaded = JsonUtility.FromJson<PlayerData>(File.ReadAllText(SavePath));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Cannot load player data. Defaults will be used. {exception.Message}", this);
                }
            }
            else
            {
                loaded = new PlayerData
                {
                    currentLevelIndex = PlayerPrefs.GetInt(LegacyCurrentLevelKey, 0),
                    highestUnlockedLevel = PlayerPrefs.GetInt(LegacyHighestLevelKey, 0)
                };
            }

            Data = loaded ?? new PlayerData();
            Data.version = Mathf.Max(1, Data.version);
            Data.highestUnlockedLevel = Mathf.Max(
                Data.highestUnlockedLevel, Data.currentLevelIndex);
            Data.highestUnlockedLevel = Mathf.Clamp(Data.highestUnlockedLevel, 0, maxIndex);
            Data.currentLevelIndex = Mathf.Clamp(
                Data.currentLevelIndex, 0, Data.highestUnlockedLevel);
        }

        public void SetCurrentLevel(int levelIndex, int levelCount)
        {
            int maxIndex = Mathf.Max(0, levelCount - 1);
            Data.currentLevelIndex = Mathf.Clamp(
                levelIndex, 0, Mathf.Min(maxIndex, Data.highestUnlockedLevel));
        }

        public void UnlockLevel(int levelIndex, int levelCount)
        {
            int maxIndex = Mathf.Max(0, levelCount - 1);
            Data.highestUnlockedLevel = Mathf.Max(
                Data.highestUnlockedLevel, Mathf.Clamp(levelIndex, 0, maxIndex));
        }

        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string temporaryPath = SavePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(Data, true));
                if (File.Exists(SavePath))
                    File.Delete(SavePath);
                File.Move(temporaryPath, SavePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Cannot save player data. {exception.Message}", this);
            }
        }
    }
}
