using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public enum BoosterType : byte
    {
        AddTray,
        ShuffleColonies,
        PickHiddenColony,
        RemoveColor
    }

    public enum BoosterTargetMode : byte
    {
        None,
        PickHiddenColony,
        RemoveColor
    }

    [DisallowMultipleComponent]
    public sealed class BoosterManager : MonoBehaviour
    {
        [Header("Shuffle Booster")]
        [SerializeField, Min(0.01f)] private float shuffleAnimationDuration = 0.45f;

        private readonly List<int> taskIds = new List<int>();

        private LevelManager levelManager;
        private ColonyLevelBuilder levelBuilder;
        private AntManager antManager;
        private PixelBoard board;
        private ColonyTray tray;
        private IReadOnlyList<ColonyColumn> columns;
        private bool addTrayUsed;

        public bool CanUseAddTray => levelManager != null &&
                                     levelManager.CanProcessGameplay && !addTrayUsed;
        public BoosterTargetMode TargetMode { get; private set; }

        public event Action StateChanged;

        internal void Configure(LevelManager owner, ColonyLevelBuilder builder,
            AntManager ants, PixelBoard pixelBoard, ColonyTray colonyTray,
            IReadOnlyList<ColonyColumn> colonyColumns)
        {
            levelManager = owner;
            levelBuilder = builder;
            antManager = ants;
            board = pixelBoard;
            tray = colonyTray;
            columns = colonyColumns;
            addTrayUsed = false;
            SetTargetMode(BoosterTargetMode.None);
            StateChanged?.Invoke();
        }

        internal void UnloadLevel()
        {
            addTrayUsed = false;
            SetTargetMode(BoosterTargetMode.None);
            levelManager = null;
            levelBuilder = null;
            antManager = null;
            board = null;
            tray = null;
            columns = null;
            taskIds.Clear();
        }

        public bool UseAddTrayBooster()
        {
            SetTargetMode(BoosterTargetMode.None);
            if (!CanUseAddTray || levelBuilder == null || !levelBuilder.TryExpandTray())
                return false;

            addTrayUsed = true;
            SoundManager.Instance?.PlaySfx(SfxId.BoosterAddTray);
            StateChanged?.Invoke();
            levelManager.ReevaluateProgress();
            return true;
        }

        public bool UseShuffleBooster()
        {
            SetTargetMode(BoosterTargetMode.None);
            bool used = levelManager != null && levelManager.CanProcessGameplay &&
                        levelBuilder != null && levelBuilder.ShuffleRemainingColonies(
                            shuffleAnimationDuration);
            if (used)
            {
                SoundManager.Instance?.PlaySfx(SfxId.BoosterShuffle);
                StateChanged?.Invoke();
            }
            return used;
        }

        public void UsePickHiddenColonyBooster()
        {
            if (levelManager == null || !levelManager.CanProcessGameplay)
                return;

            SetTargetMode(TargetMode == BoosterTargetMode.PickHiddenColony
                ? BoosterTargetMode.None
                : BoosterTargetMode.PickHiddenColony);
        }

        public void UseRemoveColorBooster()
        {
            if (levelManager == null || !levelManager.CanProcessGameplay)
                return;

            SetTargetMode(TargetMode == BoosterTargetMode.RemoveColor
                ? BoosterTargetMode.None
                : BoosterTargetMode.RemoveColor);
        }

        internal void CancelTargetMode()
        {
            SetTargetMode(BoosterTargetMode.None);
        }

        internal bool HandleColonyTarget(Colony colony, int columnIndex)
        {
            if (TargetMode != BoosterTargetMode.PickHiddenColony)
                return false;

            if (TryPickHiddenColony(colony, columnIndex))
                SetTargetMode(BoosterTargetMode.None);
            return true;
        }

        internal bool HandleBoardTarget(Ray ray)
        {
            if (TargetMode != BoosterTargetMode.RemoveColor)
                return false;

            if (TryGetBoardColor(ray, out PixelColor color))
                RemoveColor(color);
            return true;
        }

        private bool TryPickHiddenColony(Colony colony, int columnIndex)
        {
            if (levelManager == null || !levelManager.CanProcessGameplay || colony == null ||
                tray == null || !tray.HasFreeSlot || columns == null ||
                columnIndex < 0 || columnIndex >= columns.Count ||
                columns[columnIndex] == null)
                return false;

            ColonyColumn column = columns[columnIndex];
            if (column.Peek() == colony || !column.TryTakeSpecific(colony))
                return false;

            if (!tray.TryAdd(colony, out _))
                throw new InvalidOperationException(
                    "Tray capacity changed while applying Pick Hidden Colony booster.");

            SoundManager.Instance?.PlaySfx(SfxId.BoosterPick);
            levelManager.ReevaluateProgress();
            StateChanged?.Invoke();
            return true;
        }

        private bool RemoveColor(PixelColor color)
        {
            if (levelManager == null || !levelManager.CanProcessGameplay || board == null ||
                levelBuilder == null)
                return false;

            SetTargetMode(BoosterTargetMode.None);
            antManager?.CancelByColor(color);
            CancelRemainingTasks(color);
            levelBuilder.RemoveColoniesByColor(color);
            int removedPixels = board.RemoveAllPixels(color);

            if (removedPixels <= 0)
                return false;

            SoundManager.Instance?.PlaySfx(SfxId.BoosterClearColor);
            StateChanged?.Invoke();
            if (levelManager.IsPlaying)
                levelManager.ReevaluateProgress();
            return true;
        }

        private void CancelRemainingTasks(PixelColor color)
        {
            taskIds.Clear();
            levelManager.CopyTaskIdsByColor(color, taskIds);
            for (int i = 0; i < taskIds.Count; i++)
                levelManager.CancelTask(taskIds[i], false);
            taskIds.Clear();
        }

        private bool TryGetBoardColor(Ray ray, out PixelColor color)
        {
            color = default;
            if (board == null || !board.IsBuilt)
                return false;

            var plane = new Plane(board.transform.up, board.transform.position);
            if (!plane.Raycast(ray, out float distance))
                return false;

            Vector3 local = board.transform.InverseTransformPoint(ray.GetPoint(distance));
            var position = new Vector2Int(
                Mathf.RoundToInt(local.x / board.CellSize),
                Mathf.RoundToInt(local.z / board.CellSize));
            if (!board.TryGetCell(position, out PixelCell cell) || !cell.IsOccupied)
                return false;

            color = cell.Color;
            return true;
        }

        private void SetTargetMode(BoosterTargetMode mode)
        {
            if (TargetMode == mode)
                return;
            TargetMode = mode;
            StateChanged?.Invoke();
        }
    }
}
