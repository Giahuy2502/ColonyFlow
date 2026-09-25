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
        [SerializeField] private ColonyLevelData levelData;
        [SerializeField] private List<ColonyLevelData> levels = new List<ColonyLevelData>();
        [SerializeField] private PixelBoard board;
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Colony colonyPrefab;
        [SerializeField] private ColonyColumn columnPrefab;
        [SerializeField] private ColonyTray trayPrefab;
        [SerializeField] private ColonyGameplayController controller;
        [SerializeField] private AntManager antManager;
        [SerializeField] private Transform traySlotPrefab;
        [SerializeField] private Transform holePrefab;

        [Header("Hierarchy roots")]
        [SerializeField] private Transform antsRoot;
        [SerializeField] private Transform coloniesRoot;
        [SerializeField] private Transform colonyTraysRoot;

        [Header("Layout")]
        [SerializeField, Min(0f)] private float holeDistanceBelowBoard = 0.55f;

        [Header("Level")]
        [SerializeField, Min(1)] private int trayCapacity = 5;
        [SerializeField] private List<ColonyColumnSpec> columnData = new List<ColonyColumnSpec>();
        [SerializeField] private bool simulateWithoutAnt = true;
        [SerializeField, Min(0.01f)] private float simulatedTaskInterval = 0.08f;

        private readonly List<ColonyColumn> columns = new List<ColonyColumn>();
        private readonly List<List<ColonyView>> columnViews = new List<List<ColonyView>>();
        private readonly Dictionary<Colony, ColonyView> views = new Dictionary<Colony, ColonyView>();
        private readonly List<Vector3> trayPositions = new List<Vector3>();
        private readonly List<PixelData> generatedPixels = new List<PixelData>();
        private ColonyTray tray;
        private Transform antHole;
        private int loadedLevelIndex;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            if (!ApplyLevelData())
            {
                enabled = false;
                return;
            }
            tray = Instantiate(trayPrefab, colonyTraysRoot);
            tray.name = "Colony Tray";
            tray.Configure(trayCapacity);

            CreateTraySlots();
            CreateHole();
            CreateColumns();
            controller.Configure(board, tray, gameplayCamera, columns, simulateWithoutAnt, simulatedTaskInterval);
            antManager.OnInit(board, controller, antHole, antsRoot);
            levelManager.Configure(controller, antManager,
                levels.Count > 0 ? levels.Count : 1, loadedLevelIndex);
            InitializeViews();
            tray.ColonyAdded += OnColonyAdded;
            tray.ColonyRemoved += OnColonyRemoved;
        }

        private void OnDestroy()
        {
            if (tray == null)
                return;
            tray.ColonyAdded -= OnColonyAdded;
            tray.ColonyRemoved -= OnColonyRemoved;
        }

        private bool ValidateReferences()
        {
            if (board != null && levelManager != null && gameplayCamera != null &&
                colonyPrefab != null && columnPrefab != null &&
                trayPrefab != null && controller != null && antManager != null &&
                traySlotPrefab != null && holePrefab != null && antsRoot != null &&
                coloniesRoot != null && colonyTraysRoot != null)
                    return true;

            Debug.LogError("ColonyLevelBuilder has missing serialized prefab references.", this);
            return false;
        }

        private void CreateHole()
        {
            antHole = Instantiate(holePrefab, colonyTraysRoot);
            antHole.name = "Ant Hole";
            float boardCenterX = (board.Size.x - 1) * board.CellSize * 0.5f;
            float bottomBorderZ = -board.CellSize * 0.55f;
            float holeZ = bottomBorderZ - holeDistanceBelowBoard;
            antHole.localPosition = new Vector3(boardCenterX, 0f, holeZ);
            antHole.localRotation = Quaternion.identity;
        }

        private void EnsureSampleData()
        {
            // This prototype layout is deterministic. LevelData will replace it later.
            columnData = new List<ColonyColumnSpec>
            {
                new ColonyColumnSpec(new ColonySpec(PixelColor.Blue, 5)),
                new ColonyColumnSpec(new ColonySpec(PixelColor.Blue, 5),
                    new ColonySpec(PixelColor.Orange, 1)),
                new ColonyColumnSpec(new ColonySpec(PixelColor.Blue, 5)),
                new ColonyColumnSpec(new ColonySpec(PixelColor.Blue, 5)),
                new ColonyColumnSpec(new ColonySpec(PixelColor.Blue, 4))
            };
        }

        private bool ApplyLevelData()
        {
            ColonyLevelData[] resourceLevels = Resources.LoadAll<ColonyLevelData>("Levels");
            if (resourceLevels.Length > 0)
            {
                Array.Sort(resourceLevels,
                    (left, right) => string.CompareOrdinal(left.name, right.name));
                levels.Clear();
                levels.AddRange(resourceLevels);
            }

            if (levels.Count > 0)
            {
                loadedLevelIndex = levelManager.ResolveSavedLevelIndex(levels.Count);
                levelData = levels[loadedLevelIndex];
            }
            else
            {
                loadedLevelIndex = 0;
            }

            if (levelData == null)
            {
                EnsureSampleData();
                return true;
            }

            levelData.BuildPixels(generatedPixels);
            trayCapacity = levelData.TrayCapacity;
            levelData.BuildColumns(generatedPixels, columnData);
            if (!levelData.ValidateGeneratedLevel(generatedPixels, columnData, out string error))
            {
                Debug.LogError($"Cannot start Colony Flow level. {error}", levelData);
                return false;
            }
            board.Configure(levelData.BoardSize, levelData.CellSize, generatedPixels);
            return true;
        }

        private void CreateTraySlots()
        {
            float center = (board.Size.x - 1) * board.CellSize * 0.5f;
            float spacing = 0.9f;
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
            float spacing = 0.9f;
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
                    colony.name = $"Colony {columnIndex + 1}-{depth + 1} {colonySpec.color} {colonySpec.count}";
                    colony.transform.localPosition = ColumnPosition(startX, spacing, columnIndex, depth);
                    colony.transform.localRotation = Quaternion.identity;
                    colony.Configure(colonySpec.color, colonySpec.count);

                    if (colony.View == null)
                        throw new MissingReferenceException($"{colonyPrefab.name} requires its ColonyView reference.");
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
            {
                foreach (ColonyView view in columnViews[columnIndex])
                    view.Initialize(view.Colony, controller, columnIndex);
            }
        }

        private void OnColonyAdded(int slotIndex, Colony colony)
        {
            if (views.TryGetValue(colony, out ColonyView view) && slotIndex < trayPositions.Count)
            {
                view.transform.SetParent(colonyTraysRoot, true);
                view.MoveToTray(trayPositions[slotIndex]);
            }
            RefreshColumnPositions();
        }

        private void OnColonyRemoved(int slotIndex, Colony colony)
        {
            if (views.TryGetValue(colony, out ColonyView view))
                view.gameObject.SetActive(false);
        }

        private void RefreshColumnPositions()
        {
            float center = (board.Size.x - 1) * board.CellSize * 0.5f;
            float spacing = 0.9f;
            float startX = center - (columnViews.Count - 1) * spacing * 0.5f;
            for (int columnIndex = 0; columnIndex < columnViews.Count; columnIndex++)
            {
                int depth = 0;
                foreach (ColonyView view in columnViews[columnIndex])
                {
                    if (view.Colony.State == ColonyState.InColumn)
                        view.SetTargetLocalPosition(ColumnPosition(startX, spacing, columnIndex, depth++));
                }
            }
        }

        private static Vector3 ColumnPosition(float startX, float spacing, int column, int depth)
        {
            return new Vector3(startX + column * spacing, 0.35f, -3.85f - depth * 0.92f);
        }
    }
}
