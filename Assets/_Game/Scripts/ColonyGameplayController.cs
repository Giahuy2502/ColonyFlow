using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ColonyFlow
{
    public enum LevelState : byte
    {
        Waiting,
        Playing,
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
    public sealed class ColonyGameplayController : MonoBehaviour
    {
        [SerializeField] private PixelBoard board;
        [SerializeField] private ColonyTray tray;
        [SerializeField] private List<ColonyColumn> columns = new List<ColonyColumn>();
        [SerializeField] private Camera inputCamera;
        [Header("Prototype without Ant")]
        [SerializeField] private bool simulateTasks;
        [SerializeField, Min(0.01f)] private float simulatedTaskInterval = 0.1f;
        [SerializeField, Min(0f)] private float deadlockConfirmationDelay = 0.75f;

        private readonly Dictionary<int, ColonyTask> activeTasks = new Dictionary<int, ColonyTask>();
        private readonly List<Colony> trayColonies = new List<Colony>();
        private int nextTaskId = 1;
        private float simulationTimer;
        private float deadlockCandidateSince;
        private bool deadlockCandidate;
        private AntManager antManager;

        public LevelState State { get; private set; } = LevelState.Waiting;
        public int ActiveTaskCount => activeTasks.Count;

        public event Action<LevelState> StateChanged;
        public event Action<ColonyTask> TaskCreated;
        public event Action<ColonyTask> TaskCompleted;
        public event Action<ColonyTask> TaskCancelled;

        public void CopyActiveColonies(List<Colony> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            tray.CopyColonies(destination);
        }

        private void Start()
        {
            if (board == null || tray == null)
            {
                Debug.LogError("GameplayController requires a PixelBoard and ColonyTray.", this);
                enabled = false;
                return;
            }

            board.Completed += OnBoardCompleted;
            board.BoardBuilt += OnBoardBuilt;
            SetState(LevelState.Playing);
            EvaluateProgress();
        }

        public void Configure(PixelBoard pixelBoard, ColonyTray colonyTray, Camera gameplayCamera,
            IEnumerable<ColonyColumn> colonyColumns, bool simulateWithoutAnt, float taskInterval)
        {
            board = pixelBoard;
            tray = colonyTray;
            inputCamera = gameplayCamera;
            columns.Clear();
            if (colonyColumns != null)
                columns.AddRange(colonyColumns);
            simulateTasks = simulateWithoutAnt;
            simulatedTaskInterval = Mathf.Max(0.01f, taskInterval);
        }

        public void RegisterAntManager(AntManager manager)
        {
            antManager = manager;
        }

        public void ReevaluateProgress()
        {
            EvaluateAllColonies();
            EvaluateProgress();
        }

        private void OnDestroy()
        {
            if (board != null)
            {
                board.Completed -= OnBoardCompleted;
                board.BoardBuilt -= OnBoardBuilt;
            }
        }

        private void Update()
        {
            HandlePointerInput();

            if (deadlockCandidate && State == LevelState.Playing)
                EvaluateProgress();

            if (State != LevelState.Playing || !simulateTasks)
                return;

            simulationTimer += Time.deltaTime;
            if (simulationTimer < simulatedTaskInterval)
                return;

            simulationTimer = 0f;
            SimulateOneTaskPerColony();
        }

        private void HandlePointerInput()
        {
            if (State != LevelState.Playing || inputCamera == null)
                return;

            Vector2 pointerPosition;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                pointerPosition = Mouse.current.position.ReadValue();
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            else
                return;

            if (EventSystem.current != null)
            {
                bool pointerOverUi = Mouse.current != null &&
                                     Mouse.current.leftButton.wasPressedThisFrame
                    ? EventSystem.current.IsPointerOverGameObject()
                    : Touchscreen.current != null &&
                      EventSystem.current.IsPointerOverGameObject(
                          Touchscreen.current.primaryTouch.touchId.ReadValue());
                if (pointerOverUi)
                    return;
            }

            Ray ray = inputCamera.ScreenPointToRay(pointerPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f) &&
                ColonyView.TryGetClickTarget(hit.collider, out ColonyView view))
                view.HandleClick();
        }

        public bool SelectColumn(int columnIndex)
        {
            if (State != LevelState.Playing || tray == null || !tray.HasFreeSlot ||
                columnIndex < 0 || columnIndex >= columns.Count || columns[columnIndex] == null)
                return false;

            ColonyColumn column = columns[columnIndex];
            Colony colony = column.Peek();
            if (colony == null || !tray.TryAdd(colony, out _))
                return false;

            if (!column.TryTakeFront(out Colony taken) || taken != colony)
            {
                tray.Remove(colony);
                throw new InvalidOperationException("Column changed while moving a Colony to the Tray.");
            }

            EvaluateColony(colony);
            EvaluateProgress();
            return true;
        }

        // AntManager will call this to obtain an atomic, already-reserved target.
        public bool TryCreateTask(Colony colony, out ColonyTask task)
        {
            return TryCreateTask(colony, board.GetBorderEntrance(), null, out task);
        }

        public bool TryCreateTask(Colony colony, Vector2Int borderStart,
            List<Vector2Int> outboundRoute, out ColonyTask task)
        {
            task = default;
            if (State != LevelState.Playing || colony == null || !colony.CanReceiveTask)
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

            // The Colony box represents ants that have not left it yet. Once the
            // final ant is dispatched, free its tray slot immediately while the
            // ants already in flight continue owning and completing their tasks.
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
            if (!activeTasks.TryGetValue(taskId, out ColonyTask task))
                return false;

            activeTasks.Remove(taskId);
            board.ReleaseReservation(task.Target);
            task.Owner.CancelTask();
            TaskCancelled?.Invoke(task);
            EvaluateColony(task.Owner);
            EvaluateProgress();
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
            tray.CopyColonies(trayColonies);
            for (int i = 0; i < trayColonies.Count; i++)
                EvaluateColony(trayColonies[i]);
        }

        private void EvaluateColony(Colony colony)
        {
            if (colony == null || colony.State == ColonyState.Completed)
                return;

            bool canProgress = colony.InFlightCount > 0 ||
                               (colony.UnassignedCount > 0 && board.HasAvailablePixel(colony.Color));
            colony.SetBlocked(!canProgress);
        }

        private void EvaluateProgress()
        {
            if (State != LevelState.Playing)
                return;
            if (board.IsCompleted)
            {
                CancelDeadlockCheck();
                SetState(LevelState.Victory);
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
                if (colony.State == ColonyState.MovingToTray)
                {
                    CancelDeadlockCheck();
                    return;
                }
                if (colony.CanReceiveTask && board.HasAvailablePixel(colony.Color))
                {
                    CancelDeadlockCheck();
                    return;
                }
            }

            if (tray.HasFreeSlot)
            {
                for (int i = 0; i < columns.Count; i++)
                    if (columns[i] != null && columns[i].HasColony)
                    {
                        CancelDeadlockCheck();
                        return;
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
            SetState(LevelState.Failed);
        }

        private void CancelDeadlockCheck()
        {
            deadlockCandidate = false;
        }

        private void OnBoardCompleted()
        {
            SetState(LevelState.Victory);
        }

        private void OnBoardBuilt()
        {
            if (State == LevelState.Playing)
            {
                EvaluateAllColonies();
                EvaluateProgress();
            }
        }

        private void SetState(LevelState next)
        {
            if (State == next)
                return;
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
