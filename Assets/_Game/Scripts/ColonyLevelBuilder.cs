using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [Serializable]
    public sealed class ColonySpec
    {
        public PixelColor color;
        [Min(1)] public int count = 1;

        public ColonySpec(PixelColor color, int count)
        {
            this.color = color;
            this.count = count;
        }
    }

    [Serializable]
    public sealed class ColonyColumnSpec
    {
        public List<ColonySpec> colonies = new List<ColonySpec>();

        public ColonyColumnSpec(params ColonySpec[] entries)
        {
            colonies.AddRange(entries);
        }
    }

    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ColonyLevelBuilder : MonoBehaviour
    {
        [Header("Required references")]
        [SerializeField] private PixelBoard board;
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Colony colonyPrefab;
        [SerializeField] private ColonyColumn columnPrefab;
        [SerializeField] private ColonyTray trayPrefab;
        [SerializeField] private AntManager antManager;
        [SerializeField] private Transform traySlotPrefab;
        [SerializeField] private Transform holePrefab;

        [Header("Hierarchy roots")]
        [SerializeField] private Transform antsRoot;
        [SerializeField] private Transform coloniesRoot;
        [SerializeField] private Transform colonyTraysRoot;

        [Header("Layout")]
        [SerializeField, Min(0f)] private float holeDistanceBelowBoard = 0.55f;

        [Header("Gameplay")]
        [SerializeField] private bool simulateWithoutAnt;
        [SerializeField, Min(0.01f)] private float simulatedTaskInterval = 0.08f;

        private readonly List<ColonyColumn> columns = new List<ColonyColumn>();
        private readonly List<List<ColonyView>> columnViews = new List<List<ColonyView>>();
        private readonly Dictionary<Colony, ColonyView> views =
            new Dictionary<Colony, ColonyView>();
        private readonly List<Vector3> trayPositions = new List<Vector3>();
        private readonly List<PixelData> generatedPixels = new List<PixelData>();
        private readonly List<ColonyColumnSpec> columnData = new List<ColonyColumnSpec>();
        private ColonyTray tray;
        private Transform antHole;
        private int trayCapacity;
        private bool columnLayoutRefreshPending;

        private void Awake()
        {
            if (!ValidateReferences())
                enabled = false;
        }

        private void OnDestroy()
        {
            UnloadLevel();
        }

        private void LateUpdate()
        {
            if (!columnLayoutRefreshPending)
                return;

            columnLayoutRefreshPending = false;
            RefreshColumnPositions();
        }

        public bool BuildLevel(ColonyLevelData levelData)
        {
            if (!enabled || levelData == null || !ValidateReferences())
                return false;

            UnloadLevel();
            levelData.BuildPixels(generatedPixels);
            trayCapacity = levelData.TrayCapacity;
            levelData.BuildColumns(generatedPixels, columnData);
            if (!levelData.ValidateGeneratedLevel(generatedPixels, columnData, out string error))
            {
                Debug.LogError($"Cannot start Colony Flow level. {error}", levelData);
                return false;
            }

            board.Configure(levelData.BoardSize, levelData.CellSize, generatedPixels);
            tray = Instantiate(trayPrefab, colonyTraysRoot);
            tray.name = "Colony Tray";
            tray.Configure(trayCapacity);
            CreateTraySlots();
            CreateHole();
            CreateColumns();
            InitializeViews();
            tray.ColonyAdded += OnColonyAdded;
            tray.ColonyRemoved += OnColonyRemoved;

            levelManager.ConfigureRuntime(board, tray, gameplayCamera, columns,
                simulateWithoutAnt, simulatedTaskInterval);
            antManager.OnInit(board, levelManager, antHole, antsRoot);
            board.BuildBoard();
            return board.IsBuilt;
        }

        public void UnloadLevel()
        {
            if (tray != null)
            {
                tray.ColonyAdded -= OnColonyAdded;
                tray.ColonyRemoved -= OnColonyRemoved;
            }

            foreach (ColonyView view in views.Values)
                DeactivateAndDestroy(view != null ? view.gameObject : null);
            for (int i = 0; i < columns.Count; i++)
                DeactivateAndDestroy(columns[i] != null ? columns[i].gameObject : null);
            DeactivateAndDestroy(tray != null ? tray.gameObject : null);
            DeactivateAndDestroy(antHole != null ? antHole.gameObject : null);

            board?.ClearBoard();
            columns.Clear();
            columnViews.Clear();
            views.Clear();
            trayPositions.Clear();
            generatedPixels.Clear();
            columnData.Clear();
            tray = null;
            antHole = null;
            columnLayoutRefreshPending = false;
        }

        private bool ValidateReferences()
        {
            if (board != null && levelManager != null && gameplayCamera != null &&
                colonyPrefab != null && columnPrefab != null && trayPrefab != null &&
                antManager != null && traySlotPrefab != null && holePrefab != null &&
                antsRoot != null && coloniesRoot != null && colonyTraysRoot != null)
                return true;

            Debug.LogError("ColonyLevelBuilder has missing serialized references.", this);
            return false;
        }

        private void CreateHole()
        {
            antHole = Instantiate(holePrefab, colonyTraysRoot);
            antHole.name = "Ant Hole";
            float boardCenterX = (board.Size.x - 1) * board.CellSize * 0.5f;
            float bottomBorderZ = -board.CellSize * 0.55f;
            antHole.localPosition = new Vector3(
                boardCenterX, 0f, bottomBorderZ - holeDistanceBelowBoard);
            antHole.localRotation = Quaternion.identity;
        }

        private void CreateTraySlots()
        {
            float center = (board.Size.x - 1) * board.CellSize * 0.5f;
            const float spacing = 0.9f;
            float startX = center - (trayCapacity - 1) * spacing * 0.5f;
            for (int i = 0; i < trayCapacity; i++)
            {
                Vector3 position = new Vector3(startX + i * spacing, 0f, -2.65f);
                trayPositions.Add(position + Vector3.up * 0.35f);
                Transform slot = Instantiate(traySlotPrefab, tray.transform);
                slot.name = $"Tray Slot {i + 1}";
                slot.localPosition = position;
                slot.localRotation = Quaternion.identity;
            }
        }

        private void CreateColumns()
        {
            float center = (board.Size.x - 1) * board.CellSize * 0.5f;
            const float spacing = 0.9f;
            float startX = center - (columnData.Count - 1) * spacing * 0.5f;

            for (int columnIndex = 0; columnIndex < columnData.Count; columnIndex++)
            {
                ColonyColumn column = Instantiate(columnPrefab, coloniesRoot);
                column.name = $"Colony Column {columnIndex + 1}";
                var models = new List<Colony>();
                var visualList = new List<ColonyView>();
                ColonyColumnSpec spec = columnData[columnIndex];

                for (int depth = 0; depth < spec.colonies.Count; depth++)
                {
                    ColonySpec colonySpec = spec.colonies[depth];
                    Colony colony = Instantiate(colonyPrefab, column.transform);
                    colony.name = $"Colony {columnIndex + 1}-{depth + 1} " +
                                  $"{colonySpec.color} {colonySpec.count}";
                    colony.transform.localPosition =
                        ColumnPosition(startX, spacing, columnIndex, depth);
                    colony.transform.localRotation = Quaternion.identity;
                    colony.Configure(colonySpec.color, colonySpec.count);

                    if (colony.View == null)
                        throw new MissingReferenceException(
                            $"{colonyPrefab.name} requires its ColonyView reference.");
                    models.Add(colony);
                    visualList.Add(colony.View);
                    views.Add(colony, colony.View);
                }

                column.Configure(models);
                columns.Add(column);
                columnViews.Add(visualList);
            }
        }

        private void InitializeViews()
        {
            for (int columnIndex = 0; columnIndex < columnViews.Count; columnIndex++)
                foreach (ColonyView view in columnViews[columnIndex])
                    view.Initialize(view.Colony, levelManager, columnIndex);
        }

        private void OnColonyAdded(int slotIndex, Colony colony)
        {
            if (views.TryGetValue(colony, out ColonyView view) && slotIndex < trayPositions.Count)
            {
                view.transform.SetParent(colonyTraysRoot, true);
                view.MoveToTray(trayPositions[slotIndex]);
            }
            // ColonyAdded fires before ColonyColumn.TryTakeFront updates the
            // column. Reflow in LateUpdate after the column state is current.
            columnLayoutRefreshPending = true;
        }

        private void OnColonyRemoved(int slotIndex, Colony colony)
        {
            if (views.TryGetValue(colony, out ColonyView view))
                view.PlayDisappear();
        }

        private void RefreshColumnPositions()
        {
            float center = (board.Size.x - 1) * board.CellSize * 0.5f;
            const float spacing = 0.9f;
            int activeColumnCount = 0;
            for (int i = 0; i < columns.Count; i++)
                if (columns[i] != null && columns[i].HasColony)
                    activeColumnCount++;

            if (activeColumnCount == 0)
                return;

            float startX = center - (activeColumnCount - 1) * spacing * 0.5f;
            int compactColumnIndex = 0;
            for (int columnIndex = 0; columnIndex < columnViews.Count; columnIndex++)
            {
                if (columns[columnIndex] == null || !columns[columnIndex].HasColony)
                    continue;

                int depth = 0;
                foreach (ColonyView view in columnViews[columnIndex])
                    if (view.Colony.State == ColonyState.InColumn)
                        view.MoveToColumnPosition(
                            ColumnPosition(startX, spacing, compactColumnIndex, depth++));
                compactColumnIndex++;
            }
        }

        private static Vector3 ColumnPosition(float startX, float spacing,
            int column, int depth)
        {
            return new Vector3(startX + column * spacing, 0.35f, -3.85f - depth * 0.92f);
        }

        private static void DeactivateAndDestroy(GameObject target)
        {
            if (target == null)
                return;
            target.SetActive(false);
            Destroy(target);
        }
    }
}
