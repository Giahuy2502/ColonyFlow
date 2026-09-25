using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ColonyFlow
{
    public enum LevelResult : byte
    {
        Victory,
        Failed
    }

    public readonly struct ColonyTask
    {
        public int Id { get; }
        public Colony Owner { get; }
        public Vector2Int Target { get; }
        public PixelColor Color => Owner.Color;

        internal ColonyTask(int id, Colony owner, Vector2Int target)
        {
            Id = id;
            Owner = owner;
            Target = target;
        }
    }
    [DisallowMultipleComponent]
    public sealed class LevelManager : Singleton<LevelManager>
    {
        [SerializeField] private ColonyLevelBuilder levelBuilder;
        [SerializeField] private AntManager antManager;
        [SerializeField] private BoosterManager boosterManager;
        [SerializeField, Min(0f)] private float deadlockConfirmationDelay = 0.75f;

        private readonly Dictionary<int, ColonyTask> activeTasks =
            new Dictionary<int, ColonyTask>();
        private readonly List<Colony> trayColonies = new List<Colony>();
        private readonly List<ColonyColumn> columns = new List<ColonyColumn>();
        private PixelBoard board;
        private ColonyTray tray;
        private Camera inputCamera;
        private int nextTaskId = 1;
        private float simulationTimer;
        private bool simulateTasks;
        private float simulatedTaskInterval = 0.1f;
        private float deadlockCandidateSince;
        private bool deadlockCandidate;
        private bool cleanupPending;

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public bool CanProcessGameplay => IsPlaying && !IsPaused;
        public int ActiveTaskCount => activeTasks.Count;
        public int ActiveLevelIndex { get; private set; } = -1;
        public bool HasActiveLevel => ActiveLevelIndex >= 0 && board != null;
        public BoosterManager BoosterManager => boosterManager;

        public event Action<LevelResult> LevelCompleted;
        public event Action<ColonyTask> TaskCreated;
        public event Action<ColonyTask> TaskCompleted;
        public event Action<ColonyTask> TaskCancelled;

        protected override void Awake()
        {
            base.Awake();
            if (!enabled)
                return;
            if (levelBuilder == null)
                levelBuilder = FindFirstObjectByType<ColonyLevelBuilder>();
            if (antManager == null)
                antManager = AntManager.Instance;
            if (boosterManager == null)
                boosterManager = GetComponent<BoosterManager>();
            if (boosterManager == null)
                boosterManager = gameObject.AddComponent<BoosterManager>();
        }

        protected override void OnDestroy()
        {
            DetachBoardEvents();
            base.OnDestroy();
        }

        public bool StartLevel(ColonyLevelData levelData, int levelIndex)
        {
            if (levelData == null || levelBuilder == null || antManager == null)
                return false;

            UnloadLevel();
            ActiveLevelIndex = Mathf.Max(0, levelIndex);
            IsPlaying = false;
            IsPaused = false;
            if (!levelBuilder.BuildLevel(levelData))
            {
                ActiveLevelIndex = -1;
                return false;
            }

            IsPlaying = true;
            EvaluateAllColonies();
            EvaluateProgress();
            return true;
        }

        public void UnloadLevel()
        {
            IsPlaying = false;
            IsPaused = false;
            cleanupPending = false;
            deadlockCandidate = false;
            boosterManager?.UnloadLevel();
            antManager?.Shutdown();
            DetachBoardEvents();
            activeTasks.Clear();
            trayColonies.Clear();
            columns.Clear();
            board = null;
            tray = null;
            inputCamera = null;
            nextTaskId = 1;
            simulationTimer = 0f;
            levelBuilder?.UnloadLevel();
            ActiveLevelIndex = -1;
        }

        internal void ConfigureRuntime(PixelBoard pixelBoard, ColonyTray colonyTray,
            Camera gameplayCamera, IEnumerable<ColonyColumn> colonyColumns,
            bool simulateWithoutAnt, float taskInterval)
        {
            DetachBoardEvents();
            board = pixelBoard;
            tray = colonyTray;
            inputCamera = gameplayCamera;
            columns.Clear();
            if (colonyColumns != null)
                columns.AddRange(colonyColumns);
            boosterManager?.Configure(this, levelBuilder, antManager, board, tray, columns);
            simulateTasks = simulateWithoutAnt;
            simulatedTaskInterval = Mathf.Max(0.01f, taskInterval);
            board.BoardBuilt += OnBoardBuilt;
            board.Completed += OnBoardCompleted;
        }

        public void CopyActiveColonies(List<Colony> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (tray == null)
            {
                destination.Clear();
                return;
            }
            tray.CopyColonies(destination);
        }

        public void ReevaluateProgress()
        {
            EvaluateAllColonies();
            EvaluateProgress();
        }

        public void SetPaused(bool paused)
        {
            if (IsPlaying)
            {
                IsPaused = paused;
                if (paused)
                    boosterManager?.CancelTargetMode();
            }
        }

        private void Update()
        {
            if (!CanProcessGameplay)
                return;

            HandlePointerInput();
            if (deadlockCandidate)
                EvaluateProgress();
            if (!simulateTasks)
                return;

            simulationTimer += Time.deltaTime;
            if (simulationTimer < simulatedTaskInterval)
                return;
            simulationTimer = 0f;
            SimulateOneTaskPerColony();
        }

        private void LateUpdate()
        {
            if (!cleanupPending)
                return;
            cleanupPending = false;
            antManager?.CancelAll();
        }

        private void HandlePointerInput()
        {
            if (inputCamera == null)
                return;

            Vector2 pointerPosition;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                pointerPosition = Mouse.current.position.ReadValue();
            else if (Touchscreen.current != null &&
                     Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            else
                return;

            if (EventSystem.current != null)
            {
                bool pointerOverUi = Mouse.current != null &&
                                     Mouse.current.leftButton.wasPressedThisFrame
                    ? EventSystem.current.IsPointerOverGameObject()
                    : Touchscreen.current != null && EventSystem.current.IsPointerOverGameObject(
                        Touchscreen.current.primaryTouch.touchId.ReadValue());
                if (pointerOverUi)
                    return;
            }

            Ray ray = inputCamera.ScreenPointToRay(pointerPosition);
            if (boosterManager != null && boosterManager.HandleBoardTarget(ray))
                return;

            if (Physics.Raycast(ray, out RaycastHit hit, 100f) &&
                ColonyView.TryGetClickTarget(hit.collider, out ColonyView view))
                view.HandleClick();
        }

        public void HandleColonyClick(ColonyView view, int columnIndex)
        {
            if (view == null || view.Colony == null)
                return;

            if (boosterManager != null &&
                boosterManager.HandleColonyTarget(view.Colony, columnIndex))
                return;

            if (boosterManager == null ||
                boosterManager.TargetMode == BoosterTargetMode.None)
                SelectColumn(columnIndex);
        }

        public bool SelectColumn(int columnIndex)
        {
            if (!CanProcessGameplay || tray == null || !tray.HasFreeSlot ||
                columnIndex < 0 || columnIndex >= columns.Count || columns[columnIndex] == null)
                return false;

            ColonyColumn column = columns[columnIndex];
            Colony colony = column.Peek();
            if (colony == null || !tray.TryAdd(colony, out _))
                return false;

            if (!column.TryTakeFront(out Colony taken) || taken != colony)
            {
                tray.Remove(colony);
                throw new InvalidOperationException(
                    "Column changed while moving a Colony to the Tray.");
            }

            EvaluateColony(colony);
            EvaluateProgress();
            return true;
        }

        internal void CopyTaskIdsByColor(PixelColor color, List<int> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<int, ColonyTask> pair in activeTasks)
                if (pair.Value.Color == color)
                    destination.Add(pair.Key);
        }

        public bool TryCreateTask(Colony colony, out ColonyTask task)
        {
            return TryCreateTask(colony, board.GetBorderEntrance(), null, out task);
        }

        public bool TryCreateTask(Colony colony, Vector2Int borderStart,
            List<Vector2Int> outboundRoute, out ColonyTask task)
        {
            task = default;
            if (!CanProcessGameplay || colony == null || !colony.CanReceiveTask)
            {
                if (colony != null)
                    EvaluateColony(colony);
                return false;
            }

            Vector2Int target;
            bool reserved = outboundRoute == null
                ? board.TryReservePixel(colony.Color, out target)
                : board.TryReserveNearestReachablePixel(
                    colony.Color, borderStart, out target, outboundRoute);
            if (!reserved)
            {
                EvaluateColony(colony);
                return false;
            }

            if (!colony.TryBeginTask())
            {
                board.ReleaseReservation(target);
                return false;
            }

            task = new ColonyTask(nextTaskId++, colony, target);
            activeTasks.Add(task.Id, task);
            TaskCreated?.Invoke(task);
            if (colony.UnassignedCount == 0)
                tray.Remove(colony);
            return true;
        }

        public bool CompleteTask(int taskId)
        {
            if (!activeTasks.TryGetValue(taskId, out ColonyTask task))
                return false;
            if (!board.TryCollectReservedPixel(task.Target))
            {
                CancelTask(taskId);
                return false;
            }

            activeTasks.Remove(taskId);
            task.Owner.CompleteTask();
            TaskCompleted?.Invoke(task);
            EvaluateAllColonies();
            EvaluateProgress();
            return true;
        }

        public bool CancelTask(int taskId)
        {
            return CancelTask(taskId, true);
        }

        internal bool CancelTask(int taskId, bool reevaluate)
        {
            if (!activeTasks.TryGetValue(taskId, out ColonyTask task))
                return false;
            activeTasks.Remove(taskId);
            if (board != null && board.IsBuilt)
                board.ReleaseReservation(task.Target);
            task.Owner.CancelTask();
            TaskCancelled?.Invoke(task);
            if (reevaluate)
            {
                EvaluateColony(task.Owner);
                EvaluateProgress();
            }
            return true;
        }

        private void SimulateOneTaskPerColony()
        {
            tray.CopyColonies(trayColonies);
            for (int i = 0; i < trayColonies.Count; i++)
            {
                Colony colony = trayColonies[i];
                if (TryCreateTask(colony, out ColonyTask task))
                    CompleteTask(task.Id);
            }
            EvaluateProgress();
        }

        private void EvaluateAllColonies()
        {
            if (tray == null || board == null)
                return;
            tray.CopyColonies(trayColonies);
            for (int i = 0; i < trayColonies.Count; i++)
                EvaluateColony(trayColonies[i]);
        }

        private void EvaluateColony(Colony colony)
        {
            if (board == null || colony == null || colony.State == ColonyState.Completed)
                return;
            bool canProgress = colony.InFlightCount > 0 ||
                               (colony.UnassignedCount > 0 &&
                                board.HasAvailablePixel(colony.Color));
            colony.SetBlocked(!canProgress);
        }

        private void EvaluateProgress()
        {
            if (!CanProcessGameplay || board == null || tray == null)
                return;
            if (board.IsCompleted)
            {
                CancelDeadlockCheck();
                FinishLevel(LevelResult.Victory);
                return;
            }
            if (activeTasks.Count > 0 || (antManager != null && antManager.ActiveCount > 0))
            {
                CancelDeadlockCheck();
                return;
            }

            tray.CopyColonies(trayColonies);
            for (int i = 0; i < trayColonies.Count; i++)
            {
                Colony colony = trayColonies[i];
                if (colony.State == ColonyState.MovingToTray ||
                    (colony.CanReceiveTask && board.HasAvailablePixel(colony.Color)))
                {
                    CancelDeadlockCheck();
                    return;
                }
            }

            if (tray.HasFreeSlot)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i] != null && columns[i].HasColony)
                    {
                        CancelDeadlockCheck();
                        return;
                    }
                }
            }

            if (!deadlockCandidate)
            {
                deadlockCandidate = true;
                deadlockCandidateSince = Time.time;
                return;
            }
            if (Time.time - deadlockCandidateSince < deadlockConfirmationDelay)
                return;

            deadlockCandidate = false;
            FinishLevel(LevelResult.Failed);
        }

        private void CancelDeadlockCheck()
        {
            deadlockCandidate = false;
        }

        private void OnBoardCompleted()
        {
            if (CanProcessGameplay)
                FinishLevel(LevelResult.Victory);
        }

        private void OnBoardBuilt()
        {
            if (CanProcessGameplay)
            {
                EvaluateAllColonies();
                EvaluateProgress();
            }
        }

        private void FinishLevel(LevelResult result)
        {
            if (!IsPlaying)
                return;
            IsPlaying = false;
            IsPaused = false;
            boosterManager?.CancelTargetMode();
            cleanupPending = true;
            LevelCompleted?.Invoke(result);
        }

        private void DetachBoardEvents()
        {
            if (board == null)
                return;
            board.BoardBuilt -= OnBoardBuilt;
            board.Completed -= OnBoardCompleted;
        }
    }
}
