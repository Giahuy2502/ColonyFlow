using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [CreateAssetMenu(fileName = "Level_", menuName = "Colony Flow/Level Data")]
    public sealed class ColonyLevelData : ScriptableObject
    {
        [SerializeField, Range(5, 100)] private int width = 20;
        [SerializeField, Range(5, 100)] private int height = 20;
        [SerializeField, Min(1f)] private float boardWorldSize = 4.4f;
        [SerializeField] private int seed = 512;
        [SerializeField, Range(1, 10)] private int columnCount = 5;
        [SerializeField, Min(1)] private int maxColonyQuota = 20;
        [Header("Hidden Colonies")]
        [Tooltip("Number of non-front Colonies whose color and count start hidden.")]
        [SerializeField, Min(0)] private int hiddenColonyCount;
        [Header("Custom Pixel Map")]
        [SerializeField] private List<PixelColor> customColorLegend = new List<PixelColor>();
        [SerializeField] private List<string> customRows = new List<string>();

        public Vector2Int BoardSize => new Vector2Int(width, height);
        public float BoardWorldSize => boardWorldSize;
        public void BuildPixels(List<PixelData> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            BuildCustomPixels(destination);
        }

        public void BuildColumns(List<PixelData> generatedPixels,
            List<ColonyColumnSpec> destination)
        {
            if (generatedPixels == null || destination == null)
                throw new ArgumentNullException();

            destination.Clear();
            for (int i = 0; i < columnCount; i++)
                destination.Add(new ColonyColumnSpec());

            var counts = new Dictionary<PixelColor, int>();
            foreach (PixelData pixel in generatedPixels)
            {
                counts.TryGetValue(pixel.Color, out int count);
                counts[pixel.Color] = count + 1;
            }

            PixelColor[] order = (PixelColor[])Enum.GetValues(typeof(PixelColor));

            var generatedColonies = new List<ColonySpec>();
            foreach (PixelColor color in order)
            {
                if (!counts.TryGetValue(color, out int remaining))
                    continue;

                int targetQuota = Mathf.Max(1,
                    Mathf.RoundToInt(maxColonyQuota * 0.85f));
                int colonyCount = remaining <= maxColonyQuota
                    ? 1
                    : Mathf.Max(
                        Mathf.CeilToInt(remaining / (float)targetQuota),
                        Mathf.CeilToInt(remaining / (float)maxColonyQuota));
                int variation = Mathf.Max(1, maxColonyQuota / 8);
                int previousQuota = -1;

                for (int colonyIndex = 0; colonyIndex < colonyCount; colonyIndex++)
                {
                    int slotsLeft = colonyCount - colonyIndex;
                    int average = Mathf.RoundToInt(remaining / (float)slotsLeft);
                    int phase = PositiveModulo(
                        seed + (int)color * 3 + colonyIndex, 7);
                    int desired = average + GetQuotaVariation(phase, variation);
                    int minimum = Mathf.Max(1,
                        remaining - maxColonyQuota * (slotsLeft - 1));
                    int maximum = Mathf.Min(maxColonyQuota,
                        remaining - (slotsLeft - 1));
                    int quota = Mathf.Clamp(desired, minimum, maximum);

                    if (quota == previousQuota)
                    {
                        if (quota < maximum)
                            quota++;
                        else if (quota > minimum)
                            quota--;
                    }

                    generatedColonies.Add(new ColonySpec(color, quota));
                    previousQuota = quota;
                    remaining -= quota;
                }
            }

            DistributeColonies(generatedColonies, destination);
            AssignHiddenColonies(destination);
        }

        private void AssignHiddenColonies(List<ColonyColumnSpec> columns)
        {
            var candidates = new List<ColonySpec>();
            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                List<ColonySpec> colonies = columns[columnIndex].colonies;
                for (int depth = 0; depth < colonies.Count; depth++)
                {
                    ColonySpec colony = colonies[depth];
                    colony.isHidden = false;
                    if (depth > 0)
                        candidates.Add(colony);
                }
            }

            if (hiddenColonyCount <= 0 || candidates.Count == 0)
                return;

            uint randomState = unchecked((uint)seed) ^ 0x9E3779B9u;
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                randomState = randomState * 1664525u + 1013904223u;
                int swapIndex = (int)(randomState % (uint)(i + 1));
                (candidates[i], candidates[swapIndex]) =
                    (candidates[swapIndex], candidates[i]);
            }

            int count = Mathf.Min(hiddenColonyCount, candidates.Count);
            for (int i = 0; i < count; i++)
                candidates[i].isHidden = true;
        }

        private void DistributeColonies(List<ColonySpec> source,
            List<ColonyColumnSpec> destination)
        {
            int colorCount = Enum.GetValues(typeof(PixelColor)).Length;
            var coloniesByColor = new List<ColonySpec>[colorCount];
            var nextByColor = new int[colorCount];
            for (int i = 0; i < colorCount; i++)
                coloniesByColor[i] = new List<ColonySpec>();
            for (int i = 0; i < source.Count; i++)
                coloniesByColor[(int)source[i].color].Add(source[i]);

            ColonySpec previousHorizontal = null;
            for (int placement = 0; placement < source.Count; placement++)
            {
                int columnIndex = placement % destination.Count;
                if (columnIndex == 0)
                    previousHorizontal = null;

                List<ColonySpec> column = destination[columnIndex].colonies;
                ColonySpec previousVertical = column.Count > 0
                    ? column[column.Count - 1]
                    : null;
                int selectedColor = SelectNextColonyColor(
                    coloniesByColor, nextByColor, previousVertical,
                    previousHorizontal, placement);
                ColonySpec selected = coloniesByColor[selectedColor][nextByColor[selectedColor]++];
                column.Add(selected);
                previousHorizontal = selected;
            }
        }

        private int SelectNextColonyColor(List<ColonySpec>[] coloniesByColor,
            int[] nextByColor, ColonySpec previousVertical,
            ColonySpec previousHorizontal, int placement)
        {
            int colorCount = coloniesByColor.Length;
            int scanStart = PositiveModulo(seed + placement * 3, colorCount);

            // First avoid matching both neighbours. If impossible, relax the
            // horizontal rule before allowing a vertical same-color stack.
            for (int relaxation = 0; relaxation < 3; relaxation++)
            {
                int selectedColor = -1;
                int bestScore = int.MinValue;
                for (int offset = 0; offset < colorCount; offset++)
                {
                    int colorIndex = (scanStart + offset) % colorCount;
                    List<ColonySpec> colonies = coloniesByColor[colorIndex];
                    int remaining = colonies.Count - nextByColor[colorIndex];
                    if (remaining <= 0)
                        continue;
                    if (relaxation < 2 && previousVertical != null &&
                        (int)previousVertical.color == colorIndex)
                        continue;
                    if (relaxation < 1 && previousHorizontal != null &&
                        (int)previousHorizontal.color == colorIndex)
                        continue;

                    ColonySpec candidate = colonies[nextByColor[colorIndex]];
                    int score = remaining * 100;
                    if (previousVertical == null || candidate.count != previousVertical.count)
                        score += 10;
                    if (previousHorizontal == null || candidate.count != previousHorizontal.count)
                        score += 5;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        selectedColor = colorIndex;
                    }
                }

                if (selectedColor >= 0)
                    return selectedColor;
            }

            throw new InvalidOperationException("No colony is available for distribution.");
        }

        private static int GetQuotaVariation(int phase, int amount)
        {
            switch (phase)
            {
                case 0: return -amount;
                case 1: return amount;
                case 2: return -Mathf.Max(1, amount / 2);
                case 3: return Mathf.Max(1, amount / 2);
                case 4: return -Mathf.Max(1, amount - 1);
                case 5: return Mathf.Max(1, amount - 1);
                default: return 0;
            }
        }

        public bool ValidateGeneratedLevel(IReadOnlyList<PixelData> generatedPixels,
            IReadOnlyList<ColonyColumnSpec> generatedColumns, out string error)
        {
            error = string.Empty;
            if (generatedPixels == null || generatedPixels.Count == 0)
            {
                error = $"{name}: board contains no pixels.";
                return false;
            }
            if (generatedColumns == null || generatedColumns.Count == 0)
            {
                error = $"{name}: level contains no Colony columns.";
                return false;
            }
            if (columnCount <= 0 || maxColonyQuota <= 0)
            {
                error = $"{name}: column count and Colony quota must be positive.";
                return false;
            }

            int colorCount = Enum.GetValues(typeof(PixelColor)).Length;
            int[] pixelCounts = new int[colorCount];
            int[] colonyCounts = new int[colorCount];
            var occupiedPositions = new HashSet<int>();

            for (int i = 0; i < generatedPixels.Count; i++)
            {
                PixelData pixel = generatedPixels[i];
                if (pixel == null || pixel.Position.x < 0 || pixel.Position.x >= width ||
                    pixel.Position.y < 0 || pixel.Position.y >= height ||
                    !Enum.IsDefined(typeof(PixelColor), pixel.Color))
                {
                    error = $"{name}: pixel #{i} is null or outside the board.";
                    return false;
                }

                int positionIndex = pixel.Position.y * width + pixel.Position.x;
                if (!occupiedPositions.Add(positionIndex))
                {
                    error = $"{name}: duplicate pixel at {pixel.Position}.";
                    return false;
                }
                pixelCounts[(int)pixel.Color]++;
            }

            for (int columnIndex = 0; columnIndex < generatedColumns.Count; columnIndex++)
            {
                ColonyColumnSpec column = generatedColumns[columnIndex];
                if (column == null || column.colonies == null)
                {
                    error = $"{name}: Colony column {columnIndex + 1} is null.";
                    return false;
                }

                for (int colonyIndex = 0; colonyIndex < column.colonies.Count; colonyIndex++)
                {
                    ColonySpec colony = column.colonies[colonyIndex];
                    if (colony == null || colony.count <= 0 ||
                        !Enum.IsDefined(typeof(PixelColor), colony.color))
                    {
                        error = $"{name}: invalid Colony in column {columnIndex + 1}.";
                        return false;
                    }
                    colonyCounts[(int)colony.color] += colony.count;
                }
            }

            for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                if (pixelCounts[colorIndex] == colonyCounts[colorIndex])
                    continue;
                PixelColor color = (PixelColor)colorIndex;
                error = $"{name}: {color} has {pixelCounts[colorIndex]} pixels but " +
                        $"{colonyCounts[colorIndex]} Colony capacity.";
                return false;
            }
            return true;
        }

        private void BuildCustomPixels(List<PixelData> destination)
        {
            if (customRows == null || customRows.Count != height)
            {
                Debug.LogError($"{name}: custom map requires exactly {height} rows.", this);
                return;
            }

            if (customColorLegend == null || customColorLegend.Count == 0)
            {
                Debug.LogError($"{name}: custom map requires a color legend.", this);
                return;
            }

            for (int rowIndex = 0; rowIndex < customRows.Count; rowIndex++)
            {
                string row = customRows[rowIndex];
                if (string.IsNullOrEmpty(row) || row.Length != width)
                {
                    Debug.LogError(
                        $"{name}: custom row {rowIndex + 1} must contain exactly {width} cells.",
                        this);
                    destination.Clear();
                    return;
                }

                for (int x = 0; x < width; x++)
                {
                    char symbol = row[x];
                    if (symbol == '.')
                        continue;

                    int legendIndex = symbol - '0';
                    if (legendIndex < 0 || legendIndex >= customColorLegend.Count)
                    {
                        Debug.LogError(
                            $"{name}: custom row {rowIndex + 1} contains invalid symbol '{symbol}'.",
                            this);
                        destination.Clear();
                        return;
                    }

                    int y = height - rowIndex - 1;
                    destination.Add(new PixelData(
                        new Vector2Int(x, y), customColorLegend[legendIndex]));
                }
            }
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

    }
}
