using UnityEngine;

namespace ColonyFlow
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class DataManager : Singleton<DataManager>
    {
        [SerializeField] private GameData gameData;
        [SerializeField] private PlayerDataManager playerDataManager;

        public int LevelCount => gameData != null ? gameData.LevelCount : 0;
        public PlayerData PlayerData => playerDataManager != null
            ? playerDataManager.Data
            : null;

        protected override void Awake()
        {
            base.Awake();
            if (!enabled)
                return;

            if (gameData == null)
                gameData = Resources.Load<GameData>("GameData");
            if (gameData != null)
                PixelColorUtility.SetDefault(gameData.PixelColorPalette);
            if (playerDataManager == null)
                playerDataManager = PlayerDataManager.Instance != null
                    ? PlayerDataManager.Instance
                    : GetComponent<PlayerDataManager>();

            if (gameData == null || gameData.PixelColorPalette == null ||
                gameData.LevelCount == 0 || playerDataManager == null)
            {
                Debug.LogError(
                    "DataManager requires GameData with a Pixel Color Palette, and PlayerDataManager.",
                    this);
                enabled = false;
                return;
            }

            playerDataManager.Load(gameData.LevelCount);
        }

        public ColonyLevelData GetLevel(int index)
        {
            return gameData != null ? gameData.GetLevel(index) : null;
        }

        public void SetCurrentLevel(int levelIndex)
        {
            playerDataManager?.SetCurrentLevel(levelIndex, LevelCount);
        }

        public void UnlockLevel(int levelIndex)
        {
            playerDataManager?.UnlockLevel(levelIndex, LevelCount);
        }

        private void OnApplicationQuit()
        {
            playerDataManager?.Save();
        }
    }
}
