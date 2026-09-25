using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class PixelBoard : MonoBehaviour
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up
        };

        [SerializeField] private Vector2Int size = new Vector2Int(5, 5);
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [SerializeField] private List<PixelData> pixels = new List<PixelData>();
        [SerializeField] private bool buildOnStart;
        [SerializeField] private Vector2Int debugCollectPosition;

        private PixelCell[] cells;
        private HashSet<int>[] availableByColor;
        private readonly Queue<int> floodQueue = new Queue<int>();
        private readonly List<Vector2Int> pathScratch = new List<Vector2Int>(128);
        private int[] pathParents;
        private int[] pathQueue;
        private int[] pathVisitVersions;
        private int pathVisitVersion;
        private int width;
        private int height;

        public Vector2Int Size => IsBuilt ? new Vector2Int(width, height) : size;
        public float CellSize => cellSize;
        public int RemainingPixels { get; private set; }
        public int Revision { get; private set; }
        public bool IsBuilt => cells != null;
        public bool IsCompleted => IsBuilt && RemainingPixels == 0;

        public event Action BoardBuilt;
        public event Action BoardCleared;
        public event Action<Vector2Int, PixelColor> PixelCollected;
        public event Action Completed;

        private void Start()
        {
            if (buildOnStart && !IsBuilt)
                BuildBoard();
        }

        public void Configure(Vector2Int boardSize, float newCellSize,
            IReadOnlyList<PixelData> levelPixels)
        {
            if (IsBuilt)
                throw new InvalidOperationException("Configure must be called before BuildBoard.");
            if (boardSize.x <= 0 || boardSize.y <= 0 || newCellSize <= 0f || levelPixels == null)
                throw new ArgumentException("Invalid board configuration.");

            size = boardSize;
            cellSize = newCellSize;
            pixels.Clear();
            for (int i = 0; i < levelPixels.Count; i++)
                pixels.Add(levelPixels[i]);
        }

        [ContextMenu("Build Board (Play Mode)")]
        public void BuildBoard()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Build the board in Play Mode.", this);
                return;
            }

            if (!ValidateData())
                return;

            ClearRuntimeData();
            width = size.x;
            height = size.y;
            cells = new PixelCell[width * height];
            int expandedCellCount = (width + 2) * (height + 2);
            pathParents = new int[expandedCellCount];
            pathQueue = new int[expandedCellCount];
            pathVisitVersions = new int[expandedCellCount];
            CreateAvailabilitySets();

            foreach (PixelData data in pixels)
            {
                int index = ToIndex(data.Position.x, data.Position.y);
                cells[index] = new PixelCell
                {
                    Color = data.Color,
                    State = PixelCellState.Present,
                    IsOutsideReachable = false
                };
                RemainingPixels++;
            }

            BuildOutsideReachability();
            RebuildAvailableTargets();
            Revision++;
            BoardBuilt?.Invoke();
        }

        public bool TryGetCell(Vector2Int position, out PixelCell cell)
        {
            cell = default;
            if (!IsInside(position.x, position.y))
                return false;

            cell = cells[ToIndex(position.x, position.y)];
            return true;
        }

        public bool IsExposed(Vector2Int position)
        {
            return TryGetOccupiedIndex(position, out int index) && IsExposed(index);
        }

        public bool IsAvailable(Vector2Int position, PixelColor color)
        {
            if (!TryGetOccupiedIndex(position, out int index))
                return false;

            PixelCell cell = cells[index];
            return cell.State == PixelCellState.Present && cell.Color == color && IsExposed(index);
        }

        // Reserve before spawning an Ant so two Ants cannot choose the same target.
        public bool TryReservePixel(PixelColor color, out Vector2Int position)
        {
            position = default;
            if (!IsBuilt || !IsValidColor(color))
                return false;

            HashSet<int> candidates = availableByColor[(int)color];
            while (candidates.Count > 0)
            {
                int index = First(candidates);
                candidates.Remove(index);
                PixelCell cell = cells[index];
                if (cell.State != PixelCellState.Present || cell.Color != color || !IsExposed(index))
                    continue;

                cell.State = PixelCellState.Reserved;
                cells[index] = cell;
                position = ToPosition(index);
                return true;
            }
            return false;
        }

        // Finds and reserves the closest reachable pixel in one BFS pass. The returned
        // route ends beside the target because occupied pixel cells are not walkable.
        public bool TryReserveNearestReachablePixel(PixelColor color, Vector2Int borderStart,
            out Vector2Int position, List<Vector2Int> route)
        {
            position = default;
            if (!IsBuilt || !IsValidColor(color) || route == null ||
                !IsExpandedCell(borderStart) || !IsPathWalkable(borderStart) || pathQueue == null)
                return false;

            route.Clear();
            HashSet<int> candidates = availableByColor[(int)color];
            if (candidates.Count == 0)
                return false;

            BeginPathSearch();
            int head = 0;
            int tail = 0;
            int selectedCellIndex = -1;
            int selectedPathIndex = -1;
            int lowestTargetRow = int.MaxValue;
            int perimeterLength = 2 * width + 2 * height + 4;
            for (int perimeterIndex = 0; perimeterIndex < perimeterLength; perimeterIndex++)
            {
                Vector2Int perimeterCell = FromPerimeterIndex(perimeterIndex);
                int expandedIndex = ToExpandedIndex(perimeterCell);
                pathQueue[tail++] = expandedIndex;
                pathVisitVersions[expandedIndex] = pathVisitVersion;
                pathParents[expandedIndex] = -1;
            }

            while (head < tail)
            {
                int currentIndex = pathQueue[head++];
                Vector2Int current = FromExpandedIndex(currentIndex);

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int neighbour = current + Directions[i];
                    if (!IsInside(neighbour.x, neighbour.y))
                        continue;

                    int cellIndex = ToIndex(neighbour.x, neighbour.y);
                    PixelCell cell = cells[cellIndex];
                    if (candidates.Contains(cellIndex) && cell.State == PixelCellState.Present &&
                        cell.Color == color && neighbour.y < lowestTargetRow)
                    {
                        lowestTargetRow = neighbour.y;
                        selectedCellIndex = cellIndex;
                        selectedPathIndex = currentIndex;
                    }
                }

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = current + Directions[i];
                    if (!IsExpandedCell(next) || !IsPathWalkable(next))
                        continue;

                    int nextIndex = ToExpandedIndex(next);
                    if (pathVisitVersions[nextIndex] == pathVisitVersion)
                        continue;

                    pathVisitVersions[nextIndex] = pathVisitVersion;
                    pathParents[nextIndex] = currentIndex;
                    pathQueue[tail++] = nextIndex;
                }
            }

            if (selectedCellIndex < 0)
                return false;

            PixelCell selectedCell = cells[selectedCellIndex];
            candidates.Remove(selectedCellIndex);
            selectedCell.State = PixelCellState.Reserved;
            cells[selectedCellIndex] = selectedCell;
            position = ToPosition(selectedCellIndex);
            BuildPathFromSearch(selectedPathIndex, route);
            PrependPerimeterRoute(borderStart, route);
            return true;
        }

        private void PrependPerimeterRoute(Vector2Int start, List<Vector2Int> interiorRoute)
        {
            if (interiorRoute.Count == 0)
                return;

            Vector2Int boardEntrance = interiorRoute[0];
            pathScratch.Clear();
            pathScratch.Add(start);
            AppendPerimeterPath(start, boardEntrance, pathScratch);

            int firstInteriorIndex = pathScratch[pathScratch.Count - 1] == boardEntrance ? 1 : 0;
            for (int i = firstInteriorIndex; i < interiorRoute.Count; i++)
                pathScratch.Add(interiorRoute[i]);

            interiorRoute.Clear();
            interiorRoute.AddRange(pathScratch);
        }

        public bool ReleaseReservation(Vector2Int position)
        {
            if (!TryGetOccupiedIndex(position, out int index) ||
                cells[index].State != PixelCellState.Reserved)
                return false;

            PixelCell cell = cells[index];
            cell.State = PixelCellState.Present;
            cells[index] = cell;
            if (IsExposed(index))
                availableByColor[(int)cell.Color].Add(index);
            return true;
        }

        public bool TryCollectReservedPixel(Vector2Int position) => TryCollect(position, true);
        public bool TryCollectPixel(Vector2Int position) => TryCollect(position, false);

        public List<Vector2Int> GetAvailablePixels(PixelColor color)
        {
            var result = new List<Vector2Int>();
            if (!IsBuilt || !IsValidColor(color))
                return result;

            foreach (int index in availableByColor[(int)color])
                result.Add(ToPosition(index));
            return result;
        }

        public int CopyAvailablePixels(PixelColor color, List<Vector2Int> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            if (!IsBuilt || !IsValidColor(color))
                return 0;

            foreach (int index in availableByColor[(int)color])
                destination.Add(ToPosition(index));
            return destination.Count;
        }

        public bool HasAvailablePixel(PixelColor color)
        {
            return IsBuilt && IsValidColor(color) && availableByColor[(int)color].Count > 0;
        }

        public Vector3 GridToLocalPosition(Vector2Int position)
        {
            return new Vector3(position.x * cellSize, 0f, position.y * cellSize);
        }

        public Vector2Int GetBorderEntrance()
        {
            return new Vector2Int(Size.x / 2, -1);
        }

        public Vector2Int GetClosestBottomBorder(float localX)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(localX / cellSize), 0, width - 1);
            return new Vector2Int(x, -1);
        }

        public bool TryBuildPathToTarget(Vector2Int target, List<Vector2Int> result)
        {
            return TryBuildPathToTarget(target, GetBorderEntrance(), result);
        }

        public bool TryBuildPathToTarget(Vector2Int target, Vector2Int borderStart,
            List<Vector2Int> result)
        {
            if (!TryGetOccupiedIndex(target, out _) || result == null)
                return false;

            return TryBuildGridPath(borderStart, target, true, false, result);
        }

        public bool TryBuildReturnPath(Vector2Int collectedPosition, List<Vector2Int> result)
        {
            if (!IsInside(collectedPosition.x, collectedPosition.y) || result == null ||
                cells[ToIndex(collectedPosition.x, collectedPosition.y)].IsOccupied)
                return false;

            if (!TryBuildGridPath(collectedPosition, default, false, true, result))
                return false;

            Vector2Int nearestBorder = result[result.Count - 1];
            AppendPerimeterPath(nearestBorder, GetBorderEntrance(), result);
            return true;
        }

        [ContextMenu("Clear Board (Play Mode)")]
        public void Clear()
        {
            if (!Application.isPlaying)
                return;

            ClearBoard();
        }

        public void ClearBoard()
        {
            if (!IsBuilt)
                return;
            ClearRuntimeData();
            Revision++;
            BoardCleared?.Invoke();
        }

        private void ClearRuntimeData()
        {
            cells = null;
            availableByColor = null;
            pathParents = null;
            pathQueue = null;
            pathVisitVersions = null;
            pathVisitVersion = 0;
            floodQueue.Clear();
            width = 0;
            height = 0;
            RemainingPixels = 0;
        }

        private bool TryCollect(Vector2Int position, bool mustBeReserved)
        {
            if (!TryGetOccupiedIndex(position, out int index))
                return false;

            PixelCell cell = cells[index];
            bool validState = mustBeReserved
                ? cell.State == PixelCellState.Reserved
                : cell.State == PixelCellState.Present;
            if (!validState || !IsExposed(index))
                return false;

            availableByColor[(int)cell.Color].Remove(index);
            cell.State = PixelCellState.Empty;
            cell.IsOutsideReachable = true;
            cells[index] = cell;
            RemainingPixels--;

            floodQueue.Enqueue(index);
            ExpandOutsideRegion();
            Revision++;
            PixelCollected?.Invoke(position, cell.Color);

            if (IsCompleted)
                Completed?.Invoke();
            return true;
        }

        private void BuildOutsideReachability()
        {
            floodQueue.Clear();
            for (int x = 0; x < width; x++)
            {
                SeedOutside(x, 0);
                if (height > 1) SeedOutside(x, height - 1);
            }
            for (int y = 1; y < height - 1; y++)
            {
                SeedOutside(0, y);
                if (width > 1) SeedOutside(width - 1, y);
            }
            ExpandOutsideRegion();
        }

        private void SeedOutside(int x, int y)
        {
            int index = ToIndex(x, y);
            PixelCell cell = cells[index];
            if (cell.IsOccupied || cell.IsOutsideReachable)
                return;

            cell.IsOutsideReachable = true;
            cells[index] = cell;
            floodQueue.Enqueue(index);
        }

        private void ExpandOutsideRegion()
        {
            while (floodQueue.Count > 0)
            {
                int index = floodQueue.Dequeue();
                Vector2Int position = ToPosition(index);
                for (int i = 0; i < Directions.Length; i++)
                {
                    int x = position.x + Directions[i].x;
                    int y = position.y + Directions[i].y;
                    if (!IsInside(x, y))
                        continue;

                    int neighbourIndex = ToIndex(x, y);
                    PixelCell neighbour = cells[neighbourIndex];
                    if (neighbour.IsOccupied)
                    {
                        if (neighbour.State == PixelCellState.Present)
                            availableByColor[(int)neighbour.Color].Add(neighbourIndex);
                    }
                    else if (!neighbour.IsOutsideReachable)
                    {
                        neighbour.IsOutsideReachable = true;
                        cells[neighbourIndex] = neighbour;
                        floodQueue.Enqueue(neighbourIndex);
                    }
                }
            }
        }

        private void RebuildAvailableTargets()
        {
            foreach (HashSet<int> set in availableByColor)
                set.Clear();

            for (int index = 0; index < cells.Length; index++)
                if (cells[index].State == PixelCellState.Present && IsExposed(index))
                    availableByColor[(int)cells[index].Color].Add(index);
        }

        private bool IsExposed(int index)
        {
            if (!cells[index].IsOccupied)
                return false;

            Vector2Int position = ToPosition(index);
            for (int i = 0; i < Directions.Length; i++)
            {
                int x = position.x + Directions[i].x;
                int y = position.y + Directions[i].y;
                if (!IsInside(x, y))
                    return true;

                PixelCell neighbour = cells[ToIndex(x, y)];
                if (!neighbour.IsOccupied && neighbour.IsOutsideReachable)
                    return true;
            }
            return false;
        }

        private void CreateAvailabilitySets()
        {
            int colorCount = Enum.GetValues(typeof(PixelColor)).Length;
            availableByColor = new HashSet<int>[colorCount];
            for (int i = 0; i < colorCount; i++)
                availableByColor[i] = new HashSet<int>();
        }

        private bool TryGetOccupiedIndex(Vector2Int position, out int index)
        {
            index = -1;
            if (!IsInside(position.x, position.y))
                return false;

            index = ToIndex(position.x, position.y);
            return cells[index].IsOccupied;
        }

        private bool TryBuildGridPath(Vector2Int start, Vector2Int goal,
            bool stopAdjacentToGoal, bool stopAtAnyBorder, List<Vector2Int> result)
        {
            result.Clear();
            if (!IsExpandedCell(start) || !IsPathWalkable(start) || pathQueue == null)
                return false;

            BeginPathSearch();

            int head = 0;
            int tail = 0;
            int startIndex = ToExpandedIndex(start);
            pathQueue[tail++] = startIndex;
            pathVisitVersions[startIndex] = pathVisitVersion;
            pathParents[startIndex] = -1;
            int foundIndex = -1;

            while (head < tail)
            {
                int currentIndex = pathQueue[head++];
                Vector2Int current = FromExpandedIndex(currentIndex);
                bool reached = stopAtAnyBorder
                    ? IsPerimeterCell(current)
                    : stopAdjacentToGoal
                        ? Mathf.Abs(current.x - goal.x) + Mathf.Abs(current.y - goal.y) == 1
                        : current == goal;
                if (reached)
                {
                    foundIndex = currentIndex;
                    break;
                }

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = current + Directions[i];
                    if (!IsExpandedCell(next) || !IsPathWalkable(next))
                        continue;

                    int nextIndex = ToExpandedIndex(next);
                    if (pathVisitVersions[nextIndex] == pathVisitVersion)
                        continue;

                    pathVisitVersions[nextIndex] = pathVisitVersion;
                    pathParents[nextIndex] = currentIndex;
                    pathQueue[tail++] = nextIndex;
                }
            }

            if (foundIndex < 0)
                return false;

            BuildPathFromSearch(foundIndex, result);
            return true;
        }

        private void BeginPathSearch()
        {
            if (pathVisitVersion == int.MaxValue)
            {
                Array.Clear(pathVisitVersions, 0, pathVisitVersions.Length);
                pathVisitVersion = 0;
            }
            pathVisitVersion++;
        }

        private void BuildPathFromSearch(int foundIndex, List<Vector2Int> result)
        {
            result.Clear();
            for (int index = foundIndex; index >= 0; index = pathParents[index])
                result.Add(FromExpandedIndex(index));
            result.Reverse();
        }

        private void AppendPerimeterPath(Vector2Int from, Vector2Int to,
            List<Vector2Int> result)
        {
            int perimeterLength = 2 * width + 2 * height + 4;
            int fromIndex = ToPerimeterIndex(from);
            int toIndex = ToPerimeterIndex(to);
            int clockwiseSteps = (toIndex - fromIndex + perimeterLength) % perimeterLength;
            int counterClockwiseSteps = (fromIndex - toIndex + perimeterLength) % perimeterLength;
            int direction = clockwiseSteps <= counterClockwiseSteps ? 1 : -1;
            int steps = Mathf.Min(clockwiseSteps, counterClockwiseSteps);

            int index = fromIndex;
            for (int i = 0; i < steps; i++)
            {
                index = (index + direction + perimeterLength) % perimeterLength;
                result.Add(FromPerimeterIndex(index));
            }
        }

        private bool IsPerimeterCell(Vector2Int position)
        {
            return IsExpandedCell(position) &&
                   (position.x == -1 || position.x == width ||
                    position.y == -1 || position.y == height);
        }

        private int ToPerimeterIndex(Vector2Int position)
        {
            if (position.y == -1)
                return position.x + 1;
            if (position.x == width)
                return width + 2 + position.y;
            if (position.y == height)
                return width + height + 3 + (width - 1 - position.x);
            return 2 * width + height + 4 + (height - 1 - position.y);
        }

        private Vector2Int FromPerimeterIndex(int index)
        {
            int bottomCount = width + 2;
            if (index < bottomCount)
                return new Vector2Int(index - 1, -1);
            index -= bottomCount;

            int rightCount = height + 1;
            if (index < rightCount)
                return new Vector2Int(width, index);
            index -= rightCount;

            int topCount = width + 1;
            if (index < topCount)
                return new Vector2Int(width - 1 - index, height);
            index -= topCount;
            return new Vector2Int(-1, height - 1 - index);
        }

        private bool IsPathWalkable(Vector2Int position)
        {
            if (!IsInside(position.x, position.y))
                return IsExpandedCell(position);

            PixelCell cell = cells[ToIndex(position.x, position.y)];
            return !cell.IsOccupied && cell.IsOutsideReachable;
        }

        private bool IsExpandedCell(Vector2Int position)
        {
            return IsBuilt && position.x >= -1 && position.y >= -1 &&
                   position.x <= width && position.y <= height;
        }

        private int ToExpandedIndex(Vector2Int position)
        {
            return (position.y + 1) * (width + 2) + position.x + 1;
        }

        private Vector2Int FromExpandedIndex(int index)
        {
            return new Vector2Int(index % (width + 2) - 1, index / (width + 2) - 1);
        }

        private bool IsInside(int x, int y) => IsBuilt && x >= 0 && y >= 0 && x < width && y < height;
        private int ToIndex(int x, int y) => y * width + x;
        private Vector2Int ToPosition(int index) => new Vector2Int(index % width, index / width);
        private static int First(HashSet<int> set) { foreach (int value in set) return value; return -1; }
        private static bool IsValidColor(PixelColor color) => Enum.IsDefined(typeof(PixelColor), color);

        [ContextMenu("Collect Debug Pixel (Play Mode)")]
        private void CollectDebugPixel()
        {
            if (Application.isPlaying && !TryCollectPixel(debugCollectPosition))
                Debug.Log($"Pixel {debugCollectPosition} is missing, reserved or not exposed.", this);
        }

        [ContextMenu("Create Sample 5x5 Data")]
        private void CreateSampleData()
        {
            if (Application.isPlaying) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Create Sample Pixel Data");
#endif
            size = new Vector2Int(5, 5);
            pixels = new List<PixelData>();
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                    pixels.Add(new PixelData(new Vector2Int(x, y),
                        x == 2 && y == 2 ? PixelColor.Orange : PixelColor.Blue));
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private bool ValidateData()
        {
            if (size.x <= 0 || size.y <= 0 || cellSize <= 0f ||
                float.IsNaN(cellSize) || float.IsInfinity(cellSize) ||
                pixels == null || pixels.Count == 0)
            {
                Debug.LogError("Board requires positive size/cell size and non-empty pixel data.", this);
                return false;
            }

            var positions = new HashSet<int>();
            foreach (PixelData data in pixels)
            {
                if (data == null || data.Position.x < 0 || data.Position.y < 0 ||
                    data.Position.x >= size.x || data.Position.y >= size.y ||
                    !IsValidColor(data.Color))
                {
                    Debug.LogError("Pixel data contains a null, invalid color or out-of-bounds entry.", this);
                    return false;
                }

                int index = data.Position.y * size.x + data.Position.x;
                if (!positions.Add(index))
                {
                    Debug.LogError($"Duplicate pixel position: {data.Position}.", this);
                    return false;
                }
            }
            return true;
        }
    }
}
