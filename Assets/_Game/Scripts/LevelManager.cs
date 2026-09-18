using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelManager : MonoBehaviour
    {
        private const string CurrentLevelKey = "ColonyFlow.CurrentLevel";
        private const string HighestLevelKey = "ColonyFlow.HighestLevel";

        private ColonyGameplayController controller;
        private AntManager antManager;
        private int levelCount = 1;
        private bool cleanupPending;
        private bool isConfigured;
        private bool resultCanvasOpened;

        public static LevelManager Instance { get; private set; }
        public int CurrentLevelIndex { get; private set; }
        public int DisplayLevelNumber => CurrentLevelIndex + 1;
        public int HighestUnlockedLevel { get; private set; }

        public event Action<int> LevelLoaded;
        public event Action LevelRestarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one LevelManager may exist in a scene.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        public int ResolveSavedLevelIndex(int availableLevelCount)
        {
            int count = Mathf.Max(1, availableLevelCount);
            return Mathf.Clamp(PlayerPrefs.GetInt(CurrentLevelKey, 0), 0, count - 1);
        }

        public void Configure(ColonyGameplayController gameplayController,
            AntManager gameplayAntManager, int availableLevelCount, int loadedLevelIndex)
        {
            controller = gameplayController;
            antManager = gameplayAntManager;
            levelCount = Mathf.Max(1, availableLevelCount);
            CurrentLevelIndex = Mathf.Clamp(loadedLevelIndex, 0, levelCount - 1);
            HighestUnlockedLevel = Mathf.Clamp(
                PlayerPrefs.GetInt(HighestLevelKey, CurrentLevelIndex), 0, levelCount - 1);
            isConfigured = true;
        }

        private void Start()
        {
            if (!isConfigured || controller == null)
            {
                Debug.LogError("LevelManager was not configured by ColonyLevelBuilder.", this);
                enabled = false;
                return;
            }

            controller.StateChanged += OnGameplayStateChanged;
            OnGameplayStateChanged(controller.State);
            UIManager.Instance?.Open<CanvasGamePlay>();
            LevelLoaded?.Invoke(CurrentLevelIndex);
        }

        private void OnDestroy()
        {
            if (controller != null)
                controller.StateChanged -= OnGameplayStateChanged;
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (!cleanupPending)
                return;

            cleanupPending = false;
            if (antManager != null)
                antManager.CancelAll();
        }

        public void RestartLevel()
        {
            if (!isConfigured)
                return;

            LevelRestarted?.Invoke();
            ReloadScene();
        }

        public void NextLevel()
        {
            if (!isConfigured || controller.State != LevelState.Victory)
                return;

            int nextIndex = Mathf.Min(CurrentLevelIndex + 1, levelCount - 1);
            SaveCurrentLevel(nextIndex);
            ReloadScene();
        }

        public void LoadLevel(int levelIndex)
        {
            if (!isConfigured || levelIndex < 0 || levelIndex >= levelCount)
                return;

            SaveCurrentLevel(levelIndex);
            ReloadScene();
        }

        private void OnGameplayStateChanged(LevelState state)
        {
            if (state == LevelState.Victory)
            {
                HighestUnlockedLevel = Mathf.Max(
                    HighestUnlockedLevel, Mathf.Min(CurrentLevelIndex + 1, levelCount - 1));
                PlayerPrefs.SetInt(HighestLevelKey, HighestUnlockedLevel);
                PlayerPrefs.Save();
                cleanupPending = true;
                resultCanvasOpened = UIManager.Instance != null &&
                                     UIManager.Instance.Open<CanvasVictory>() != null;
            }
            else if (state == LevelState.Failed)
            {
                cleanupPending = true;
                resultCanvasOpened = UIManager.Instance != null &&
                                     UIManager.Instance.Open<CanvasFail>() != null;
            }
        }

        private void OnGUI()
        {
            if (!isConfigured || controller == null || UIManager.Instance != null ||
                resultCanvasOpened)
                return;

            float scale = Mathf.Max(1f, Screen.width / 540f);
            GUIStyle levelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(22f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(0f, 18f * scale, Screen.width, 42f * scale),
                $"Level {DisplayLevelNumber}", levelStyle);

            if (controller.State != LevelState.Victory && controller.State != LevelState.Failed)
                return;

            float panelWidth = Mathf.Min(Screen.width - 48f * scale, 360f * scale);
            float panelHeight = 210f * scale;
            Rect panel = new Rect((Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
            GUI.Box(panel, GUIContent.none);

            GUIStyle titleStyle = new GUIStyle(levelStyle)
            {
                fontSize = Mathf.RoundToInt(30f * scale)
            };
            string title = controller.State == LevelState.Victory ? "LEVEL COMPLETE" : "NO MORE MOVES";
            GUI.Label(new Rect(panel.x, panel.y + 22f * scale, panel.width, 52f * scale),
                title, titleStyle);

            float buttonWidth = panel.width - 64f * scale;
            Rect primaryButton = new Rect(panel.x + 32f * scale, panel.y + 92f * scale,
                buttonWidth, 48f * scale);
            if (controller.State == LevelState.Victory && CurrentLevelIndex < levelCount - 1)
            {
                if (GUI.Button(primaryButton, "NEXT LEVEL"))
                    NextLevel();
            }
            else if (GUI.Button(primaryButton, "RESTART"))
            {
                RestartLevel();
            }

            Rect restartButton = new Rect(panel.x + 32f * scale, panel.y + 150f * scale,
                buttonWidth, 36f * scale);
            if (controller.State == LevelState.Victory && GUI.Button(restartButton, "REPLAY"))
                RestartLevel();
        }

        private void SaveCurrentLevel(int index)
        {
            CurrentLevelIndex = Mathf.Clamp(index, 0, levelCount - 1);
            PlayerPrefs.SetInt(CurrentLevelKey, CurrentLevelIndex);
            PlayerPrefs.Save();
        }

        private static void ReloadScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }
    }
}
