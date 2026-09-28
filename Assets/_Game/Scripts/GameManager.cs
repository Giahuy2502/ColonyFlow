using System;
using System.Collections;
using UnityEngine;

namespace ColonyFlow
{
    public enum GameState : byte
    {
        Loading,
        MainMenu,
        Playing,
        Paused,
        Victory,
        Failed
    }

    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameManager : Singleton<GameManager>
    {
        [SerializeField] private DataManager dataManager;
        [SerializeField] private LevelManager levelManager;
        [Header("Level Testing")]
        [SerializeField] private bool useInspectorLevel;
        [SerializeField, Min(1)] private int inspectorLevelNumber = 1;

        private bool doubleSpeed;
        private Coroutine loadRoutine;

        public GameState State { get; private set; } = GameState.Loading;
        public int DisplayLevelNumber
        {
            get
            {
                bool showActiveLevel = levelManager != null && levelManager.HasActiveLevel &&
                                       State != GameState.MainMenu;
                int index = showActiveLevel
                    ? levelManager.ActiveLevelIndex
                    : GetSelectedLevelIndex();
                return index + 1;
            }
        }
        public bool IsDoubleSpeed => doubleSpeed;

        public event Action<GameState> StateChanged;

        protected override void Awake()
        {
            base.Awake();
            if (!enabled)
                return;
            if (dataManager == null)
                dataManager = DataManager.Instance;
            if (levelManager == null)
                levelManager = LevelManager.Instance;
        }

        private void Start()
        {
            if (dataManager == null || levelManager == null || dataManager.LevelCount == 0)
            {
                Debug.LogError("GameManager requires configured DataManager and LevelManager.", this);
                enabled = false;
                return;
            }

            levelManager.LevelCompleted += OnLevelCompleted;
            EnterMainMenu();
        }

        protected override void OnDestroy()
        {
            if (levelManager != null)
                levelManager.LevelCompleted -= OnLevelCompleted;
            Time.timeScale = 1f;
            base.OnDestroy();
        }

        public void PlayGame()
        {
            RequestLoadLevel(GetSelectedLevelIndex());
        }

        private int GetSelectedLevelIndex()
        {
            int savedLevelIndex = dataManager?.PlayerData?.currentLevelIndex ?? 0;
            if (!useInspectorLevel || dataManager == null || dataManager.LevelCount == 0)
                return savedLevelIndex;

            return Mathf.Clamp(inspectorLevelNumber - 1, 0, dataManager.LevelCount - 1);
        }

        public void PauseGame()
        {
            if (State != GameState.Playing)
                return;
            levelManager.SetPaused(true);
            SetState(GameState.Paused);
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            if (State != GameState.Paused)
                return;
            levelManager.SetPaused(false);
            SetState(GameState.Playing);
            ApplyGameSpeed();
        }

        public void OpenSettings()
        {
            if (State == GameState.Playing)
                PauseGame();
            UIManager.Instance?.Open<CanvasSettings>();
        }

        public void CloseSettings()
        {
            UIManager.Instance?.Close<CanvasSettings>();
            if (State == GameState.Paused)
                ResumeGame();
        }

        public void GoToMainMenu()
        {
            levelManager.UnloadLevel();
            EnterMainMenu();
        }

        public void RestartLevel()
        {
            int index = levelManager.HasActiveLevel
                ? levelManager.ActiveLevelIndex
                : dataManager.PlayerData.currentLevelIndex;
            RequestLoadLevel(index);
        }

        public void ReplayLevel()
        {
            RestartLevel();
        }

        public void NextLevel()
        {
            if (State != GameState.Victory)
                return;
            RequestLoadLevel(dataManager.PlayerData.currentLevelIndex);
        }

        public void ToggleGameSpeed()
        {
            if (State != GameState.Playing)
                return;
            doubleSpeed = !doubleSpeed;
            ApplyGameSpeed();
        }

        private void RequestLoadLevel(int levelIndex)
        {
            if (loadRoutine != null)
                return;

            ColonyLevelData data = dataManager.GetLevel(levelIndex);
            if (data == null)
            {
                Debug.LogError($"Cannot load level index {levelIndex}.", this);
                EnterMainMenu();
                return;
            }

            loadRoutine = StartCoroutine(LoadLevelRoutine(data, levelIndex));
        }

        private IEnumerator LoadLevelRoutine(ColonyLevelData data, int levelIndex)
        {
            SetState(GameState.Loading);
            Time.timeScale = 1f;
            UIManager.Instance?.CloseAll();
            CanvasLoading loading = UIManager.Instance?.Open<CanvasLoading>();

            if (loading != null)
            {
                float elapsed = 0f;
                while (elapsed < loading.Duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    loading.SetProgress(elapsed / loading.Duration);
                    yield return null;
                }
                loading.SetProgress(1f);
                yield return null;
            }

            if (!levelManager.StartLevel(data, levelIndex))
            {
                loadRoutine = null;
                EnterMainMenu();
                yield break;
            }

            doubleSpeed = false;
            SetState(GameState.Playing);
            SoundManager.Instance?.PlayMusic(MusicId.Gameplay);
            UIManager.Instance?.Close<CanvasLoading>();
            UIManager.Instance?.Open<CanvasGamePlay>();
            ApplyGameSpeed();
            loadRoutine = null;
        }

        private void OnLevelCompleted(LevelResult result)
        {
            Time.timeScale = 1f;
            if (result == LevelResult.Victory)
            {
                SoundManager.Instance?.StopMusic(0.25f);
                SoundManager.Instance?.PlaySfx(SfxId.Victory);
                int nextIndex = Mathf.Min(
                    levelManager.ActiveLevelIndex + 1, dataManager.LevelCount - 1);
                dataManager.UnlockLevel(nextIndex);
                dataManager.SetCurrentLevel(nextIndex);
                SetState(GameState.Victory);
                UIManager.Instance?.Open<CanvasVictory>();
            }
            else
            {
                SoundManager.Instance?.StopMusic(0.25f);
                SoundManager.Instance?.PlaySfx(SfxId.Failed);
                SetState(GameState.Failed);
                UIManager.Instance?.Open<CanvasFail>();
            }
        }

        private void EnterMainMenu()
        {
            doubleSpeed = false;
            Time.timeScale = 0f;
            UIManager.Instance?.CloseAll();
            SetState(GameState.MainMenu);
            SoundManager.Instance?.PlayMusic(MusicId.MainMenu);
            UIManager.Instance?.Open<CanvasMainMenu>();
        }

        private void ApplyGameSpeed()
        {
            Time.timeScale = State == GameState.Playing
                ? (doubleSpeed ? 2f : 1f)
                : 0f;
        }

        private void SetState(GameState next)
        {
            if (State == next)
                return;
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
