using System;
using System.Collections;
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
        [SerializeField] private ColonyTray tray;
        [SerializeField] private AntManager antManager;
        [SerializeField] private Transform antHole;

        [Header("Hierarchy roots")]
        [SerializeField] private Transform antsRoot;
        [SerializeField] private Transform coloniesRoot;

        [Header("Gameplay")]
        [SerializeField] private bool simulateWithoutAnt;
        [SerializeField, Min(0.01f)] private float simulatedTaskInterval = 0.08f;

        [Header("Column layout")]
        [SerializeField, Min(0f)] private float columnSpacing = 0.82f;
        [SerializeField] private float columnHeight = 0.35f;
        [SerializeField] private float columnStartDepth = -3.65f;
        [SerializeField, Min(0f)] private float columnDepthSpacing = 0.83f;
        [SerializeField] private float columnWorldCenterX;

        private readonly List<ColonyColumn> columns = new List<ColonyColumn>();
        private readonly List<List<ColonyView>> columnViews = new List<List<ColonyView>>();
        private readonly Dictionary<Colony, ColonyView> views =
            new Dictionary<Colony, ColonyView>();
        private readonly List<Vector3> trayPositions = new List<Vector3>();
        private readonly List<Transform> traySlots = new List<Transform>();
        private readonly List<Vector3> traySlotScales = new List<Vector3>();
        private readonly List<PixelData> generatedPixels = new List<PixelData>();
        private readonly List<ColonyColumnSpec> columnData = new List<ColonyColumnSpec>();
        private readonly List<Colony> colonyScratch = new List<Colony>();
        private readonly List<Colony> shuffledColonies = new List<Colony>();
        private readonly List<int> remainingCounts = new List<int>();
        private int trayCapacity;
        private bool columnLayoutRefreshPending;
        private bool pickFeedbackRefreshPending;
        private float pendingColumnAnimationDuration = -1f;

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
            if (columnLayoutRefreshPending)
            {
                columnLayoutRefreshPending = false;
                float animationDuration = pendingColumnAnimationDuration;
                pendingColumnAnimationDuration = -1f;
                RefreshColumnPositions(animationDuration);
                pickFeedbackRefreshPending = true;
            }

            if (pickFeedbackRefreshPending)
                RefreshPickFeedback();
        }

        public bool BuildLevel(ColonyLevelData levelData)
        {
            if (!enabled || levelData == null || !ValidateReferences())
                return false;

            UnloadLevel();
            levelData.BuildPixels(generatedPixels);
            tray.ResetCapacity();
            trayCapacity = tray.Capacity;
            levelData.BuildColumns(generatedPixels, columnData);
            if (!levelData.ValidateGeneratedLevel(generatedPixels, columnData, out string error))
            {
                Debug.LogError($"Cannot start Colony Flow level. {error}", levelData);
                return false;
            }

            board.Configure(levelData.BoardSize, levelData.BoardWorldSize, generatedPixels);
            AlignColumnsRoot();
            if (!PrepareTraySlots())
                return false;
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
            StopAllCoroutines();
            if (tray != null)
            {
                tray.ColonyAdded -= OnColonyAdded;
                tray.ColonyRemoved -= OnColonyRemoved;
            }

            foreach (ColonyView view in views.Values)
                DeactivateAndDestroy(view != null ? view.gameObject : null);
            for (int i = 0; i < columns.Count; i++)
                DeactivateAndDestroy(columns[i] != null ? columns[i].gameObject : null);
            for (int i = 0; i < traySlots.Count && i < traySlotScales.Count; i++)
                if (traySlots[i] != null)
                {
                    traySlots[i].localScale = traySlotScales[i];
                    if (Application.isPlaying)
                        traySlots[i].gameObject.SetActive(false);
                }

            board?.ClearBoard();
            columns.Clear();
            columnViews.Clear();
            views.Clear();
            trayPositions.Clear();
            traySlots.Clear();
            traySlotScales.Clear();
            generatedPixels.Clear();
            columnData.Clear();
            colonyScratch.Clear();
            shuffledColonies.Clear();
            remainingCounts.Clear();
            columnLayoutRefreshPending = false;
            pickFeedbackRefreshPending = false;
            pendingColumnAnimationDuration = -1f;
        }

        private bool ValidateReferences()
        {
            if (board != null && levelManager != null && gameplayCamera != null &&
                colonyPrefab != null && columnPrefab != null && tray != null &&
                antManager != null && antHole != null &&
                antsRoot != null && coloniesRoot != null)
                return true;

            Debug.LogError("ColonyLevelBuilder has missing serialized references.", this);
            return false;
        }

        private bool PrepareTraySlots()
        {
            traySlots.Clear();
            traySlotScales.Clear();
            for (int i = 0; i < tray.SlotObjectCount; i++)
            {
                Transform slot = tray.GetSlotObject(i);
                if (slot == null)
                {
                    Debug.LogError($"{tray.name} has an empty slot reference at index {i}.", tray);
                    return false;
                }
                traySlots.Add(slot);
                slot.localScale = tray.SlotScale;
                traySlotScales.Add(tray.SlotScale);
            }

            if (traySlots.Count < trayCapacity)
            {
                Debug.LogError(
                    $"{tray.name} needs at least {trayCapacity} assigned slots, but only " +
                    $"{traySlots.Count} are assigned in its Slot Objects list.", tray);
                return false;
            }

            for (int i = 0; i < traySlots.Count; i++)
                traySlots[i].gameObject.SetActive(i < trayCapacity);
            RefreshTrayLayout(false);
            return true;
        }

        public bool TryExpandTray()
        {
            if (tray == null || trayCapacity + tray.ExpansionAmount > traySlots.Count ||
                !tray.ExpandCapacity(tray.ExpansionAmount))
                return false;

            trayCapacity = tray.Capacity;
            int firstNewSlotIndex = trayCapacity - tray.ExpansionAmount;
            var newSlots = new List<(Transform slot, Vector3 targetScale)>(
                tray.ExpansionAmount);
            for (int i = firstNewSlotIndex; i < trayCapacity; i++)
            {
                Transform slot = traySlots[i];
                Vector3 targetScale = traySlotScales[i];
                slot.localScale = Vector3.zero;
                slot.gameObject.SetActive(true);
                newSlots.Add((slot, targetScale));
            }

            RefreshTrayLayout(true);
            for (int i = 0; i < newSlots.Count; i++)
                StartCoroutine(PopTraySlot(newSlots[i].slot, newSlots[i].targetScale));
            RequestPickFeedbackRefresh();
            return true;
        }

        public bool ShuffleRemainingColonies(float animationDuration)
        {
            shuffledColonies.Clear();
            remainingCounts.Clear();
            for (int i = 0; i < columns.Count; i++)
            {
                columns[i].CopyRemaining(colonyScratch);
                remainingCounts.Add(colonyScratch.Count);
                shuffledColonies.AddRange(colonyScratch);
            }

            if (shuffledColonies.Count < 2)
                return false;

            var originalOrder = new List<Colony>(shuffledColonies);
            for (int i = shuffledColonies.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (shuffledColonies[i], shuffledColonies[swapIndex]) =
                    (shuffledColonies[swapIndex], shuffledColonies[i]);
            }

            bool changed = false;
            for (int i = 0; i < shuffledColonies.Count; i++)
            {
                if (shuffledColonies[i] == originalOrder[i])
                    continue;
                changed = true;
                break;
            }
            if (!changed)
            {
                Colony first = shuffledColonies[0];
                shuffledColonies.RemoveAt(0);
                shuffledColonies.Add(first);
            }

            int sourceIndex = 0;
            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                colonyScratch.Clear();
                columnViews[columnIndex].Clear();
                int count = remainingCounts[columnIndex];
                for (int i = 0; i < count; i++)
                {
                    Colony colony = shuffledColonies[sourceIndex++];
                    colonyScratch.Add(colony);
                    colony.transform.SetParent(columns[columnIndex].transform, true);
                    if (views.TryGetValue(colony, out ColonyView view))
                    {
                        view.SetOwnerColumn(columnIndex);
                        columnViews[columnIndex].Add(view);
                    }
                }
                columns[columnIndex].ReplaceRemaining(colonyScratch);
            }

            pendingColumnAnimationDuration = Mathf.Max(0.01f, animationDuration);
            columnLayoutRefreshPending = true;
            return true;
        }

        public int RemoveColoniesByColor(PixelColor color)
        {
            colonyScratch.Clear();
            for (int i = 0; i < columns.Count; i++)
                columns[i].RemoveColor(color, colonyScratch);

            foreach (Colony colony in views.Keys)
            {
                if (colony != null && colony.Color == color &&
                    !colonyScratch.Contains(colony))
                    colonyScratch.Add(colony);
            }

            for (int i = 0; i < colonyScratch.Count; i++)
                colonyScratch[i].RemoveByBooster();

            columnLayoutRefreshPending = true;
            return colonyScratch.Count;
        }

        internal void RefreshPickFeedback()
        {
            pickFeedbackRefreshPending = false;
            for (int columnIndex = 0; columnIndex < columnViews.Count; columnIndex++)
                for (int i = 0; i < columnViews[columnIndex].Count; i++)
                {
                    ColonyView view = columnViews[columnIndex][i];
                    if (view != null)
                        view.SetPickFeedback(levelManager != null &&
                            levelManager.IsColonyNormallySelectable(
                                view.Colony, columnIndex));
                }
        }

        private void RequestPickFeedbackRefresh()
        {
            pickFeedbackRefreshPending = true;
        }

        private void RefreshTrayLayout(bool animateColonies)
        {
            trayPositions.Clear();
            float spacing = tray.SlotSpacing;
            float startX = tray.SlotCenterX - (trayCapacity - 1) * spacing * 0.5f;
            for (int i = 0; i < trayCapacity; i++)
            {
                Vector3 slotPosition =
                    new Vector3(startX + i * spacing, tray.SlotHeight, tray.Depth);
                trayPositions.Add(slotPosition + Vector3.up * tray.ColonyHeight);
                if (i < traySlots.Count && traySlots[i] != null)
                    traySlots[i].localPosition = slotPosition;

                Colony colony = tray.GetSlot(i);
                if (animateColonies && colony != null &&
                    views.TryGetValue(colony, out ColonyView view))
                    view.MoveWithinTray(trayPositions[i]);
            }
        }

        private IEnumerator PopTraySlot(Transform slot, Vector3 targetScale)
        {
            float elapsed = 0f;
            while (slot != null && elapsed < tray.SlotPopDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / tray.SlotPopDuration);
                float eased = 1f - Mathf.Pow(1f - progress, tray.SlotPopEasePower);
                float overshoot = 1f +
                                  Mathf.Sin(progress * Mathf.PI) * tray.SlotPopOvershoot;
                slot.localScale = targetScale * eased * overshoot;
                yield return null;
            }
            if (slot != null)
                slot.localScale = targetScale;
        }

        private void CreateColumns()
        {
            float center = board.GridCenterLocalX;
            float startX = center - (columnData.Count - 1) * columnSpacing * 0.5f;

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
                        ColumnPosition(startX, columnIndex, depth);
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

        private void AlignColumnsRoot()
        {
            if (board == null || coloniesRoot == null)
                return;

            float localCenterX = board.GridCenterLocalX;
            Vector3 currentWorldCenter = coloniesRoot.TransformPoint(
                new Vector3(localCenterX, 0f, 0f));
            Vector3 rootPosition = coloniesRoot.position;
            rootPosition.x += columnWorldCenterX - currentWorldCenter.x;
            coloniesRoot.position = rootPosition;
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
                view.transform.SetParent(tray.transform, true);
                view.MoveToTray(trayPositions[slotIndex]);
            }
            // ColonyAdded fires before ColonyColumn.TryTakeFront updates the
            // column. Reflow in LateUpdate after the column state is current.
            columnLayoutRefreshPending = true;
            RequestPickFeedbackRefresh();
        }

        private void OnColonyRemoved(int slotIndex, Colony colony)
        {
            if (views.TryGetValue(colony, out ColonyView view))
                view.PlayDisappear();
            RequestPickFeedbackRefresh();
        }

        private void RefreshColumnPositions(float animationDuration)
        {
            float center = board.GridCenterLocalX;
            int activeColumnCount = 0;
            for (int i = 0; i < columns.Count; i++)
                if (columns[i] != null && columns[i].HasColony)
                    activeColumnCount++;

            if (activeColumnCount == 0)
                return;

            float startX = center - (activeColumnCount - 1) * columnSpacing * 0.5f;
            int compactColumnIndex = 0;
            for (int columnIndex = 0; columnIndex < columnViews.Count; columnIndex++)
            {
                if (columns[columnIndex] == null || !columns[columnIndex].HasColony)
                    continue;

                int depth = 0;
                foreach (ColonyView view in columnViews[columnIndex])
                    if (view.Colony.State == ColonyState.InColumn)
                    {
                        Vector3 target = ColumnPosition(
                            startX, compactColumnIndex, depth++);
                        if (animationDuration > 0f)
                            view.MoveToColumnPositionOverDuration(target, animationDuration);
                        else
                            view.MoveToColumnPosition(target);
                    }
                compactColumnIndex++;
            }
        }

        private Vector3 ColumnPosition(float startX, int column, int depth)
        {
            return new Vector3(startX + column * columnSpacing, columnHeight,
                columnStartDepth - depth * columnDepthSpacing);
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
